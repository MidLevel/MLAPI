using System;
using System.Collections.Generic;
using System.Text;
using MLAPI.Messaging;
using MLAPI.Serialization;
using MLAPI.Transports;
using NUnit.Framework;

namespace MLAPI.EditorTests
{
    public class RpcBatcherTests
    {
        [Test]
        public void SendWithThreshold()
        {
            const int k_BatchThreshold = 256;
            const int k_QueueItemCount = 128;

            var sendBatcher = new MessageBatcher();
            var sendStreamQueue = new Queue<NetworkBuffer>();
            for (int i = 0; i < k_QueueItemCount; ++i)
            {
                var randomData = Encoding.ASCII.GetBytes(Guid.NewGuid().ToString());
                var queueItem = new MessageFrameItem
                {
                    NetworkId = 123,
                    ClientNetworkIds = new ulong[] { 123 },
                    NetworkChannel = NetworkChannel.ChannelUnused + 123,
                    MessageType = i % 2 == 0 ? MessageQueueContainer.MessageType.ServerRpc : MessageQueueContainer.MessageType.ClientRpc,
                    MessageData = new ArraySegment<byte>(randomData, 0, randomData.Length)
                };
                sendBatcher.QueueItem(queueItem);
                sendBatcher.SendItems(k_BatchThreshold,
                    (networkId, sendStream) =>
                    {
                        var queueStream = new NetworkBuffer();
                        sendStream.Buffer.CopyTo(queueStream);
                        sendStreamQueue.Enqueue(queueStream);
                    });
            }

            // batch the rest
            sendBatcher.SendItems( /* thresholdBytes = */ 0,
                (networkId, sendStream) =>
                {
                    var queueStream = new NetworkBuffer();
                    sendStream.Buffer.CopyTo(queueStream);
                    sendStreamQueue.Enqueue(queueStream);
                });

            var recvBatcher = new MessageBatcher();
            var recvItemCounter = 0;
            foreach (var recvStream in sendStreamQueue)
            {
                recvStream.Position = 0;
                recvBatcher.ReceiveItems(recvStream, (stream, type, id, time, channel) => ++recvItemCounter, default, default, default);
            }

            Assert.AreEqual(k_QueueItemCount, recvItemCounter);
        }

        [Test]
        public void SendWithoutThreshold()
        {
            const int k_BatchThreshold = 0;
            const int k_QueueItemCount = 128;

            var sendBatcher = new MessageBatcher();
            var sendStreamQueue = new Queue<NetworkBuffer>();
            for (int i = 0; i < k_QueueItemCount; ++i)
            {
                var randomData = Encoding.ASCII.GetBytes(Guid.NewGuid().ToString());
                var queueItem = new MessageFrameItem
                {
                    NetworkId = 123,
                    ClientNetworkIds = new ulong[] { 123 },
                    NetworkChannel = NetworkChannel.ChannelUnused + 123,
                    MessageType = i % 2 == 0 ? MessageQueueContainer.MessageType.ServerRpc : MessageQueueContainer.MessageType.ClientRpc,
                    MessageData = new ArraySegment<byte>(randomData, 0, randomData.Length)
                };
                sendBatcher.QueueItem(queueItem);
                sendBatcher.SendItems(k_BatchThreshold,
                    (networkId, sendStream) =>
                    {
                        var queueStream = new NetworkBuffer();
                        sendStream.Buffer.CopyTo(queueStream);
                        sendStreamQueue.Enqueue(queueStream);
                    });
            }

            // batch the rest
            sendBatcher.SendItems( /* thresholdBytes = */ 0,
                (networkId, sendStream) =>
                {
                    var queueStream = new NetworkBuffer();
                    sendStream.Buffer.CopyTo(queueStream);
                    sendStreamQueue.Enqueue(queueStream);
                });

            var recvBatcher = new MessageBatcher();
            var recvItemCounter = 0;
            foreach (var recvStream in sendStreamQueue)
            {
                recvStream.Position = 0;
                recvBatcher.ReceiveItems(recvStream, (stream, type, id, time, channel) => ++recvItemCounter, default, default, default);
            }

            Assert.AreEqual(k_QueueItemCount, recvItemCounter);
        }
    }
}
