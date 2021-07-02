using System;
using System.Collections.Generic;
using MLAPI.Serialization;
using MLAPI.Serialization.Pooled;
using MLAPI.Profiling;
using MLAPI.Transports;
using MLAPI.Logging;

namespace MLAPI.Messaging
{
    /// <summary>
    /// RpcQueueContainer
    /// Handles the management of an Rpc Queue
    /// </summary>
    internal class RpcQueueContainer : INetworkUpdateSystem, IDisposable
    {
        private const int k_MinQueueHistory = 2; //We need a minimum of 2 queue history buffers in order to properly handle looping back Rpcs when a host

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int s_RpcQueueContainerInstances;
#endif

        public enum QueueItemType
        {
            ServerRpc,
            ClientRpc,
            CreateObject, //MLAPI Constant *** We need to determine if these belong here ***
            DestroyObject, //MLAPI Constant

            None //Indicates end of frame
        }

        public enum RpcQueueProcessingTypes
        {
            Send,
            Receive,
        }

        // Inbound and Outbound QueueHistoryFrames
        private readonly Dictionary<RpcQueueHistoryFrame.QueueFrameType, Dictionary<int, Dictionary<NetworkUpdateStage, RpcQueueHistoryFrame>>> m_QueueHistory =
            new Dictionary<RpcQueueHistoryFrame.QueueFrameType, Dictionary<int, Dictionary<NetworkUpdateStage, RpcQueueHistoryFrame>>>();

        private RpcQueueProcessor m_RpcQueueProcessor;

        private uint m_OutboundFramesProcessed;
        private uint m_InboundFramesProcessed;
        private uint m_MaxFrameHistory;
        private int m_InboundStreamBufferIndex;
        private int m_OutBoundStreamBufferIndex;
        private bool m_IsTestingEnabled;
        private bool m_ProcessUpdateStagesExternally;
        private bool m_IsNotUsingBatching;

        internal readonly NetworkManager NetworkManager;

        /// <summary>
        /// Returns if batching is enabled
        /// </summary>
        /// <returns>true or false</returns>
        public bool IsUsingBatching()
        {
            return !m_IsNotUsingBatching;
        }

        /// <summary>
        /// Enables or disables batching
        /// </summary>
        /// <param name="isbatchingEnabled">true or false</param>
        public void EnableBatchedRpcs(bool isbatchingEnabled)
        {
            m_IsNotUsingBatching = !isbatchingEnabled;
        }

        /// <summary>
        /// Returns how many frames have been processed (Inbound/Outbound)
        /// </summary>
        /// <param name="queueType"></param>
        /// <returns>number of frames procssed</returns>
        public uint GetStreamBufferFrameCount(RpcQueueHistoryFrame.QueueFrameType queueType)
        {
            return queueType == RpcQueueHistoryFrame.QueueFrameType.Inbound ? m_InboundFramesProcessed : m_OutboundFramesProcessed;
        }

        /// <summary>
        /// NSS-TODO: This will need to be removed once we determine how we want to handle specific
        /// internal MLAPI commands relative to RPCS.
        /// Example: An network object is destroyed via server side (internal mlapi) command, but prior to this several RPCs are invoked for the to be destroyed object (Client RPC)
        /// If both the DestroyObject internal mlapi command and the ClientRPCs are received in the same frame but the internal mlapi DestroyObject command is processed prior to the
        /// RPCs being invoked then the object won't exist and additional warnings will be logged that the object no longer exists.
        /// The vices versa scenario (create and then RPCs sent) is an unlikely/improbable scenario, but just in case added the CreateObject to this special case scenario.
        ///
        /// To avoid the DestroyObject scenario, the internal MLAPI commands (DestroyObject and CreateObject) are always invoked after RPCs.
        /// </summary>
        /// <param name="queueItem">item to add to the internal MLAPI queue</param>
        public void AddToInternalMLAPISendQueue(RpcFrameQueueItem queueItem)
        {
            m_RpcQueueProcessor.QueueInternalMLAPICommand(queueItem);
        }

