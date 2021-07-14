using System;
using System.Collections.Generic;
using System.Linq;
using MLAPI.Logging;
using MLAPI.Profiling;
using MLAPI.Serialization;
using MLAPI.Serialization.Pooled;
using MLAPI.Transports;
using UnityEngine;

namespace MLAPI.Messaging
{
    /// <summary>
    /// MessageQueueContainer
    /// Handles the management of a Message Queue
    /// </summary>
    internal class MessageQueueContainer : INetworkUpdateSystem, IDisposable
    {
        private const int k_MinQueueHistory = 2; //We need a minimum of 2 queue history buffers in order to properly handle looping back messages when a host

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int s_MessageQueueContainerInstances;
#endif

        public enum MessageType
        {
            ConnectionRequest,
            ConnectionApproved,
            ClientRpc,
            ServerRpc,
            CreateObject,
            CreateObjects,
            DestroyObject,
            DestroyObjects,
            ChangeOwner,
            TimeSync,
            UnnamedMessage,
            NamedMessage,
            ServerLog,
            SnapshotData,
            NetworkVariableDelta,
            SwitchScene,
            ClientSwitchSceneCompleted,
            AllClientsLoadedScene,
            ParentSync,

            None //Indicates end of frame
        }

        public enum MessageQueueProcessingTypes
        {
            Send,
            Receive,
        }

        // Inbound and Outbound QueueHistoryFrames
        private readonly Dictionary<MessageQueueHistoryFrame.QueueFrameType, Dictionary<int, Dictionary<NetworkUpdateStage, MessageQueueHistoryFrame>>> m_QueueHistory =
            new Dictionary<MessageQueueHistoryFrame.QueueFrameType, Dictionary<int, Dictionary<NetworkUpdateStage, MessageQueueHistoryFrame>>>();

        private MessageQueueProcessor m_MessageQueueProcessor;

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
        public void EnableBatchedMessages(bool isbatchingEnabled)
        {
            m_IsNotUsingBatching = !isbatchingEnabled;
        }

        /// <summary>
        /// Returns how many frames have been processed (Inbound/Outbound)
        /// </summary>
        /// <param name="queueType"></param>
        /// <returns>number of frames procssed</returns>
        public uint GetStreamBufferFrameCount(MessageQueueHistoryFrame.QueueFrameType queueType)
        {
            return queueType == MessageQueueHistoryFrame.QueueFrameType.Inbound ? m_InboundFramesProcessed : m_OutboundFramesProcessed;
        }

        /// <summary>
        /// Creates a context for an internal command.
        /// The context contains a NetworkWriter property used to fill out the command body.
        /// If used as IDisposable, the command will be sent at the end of the using() block.
        /// If not used as IDisposable, the command can be sent by calling context.Finalize()
        /// </summary>
        /// <param name="messageType">The type of message being sent</param>
        /// <param name="transportChannel">The channel the message is being sent on</param>
        /// <param name="clientIds">The destinations for this message</param>
        /// <param name="updateStage">The stage at which the message will be processed on the receiving side</param>
        /// <returns></returns>
        internal InternalCommandContext? EnterInternalCommandContext(MessageType messageType, NetworkChannel transportChannel, ulong[] clientIds, NetworkUpdateStage updateStage)
        {
            PooledNetworkWriter writer;
            if (updateStage == NetworkUpdateStage.Initialization)
            {
                NetworkLog.LogWarning($"Trying to send a message of type {messageType} to be executed during Initialization stage. Changing to EarlyUpdate.");
                updateStage = NetworkUpdateStage.EarlyUpdate;
            }

            if (NetworkManager.IsServer)
            {
                clientIds = clientIds.Where(id => id != NetworkManager.ServerClientId).ToArray();
            }

            if (clientIds.Length == 0)
            {
                return null;
            }

            writer = BeginAddQueueItemToFrame(messageType, Time.realtimeSinceStartup, transportChannel, 0,
                clientIds, MessageQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);

            writer.WriteByte((byte)messageType);
            writer.WriteByte((byte)updateStage); // NetworkUpdateStage

            return new InternalCommandContext(writer, clientIds, updateStage, this);
        }


