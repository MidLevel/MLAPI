using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MLAPI.Serialization.Pooled;
using MLAPI.Serialization;
using MLAPI.Configuration;
using MLAPI.Profiling;
using MLAPI.Transports;
using UnityEngine;

namespace MLAPI.Messaging
{
    internal class MessageBatcher
    {
        public class SendStream
        {
            public NetworkChannel NetworkChannel;
            public PooledNetworkBuffer Buffer;
            public PooledNetworkWriter Writer;
            public bool IsEmpty = true;

            public SendStream()
            {
                Buffer = PooledNetworkBuffer.Get();
                Writer = PooledNetworkWriter.Get(Buffer);
            }
        }

        // Stores the stream of batched RPC to send to each client, by ClientId
        private readonly Dictionary<ulong, SendStream> m_SendDict = new Dictionary<ulong, SendStream>();

        public void Shutdown()
        {
            foreach (var kvp in m_SendDict)
            {
                kvp.Value.Writer.Dispose();
                kvp.Value.Buffer.Dispose();
            }
            m_SendDict.Clear();
        }

        // Used to store targets, internally
        private ulong[] m_TargetList = new ulong[0];

        // Used to mark longer lengths. Works because we can't have zero-sized messages
        private const byte k_LongLenMarker = 0;

        internal static void PushLength(int length, ref PooledNetworkWriter writer)
        {
            // If length is single byte we write it
            if (length < 256)
            {
                writer.WriteByte((byte)length); // write the amounts of bytes that are coming up
            }
            else
            {
                // otherwise we write a two-byte length
                writer.WriteByte(k_LongLenMarker); // mark larger size
                writer.WriteByte((byte)(length % 256)); // write the length modulo 256
                writer.WriteByte((byte)(length / 256)); // write the length divided by 256
            }
        }

        private int PopLength(in NetworkBuffer messageBuffer)
        {
            int read = messageBuffer.ReadByte();
            // if we read a non-zero value, we have a single byte length
            // or a -1 error we can return
            if (read != k_LongLenMarker)
            {
                return read;
            }

            // otherwise, a two-byte length follows. We'll read in len1, len2
            int len1 = messageBuffer.ReadByte();
            if (len1 < 0)
            {
                // pass errors back to caller
                return len1;
            }

            int len2 = messageBuffer.ReadByte();
            if (len2 < 0)
            {
                // pass errors back to caller
                return len2;
            }

            return len1 + len2 * 256;
        }

        /// <summary>
        /// FillTargetList
        /// Fills a list with the ClientId's an item is targeted to
        /// </summary>
        /// <param name="item">the FrameQueueItem we want targets for</param>
        /// <param name="networkIdList">the list to fill</param>
        private static void FillTargetList(in MessageFrameItem item, ref ulong[] networkIdList)
        {
            switch (item.MessageType)
            {
                // todo: revisit .resize() and .ToArry() usage, for performance
                case MessageQueueContainer.MessageType.ServerRpc:
                    Array.Resize(ref networkIdList, 1);
                    networkIdList[0] = item.NetworkId;
                    break;
                default:
                // todo: consider the implications of default usage of queueItem.clientIds
                case MessageQueueContainer.MessageType.ClientRpc:
                    // copy the list
                    networkIdList = item.ClientNetworkIds.ToArray();
                    break;
            }
        }

        /// <summary>
        /// QueueItem
        /// Add a FrameQueueItem to be sent
        /// </summary>queueItem
        /// <param name="item">the threshold in bytes</param>
        public void QueueItem(in MessageFrameItem item)
        {
            FillTargetList(item, ref m_TargetList);

            foreach (ulong clientId in m_TargetList)
            {
                if (!m_SendDict.ContainsKey(clientId))
                {
                    // todo: consider what happens if many clients join and leave the game consecutively
                    // we probably need a cleanup mechanism at some point
                    m_SendDict[clientId] = new SendStream();
                }

                SendStream sendStream = m_SendDict[clientId];

                if (sendStream.IsEmpty)
                {
                    sendStream.IsEmpty = false;
                    sendStream.NetworkChannel = item.NetworkChannel;
                }

                // write the amounts of bytes that are coming up
                PushLength(item.MessageData.Count, ref sendStream.Writer);

                // write the message to send
                sendStream.Writer.WriteBytes(item.MessageData.Array, item.MessageData.Count, item.MessageData.Offset);

                ProfilerStatManager.BytesSent.Record(item.MessageData.Count);
                ProfilerStatManager.MessagesSent.Record();
                PerformanceDataManager.Increment(ProfilerConstants.ByteSent, item.MessageData.Count);
                PerformanceDataManager.Increment(ProfilerConstants.MessagesSent);
            }
        }

        public delegate void SendCallbackType(ulong clientId, SendStream messageStream);

        public delegate void ReceiveCallbackType(NetworkBuffer messageStream, MessageQueueContainer.MessageType messageType, ulong clientId, float receiveTime, NetworkChannel receiveChannel);

        /// <summary>
        /// SendItems
        /// Send any batch of messages that are of length above threshold
        /// </summary>
        /// <param name="thresholdBytes"> the threshold in bytes</param>
        /// <param name="sendCallback"> the function to call for sending the batch</param>
        public void SendItems(int thresholdBytes, SendCallbackType sendCallback)
        {
            foreach (KeyValuePair<ulong, SendStream> entry in m_SendDict)
            {
                if (!entry.Value.IsEmpty)
                {
                    // read the queued message
                    int length = (int)m_SendDict[entry.Key].Buffer.Length;

                    if (length >= thresholdBytes)
                    {
                        sendCallback(entry.Key, entry.Value);
                        // clear the batch that was sent from the SendDict
                        entry.Value.Buffer.SetLength(0);
                        entry.Value.Buffer.Position = 0;
                        entry.Value.IsEmpty = true;
                        ProfilerStatManager.MessageBatchesSent.Record();
                        PerformanceDataManager.Increment(ProfilerConstants.MessageBatchesSent);
                    }
                }
            }
        }

        /// <summary>
        /// ReceiveItems
        /// Process the messageStream and call the callback with individual messages
        /// </summary>
        /// <param name="messageBuffer"> the messageStream containing the batched messages</param>
        /// <param name="receiveCallback"> the callback to call has type int f(message, type, clientId, time) </param>
        /// <param name="messageType"> the message type to pass back to callback</param>
        /// <param name="clientId"> the clientId to pass back to callback</param>
        /// <param name="receiveTime"> the packet receive time to pass back to callback</param>
        public void ReceiveItems(in NetworkBuffer messageBuffer, ReceiveCallbackType receiveCallback, ulong clientId, float receiveTime, NetworkChannel receiveChannel)
        {
            using (var copy = PooledNetworkBuffer.Get())
            {
                do
                {
                    // read the length of the next message
                    int messageSize = PopLength(messageBuffer);
                    if (messageSize < 0)
                    {
                        // abort if there's an error reading lengths
                        return;
                    }

                    // copy what comes after current stream position
                    long position = messageBuffer.Position;
                    copy.SetLength(messageSize);
                    copy.Position = 0;
                    Buffer.BlockCopy(messageBuffer.GetBuffer(), (int)position, copy.GetBuffer(), 0, messageSize);

                    var messageType = (MessageQueueContainer.MessageType)copy.ReadByte();
                    receiveCallback(copy, messageType, clientId, receiveTime, receiveChannel);

                    // seek over the message
                    // MessageReceiveQueueItem peeks at content, it doesn't advance
                    messageBuffer.Seek(messageSize, SeekOrigin.Current);
                } while (messageBuffer.Position < messageBuffer.Length);
            }
        }
    }
}
