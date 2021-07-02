using System;
using System.Collections.Generic;
using Unity.Profiling;
using MLAPI.Configuration;
using MLAPI.Profiling;
using MLAPI.Logging;
using UnityEngine;

namespace MLAPI.Messaging
{
    /// <summary>
    /// RpcQueueProcessing
    /// Handles processing of RpcQueues
    /// Inbound to invocation
    /// Outbound to send
    /// </summary>
    internal class RpcQueueProcessor
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static ProfilerMarker s_ProcessReceiveQueue = new ProfilerMarker($"{nameof(RpcQueueProcessor)}.{nameof(ProcessReceiveQueue)}");
        private static ProfilerMarker s_ProcessSendQueue = new ProfilerMarker($"{nameof(RpcQueueProcessor)}.{nameof(ProcessSendQueue)}");
#endif

        // Batcher object used to manage the RPC batching on the send side
        private readonly RpcBatcher m_RpcBatcher = new RpcBatcher();
        private const int k_BatchThreshold = 512;

        //NSS-TODO: Need to determine how we want to handle all other MLAPI send types
        //Temporary place to keep internal MLAPI messages
        private readonly List<RpcFrameQueueItem> m_InternalMLAPISendQueue = new List<RpcFrameQueueItem>();

        //The RpcQueueContainer that is associated with this RpcQueueProcessor
        private RpcQueueContainer m_RpcQueueContainer;

        private readonly NetworkManager m_NetworkManager;

        /// <summary>
        /// ProcessReceiveQueue
        /// Public facing interface method to start processing all RPCs in the current inbound frame
        /// </summary>
        public void ProcessReceiveQueue(NetworkUpdateStage currentStage, bool isTesting)
        {
            bool advanceFrameHistory = false;
            if (!ReferenceEquals(m_RpcQueueContainer, null))
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                s_ProcessReceiveQueue.Begin();
#endif
                var currentFrame = m_RpcQueueContainer.GetQueueHistoryFrame(RpcQueueHistoryFrame.QueueFrameType.Inbound, currentStage);
                var nextFrame = m_RpcQueueContainer.GetQueueHistoryFrame(RpcQueueHistoryFrame.QueueFrameType.Inbound, currentStage, true);
                if (nextFrame.IsDirty && nextFrame.HasLoopbackData)
                {
                    advanceFrameHistory = true;
                }

                if (currentFrame != null && currentFrame.IsDirty)
                {
                    var currentQueueItem = currentFrame.GetFirstQueueItem();
                    while (currentQueueItem.QueueItemType != RpcQueueContainer.QueueItemType.None)
                    {
                        advanceFrameHistory = true;

                        if (!isTesting)
                        {
                            try
                            {
                                m_NetworkManager.InvokeRpc(currentQueueItem);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogException(ex);

                                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                                {
                                    NetworkLog.LogWarning($"A {currentQueueItem.QueueItemType} threw an exception while executing! Please check Unity logs for more information.");
                                }
                            }
                        }

                        PerformanceDataManager.Increment(ProfilerConstants.RpcQueueProcessed);
                        currentQueueItem = currentFrame.GetNextQueueItem();
                    }

                    //We call this to dispose of the shared stream writer and stream
                    currentFrame.CloseQueue();
                }

                if (advanceFrameHistory)
                {
                    m_RpcQueueContainer.AdvanceFrameHistory(RpcQueueHistoryFrame.QueueFrameType.Inbound);
                }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                s_ProcessReceiveQueue.End();
#endif
            }
        }

        /// <summary>
        /// ProcessSendQueue
        /// Called to send both performance RPC and internal messages and then flush the outbound queue
        /// </summary>
        internal void ProcessSendQueue(bool isListening)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_ProcessSendQueue.Begin();
#endif

            RpcQueueSendAndFlush(isListening);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_ProcessSendQueue.End();