        /// <summary>
        /// Will process the RPC queue and then move to the next available frame
        /// </summary>
        /// <param name="queueType">Inbound or Outbound</param>
        /// <param name="currentUpdateStage">Network Update Stage assigned RpcQueueHistoryFrame to be processed and flushed</param>
        public void ProcessAndFlushRpcQueue(RpcQueueProcessingTypes queueType, NetworkUpdateStage currentUpdateStage)
        {
            bool isListening = !ReferenceEquals(NetworkManager, null) && NetworkManager.IsListening;
            switch (queueType)
            {
                case RpcQueueProcessingTypes.Receive:
                    {
                        m_RpcQueueProcessor.ProcessReceiveQueue(currentUpdateStage, m_IsTestingEnabled);
                        break;
                    }
                case RpcQueueProcessingTypes.Send:
                    {
                        m_RpcQueueProcessor.ProcessSendQueue(isListening);
                        break;
                    }
            }
        }

        /// <summary>
        /// Gets the current frame for the Inbound or Outbound queue
        /// </summary>
        /// <param name="qType">Inbound or Outbound</param>
        /// <param name="currentUpdateStage">Network Update Stage the RpcQueueHistoryFrame is assigned to</param>
        /// <returns>QueueHistoryFrame</returns>
        public RpcQueueHistoryFrame GetCurrentFrame(RpcQueueHistoryFrame.QueueFrameType qType, NetworkUpdateStage currentUpdateStage)
        {
            if (m_QueueHistory.ContainsKey(qType))
            {
                int streamBufferIndex = GetStreamBufferIndex(qType);

                if (m_QueueHistory[qType].ContainsKey(streamBufferIndex))
                {
                    if (m_QueueHistory[qType][streamBufferIndex].ContainsKey(currentUpdateStage))
                    {
                        return m_QueueHistory[qType][streamBufferIndex][currentUpdateStage];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the queue type's current stream buffer index
        /// </summary>
        /// <param name="queueType">Inbound or Outbound</param>
        /// <returns></returns>
        private int GetStreamBufferIndex(RpcQueueHistoryFrame.QueueFrameType queueType)
        {
            return queueType == RpcQueueHistoryFrame.QueueFrameType.Inbound ? m_InboundStreamBufferIndex : m_OutBoundStreamBufferIndex;
        }

        /// <summary>
        /// Progresses the current frame to the next QueueHistoryFrame for the QueueHistoryFrame.QueueFrameType.
        /// All other frames other than the current frame is considered the live rollback history
        /// </summary>
        /// <param name="queueType">Inbound or Outbound</param>
        public void AdvanceFrameHistory(RpcQueueHistoryFrame.QueueFrameType queueType)
        {
            int streamBufferIndex = GetStreamBufferIndex(queueType);

            if (!m_QueueHistory.ContainsKey(queueType))
            {
                UnityEngine.Debug.LogError($"You must initialize the {nameof(RpcQueueContainer)} before using MLAPI!");
                return;
            }

            if (!m_QueueHistory[queueType].ContainsKey(streamBufferIndex))
            {
                UnityEngine.Debug.LogError($"{nameof(RpcQueueContainer)} {queueType} queue stream buffer index out of range! [{streamBufferIndex}]");
                return;
            }


            foreach (KeyValuePair<NetworkUpdateStage, RpcQueueHistoryFrame> queueHistoryByUpdates in m_QueueHistory[queueType][streamBufferIndex])
            {
                var rpcQueueHistoryItem = queueHistoryByUpdates.Value;

                //This only gets reset when we advanced to next frame (do not reset this in the ResetQueueHistoryFrame)
                rpcQueueHistoryItem.HasLoopbackData = false;

                if (rpcQueueHistoryItem.QueueItemOffsets.Count > 0)
                {
                    PerformanceDataManager.Increment(
                        queueType == RpcQueueHistoryFrame.QueueFrameType.Inbound
                            ? ProfilerConstants.RpcInQueueSize
                            : ProfilerConstants.RpcOutQueueSize, (int) rpcQueueHistoryItem.TotalSize);
                }

                ResetQueueHistoryFrame(rpcQueueHistoryItem);
                IncrementAndSetQueueHistoryFrame(rpcQueueHistoryItem);
            }

            //Roll to the next stream buffer
            streamBufferIndex++;

            //If we have hit our maximum history, roll back over to the first one
            if (streamBufferIndex >= m_MaxFrameHistory)
            {
                streamBufferIndex = 0;
            }

            if (queueType == RpcQueueHistoryFrame.QueueFrameType.Inbound)
            {
                m_InboundStreamBufferIndex = streamBufferIndex;
            }
            else
            {
                m_OutBoundStreamBufferIndex = streamBufferIndex;
            }
        }

        /// <summary>
        /// IncrementAndSetQueueHistoryFrame
        /// Increments and sets frame count for this queue frame
        /// </summary>
        /// <param name="rpcQueueFrame">QueueHistoryFrame to be reset</param>
        private void IncrementAndSetQueueHistoryFrame(RpcQueueHistoryFrame rpcQueueFrame)
        {
            if (rpcQueueFrame.GetQueueFrameType() == RpcQueueHistoryFrame.QueueFrameType.Inbound)
            {
                m_InboundFramesProcessed++;
            }
            else
            {
                m_OutboundFramesProcessed++;
            }
        }

        /// <summary>
        /// ResetQueueHistoryFrame
        /// Resets the queue history frame passed to this method
        /// </summary>
        /// <param name="rpcQueueFrame">QueueHistoryFrame to be reset</param>
        private static void ResetQueueHistoryFrame(RpcQueueHistoryFrame rpcQueueFrame)
        {
            //If we are dirt and have loopback data then don't clear this frame
            if (rpcQueueFrame.IsDirty && !rpcQueueFrame.HasLoopbackData)
            {
                rpcQueueFrame.TotalSize = 0;
                rpcQueueFrame.QueueItemOffsets.Clear();
                rpcQueueFrame.QueueBuffer.Position = 0;
                rpcQueueFrame.MarkCurrentStreamPosition();
                rpcQueueFrame.IsDirty = false;
            }
        }

        /// <summary>
        /// AddQueueItemToInboundFrame
        /// Adds an RPC queue item to the outbound frame
        /// </summary>
        /// <param name="qItemType">type of rpc (client or server)</param>
        /// <param name="timeStamp">when it was received</param>
        /// <param name="sourceNetworkId">who sent the rpc</param>
        /// <param name="message">the message being received</param>
        internal void AddQueueItemToInboundFrame(QueueItemType qItemType, float timeStamp, ulong sourceNetworkId, NetworkBuffer message)
        {
            long originalPosition = message.Position;

            NetworkUpdateStage updateStage;

            using (var reader = PooledNetworkReader.Get(message))
            {
                var longValue = reader.ReadUInt64Packed(); // NetworkObjectId (temporary, we reset position just below)
                var shortValue = reader.ReadUInt16Packed(); // NetworkBehaviourId (temporary, we reset position just below)
                var intValue = reader.ReadUInt32Packed(); // NetworkRpcMethodId (temporary, we reset position just below)
                updateStage = (NetworkUpdateStage)reader.ReadByteDirect();
            }

            message.Position = originalPosition;
            var rpcQueueHistoryItem = GetQueueHistoryFrame(RpcQueueHistoryFrame.QueueFrameType.Inbound, updateStage);
            rpcQueueHistoryItem.IsDirty = true;

            long startPosition = rpcQueueHistoryItem.QueueBuffer.Position;

            //Write the packed version of the queueItem to our current queue history buffer
            rpcQueueHistoryItem.QueueWriter.WriteUInt16((ushort)qItemType);
            rpcQueueHistoryItem.QueueWriter.WriteSingle(timeStamp);
            rpcQueueHistoryItem.QueueWriter.WriteUInt64(sourceNetworkId);

            //Inbound we copy the entire packet and store the position offset
            long streamSize = message.Length;
            rpcQueueHistoryItem.QueueWriter.WriteInt64(streamSize);
            rpcQueueHistoryItem.QueueWriter.WriteInt64(message.Position);
            rpcQueueHistoryItem.QueueWriter.WriteBytes(message.GetBuffer(), streamSize);

            //Add the packed size to the offsets for parsing over various entries
            rpcQueueHistoryItem.QueueItemOffsets.Add((uint)rpcQueueHistoryItem.QueueBuffer.Position);

            //Calculate the packed size based on stream progression
            rpcQueueHistoryItem.TotalSize += (uint)(rpcQueueHistoryItem.QueueBuffer.Position - startPosition);
        }

        /// <summary>
        /// SetLoopBackFrameItem
        /// ***Temporary fix for host mode loopback RPC writer work-around
        /// Sets the next frame inbond buffer as the loopback queue history frame in the current frame's outbound buffer
        /// </summary>
        /// <param name="updateStage"></param>
        public void SetLoopBackFrameItem(NetworkUpdateStage updateStage)
        {
            //Get the next frame's inbound queue history frame
            var loopbackHistoryframe = GetQueueHistoryFrame(RpcQueueHistoryFrame.QueueFrameType.Inbound, updateStage, true);

            //Get the current frame's outbound queue history frame
            var rpcQueueHistoryItem = GetQueueHistoryFrame(RpcQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate, false);

            if (rpcQueueHistoryItem != null)
            {
                rpcQueueHistoryItem.LoopbackHistoryFrame = loopbackHistoryframe;
            }
            else
            {
                UnityEngine.Debug.LogError($"Could not find the outbound {nameof(RpcQueueHistoryFrame)}!");
            }
        }

        /// <summary>
        /// GetLoopBackWriter
        /// Gets the loop back writer for the history frame (if one exists)
        /// ***Temporary fix for host mode loopback RPC writer work-around
        /// </summary>
        /// <param name="queueFrameType">type of queue frame</param>
        /// <param name="updateStage">state it should be invoked on</param>
        /// <returns></returns>
        public RpcQueueHistoryFrame GetLoopBackHistoryFrame(RpcQueueHistoryFrame.QueueFrameType queueFrameType, NetworkUpdateStage updateStage)
        {
            return GetQueueHistoryFrame(queueFrameType, updateStage, false);
        }

        /// <summary>
        /// BeginAddQueueItemToOutboundFrame
        /// Adds a queue item to the outbound queue frame
        /// </summary>
        /// <param name="qItemType">type of the queue item</param>
        /// <param name="timeStamp">when queue item was submitted</param>
        /// <param name="networkChannel">channel this queue item is being sent</param>
        /// <param name="sourceNetworkId">source network id of the sender</param>
        /// <param name="targetNetworkIds">target network id(s)</param>
        /// <param name="queueFrameType">type of queue frame</param>
        /// <param name="updateStage">what update stage the RPC should be invoked on</param>
        /// <returns>PooledNetworkWriter</returns>
        public PooledNetworkWriter BeginAddQueueItemToFrame(QueueItemType qItemType, float timeStamp, NetworkChannel networkChannel, ulong sourceNetworkId, ulong[] targetNetworkIds,
            RpcQueueHistoryFrame.QueueFrameType queueFrameType, NetworkUpdateStage updateStage)
        {
            bool getNextFrame = NetworkManager.IsHost && queueFrameType == RpcQueueHistoryFrame.QueueFrameType.Inbound;

            var rpcQueueHistoryItem = GetQueueHistoryFrame(queueFrameType, updateStage, getNextFrame);
            rpcQueueHistoryItem.IsDirty = true;

            //Write the packed version of the queueItem to our current queue history buffer
            rpcQueueHistoryItem.QueueWriter.WriteUInt16((ushort)qItemType);
            rpcQueueHistoryItem.QueueWriter.WriteSingle(timeStamp);
            rpcQueueHistoryItem.QueueWriter.WriteUInt64(sourceNetworkId);

            if (queueFrameType != RpcQueueHistoryFrame.QueueFrameType.Inbound)
            {
                rpcQueueHistoryItem.QueueWriter.WriteByte((byte)networkChannel);

                if (targetNetworkIds != null && targetNetworkIds.Length != 0)
                {
                    //In the event the host is one of the networkIds, for outbound we want to ignore it (at this spot only!!)
                    //Get a count of clients we are going to send to (and write into the buffer)
                    var numberOfClients = 0;
                    for (int i = 0; i < targetNetworkIds.Length; i++)
                    {
                        if (NetworkManager.IsHost && targetNetworkIds[i] == NetworkManager.ServerClientId)
                        {
                            continue;
                        }

                        numberOfClients++;
                    }

                    //Write our total number of clients
                    rpcQueueHistoryItem.QueueWriter.WriteInt32(numberOfClients);

                    //Now write the cliend ids
                    for (int i = 0; i < targetNetworkIds.Length; i++)
                    {
                        if (NetworkManager.IsHost && targetNetworkIds[i] == NetworkManager.ServerClientId)
                        {
                            continue;
                        }

                        rpcQueueHistoryItem.QueueWriter.WriteUInt64(targetNetworkIds[i]);
                    }
                }
                else
                {
                    rpcQueueHistoryItem.QueueWriter.WriteInt32(0);
                }
            }

            //Mark where we started in the stream to later determine the actual RPC message size (position before writing RPC message vs position after write has completed)
            rpcQueueHistoryItem.MarkCurrentStreamPosition();

            //Write a filler dummy size of 0 to hold this position in order to write to it once the RPC is done writing.
            rpcQueueHistoryItem.QueueWriter.WriteInt64(0);

            if (NetworkManager.IsHost && queueFrameType == RpcQueueHistoryFrame.QueueFrameType.Inbound)
            {
                if (!IsUsingBatching())
                {
                    rpcQueueHistoryItem.QueueWriter.WriteInt64(1);
                }
                else
                {
                    rpcQueueHistoryItem.QueueWriter.WriteInt64(0);
                }

                rpcQueueHistoryItem.HasLoopbackData = true; //The only case for this is when it is the Host
            }

            //Return the writer to the invoking method.
            return rpcQueueHistoryItem.QueueWriter;
        }

        /// <summary>
        /// Signifies the end of this outbound RPC.
        /// We store final MSG size and track the total current frame queue size
        /// </summary>
        /// <param name="writer">NetworkWriter that was used</param>
        /// <param name="queueFrameType">type of the queue frame that was used</param>
        /// <param name="updateStage">stage the RPC is going to be invoked</param>
        public void EndAddQueueItemToFrame(NetworkWriter writer, RpcQueueHistoryFrame.QueueFrameType queueFrameType, NetworkUpdateStage updateStage)
        {
            bool getNextFrame = NetworkManager.IsHost && queueFrameType == RpcQueueHistoryFrame.QueueFrameType.Inbound;

            var rpcQueueHistoryItem = GetQueueHistoryFrame(queueFrameType, updateStage, getNextFrame);
            var loopBackHistoryFrame = rpcQueueHistoryItem.LoopbackHistoryFrame;

            var pbWriter = (PooledNetworkWriter)writer;
            if (pbWriter != rpcQueueHistoryItem.QueueWriter && !getNextFrame)
            {
                UnityEngine.Debug.LogError($"{nameof(RpcQueueContainer)} {queueFrameType} passed writer is not the same as the current {nameof(PooledNetworkWriter)} for the {queueFrameType}!");
            }

            //The total size of the frame is the last known position of the stream
            rpcQueueHistoryItem.TotalSize = (uint)rpcQueueHistoryItem.QueueBuffer.Position;

            long currentPosition = rpcQueueHistoryItem.QueueBuffer.Position;
            ulong bitPosition = rpcQueueHistoryItem.QueueBuffer.BitPosition;

            //////////////////////////////////////////////////////////////
            //>>>> REPOSITIONING STREAM TO RPC MESSAGE SIZE LOCATION <<<<
            //////////////////////////////////////////////////////////////
            rpcQueueHistoryItem.QueueBuffer.Position = rpcQueueHistoryItem.GetCurrentMarkedPosition();

            long messageOffset = 8;
            if (getNextFrame && IsUsingBatching())
            {
                messageOffset += 8;
            }

            //subtracting 8 byte to account for the value of the size of the RPC  (why the 8 above in
            long messageSize = (long)(rpcQueueHistoryItem.TotalSize - (rpcQueueHistoryItem.GetCurrentMarkedPosition() + messageOffset));

            if (messageSize > 0)
            {
                //Write the actual size of the RPC message
                rpcQueueHistoryItem.QueueWriter.WriteInt64(messageSize);
            }
            else
            {
                UnityEngine.Debug.LogWarning("MSGSize of < zero detected!!  Setting message size to zero!");
                rpcQueueHistoryItem.QueueWriter.WriteInt64(0);
            }

            if (loopBackHistoryFrame != null)
            {
                if (messageSize > 0)
                {
                    //Point to where the size of the message is stored
                    loopBackHistoryFrame.QueueBuffer.Position = loopBackHistoryFrame.GetCurrentMarkedPosition();

                    //Write the actual size of the RPC message
                    loopBackHistoryFrame.QueueWriter.WriteInt64(messageSize);

                    if (!IsUsingBatching())
                    {
                        //Write the offset for the header info copied
                        loopBackHistoryFrame.QueueWriter.WriteInt64(1);
                    }
                    else
                    {
                        //Write the offset for the header info copied
                        loopBackHistoryFrame.QueueWriter.WriteInt64(0);
                    }

                    //Write RPC data
                    loopBackHistoryFrame.QueueWriter.WriteBytes(rpcQueueHistoryItem.QueueBuffer.GetBuffer(), messageSize, (int)rpcQueueHistoryItem.QueueBuffer.Position);

                    //Set the total size for this stream
                    loopBackHistoryFrame.TotalSize = (uint)loopBackHistoryFrame.QueueBuffer.Position;

                    //Add the total size to the offsets for parsing over various entries
                    loopBackHistoryFrame.QueueItemOffsets.Add((uint)loopBackHistoryFrame.QueueBuffer.Position);
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"{nameof(messageSize)} < zero detected! Setting message size to zero!");
                    //Write the actual size of the RPC message
                    loopBackHistoryFrame.QueueWriter.WriteInt64(0);
                }

                rpcQueueHistoryItem.LoopbackHistoryFrame = null;
            }

            //////////////////////////////////////////////////////////////
            //<<<< REPOSITIONING STREAM BACK TO THE CURRENT TAIL >>>>
            //////////////////////////////////////////////////////////////
            rpcQueueHistoryItem.QueueBuffer.Position = currentPosition;
            rpcQueueHistoryItem.QueueBuffer.BitPosition = bitPosition;

            //Add the packed size to the offsets for parsing over various entries
            rpcQueueHistoryItem.QueueItemOffsets.Add((uint)rpcQueueHistoryItem.QueueBuffer.Position);
        }

        /// <summary>
        /// Gets the current queue history frame (inbound or outbound)
        /// </summary>
        /// <param name="frameType">inbound or outbound</param>
        /// <param name="updateStage">network update stage the queue history frame is assigned to</param>
        /// <param name="getNextFrame">whether to get the next frame or not (true/false)</param>
        /// <returns>QueueHistoryFrame or null</returns>
        public RpcQueueHistoryFrame GetQueueHistoryFrame(RpcQueueHistoryFrame.QueueFrameType frameType, NetworkUpdateStage updateStage, bool getNextFrame = false)
        {
            int streamBufferIndex = GetStreamBufferIndex(frameType);

            //We want to write into the future/next frame
            if (getNextFrame)
            {
                streamBufferIndex++;

                //If we have hit our maximum history, roll back over to the first one
                if (streamBufferIndex >= m_MaxFrameHistory)
                {
                    streamBufferIndex = 0;
                }
            }

            if (!m_QueueHistory.ContainsKey(frameType))
            {
                UnityEngine.Debug.LogError($"{nameof(RpcQueueHistoryFrame)} {nameof(RpcQueueHistoryFrame.QueueFrameType)} {frameType} does not exist!");
                return null;
            }

            if (!m_QueueHistory[frameType].ContainsKey(streamBufferIndex))
            {
                UnityEngine.Debug.LogError($"{nameof(RpcQueueContainer)} {frameType} queue stream buffer index out of range! [{streamBufferIndex}]");
                return null;
            }

            if (!m_QueueHistory[frameType][streamBufferIndex].ContainsKey(updateStage))
            {
                UnityEngine.Debug.LogError($"{nameof(RpcQueueContainer)} {updateStage} update type does not exist!");
                return null;
            }

            return m_QueueHistory[frameType][streamBufferIndex][updateStage];
        }

        /// <summary>
        /// The NetworkUpdate method used by the NetworkUpdateLoop
        /// </summary>
        /// <param name="updateStage">the stage to process RPC Queues</param>
        public void NetworkUpdate(NetworkUpdateStage updateStage)
        {
            ProcessAndFlushRpcQueue(RpcQueueProcessingTypes.Receive, updateStage);

            if (updateStage == NetworkUpdateStage.PostLateUpdate)
            {
                ProcessAndFlushRpcQueue(RpcQueueProcessingTypes.Send, updateStage);
            }
        }

        /// <summary>
        /// This will allocate [maxFrameHistory] + [1 currentFrame] number of PooledNetworkBuffers and keep them open until the session ends
        /// Note: For zero frame history set maxFrameHistory to zero
        /// </summary>
        /// <param name="maxFrameHistory"></param>
        private void Initialize(uint maxFrameHistory)
        {
            //This makes sure that we don't exceed a ridiculous value by capping the number of queue history frames to ushort.MaxValue
            //If this value is exceeded, then it will be kept at the ceiling of ushort.Maxvalue.
            //Note: If running at a 60pps rate (16ms update frequency) this would yield 17.47 minutes worth of queue frame history.
            if (maxFrameHistory > ushort.MaxValue)
            {
                if (NetworkLog.CurrentLogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogWarning($"The {nameof(RpcQueueHistoryFrame)} size cannot exceed {ushort.MaxValue} {nameof(RpcQueueHistoryFrame)}s! Capping at {ushort.MaxValue} {nameof(RpcQueueHistoryFrame)}s.");
                }
                maxFrameHistory = ushort.MaxValue;
            }

            ClearParameters();

            m_RpcQueueProcessor = new RpcQueueProcessor(this, NetworkManager);
            m_MaxFrameHistory = maxFrameHistory + k_MinQueueHistory;

            if (!m_QueueHistory.ContainsKey(RpcQueueHistoryFrame.QueueFrameType.Inbound))
            {
                m_QueueHistory.Add(RpcQueueHistoryFrame.QueueFrameType.Inbound, new Dictionary<int, Dictionary<NetworkUpdateStage, RpcQueueHistoryFrame>>());
            }

            if (!m_QueueHistory.ContainsKey(RpcQueueHistoryFrame.QueueFrameType.Outbound))
            {
                m_QueueHistory.Add(RpcQueueHistoryFrame.QueueFrameType.Outbound, new Dictionary<int, Dictionary<NetworkUpdateStage, RpcQueueHistoryFrame>>());
            }

            for (int i = 0; i < m_MaxFrameHistory; i++)
            {
                if (!m_QueueHistory[RpcQueueHistoryFrame.QueueFrameType.Outbound].ContainsKey(i))
                {
                    m_QueueHistory[RpcQueueHistoryFrame.QueueFrameType.Outbound].Add(i, new Dictionary<NetworkUpdateStage, RpcQueueHistoryFrame>());
                    var queueHistoryFrame = new RpcQueueHistoryFrame(RpcQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);
                    queueHistoryFrame.QueueBuffer = PooledNetworkBuffer.Get();
                    queueHistoryFrame.QueueBuffer.Position = 0;
                    queueHistoryFrame.QueueWriter = PooledNetworkWriter.Get(queueHistoryFrame.QueueBuffer);
                    queueHistoryFrame.QueueReader = PooledNetworkReader.Get(queueHistoryFrame.QueueBuffer);
                    queueHistoryFrame.QueueItemOffsets = new List<uint>();

                    //For now all outbound, we will always have a single update in which they are processed (LATEUPDATE)
                    m_QueueHistory[RpcQueueHistoryFrame.QueueFrameType.Outbound][i].Add(NetworkUpdateStage.PostLateUpdate, queueHistoryFrame);
                }

                if (!m_QueueHistory[RpcQueueHistoryFrame.QueueFrameType.Inbound].ContainsKey(i))
                {
                    m_QueueHistory[RpcQueueHistoryFrame.QueueFrameType.Inbound].Add(i, new Dictionary<NetworkUpdateStage, RpcQueueHistoryFrame>());

                    //For inbound, we create a queue history frame per update stage
                    foreach (NetworkUpdateStage netUpdateStage in Enum.GetValues(typeof(NetworkUpdateStage)))
                    {
                        var rpcQueueHistoryFrame = new RpcQueueHistoryFrame(RpcQueueHistoryFrame.QueueFrameType.Inbound, netUpdateStage);
                        rpcQueueHistoryFrame.QueueBuffer = PooledNetworkBuffer.Get();
                        rpcQueueHistoryFrame.QueueBuffer.Position = 0;
                        rpcQueueHistoryFrame.QueueWriter = PooledNetworkWriter.Get(rpcQueueHistoryFrame.QueueBuffer);
                        rpcQueueHistoryFrame.QueueReader = PooledNetworkReader.Get(rpcQueueHistoryFrame.QueueBuffer);
                        rpcQueueHistoryFrame.QueueItemOffsets = new List<uint>();
                        m_QueueHistory[RpcQueueHistoryFrame.QueueFrameType.Inbound][i].Add(netUpdateStage, rpcQueueHistoryFrame);
                    }
                }
            }

            //As long as this instance is using the pre-defined update stages
            if (!m_ProcessUpdateStagesExternally)
            {
                //Register with the network update loop system
                this.RegisterAllNetworkUpdates();
            }
        }

        /// <summary>
        /// Clears the stream indices and frames process properties
        /// </summary>
        private void ClearParameters()
        {
            m_InboundStreamBufferIndex = 0;
            m_OutBoundStreamBufferIndex = 0;
            m_OutboundFramesProcessed = 0;
            m_InboundFramesProcessed = 0;
        }

        /// <summary>
        /// Flushes the internal messages
        /// Removes itself from the network update loop
        /// Disposes readers, writers, clears the queue history, and resets any parameters
        /// </summary>
        private void Shutdown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

            if (NetworkLog.CurrentLogLevel == LogLevel.Developer)
            {
                NetworkLog.LogInfo($"[Instance : {s_RpcQueueContainerInstances}] {nameof(RpcQueueContainer)} shutting down.");
            }
#endif
            //As long as this instance is using the pre-defined update stages
            if (!m_ProcessUpdateStagesExternally)
            {
                //Remove ourself from the network loop update system
                this.UnregisterAllNetworkUpdates();
            }

            //Make sure any remaining internal messages are sent before completely shutting down.
            m_RpcQueueProcessor.InternalMessagesSendAndFlush(NetworkManager.IsListening);

            //Dispose of any readers and writers
            foreach (var queueHistorySection in m_QueueHistory)
            {
                foreach (var queueHistoryItemByStage in queueHistorySection.Value)
                {
                    foreach (var queueHistoryItem in queueHistoryItemByStage.Value)
                    {
                        queueHistoryItem.Value.QueueWriter?.Dispose();
                        queueHistoryItem.Value.QueueReader?.Dispose();
                        queueHistoryItem.Value.QueueBuffer?.Dispose();
                    }
                }
            }

            //Clear history and parameters
            m_QueueHistory.Clear();

            ClearParameters();
        }

        /// <summary>
        /// Cleans up our instance count and warns if there instantiation issues
        /// </summary>
        public void Dispose()
        {
            Shutdown();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (s_RpcQueueContainerInstances > 0)
            {
                if (NetworkLog.CurrentLogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogInfo($"[Instance : {s_RpcQueueContainerInstances}] {nameof(RpcQueueContainer)} disposed.");
                }

                s_RpcQueueContainerInstances--;
            }
            else //This should never happen...if so something else has gone very wrong.
            {
                if (NetworkLog.CurrentLogLevel >= LogLevel.Normal)
                {
                    NetworkLog.LogError($"[*** Warning ***] {nameof(RpcQueueContainer)} is being disposed twice?");
                }

                throw new Exception("[*** Warning ***] System state is not stable!  Check all references to the Dispose method!");
            }
#endif
        }

        /// <summary>
        /// RpcQueueContainer - Constructor
        /// Note about processExternally: this values determines if it will register with the Network Update Loop
        /// or not.  If not, then most likely unit tests are being preformed on this class.  The default value is false.
        /// </summary>
        /// <param name="maxFrameHistory"></param>
        /// <param name="processExternally">determines if it handles processing externally</param>
        public RpcQueueContainer(NetworkManager networkManager, uint maxFrameHistory = 0, bool processExternally = false)
        {
            NetworkManager = networkManager;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Keep track of how many instances we have instantiated
            s_RpcQueueContainerInstances++;

            if (NetworkLog.CurrentLogLevel == LogLevel.Developer)
            {
                NetworkLog.LogInfo($"[Instance : {s_RpcQueueContainerInstances}] {nameof(RpcQueueContainer)} Initialized");
            }
#endif

            m_ProcessUpdateStagesExternally = processExternally;
            Initialize(maxFrameHistory);
        }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Enables testing of the RpcQueueContainer
        /// </summary>
        /// <param name="enabled"></param>
        public void SetTestingState(bool enabled)
        {
            m_IsTestingEnabled = enabled;
        }
#endif
    }
}
