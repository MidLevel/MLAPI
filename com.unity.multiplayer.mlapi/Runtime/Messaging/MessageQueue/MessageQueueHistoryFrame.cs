using System.Collections.Generic;
using MLAPI.Serialization.Pooled;
using MLAPI.Transports;
using UnityEngine;

namespace MLAPI.Messaging
{
    /// <summary>
    /// Used by the MessageQueueContainer to hold queued messages
    /// </summary>
    public class MessageQueueHistoryFrame
    {
        public enum QueueFrameType
        {
            Inbound,
            Outbound,
        }

        public bool IsDirty; //Used to determine if this queue history frame has been reset (cleaned) yet
        public bool HasLoopbackData; //Used to determine if a dirt frame is dirty because messages are being looped back betwen HostClient and HostServer
        public uint TotalSize;
        public List<uint> QueueItemOffsets;

        public PooledNetworkBuffer QueueBuffer;
        public PooledNetworkWriter QueueWriter;
        public MessageQueueHistoryFrame LoopbackHistoryFrame; //Temporary fix for Host mode loopback work around.


        public PooledNetworkReader QueueReader;

        private int m_QueueItemOffsetIndex;
        private MessageFrameItem m_CurrentItem;
        private readonly QueueFrameType m_QueueFrameType;
        private int m_MaximumClients;
        private long m_CurrentStreamSizeMark;
        private NetworkUpdateStage m_StreamUpdateStage; //Update stage specific to messages (typically inbound has most potential for variation)
        private int m_MaxStreamBounds;
        private const int k_MinStreamBounds = 0;

        /// <summary>
        /// Returns whether this is an inbound or outbound frame
        /// </summary>
        /// <returns></returns>
        public QueueFrameType GetQueueFrameType()
        {
            return m_QueueFrameType;
        }

        /// <summary>
        /// Marks the current size of the stream  (used primarily for sanity checks)
        /// </summary>
        public void MarkCurrentStreamPosition()
        {
            if (QueueBuffer != null)
            {
                m_CurrentStreamSizeMark = QueueBuffer.Position;
            }
            else
            {
                m_CurrentStreamSizeMark = 0;
            }
        }

        /// <summary>
        /// Returns the current position that was marked (to track size of msg)
        /// </summary>
        /// <returns>m_CurrentStreamSizeMark</returns>
        public long GetCurrentMarkedPosition()
        {
            return m_CurrentStreamSizeMark;
        }

        /// <summary>
        /// Internal method to get the current Queue Item from the stream at its current position
        /// </summary>
        /// <returns>FrameQueueItem</returns>
        internal MessageFrameItem GetCurrentQueueItem()
        {
            //Write the packed version of the queueItem to our current queue history buffer
            m_CurrentItem.MessageType = (MessageQueueContainer.MessageType)QueueReader.ReadUInt16();
            m_CurrentItem.Timestamp = QueueReader.ReadSingle();
            m_CurrentItem.NetworkId = QueueReader.ReadUInt64();
            m_CurrentItem.NetworkChannel = (NetworkChannel)QueueReader.ReadByteDirect();

            //Clear out any current value for the client ids
            m_CurrentItem.ClientNetworkIds = new ulong[0];

            //If outbound, determine if any client ids needs to be added
            if (m_QueueFrameType == QueueFrameType.Outbound)
            {
                //Outbound we care about both channel and clients
                int numClients = QueueReader.ReadInt32();
                if (numClients > 0 && numClients < m_MaximumClients)
                {
                    ulong[] clientIdArray = new ulong[numClients];
                    for (int i = 0; i < numClients; i++)
                    {
                        clientIdArray[i] = QueueReader.ReadUInt64();
                    }

                    m_CurrentItem.ClientNetworkIds = clientIdArray;
                }
            }

            m_CurrentItem.UpdateStage = m_StreamUpdateStage;

            //Get the stream size
            m_CurrentItem.StreamSize = QueueReader.ReadInt64();

            //Sanity checking for boundaries
            if (m_CurrentItem.StreamSize < m_MaxStreamBounds && m_CurrentItem.StreamSize >= k_MinStreamBounds)
            {
                //Inbound and Outbound message streams are handled differently
                if (m_QueueFrameType == QueueFrameType.Inbound)
                {
                    //Get our offset
                    long position = QueueReader.ReadInt64();

                    //Always make sure we are positioned at the start of the stream before we write
                    m_CurrentItem.NetworkBuffer.Position = 0;

                    //Write the entire message to the m_CurrentQueueItem stream (1 stream is re-used for all incoming messages)
                    m_CurrentItem.NetworkWriter.ReadAndWrite(QueueReader, m_CurrentItem.StreamSize);

                    //Reset the position back to the offset so std API can process the message properly
                    //(i.e. minus the already processed header)
                    m_CurrentItem.NetworkBuffer.Position = position;
                }
                else
                {
                    //Create a byte array segment for outbound sending
                    m_CurrentItem.MessageData = QueueReader.CreateArraySegment((int)m_CurrentItem.StreamSize, (int)QueueBuffer.Position);
                }
            }
            else
            {
                Debug.LogWarning($"{nameof(m_CurrentItem)}.{nameof(MessageFrameItem.StreamSize)} exceeds allowed size ({m_MaxStreamBounds} vs {m_CurrentItem.StreamSize})! Exiting from the current MessageQueue enumeration loop!");
                m_CurrentItem.MessageType = MessageQueueContainer.MessageType.None;
            }

            return m_CurrentItem;
        }