#endif
            InternalMessagesSendAndFlush(isListening);
        }

        /// <summary>
        ///  QueueInternalMLAPICommand
        ///  Added this as an example of how to add internal messages to the outbound send queue
        /// </summary>
        /// <param name="queueItem">message queue item to add</param>
        public void QueueInternalMLAPICommand(RpcFrameQueueItem queueItem)
        {
            m_InternalMLAPISendQueue.Add(queueItem);
        }

        /// <summary>
        /// Generic Sending Method for Internal Messages ADD_OBJECT and DESTROY_OBJECT
        /// </summary>
        internal void InternalMessagesSendAndFlush(bool isListening)
        {
            foreach (RpcFrameQueueItem queueItem in m_InternalMLAPISendQueue)
            {
                var poolStream = queueItem.NetworkBuffer;

                switch (queueItem.QueueItemType)
                {
                    case RpcQueueContainer.QueueItemType.CreateObject:
                        {
                            if (isListening)
                            {
                                foreach (ulong clientId in queueItem.ClientNetworkIds)
                                {
                                    m_NetworkManager.MessageSender.Send(clientId, NetworkConstants.ADD_OBJECT, queueItem.NetworkChannel, poolStream);
                                }

                                PerformanceDataManager.Increment(ProfilerConstants.RpcSent, queueItem.ClientNetworkIds.Length);
                                break;
                            }

                            PerformanceDataManager.Increment(ProfilerConstants.RpcSent, queueItem.ClientNetworkIds.Length);
                            break;
                        }
                    case RpcQueueContainer.QueueItemType.DestroyObject:
                        {
                            if (isListening)
                            {
                                foreach (ulong clientId in queueItem.ClientNetworkIds)
                                {
                                    m_NetworkManager.MessageSender.Send(clientId, NetworkConstants.DESTROY_OBJECT, queueItem.NetworkChannel, poolStream);
                                }

                                PerformanceDataManager.Increment(ProfilerConstants.RpcSent, queueItem.ClientNetworkIds.Length);
                                break;
                            }

                            PerformanceDataManager.Increment(ProfilerConstants.RpcSent, queueItem.ClientNetworkIds.Length);
                            break;
                        }
                }

                poolStream.Dispose();
            }

            m_InternalMLAPISendQueue.Clear();
        }

        /// <summary>
        /// Sends all RPC queue items in the current outbound frame
        /// </summary>
        /// <param name="isListening">if flase it will just process through the queue items but attempt to send</param>
        private void RpcQueueSendAndFlush(bool isListening)
        {
            var advanceFrameHistory = false;
            if (!ReferenceEquals(m_RpcQueueContainer, null))
            {
                var currentFrame = m_RpcQueueContainer.GetCurrentFrame(RpcQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);
                if (currentFrame != null)
                {
                    var currentQueueItem = currentFrame.GetFirstQueueItem();
                    while (currentQueueItem.QueueItemType != RpcQueueContainer.QueueItemType.None)
                    {
                        advanceFrameHistory = true;
                        if (m_RpcQueueContainer.IsUsingBatching())
                        {
                            m_RpcBatcher.QueueItem(currentQueueItem);

                            if (isListening)
                            {
                                m_RpcBatcher.SendItems(k_BatchThreshold, SendCallback);
                            }
                        }
                        else
                        {
                            if (isListening)
                            {
                                SendFrameQueueItem(currentQueueItem);
                            }
                        }

                        currentQueueItem = currentFrame.GetNextQueueItem();
                    }

                    //If the size is < m_BatchThreshold then just send the messages
                    if (advanceFrameHistory && m_RpcQueueContainer.IsUsingBatching())
                    {
                        m_RpcBatcher.SendItems(0, SendCallback);
                    }
                }

                //If we processed any RPCs, then advance to the next frame
                if (advanceFrameHistory)
                {
                    m_RpcQueueContainer.AdvanceFrameHistory(RpcQueueHistoryFrame.QueueFrameType.Outbound);
                }
            }
        }

        /// <summary>
        /// This is the callback from the batcher when it need to send a batch
        /// </summary>
        /// <param name="clientId"> clientId to send to</param>
        /// <param name="sendStream"> the stream to send</param>
        private void SendCallback(ulong clientId, RpcBatcher.SendStream sendStream)
        {
            var length = (int)sendStream.Buffer.Length;
            var bytes = sendStream.Buffer.GetBuffer();
            var sendBuffer = new ArraySegment<byte>(bytes, 0, length);

            m_RpcQueueContainer.NetworkManager.NetworkConfig.NetworkTransport.Send(clientId, sendBuffer, sendStream.NetworkChannel);
        }

        /// <summary>
        /// SendFrameQueueItem
        /// Sends the RPC Queue Item to the specified destination
        /// </summary>
        /// <param name="queueItem">Information on what to send</param>
        private void SendFrameQueueItem(RpcFrameQueueItem queueItem)
        {
            switch (queueItem.QueueItemType)
            {
                case RpcQueueContainer.QueueItemType.ServerRpc:
                    {
                        m_RpcQueueContainer.NetworkManager.NetworkConfig.NetworkTransport.Send(queueItem.NetworkId, queueItem.MessageData, queueItem.NetworkChannel);

                        //For each packet sent, we want to record how much data we have sent

                        PerformanceDataManager.Increment(ProfilerConstants.ByteSent, (int)queueItem.StreamSize);
                        PerformanceDataManager.Increment(ProfilerConstants.RpcSent);
                        break;
                    }
                case RpcQueueContainer.QueueItemType.ClientRpc:
                    {
                        foreach (ulong clientid in queueItem.ClientNetworkIds)
                        {
                            m_RpcQueueContainer.NetworkManager.NetworkConfig.NetworkTransport.Send(clientid, queueItem.MessageData, queueItem.NetworkChannel);

                            //For each packet sent, we want to record how much data we have sent
                            PerformanceDataManager.Increment(ProfilerConstants.ByteSent, (int)queueItem.StreamSize);
                        }

                        //For each client we send to, we want to record how many RPCs we have sent
                        PerformanceDataManager.Increment(ProfilerConstants.RpcSent, queueItem.ClientNetworkIds.Length);

                        break;
                    }
            }
        }

        internal RpcQueueProcessor(RpcQueueContainer rpcQueueContainer, NetworkManager networkManager)
        {
            m_RpcQueueContainer = rpcQueueContainer;
            m_NetworkManager = networkManager;
        }
    }
}
