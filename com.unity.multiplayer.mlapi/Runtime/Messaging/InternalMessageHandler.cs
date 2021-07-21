using System;
using System.IO;
using MLAPI.Connection;
using MLAPI.Logging;
using MLAPI.SceneManagement;
using MLAPI.Serialization.Pooled;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using MLAPI.Configuration;
using MLAPI.Messaging.Buffering;
using MLAPI.Profiling;
using MLAPI.Serialization;

namespace MLAPI.Messaging
{
    internal class InternalMessageHandler : IInternalMessageHandler
    {
        public NetworkManager NetworkManager => m_NetworkManager;
        private NetworkManager m_NetworkManager;

        public InternalMessageHandler(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager;
        }

        public void HandleConnectionRequest(ulong clientId, Stream stream)
        {
            if (NetworkManager.PendingClients.TryGetValue(clientId, out PendingClient client))
            {
                // Set to pending approval to prevent future connection requests from being approved
                client.ConnectionState = PendingClient.State.PendingApproval;
            }

            using (var reader = PooledNetworkReader.Get(stream))
            {
                ulong configHash = reader.ReadUInt64Packed();
                if (!NetworkManager.NetworkConfig.CompareConfig(configHash))
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning($"{nameof(NetworkConfig)} mismatch. The configuration between the server and client does not match");
                    }

                    NetworkManager.DisconnectClient(clientId);
                    return;
                }