        /// <summary>
        /// Handles getting the next queue item from this frame
        /// If none are remaining, then it returns a queue item type of NONE
        /// </summary>
        /// <returns>FrameQueueItem</returns>
        internal MessageFrameItem GetNextQueueItem()
        {
            QueueBuffer.Position = QueueItemOffsets[m_QueueItemOffsetIndex];
            m_QueueItemOffsetIndex++;
            if (m_QueueItemOffsetIndex >= QueueItemOffsets.Count)
            {
                m_CurrentItem.MessageType = MessageQueueContainer.MessageType.None;
                return m_CurrentItem;
            }

            return GetCurrentQueueItem();
        }

        /// <summary>
        /// Should be called the first time a queue item is pulled from a queue history frame.
        /// This will reset the frame's stream indices and add a new stream and stream writer to the m_CurrentQueueItem instance.
        /// </summary>
        /// <returns>FrameQueueItem</returns>
        internal MessageFrameItem GetFirstQueueItem()
        {
            if (QueueBuffer.Position > 0)
            {
                m_QueueItemOffsetIndex = 0;
                QueueBuffer.Position = 0;

                if (m_QueueFrameType == QueueFrameType.Inbound)
                {
                    if (m_CurrentItem.NetworkBuffer == null)
                    {
                        m_CurrentItem.NetworkBuffer = PooledNetworkBuffer.Get();
                    }

                    if (m_CurrentItem.NetworkWriter == null)
                    {
                        m_CurrentItem.NetworkWriter = PooledNetworkWriter.Get(m_CurrentItem.NetworkBuffer);
                    }

                    if (m_CurrentItem.NetworkReader == null)
                    {
                        m_CurrentItem.NetworkReader = PooledNetworkReader.Get(m_CurrentItem.NetworkBuffer);
                    }
                }

                return GetCurrentQueueItem();
            }

            m_CurrentItem.MessageType = MessageQueueContainer.MessageType.None;
            return m_CurrentItem;
        }

        /// <summary>
        /// Should be called once all processing of the current frame is complete.
        /// This only closes the m_CurrentQueueItem's stream which is used as a "middle-man" (currently)
        /// for delivering the message to the method requesting a queue item from a frame.
        /// </summary>
        public void CloseQueue()
        {
            if (m_CurrentItem.NetworkWriter != null)
            {
                m_CurrentItem.NetworkWriter.Dispose();
                m_CurrentItem.NetworkWriter = null;
            }

            if (m_CurrentItem.NetworkReader != null)
            {
                m_CurrentItem.NetworkReader.Dispose();
                m_CurrentItem.NetworkReader = null;
            }

            if (m_CurrentItem.NetworkBuffer != null)
            {
                m_CurrentItem.NetworkBuffer.Dispose();
                m_CurrentItem.NetworkBuffer = null;
            }
        }


        /// <summary>
        /// QueueHistoryFrame Constructor
        /// </summary>
        /// <param name="queueType">Inbound or Outbound</param>
        /// <param name="updateStage">Network Update Stage this MessageQueueHistoryFrame is assigned to</param>
        /// <param name="maxClients">maximum number of clients</param>
        /// <param name="maxStreamBounds">maximum size of the message stream a message can have (defaults to 1MB)</param>
        public MessageQueueHistoryFrame(QueueFrameType queueType, NetworkUpdateStage updateStage, int maxClients = 512, int maxStreamBounds = 1 << 20)
        {
            //The added 512 is the Queue History Frame header information, leaving room to grow
            m_MaxStreamBounds = maxStreamBounds + 512;
            m_MaximumClients = maxClients;
            m_QueueFrameType = queueType;
            m_CurrentItem = new MessageFrameItem();
            m_StreamUpdateStage = updateStage;
        }
    }
}
