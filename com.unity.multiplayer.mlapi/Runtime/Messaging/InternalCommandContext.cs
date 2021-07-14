using System;
using MLAPI.Serialization.Pooled;
using System.Linq;

namespace MLAPI.Messaging
{
    /// <summary>
    /// A context used for building an internal command.
    /// </summary>
    internal struct InternalCommandContext : IDisposable
    {
        public PooledNetworkWriter NetworkWriter;

        private ulong[] ClientIds;
        private NetworkUpdateStage UpdateStage;
        private MessageQueueContainer Owner;

        public InternalCommandContext(PooledNetworkWriter writer, ulong[] clientIds, NetworkUpdateStage updateStage, MessageQueueContainer owner)
        {
            NetworkWriter = writer;
            ClientIds = clientIds;
            UpdateStage = updateStage;
            Owner = owner;
        }

        public void Dispose()
        {
            Finalize();
        }

        public void Finalize()
        {
            if (Owner.NetworkManager.IsHost)
            {
                var containsServerClientId = ClientIds.Contains(Owner.NetworkManager.ServerClientId);
                if (containsServerClientId && ClientIds.Length == 1)
                {
                    Owner.EndAddQueueItemToFrame(NetworkWriter, MessageQueueHistoryFrame.QueueFrameType.Inbound, UpdateStage);
                    return;
                }
            }

            Owner.EndAddQueueItemToFrame(NetworkWriter, MessageQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);
        }
    }
}