        /// <summary>
        /// Will process the message queue and then move to the next available frame
        /// </summary>
        /// <param name="queueType">Inbound or Outbound</param>
        /// <param name="currentUpdateStage">Network Update Stage assigned MessageQueueHistoryFrame to be processed and flushed</param>
        public void ProcessAndFlushMessageQueue(MessageQueueProcessingTypes queueType, NetworkUpdateStage currentUpdateStage)
        {
            bool isListening = !ReferenceEquals(NetworkManager, null) && NetworkManager.IsListening;
            switch (queueType)
            {
                case MessageQueueProcessingTypes.Receive:
                    {
                        m_MessageQueueProcessor.ProcessReceiveQueue(currentUpdateStage, m_IsTestingEnabled);
                        break;
                    }
                case MessageQueueProcessingTypes.Send:
                    {
                        m_MessageQueueProcessor.ProcessSendQueue(isListening);
                        break;
                    }
            }
        }

        /// <summary>
        /// Gets the current frame for the Inbound or Outbound queue
        /// </summary>
        /// <param name="qType">Inbound or Outbound</param>
        /// <param name="currentUpdateStage">Network Update Stage the MessageQueueHistoryFrame is assigned to</param>
        /// <returns>QueueHistoryFrame</returns>
        public MessageQueueHistoryFrame GetCurrentFrame(MessageQueueHistoryFrame.QueueFrameType qType, NetworkUpdateStage currentUpdateStage)
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
        private int GetStreamBufferIndex(MessageQueueHistoryFrame.QueueFrameType queueType)
        {
            return queueType == MessageQueueHistoryFrame.QueueFrameType.Inbound ? m_InboundStreamBufferIndex : m_OutBoundStreamBufferIndex;
        }