                if (NetworkManager.NetworkConfig.ConnectionApproval)
                {
                    byte[] connectionBuffer = reader.ReadByteArray();
                    NetworkManager.InvokeConnectionApproval(connectionBuffer, clientId,
                        (createPlayerObject, playerPrefabHash, approved, position, rotation) =>
                            NetworkManager.HandleApproval(clientId, createPlayerObject, playerPrefabHash, approved, position, rotation));
                }
                else
                {
                    NetworkManager.HandleApproval(clientId, NetworkManager.NetworkConfig.PlayerPrefab != null, null, true, null, null);
                }
            }
        }

        /// <summary>
        /// Client Side: handles the connection approved message
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="stream"></param>
        /// <param name="receiveTime"></param>
        public void HandleConnectionApproved(ulong clientId, Stream stream, float receiveTime)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                NetworkManager.LocalClientId = reader.ReadUInt64Packed();

                float netTime = reader.ReadSinglePacked();
                NetworkManager.UpdateNetworkTime(clientId, netTime, receiveTime, true);

                NetworkManager.ConnectedClients.Add(NetworkManager.LocalClientId, new NetworkClient { ClientId = NetworkManager.LocalClientId });
            }
        }

        public void HandleAddObject(ulong clientId, Stream stream)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                var isPlayerObject = reader.ReadBool();
                var networkId = reader.ReadUInt64Packed();
                var ownerClientId = reader.ReadUInt64Packed();
                var hasParent = reader.ReadBool();
                ulong? parentNetworkId = null;

                if (hasParent)
                {
                    parentNetworkId = reader.ReadUInt64Packed();
                }

                var softSync = reader.ReadBool();
                var prefabHash = reader.ReadUInt32Packed();

                Vector3? pos = null;
                Quaternion? rot = null;
                if (reader.ReadBool())
                {
                    pos = new Vector3(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                    rot = Quaternion.Euler(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                }

                var (isReparented, latestParent) = NetworkObject.ReadNetworkParenting(reader);

                var hasPayload = reader.ReadBool();
                var payLoadLength = hasPayload ? reader.ReadInt32Packed() : 0;

                var networkObject = NetworkManager.SpawnManager.CreateLocalNetworkObject(softSync, prefabHash, ownerClientId, parentNetworkId, pos, rot, isReparented);
                networkObject.SetNetworkParenting(isReparented, latestParent);
                NetworkManager.SpawnManager.SpawnNetworkObjectLocally(networkObject, networkId, softSync, isPlayerObject, ownerClientId, stream, hasPayload, payLoadLength, true, false);

                Queue<BufferManager.BufferedMessage> bufferQueue = NetworkManager.BufferManager.ConsumeBuffersForNetworkId(networkId);

                // Apply buffered messages
                if (bufferQueue != null)
                {
                    while (bufferQueue.Count > 0)
                    {
                        BufferManager.BufferedMessage message = bufferQueue.Dequeue();
                        NetworkManager.HandleIncomingData(message.SenderClientId, message.NetworkChannel, new ArraySegment<byte>(message.NetworkBuffer.GetBuffer(), (int)message.NetworkBuffer.Position, (int)message.NetworkBuffer.Length), message.ReceiveTime, false);
                        BufferManager.RecycleConsumedBufferedMessage(message);
                    }
                }
            }
        }

        public void HandleDestroyObject(ulong clientId, Stream stream)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                ulong networkId = reader.ReadUInt64Packed();
                if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkId, out NetworkObject networkObject))
                {
                    // This is the same check and log message that happens inside OnDespawnObject, but we have to do it here
                    // while we still have access to the network ID, otherwise the log message will be less useful.
                    Debug.LogWarning($"Trying to destroy object {networkId} but it doesn't seem to exist anymore!");
                    return;
                }

                NetworkManager.SpawnManager.OnDespawnObject(networkObject, true);
            }
        }

        /// <summary>
        /// Called for all Scene Management related events
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="stream"></param>
        public void HandleSceneEvent(ulong clientId, Stream stream)
        {
            NetworkManager.SceneManager.HandleSceneEvent(clientId, stream);
        }


        public void HandleChangeOwner(ulong clientId, Stream stream)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                ulong networkId = reader.ReadUInt64Packed();
                ulong ownerClientId = reader.ReadUInt64Packed();

                if (NetworkManager.SpawnManager.SpawnedObjects[networkId].OwnerClientId == NetworkManager.LocalClientId)
                {
                    //We are current owner.
                    NetworkManager.SpawnManager.SpawnedObjects[networkId].InvokeBehaviourOnLostOwnership();
                }

                if (ownerClientId == NetworkManager.LocalClientId)
                {
                    //We are new owner.
                    NetworkManager.SpawnManager.SpawnedObjects[networkId].InvokeBehaviourOnGainedOwnership();
                }

                NetworkManager.SpawnManager.SpawnedObjects[networkId].OwnerClientId = ownerClientId;
            }
        }

        public void HandleAddObjects(ulong clientId, Stream stream)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                ushort objectCount = reader.ReadUInt16Packed();

                for (int i = 0; i < objectCount; i++)
                {
                    HandleAddObject(clientId, stream);
                }
            }
        }

        public void HandleDestroyObjects(ulong clientId, Stream stream)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                ushort objectCount = reader.ReadUInt16Packed();

                for (int i = 0; i < objectCount; i++)
                {
                    HandleDestroyObject(clientId, stream);
                }
            }
        }

        public void HandleTimeSync(ulong clientId, Stream stream, float receiveTime)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                float netTime = reader.ReadSinglePacked();
                NetworkManager.UpdateNetworkTime(clientId, netTime, receiveTime);
            }
        }

        public void HandleNetworkVariableDelta(ulong clientId, Stream stream, Action<ulong, PreBufferPreset> bufferCallback, PreBufferPreset bufferPreset)
        {
            if (!NetworkManager.NetworkConfig.EnableNetworkVariable)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"{nameof(NetworkConstants.NETWORK_VARIABLE_DELTA)} received but {nameof(NetworkConfig.EnableNetworkVariable)} is false");
                }

                return;
            }

            using (var reader = PooledNetworkReader.Get(stream))
            {
                ulong networkObjectId = reader.ReadUInt64Packed();
                ushort networkBehaviourIndex = reader.ReadUInt16Packed();

                if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
                {
                    NetworkBehaviour instance = networkObject.GetNetworkBehaviourAtOrderIndex(networkBehaviourIndex);

                    if (instance == null)
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                        {
                            NetworkLog.LogWarning($"{nameof(NetworkConstants.NETWORK_VARIABLE_DELTA)} message received for a non-existent behaviour. {nameof(networkObjectId)}: {networkObjectId}, {nameof(networkBehaviourIndex)}: {networkBehaviourIndex}");
                        }
                    }
                    else
                    {
                        NetworkBehaviour.HandleNetworkVariableDeltas(instance.NetworkVariableFields, stream, clientId, instance, NetworkManager);
                    }
                }
                else if (NetworkManager.IsServer || !NetworkManager.NetworkConfig.EnableMessageBuffering)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning($"{nameof(NetworkConstants.NETWORK_VARIABLE_DELTA)} message received for a non-existent object with {nameof(networkObjectId)}: {networkObjectId}. This delta was lost.");
                    }
                }
                else
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                    {
                        NetworkLog.LogWarning($"{nameof(NetworkConstants.NETWORK_VARIABLE_DELTA)} message received for a non-existent object with {nameof(networkObjectId)}: {networkObjectId}. This delta will be buffered and might be recovered.");
                    }

                    bufferCallback(networkObjectId, bufferPreset);
                }
            }
        }

        /// <summary>
        /// Converts the stream to a PerformanceQueueItem and adds it to the receive queue
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="stream"></param>
        /// <param name="receiveTime"></param>
        public void RpcReceiveQueueItem(ulong clientId, Stream stream, float receiveTime, RpcQueueContainer.QueueItemType queueItemType)
        {
            if (NetworkManager.IsServer && clientId == NetworkManager.ServerClientId)
            {
                return;
            }

            ProfilerStatManager.RpcsRcvd.Record();
            PerformanceDataManager.Increment(ProfilerConstants.RpcReceived);

            var rpcQueueContainer = NetworkManager.RpcQueueContainer;
            rpcQueueContainer.AddQueueItemToInboundFrame(queueItemType, receiveTime, clientId, (NetworkBuffer)stream);
        }

        public void HandleUnnamedMessage(ulong clientId, Stream stream)
        {
            PerformanceDataManager.Increment(ProfilerConstants.UnnamedMessageReceived);
            ProfilerStatManager.UnnamedMessage.Record();
            NetworkManager.CustomMessagingManager.InvokeUnnamedMessage(clientId, stream);
        }

        public void HandleNamedMessage(ulong clientId, Stream stream)
        {
            PerformanceDataManager.Increment(ProfilerConstants.NamedMessageReceived);
            ProfilerStatManager.NamedMessage.Record();
            using (var reader = PooledNetworkReader.Get(stream))
            {
                ulong hash = reader.ReadUInt64Packed();

                NetworkManager.CustomMessagingManager.InvokeNamedMessage(hash, clientId, stream);
            }
        }

        public void HandleNetworkLog(ulong clientId, Stream stream)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                var logType = (NetworkLog.LogType)reader.ReadByte();
                string message = reader.ReadStringPacked();

                switch (logType)
                {
                    case NetworkLog.LogType.Info:
                        NetworkLog.LogInfoServerLocal(message, clientId);
                        break;
                    case NetworkLog.LogType.Warning:
                        NetworkLog.LogWarningServerLocal(message, clientId);
                        break;
                    case NetworkLog.LogType.Error:
                        NetworkLog.LogErrorServerLocal(message, clientId);
                        break;
                }
            }
        }

        internal static void HandleSnapshot(ulong clientId, Stream messageStream)
        {
            NetworkManager.Singleton.SnapshotSystem.ReadSnapshot(clientId, messageStream);
        }

        internal static void HandleAck(ulong clientId, Stream messageStream)
        {
            NetworkManager.Singleton.SnapshotSystem.ReadAck(clientId, messageStream);
        }

        public void HandleAllClientsSwitchSceneCompleted(ulong clientId, Stream stream)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                var clientIds = reader.ReadULongArray();
                var timedOutClientIds = reader.ReadULongArray();
                NetworkManager.SceneManager.AllClientsReady(clientIds, timedOutClientIds);
            }
        }
    }
}