        /// <summary>
        /// Progresses the current frame to the next QueueHistoryFrame for the QueueHistoryFrame.QueueFrameType.
        /// All other frames other than the current frame is considered the live rollback history
        /// </summary>
        /// <param name="queueType">Inbound or Outbound</param>
        public void AdvanceFrameHistory(MessageQueueHistoryFrame.QueueFrameType queueType)
        {
            int streamBufferIndex = GetStreamBufferIndex(queueType);

            if (!m_QueueHistory.ContainsKey(queueType))
            {
                Debug.LogError($"You must initialize the {nameof(MessageQueueContainer)} before using MLAPI!");
                return;
            }

            if (!m_QueueHistory[queueType].ContainsKey(streamBufferIndex))
            {
                Debug.LogError($"{nameof(MessageQueueContainer)} {queueType} queue stream buffer index out of range! [{streamBufferIndex}]");
                return;
            }


            foreach (KeyValuePair<NetworkUpdateStage, MessageQueueHistoryFrame> queueHistoryByUpdates in m_QueueHistory[queueType][streamBufferIndex])
            {
                var messageQueueHistoryItem = queueHistoryByUpdates.Value;

                //This only gets reset when we advanced to next frame (do not reset this in the ResetQueueHistoryFrame)
                messageQueueHistoryItem.HasLoopbackData = false;

                if (messageQueueHistoryItem.QueueItemOffsets.Count > 0)
                {
                    if (queueType == MessageQueueHistoryFrame.QueueFrameType.Inbound)
                    {
                        ProfilerStatManager.MessageInQueueSize.Record((int)messageQueueHistoryItem.TotalSize);
                        PerformanceDataManager.Increment(ProfilerConstants.MessageInQueueSize, (int)messageQueueHistoryItem.TotalSize);
                    }
                    else
                    {
                        ProfilerStatManager.MessageOutQueueSize.Record((int)messageQueueHistoryItem.TotalSize);
                        PerformanceDataManager.Increment(ProfilerConstants.MessageOutQueueSize, (int)messageQueueHistoryItem.TotalSize);
                    }
                }

                ResetQueueHistoryFrame(messageQueueHistoryItem);
                IncrementAndSetQueueHistoryFrame(messageQueueHistoryItem);
            }

            //Roll to the next stream buffer
            streamBufferIndex++;

            //If we have hit our maximum history, roll back over to the first one
            if (streamBufferIndex >= m_MaxFrameHistory)
            {
                streamBufferIndex = 0;
            }

            if (queueType == MessageQueueHistoryFrame.QueueFrameType.Inbound)
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
        /// <param name="messageQueueFrame">QueueHistoryFrame to be reset</param>
        private void IncrementAndSetQueueHistoryFrame(MessageQueueHistoryFrame messageQueueFrame)
        {
            if (messageQueueFrame.GetQueueFrameType() == MessageQueueHistoryFrame.QueueFrameType.Inbound)
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
        /// <param name="messageQueueFrame">QueueHistoryFrame to be reset</param>
        private static void ResetQueueHistoryFrame(MessageQueueHistoryFrame messageQueueFrame)
        {
            //If we are dirt and have loopback data then don't clear this frame
            if (messageQueueFrame.IsDirty && !messageQueueFrame.HasLoopbackData)
            {
                messageQueueFrame.TotalSize = 0;
                messageQueueFrame.QueueItemOffsets.Clear();
                messageQueueFrame.QueueBuffer.Position = 0;
                messageQueueFrame.MarkCurrentStreamPosition();
                messageQueueFrame.IsDirty = false;
            }
        }

        /// <summary>
        /// AddQueueItemToInboundFrame
        /// Adds a message queue item to the outbound frame
        /// </summary>
        /// <param name="qItemType">type of message</param>
        /// <param name="timeStamp">when it was received</param>
        /// <param name="sourceNetworkId">who sent the message</param>
        /// <param name="message">the message being received</param>
        internal void AddQueueItemToInboundFrame(MessageType qItemType, float timeStamp, ulong sourceNetworkId, NetworkBuffer message, NetworkChannel receiveChannel)
        {
            var updateStage = (NetworkUpdateStage)message.ReadByte();

            var messageFrameItem = GetQueueHistoryFrame(MessageQueueHistoryFrame.QueueFrameType.Inbound, updateStage);
            messageFrameItem.IsDirty = true;

            long startPosition = messageFrameItem.QueueBuffer.Position;

            //Write the packed version of the queueItem to our current queue history buffer
            messageFrameItem.QueueWriter.WriteUInt16((ushort)qItemType);
            messageFrameItem.QueueWriter.WriteSingle(timeStamp);
            messageFrameItem.QueueWriter.WriteUInt64(sourceNetworkId);
            messageFrameItem.QueueWriter.WriteByte((byte)receiveChannel);

            //Inbound we copy the entire packet and store the position offset
            long streamSize = message.Length - message.Position;
            messageFrameItem.QueueWriter.WriteInt64(streamSize);
            messageFrameItem.QueueWriter.WriteInt64(0);
            messageFrameItem.QueueWriter.WriteBytes(message.GetBuffer(), streamSize, (int)message.Position);

            //Add the packed size to the offsets for parsing over various entries
            messageFrameItem.QueueItemOffsets.Add((uint)messageFrameItem.QueueBuffer.Position);

            //Calculate the packed size based on stream progression
            messageFrameItem.TotalSize += (uint)(messageFrameItem.QueueBuffer.Position - startPosition);
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
            var loopbackHistoryframe = GetQueueHistoryFrame(MessageQueueHistoryFrame.QueueFrameType.Inbound, updateStage, true);

            //Get the current frame's outbound queue history frame
            var messageQueueHistoryItem = GetQueueHistoryFrame(MessageQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);

            if (messageQueueHistoryItem != null)
            {
                messageQueueHistoryItem.LoopbackHistoryFrame = loopbackHistoryframe;
            }
            else
            {
                Debug.LogError($"Could not find the outbound {nameof(MessageQueueHistoryFrame)}!");
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
        public MessageQueueHistoryFrame GetLoopBackHistoryFrame(MessageQueueHistoryFrame.QueueFrameType queueFrameType, NetworkUpdateStage updateStage)
        {
            return GetQueueHistoryFrame(queueFrameType, updateStage);
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
        public PooledNetworkWriter BeginAddQueueItemToFrame(MessageType qItemType, float timeStamp, NetworkChannel networkChannel, ulong sourceNetworkId, ulong[] targetNetworkIds,
            MessageQueueHistoryFrame.QueueFrameType queueFrameType, NetworkUpdateStage updateStage)
        {
            bool getNextFrame = NetworkManager.IsHost && queueFrameType == MessageQueueHistoryFrame.QueueFrameType.Inbound;

            var messageQueueHistoryFrame = GetQueueHistoryFrame(queueFrameType, updateStage, getNextFrame);
            messageQueueHistoryFrame.IsDirty = true;

            //Write the packed version of the queueItem to our current queue history buffer
            messageQueueHistoryFrame.QueueWriter.WriteUInt16((ushort)qItemType);
            messageQueueHistoryFrame.QueueWriter.WriteSingle(timeStamp);
            messageQueueHistoryFrame.QueueWriter.WriteUInt64(sourceNetworkId);
            messageQueueHistoryFrame.QueueWriter.WriteByte((byte)networkChannel);

            if (queueFrameType != MessageQueueHistoryFrame.QueueFrameType.Inbound)
            {
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
                    messageQueueHistoryFrame.QueueWriter.WriteInt32(numberOfClients);

                    //Now write the cliend ids
                    for (int i = 0; i < targetNetworkIds.Length; i++)
                    {
                        if (NetworkManager.IsHost && targetNetworkIds[i] == NetworkManager.ServerClientId)
                        {
                            continue;
                        }

                        messageQueueHistoryFrame.QueueWriter.WriteUInt64(targetNetworkIds[i]);
                    }
                }
                else
                {
                    messageQueueHistoryFrame.QueueWriter.WriteInt32(0);
                }
            }

            //Mark where we started in the stream to later determine the actual RPC message size (position before writing RPC message vs position after write has completed)
            messageQueueHistoryFrame.MarkCurrentStreamPosition();

            //Write a filler dummy size of 0 to hold this position in order to write to it once the RPC is done writing.
            messageQueueHistoryFrame.QueueWriter.WriteInt64(0);

            if (NetworkManager.IsHost && queueFrameType == MessageQueueHistoryFrame.QueueFrameType.Inbound)
            {
                if (!IsUsingBatching())
                {
                    messageQueueHistoryFrame.QueueWriter.WriteInt64(1);
                }
                else
                {
                    messageQueueHistoryFrame.QueueWriter.WriteInt64(0);
                }

                messageQueueHistoryFrame.HasLoopbackData = true; //The only case for this is when it is the Host
            }

            //Return the writer to the invoking method.
            return messageQueueHistoryFrame.QueueWriter;
        }

        /// <summary>
        /// Signifies the end of this outbound RPC.
        /// We store final MSG size and track the total current frame queue size
        /// </summary>
        /// <param name="writer">NetworkWriter that was used</param>
        /// <param name="queueFrameType">type of the queue frame that was used</param>
        /// <param name="updateStage">stage the RPC is going to be invoked</param>
        public void EndAddQueueItemToFrame(NetworkWriter writer, MessageQueueHistoryFrame.QueueFrameType queueFrameType, NetworkUpdateStage updateStage)
        {
            bool getNextFrame = NetworkManager.IsHost && queueFrameType == MessageQueueHistoryFrame.QueueFrameType.Inbound;

            var messageQueueHistoryItem = GetQueueHistoryFrame(queueFrameType, updateStage, getNextFrame);
            var loopBackHistoryFrame = messageQueueHistoryItem.LoopbackHistoryFrame;

            var pbWriter = (PooledNetworkWriter)writer;
            if (pbWriter != messageQueueHistoryItem.QueueWriter)
            {
                Debug.LogError($"{nameof(MessageQueueContainer)} {queueFrameType} passed writer is not the same as the current {nameof(PooledNetworkWriter)} for the {queueFrameType}!");
            }

            //The total size of the frame is the last known position of the stream
            messageQueueHistoryItem.TotalSize = (uint)messageQueueHistoryItem.QueueBuffer.Position;

            long currentPosition = messageQueueHistoryItem.QueueBuffer.Position;
            ulong bitPosition = messageQueueHistoryItem.QueueBuffer.BitPosition;

            //////////////////////////////////////////////////////////////
            //>>>> REPOSITIONING STREAM TO RPC MESSAGE SIZE LOCATION <<<<
            //////////////////////////////////////////////////////////////
            messageQueueHistoryItem.QueueBuffer.Position = messageQueueHistoryItem.GetCurrentMarkedPosition();

            long messageOffset = 8;
            if (getNextFrame && IsUsingBatching())
            {
                messageOffset += 8;
            }

            //subtracting 8 byte to account for the value of the size of the RPC  (why the 8 above in
            long messageSize = messageQueueHistoryItem.TotalSize - (messageQueueHistoryItem.GetCurrentMarkedPosition() + messageOffset);

            if (messageSize > 0)
            {
                //Write the actual size of the RPC message
                messageQueueHistoryItem.QueueWriter.WriteInt64(messageSize);
            }
            else
            {
                Debug.LogWarning("MSGSize of < zero detected!!  Setting message size to zero!");
                messageQueueHistoryItem.QueueWriter.WriteInt64(0);
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

                    //Write message data
                    loopBackHistoryFrame.QueueWriter.WriteBytes(
                        messageQueueHistoryItem.QueueBuffer.GetBuffer(),
                        messageSize,
                        (int)messageQueueHistoryItem.QueueBuffer.Position + 2 // Skip the 2 byte header
                    );

                    //Set the total size for this stream
                    loopBackHistoryFrame.TotalSize = (uint)loopBackHistoryFrame.QueueBuffer.Position;

                    //Add the total size to the offsets for parsing over various entries
                    loopBackHistoryFrame.QueueItemOffsets.Add((uint)loopBackHistoryFrame.QueueBuffer.Position);
                }
                else
                {
                    Debug.LogWarning($"{nameof(messageSize)} < zero detected! Setting message size to zero!");
                    //Write the actual size of the RPC message
                    loopBackHistoryFrame.QueueWriter.WriteInt64(0);
                }

                messageQueueHistoryItem.LoopbackHistoryFrame = null;
            }

            //////////////////////////////////////////////////////////////
            //<<<< REPOSITIONING STREAM BACK TO THE CURRENT TAIL >>>>
            //////////////////////////////////////////////////////////////
            messageQueueHistoryItem.QueueBuffer.Position = currentPosition;
            messageQueueHistoryItem.QueueBuffer.BitPosition = bitPosition;

            //Add the packed size to the offsets for parsing over various entries
            messageQueueHistoryItem.QueueItemOffsets.Add((uint)messageQueueHistoryItem.QueueBuffer.Position);
        }

        /// <summary>
        /// Gets the current queue history frame (inbound or outbound)
        /// </summary>
        /// <param name="frameType">inbound or outbound</param>
        /// <param name="updateStage">network update stage the queue history frame is assigned to</param>
        /// <param name="getNextFrame">whether to get the next frame or not (true/false)</param>
        /// <returns>QueueHistoryFrame or null</returns>
        public MessageQueueHistoryFrame GetQueueHistoryFrame(MessageQueueHistoryFrame.QueueFrameType frameType, NetworkUpdateStage updateStage, bool getNextFrame = false)
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
                Debug.LogError($"{nameof(MessageQueueHistoryFrame)} {nameof(MessageQueueHistoryFrame.QueueFrameType)} {frameType} does not exist!");
                return null;
            }

            if (!m_QueueHistory[frameType].ContainsKey(streamBufferIndex))
            {
                Debug.LogError($"{nameof(MessageQueueContainer)} {frameType} queue stream buffer index out of range! [{streamBufferIndex}]");
                return null;
            }

            if (!m_QueueHistory[frameType][streamBufferIndex].ContainsKey(updateStage))
            {
                Debug.LogError($"{nameof(MessageQueueContainer)} {updateStage} update type does not exist!");
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
            ProcessAndFlushMessageQueue(MessageQueueProcessingTypes.Receive, updateStage);

            if (updateStage == NetworkUpdateStage.PostLateUpdate)
            {
                ProcessAndFlushMessageQueue(MessageQueueProcessingTypes.Send, updateStage);
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
                    NetworkLog.LogWarning($"The {nameof(MessageQueueHistoryFrame)} size cannot exceed {ushort.MaxValue} {nameof(MessageQueueHistoryFrame)}s! Capping at {ushort.MaxValue} {nameof(MessageQueueHistoryFrame)}s.");
                }
                maxFrameHistory = ushort.MaxValue;
            }

            ClearParameters();

            m_MessageQueueProcessor = new MessageQueueProcessor(this, NetworkManager);
            m_MaxFrameHistory = maxFrameHistory + k_MinQueueHistory;

            if (!m_QueueHistory.ContainsKey(MessageQueueHistoryFrame.QueueFrameType.Inbound))
            {
                m_QueueHistory.Add(MessageQueueHistoryFrame.QueueFrameType.Inbound, new Dictionary<int, Dictionary<NetworkUpdateStage, MessageQueueHistoryFrame>>());
            }

            if (!m_QueueHistory.ContainsKey(MessageQueueHistoryFrame.QueueFrameType.Outbound))
            {
                m_QueueHistory.Add(MessageQueueHistoryFrame.QueueFrameType.Outbound, new Dictionary<int, Dictionary<NetworkUpdateStage, MessageQueueHistoryFrame>>());
            }

            for (int i = 0; i < m_MaxFrameHistory; i++)
            {
                if (!m_QueueHistory[MessageQueueHistoryFrame.QueueFrameType.Outbound].ContainsKey(i))
                {
                    m_QueueHistory[MessageQueueHistoryFrame.QueueFrameType.Outbound].Add(i, new Dictionary<NetworkUpdateStage, MessageQueueHistoryFrame>());
                    var queueHistoryFrame = new MessageQueueHistoryFrame(MessageQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);
                    queueHistoryFrame.QueueBuffer = PooledNetworkBuffer.Get();
                    queueHistoryFrame.QueueBuffer.Position = 0;
                    queueHistoryFrame.QueueWriter = PooledNetworkWriter.Get(queueHistoryFrame.QueueBuffer);
                    queueHistoryFrame.QueueReader = PooledNetworkReader.Get(queueHistoryFrame.QueueBuffer);
                    queueHistoryFrame.QueueItemOffsets = new List<uint>();

                    //For now all outbound, we will always have a single update in which they are processed (LATEUPDATE)
                    m_QueueHistory[MessageQueueHistoryFrame.QueueFrameType.Outbound][i].Add(NetworkUpdateStage.PostLateUpdate, queueHistoryFrame);
                }

                if (!m_QueueHistory[MessageQueueHistoryFrame.QueueFrameType.Inbound].ContainsKey(i))
                {
                    m_QueueHistory[MessageQueueHistoryFrame.QueueFrameType.Inbound].Add(i, new Dictionary<NetworkUpdateStage, MessageQueueHistoryFrame>());

                    //For inbound, we create a queue history frame per update stage
                    foreach (NetworkUpdateStage netUpdateStage in Enum.GetValues(typeof(NetworkUpdateStage)))
                    {
                        var messageQueueHistoryFrame = new MessageQueueHistoryFrame(MessageQueueHistoryFrame.QueueFrameType.Inbound, netUpdateStage);
                        messageQueueHistoryFrame.QueueBuffer = PooledNetworkBuffer.Get();
                        messageQueueHistoryFrame.QueueBuffer.Position = 0;
                        messageQueueHistoryFrame.QueueWriter = PooledNetworkWriter.Get(messageQueueHistoryFrame.QueueBuffer);
                        messageQueueHistoryFrame.QueueReader = PooledNetworkReader.Get(messageQueueHistoryFrame.QueueBuffer);
                        messageQueueHistoryFrame.QueueItemOffsets = new List<uint>();
                        m_QueueHistory[MessageQueueHistoryFrame.QueueFrameType.Inbound][i].Add(netUpdateStage, messageQueueHistoryFrame);
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
                NetworkLog.LogInfo($"[Instance : {s_MessageQueueContainerInstances}] {nameof(MessageQueueContainer)} shutting down.");
            }
#endif
            //As long as this instance is using the pre-defined update stages
            if (!m_ProcessUpdateStagesExternally)
            {
                //Remove ourself from the network loop update system
                this.UnregisterAllNetworkUpdates();
            }

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
            m_MessageQueueProcessor.Shutdown();

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
            if (s_MessageQueueContainerInstances > 0)
            {
                if (NetworkLog.CurrentLogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogInfo($"[Instance : {s_MessageQueueContainerInstances}] {nameof(MessageQueueContainer)} disposed.");
                }

                s_MessageQueueContainerInstances--;
            }
            else //This should never happen...if so something else has gone very wrong.
            {
                if (NetworkLog.CurrentLogLevel >= LogLevel.Normal)
                {
                    NetworkLog.LogError($"[*** Warning ***] {nameof(MessageQueueContainer)} is being disposed twice?");
                }

                throw new Exception("[*** Warning ***] System state is not stable!  Check all references to the Dispose method!");
            }
#endif
        }

        /// <summary>
        /// MessageQueueContainer - Constructor
        /// Note about processExternally: this values determines if it will register with the Network Update Loop
        /// or not.  If not, then most likely unit tests are being preformed on this class.  The default value is false.
        /// </summary>
        /// <param name="maxFrameHistory"></param>
        /// <param name="processExternally">determines if it handles processing externally</param>
        public MessageQueueContainer(NetworkManager networkManager, uint maxFrameHistory = 0, bool processExternally = false)
        {
            NetworkManager = networkManager;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Keep track of how many instances we have instantiated
            s_MessageQueueContainerInstances++;

            if (NetworkLog.CurrentLogLevel == LogLevel.Developer)
            {
                NetworkLog.LogInfo($"[Instance : {s_MessageQueueContainerInstances}] {nameof(MessageQueueContainer)} Initialized");
            }
#endif

            m_ProcessUpdateStagesExternally = processExternally;
            Initialize(maxFrameHistory);
        }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Enables testing of the MessageQueueContainer
        /// </summary>
        /// <param name="enabled"></param>
        public void SetTestingState(bool enabled)
        {
            m_IsTestingEnabled = enabled;
        }
#endif
    }
}
