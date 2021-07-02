using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.Linq;
using System.IO;
using MLAPI.Configuration;
using MLAPI.Logging;
using MLAPI.Messaging;
using MLAPI.NetworkVariable;
using MLAPI.Profiling;
using MLAPI.Reflection;
using MLAPI.Serialization;
using MLAPI.Serialization.Pooled;
using MLAPI.Transports;

namespace MLAPI
{
    /// <summary>
    /// The base class to override to write network code. Inherits MonoBehaviour
    /// </summary>
    public abstract class NetworkBehaviour : MonoBehaviour
    {
#pragma warning disable IDE1006 // disable naming rule violation check
        // RuntimeAccessModifiersILPP will make this `protected`
        internal enum __RpcExecStage
#pragma warning restore IDE1006 // restore naming rule violation check
        {
            None = 0,
            Server = 1,
            Client = 2
        }

#pragma warning disable 414 // disable assigned but its value is never used
#pragma warning disable IDE1006 // disable naming rule violation check
        [NonSerialized]
        // RuntimeAccessModifiersILPP will make this `protected`
        internal __RpcExecStage __rpc_exec_stage = __RpcExecStage.None;
#pragma warning restore 414 // restore assigned but its value is never used
#pragma warning restore IDE1006 // restore naming rule violation

#pragma warning disable IDE1006 // disable naming rule violation check
        // RuntimeAccessModifiersILPP will make this `protected`
        internal NetworkSerializer __beginSendServerRpc(uint rpcMethodId, ServerRpcParams serverRpcParams, RpcDelivery rpcDelivery)
#pragma warning restore IDE1006 // restore naming rule violation check
        {
            PooledNetworkWriter writer;

            var rpcQueueContainer = NetworkManager.RpcQueueContainer;
            var isUsingBatching = rpcQueueContainer.IsUsingBatching();
            var transportChannel = rpcDelivery == RpcDelivery.Reliable ? NetworkChannel.ReliableRpc : NetworkChannel.UnreliableRpc;

            if (IsHost)
            {
                writer = rpcQueueContainer.BeginAddQueueItemToFrame(RpcQueueContainer.QueueItemType.ServerRpc, Time.realtimeSinceStartup, transportChannel,
                    NetworkManager.ServerClientId, null, RpcQueueHistoryFrame.QueueFrameType.Inbound, serverRpcParams.Send.UpdateStage);

                if (!isUsingBatching)
                {
                    writer.WriteByte(NetworkConstants.SERVER_RPC); // MessageType
                }
            }
            else
            {
                writer = rpcQueueContainer.BeginAddQueueItemToFrame(RpcQueueContainer.QueueItemType.ServerRpc, Time.realtimeSinceStartup, transportChannel,
                    NetworkManager.ServerClientId, null, RpcQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);
                if (!isUsingBatching)
                {
                    writer.WriteByte(NetworkConstants.SERVER_RPC); // MessageType
                }
            }

            writer.WriteUInt64Packed(NetworkObjectId); // NetworkObjectId
            writer.WriteUInt16Packed(NetworkBehaviourId); // NetworkBehaviourId
            writer.WriteUInt32Packed(rpcMethodId); // NetworkRpcMethodId
            writer.WriteByte((byte)serverRpcParams.Send.UpdateStage); // NetworkUpdateStage

            return writer.Serializer;
        }

#pragma warning disable IDE1006 // disable naming rule violation check
        // RuntimeAccessModifiersILPP will make this `protected`
        internal void __endSendServerRpc(NetworkSerializer serializer, uint rpcMethodId, ServerRpcParams serverRpcParams, RpcDelivery rpcDelivery)
#pragma warning restore IDE1006 // restore naming rule violation check
        {
            if (serializer == null)
            {
                return;
            }

            var rpcQueueContainer = NetworkManager.RpcQueueContainer;
            if (IsHost)
            {
                rpcQueueContainer.EndAddQueueItemToFrame(serializer.Writer, RpcQueueHistoryFrame.QueueFrameType.Inbound, serverRpcParams.Send.UpdateStage);
            }
            else
            {
                rpcQueueContainer.EndAddQueueItemToFrame(serializer.Writer, RpcQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);
            }
        }

#pragma warning disable IDE1006 // disable naming rule violation check
        // RuntimeAccessModifiersILPP will make this `protected`
        internal NetworkSerializer __beginSendClientRpc(uint rpcMethodId, ClientRpcParams clientRpcParams, RpcDelivery rpcDelivery)
#pragma warning restore IDE1006 // restore naming rule violation check
        {
            PooledNetworkWriter writer;

            // This will start a new queue item entry and will then return the writer to the current frame's stream
            var rpcQueueContainer = NetworkManager.RpcQueueContainer;
            var isUsingBatching = rpcQueueContainer.IsUsingBatching();
            var transportChannel = rpcDelivery == RpcDelivery.Reliable ? NetworkChannel.ReliableRpc : NetworkChannel.UnreliableRpc;

            ulong[] clientIds = clientRpcParams.Send.TargetClientIds ?? NetworkManager.ConnectedClientsList.Select(c => c.ClientId).ToArray();
            if (clientRpcParams.Send.TargetClientIds != null && clientRpcParams.Send.TargetClientIds.Length == 0)
            {
                clientIds = NetworkManager.ConnectedClientsList.Select(c => c.ClientId).ToArray();
            }

            //NOTES ON BELOW CHANGES:
            //The following checks for IsHost and whether the host client id is part of the clients to recieve the RPC
            //Is part of a patch-fix to handle looping back RPCs into the next frame's inbound queue.
            //!!! This code is temporary and will change (soon) when NetworkSerializer can be configured for mutliple NetworkWriters!!!
            var containsServerClientId = clientIds.Contains(NetworkManager.ServerClientId);
            if (IsHost && containsServerClientId)
            {
                //Always write to the next frame's inbound queue
                writer = rpcQueueContainer.BeginAddQueueItemToFrame(RpcQueueContainer.QueueItemType.ClientRpc, Time.realtimeSinceStartup, transportChannel,
                    NetworkManager.ServerClientId, null, RpcQueueHistoryFrame.QueueFrameType.Inbound, clientRpcParams.Send.UpdateStage);

                //Handle sending to the other clients, if so the above notes explain why this code is here (a temporary patch-fix)
                if (clientIds.Length > 1)
                {
                    //Set the loopback frame
                    rpcQueueContainer.SetLoopBackFrameItem(clientRpcParams.Send.UpdateStage);

                    //Switch to the outbound queue
                    writer = rpcQueueContainer.BeginAddQueueItemToFrame(RpcQueueContainer.QueueItemType.ClientRpc, Time.realtimeSinceStartup, transportChannel, NetworkObjectId,
                        clientIds, RpcQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);

                    if (!isUsingBatching)
                    {
                        writer.WriteByte(NetworkConstants.CLIENT_RPC); // MessageType
                    }
                }
                else
                {
                    if (!isUsingBatching)
                    {
                        writer.WriteByte(NetworkConstants.CLIENT_RPC); // MessageType
                    }
                }
            }
            else
            {
                writer = rpcQueueContainer.BeginAddQueueItemToFrame(RpcQueueContainer.QueueItemType.ClientRpc, Time.realtimeSinceStartup, transportChannel, NetworkObjectId,
                    clientIds, RpcQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);

                if (!isUsingBatching)
                {
                    writer.WriteByte(NetworkConstants.CLIENT_RPC); // MessageType
                }
            }

            writer.WriteUInt64Packed(NetworkObjectId); // NetworkObjectId
            writer.WriteUInt16Packed(NetworkBehaviourId); // NetworkBehaviourId
            writer.WriteUInt32Packed(rpcMethodId); // NetworkRpcMethodId
            writer.WriteByte((byte)clientRpcParams.Send.UpdateStage); // NetworkUpdateStage

            return writer.Serializer;
        }

#pragma warning disable IDE1006 // disable naming rule violation check
        // RuntimeAccessModifiersILPP will make this `protected`
        internal void __endSendClientRpc(NetworkSerializer serializer, uint rpcMethodId, ClientRpcParams clientRpcParams, RpcDelivery rpcDelivery)
#pragma warning restore IDE1006 // restore naming rule violation check
        {
            if (serializer == null)
            {
                return;
            }

            var rpcQueueContainer = NetworkManager.RpcQueueContainer;

            if (IsHost)
            {
                ulong[] clientIds = clientRpcParams.Send.TargetClientIds ?? NetworkManager.ConnectedClientsList.Select(c => c.ClientId).ToArray();
                if (clientRpcParams.Send.TargetClientIds != null && clientRpcParams.Send.TargetClientIds.Length == 0)
                {
                    clientIds = NetworkManager.ConnectedClientsList.Select(c => c.ClientId).ToArray();
                }

                var containsServerClientId = clientIds.Contains(NetworkManager.ServerClientId);
                if (containsServerClientId && clientIds.Length == 1)
                {
                    rpcQueueContainer.EndAddQueueItemToFrame(serializer.Writer, RpcQueueHistoryFrame.QueueFrameType.Inbound, clientRpcParams.Send.UpdateStage);
                    return;
                }
            }

            rpcQueueContainer.EndAddQueueItemToFrame(serializer.Writer, RpcQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);
        }

        /// <summary>
        /// Gets the NetworkManager that owns this NetworkBehaviour instance
        /// </summary>
        public NetworkManager NetworkManager => NetworkObject.NetworkManager;

        /// <summary>
        /// Gets if the object is the the personal clients player object
        /// </summary>
        public bool IsLocalPlayer => NetworkObject.IsLocalPlayer;

        /// <summary>
        /// Gets if the object is owned by the local player or if the object is the local player object
        /// </summary>
        public bool IsOwner => NetworkObject.IsOwner;

        /// <summary>
        /// Gets if we are executing as server
        /// </summary>
        protected bool IsServer => IsRunning && NetworkManager.IsServer;

        /// <summary>
        /// Gets if we are executing as client
        /// </summary>
        protected bool IsClient => IsRunning && NetworkManager.IsClient;

        /// <summary>
        /// Gets if we are executing as Host, I.E Server and Client
        /// </summary>
        protected bool IsHost => IsRunning && NetworkManager.IsHost;

        private bool IsRunning => NetworkManager != null && NetworkManager.IsListening;

        /// <summary>
        /// Gets Whether or not the object has a owner
        /// </summary>
        public bool IsOwnedByServer => NetworkObject.IsOwnedByServer;

        /// <summary>
        /// Gets the NetworkObject that owns this NetworkBehaviour instance
        /// </summary>
        public NetworkObject NetworkObject
        {
            get
            {
                if (m_NetworkObject == null)
                {
                    m_NetworkObject = GetComponentInParent<NetworkObject>();
                }

                if (m_NetworkObject == null && NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"Could not get {nameof(NetworkObject)} for the {nameof(NetworkBehaviour)}. Are you missing a {nameof(NetworkObject)} component?");
                }

                return m_NetworkObject;
            }
        }

        /// <summary>
        /// Gets whether or not this NetworkBehaviour instance has a NetworkObject owner.
        /// </summary>
        public bool HasNetworkObject => NetworkObject != null;

        private NetworkObject m_NetworkObject = null;

        /// <summary>
        /// Gets the NetworkId of the NetworkObject that owns this NetworkBehaviour
        /// </summary>
        public ulong NetworkObjectId => NetworkObject.NetworkObjectId;

        /// <summary>
        /// Gets NetworkId for this NetworkBehaviour from the owner NetworkObject
        /// </summary>
        public ushort NetworkBehaviourId => NetworkObject.GetNetworkBehaviourOrderIndex(this);

        /// <summary>
        /// Internally caches the Id of this behaviour in a NetworkObject. Makes look-up faster
        /// </summary>
        internal ushort NetworkBehaviourIdCache = 0;

        /// <summary>
        /// Returns a the NetworkBehaviour with a given BehaviourId for the current NetworkObject
        /// </summary>
        /// <param name="behaviourId">The behaviourId to return</param>
        /// <returns>Returns NetworkBehaviour with given behaviourId</returns>
        protected NetworkBehaviour GetNetworkBehaviour(ushort behaviourId)
        {
            return NetworkObject.GetNetworkBehaviourAtOrderIndex(behaviourId);
        }

        /// <summary>
        /// Gets the ClientId that owns the NetworkObject
        /// </summary>
        public ulong OwnerClientId => NetworkObject.OwnerClientId;


        /// <summary>
        /// Gets called when message handlers are ready to be registered and the network is setup
        /// </summary>
        public virtual void OnNetworkSpawn() { }

        /// <summary>
        /// Gets called when the <see cref="NetworkObject"/> gets spawned, message handlers are ready to be registered and the network is setup. Provides a Payload if it was provided
        /// </summary>
        /// <param name="stream">The stream containing the spawn payload</param>
        public virtual void OnNetworkSpawn(Stream stream)
        {
            OnNetworkSpawn();
        }

        /// <summary>
        /// Gets called when the <see cref="NetworkObject"/> gets de-spawned. Is called both on the server and clients.
        /// </summary>
        public virtual void OnNetworkDespawn() { }

        internal void InternalOnNetworkSpawn()
        {
            InitializeVariables();
        }

        internal void InternalOnNetworkDespawn()
        {

        }

        /// <summary>
        /// Gets called when the local client gains ownership of this object
        /// </summary>
        public virtual void OnGainedOwnership() { }

        /// <summary>
        /// Gets called when we loose ownership of this object
        /// </summary>
        public virtual void OnLostOwnership() { }

        /// <summary>
        /// Gets called when the parent NetworkObject of this NetworkBehaviour's NetworkObject has changed
        /// </summary>
        public virtual void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject) { }

        private bool m_VarInit = false;

        private readonly List<HashSet<int>> m_ChannelMappedNetworkVariableIndexes = new List<HashSet<int>>();
        private readonly List<NetworkChannel> m_ChannelsForNetworkVariableGroups = new List<NetworkChannel>();
        internal readonly List<INetworkVariable> NetworkVariableFields = new List<INetworkVariable>();

        private static Dictionary<Type, FieldInfo[]> s_FieldTypes = new Dictionary<Type, FieldInfo[]>();

        private static FieldInfo[] GetFieldInfoForType(Type type)
        {
            if (!s_FieldTypes.ContainsKey(type))
            {
                s_FieldTypes.Add(type, GetFieldInfoForTypeRecursive(type));
            }

            return s_FieldTypes[type];
        }

        private static FieldInfo[] GetFieldInfoForTypeRecursive(Type type, List<FieldInfo> list = null)
        {
            if (list == null)
            {
                list = new List<FieldInfo>();
                list.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            }
            else
            {
                list.AddRange(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance));
            }

            if (type.BaseType != null && type.BaseType != typeof(NetworkBehaviour))
            {
                return GetFieldInfoForTypeRecursive(type.BaseType, list);
            }

            return list.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
        }

        internal void InitializeVariables()
        {
            if (m_VarInit)
            {
                return;
            }

            m_VarInit = true;

            FieldInfo[] sortedFields = GetFieldInfoForType(GetType());

            for (int i = 0; i < sortedFields.Length; i++)
            {
                Type fieldType = sortedFields[i].FieldType;

                if (fieldType.HasInterface(typeof(INetworkVariable)))
                {
                    var instance = (INetworkVariable)sortedFields[i].GetValue(this);

                    if (instance == null)
                    {
                        instance = (INetworkVariable)Activator.CreateInstance(fieldType, true);
                        sortedFields[i].SetValue(this, instance);
                    }

                    instance.SetNetworkBehaviour(this);

                    var instanceNameProperty = fieldType.GetProperty(nameof(INetworkVariable.Name));
                    instanceNameProperty?.SetValue(instance, sortedFields[i].Name);

                    NetworkVariableFields.Add(instance);
                }
            }

            {
                // Create index map for channels
                var firstLevelIndex = new Dictionary<NetworkChannel, int>();
                int secondLevelCounter = 0;

                for (int i = 0; i < NetworkVariableFields.Count; i++)
                {
                    NetworkChannel networkChannel = NetworkVariableFields[i].GetChannel();

                    if (!firstLevelIndex.ContainsKey(networkChannel))
                    {
                        firstLevelIndex.Add(networkChannel, secondLevelCounter);
                        m_ChannelsForNetworkVariableGroups.Add(networkChannel);
                        secondLevelCounter++;
                    }

                    if (firstLevelIndex[networkChannel] >= m_ChannelMappedNetworkVariableIndexes.Count)
                    {
                        m_ChannelMappedNetworkVariableIndexes.Add(new HashSet<int>());
                    }

                    m_ChannelMappedNetworkVariableIndexes[firstLevelIndex[networkChannel]].Add(i);
                }
            }
        }

        internal void PreNetworkVariableWrite()
        {
            // reset our "which variables got written" data
            m_NetworkVariableIndexesToReset.Clear();
            m_NetworkVariableIndexesToResetSet.Clear();
        }

        internal void PostNetworkVariableWrite()
        {
            // mark any variables we wrote as no longer dirty
            for (int i = 0; i < m_NetworkVariableIndexesToReset.Count; i++)
            {
                NetworkVariableFields[m_NetworkVariableIndexesToReset[i]].ResetDirty();
            }
        }

        internal void VariableUpdate(ulong clientId)
        {
            if (!m_VarInit)
            {
                InitializeVariables();
            }

            PreNetworkVariableWrite();
            NetworkVariableUpdate(clientId, NetworkBehaviourId);
        }

        private readonly List<int> m_NetworkVariableIndexesToReset = new List<int>();
        private readonly HashSet<int> m_NetworkVariableIndexesToResetSet = new HashSet<int>();

        private void NetworkVariableUpdate(ulong clientId, int behaviourIndex)
        {
            if (!CouldHaveDirtyNetworkVariables())
            {
                return;
            }

            if (NetworkManager.UseSnapshot)
            {
                for (int k = 0; k < NetworkVariableFields.Count; k++)
                {
                    NetworkManager.SnapshotSystem.Store(NetworkObjectId, behaviourIndex, k, NetworkVariableFields[k]);
                }
            }

            if (NetworkManager.UseClassicDelta)
            {
                for (int j = 0; j < m_ChannelMappedNetworkVariableIndexes.Count; j++)
                {
                    using (var buffer = PooledNetworkBuffer.Get())
                    {
                        using (var writer = PooledNetworkWriter.Get(buffer))
                        {
                            writer.WriteUInt64Packed(NetworkObjectId);
                            writer.WriteUInt16Packed(NetworkObject.GetNetworkBehaviourOrderIndex(this));

                            bool writtenAny = false;
                            for (int k = 0; k < NetworkVariableFields.Count; k++)
                            {
                                if (!m_ChannelMappedNetworkVariableIndexes[j].Contains(k))
                                {
                                    // This var does not belong to the currently iterating channel group.
                                    if (NetworkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                                    {
                                        writer.WriteUInt16Packed(0);
                                    }
                                    else
                                    {
                                        writer.WriteBool(false);
                                    }

                                    continue;
                                }

                                bool isDirty =
                                    NetworkVariableFields[k]
                                        .IsDirty(); // cache this here. You never know what operations users will do in the dirty methods

                                //   if I'm dirty AND a client, write (server always has all permissions)
                                //   if I'm dirty AND the server AND the client can read me, send.
                                bool shouldWrite = isDirty &&
                                                   (!IsServer || NetworkVariableFields[k].CanClientRead(clientId));

                                if (NetworkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                                {
                                    if (!shouldWrite)
                                    {
                                        writer.WriteUInt16Packed(0);
                                    }
                                }
                                else
                                {
                                    writer.WriteBool(shouldWrite);
                                }

                                if (shouldWrite)
                                {
                                    writtenAny = true;

                                    if (NetworkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                                    {
                                        using (var varBuffer = PooledNetworkBuffer.Get())
                                        {
                                            NetworkVariableFields[k].WriteDelta(varBuffer);
                                            varBuffer.PadBuffer();

                                            writer.WriteUInt16Packed((ushort)varBuffer.Length);
                                            buffer.CopyFrom(varBuffer);
                                        }
                                    }
                                    else
                                    {
                                        NetworkVariableFields[k].WriteDelta(buffer);
                                    }

                                    if (!m_NetworkVariableIndexesToResetSet.Contains(k))
                                    {
                                        m_NetworkVariableIndexesToResetSet.Add(k);
                                        m_NetworkVariableIndexesToReset.Add(k);
                                    }
                                }
                            }

                            if (writtenAny)
                            {
                                NetworkManager.MessageSender.Send(clientId, NetworkConstants.NETWORK_VARIABLE_DELTA,
                                    m_ChannelsForNetworkVariableGroups[j], buffer);
                            }
                        }
                    }
                }
            }
        }

        private bool CouldHaveDirtyNetworkVariables()
        {
            // TODO: There should be a better way by reading one dirty variable vs. 'n'
            for (int i = 0; i < NetworkVariableFields.Count; i++)
            {
                if (NetworkVariableFields[i].IsDirty())
                {
                    return true;
                }
            }

            return false;
        }

        internal static void HandleNetworkVariableDeltas(List<INetworkVariable> networkVariableList, Stream stream, ulong clientId, NetworkBehaviour logInstance, NetworkManager networkManager)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                for (int i = 0; i < networkVariableList.Count; i++)
                {
                    ushort varSize = 0;

                    if (networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                    {
                        varSize = reader.ReadUInt16Packed();

                        if (varSize == 0)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (!reader.ReadBool())
                        {
                            continue;
                        }
                    }

                    if (networkManager.IsServer && !networkVariableList[i].CanClientWrite(clientId))
                    {
                        if (networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                        {
                            if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                            {
                                NetworkLog.LogWarning($"Client wrote to {nameof(NetworkVariable)} without permission. => {(logInstance != null ? ($"{nameof(NetworkObjectId)}: {logInstance.NetworkObjectId} - {nameof(NetworkObject.GetNetworkBehaviourOrderIndex)}(): {logInstance.NetworkObject.GetNetworkBehaviourOrderIndex(logInstance)} - VariableIndex: {i}") : string.Empty)}");
                                NetworkLog.LogError($"[{networkVariableList[i].GetType().Name}]");
                            }

                            stream.Position += varSize;
                            continue;
                        }

                        //This client wrote somewhere they are not allowed. This is critical
                        //We can't just skip this field. Because we don't actually know how to dummy read
                        //That is, we don't know how many bytes to skip. Because the interface doesn't have a
                        //Read that gives us the value. Only a Read that applies the value straight away
                        //A dummy read COULD be added to the interface for this situation, but it's just being too nice.
                        //This is after all a developer fault. A critical error should be fine.
                        // - TwoTen

                        if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                        {
                            NetworkLog.LogError($"Client wrote to {nameof(NetworkVariable)} without permission. No more variables can be read. This is critical. => {(logInstance != null ? ($"{nameof(NetworkObjectId)}: {logInstance.NetworkObjectId} - {nameof(NetworkObject.GetNetworkBehaviourOrderIndex)}(): {logInstance.NetworkObject.GetNetworkBehaviourOrderIndex(logInstance)} - VariableIndex: {i}") : string.Empty)}");
                            NetworkLog.LogError($"[{networkVariableList[i].GetType().Name}]");
                        }

                        return;
                    }

                    long readStartPos = stream.Position;

                    networkVariableList[i].ReadDelta(stream, networkManager.IsServer);
                    PerformanceDataManager.Increment(ProfilerConstants.NetworkVarDeltas);

                    if (networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                    {
                        (stream as NetworkBuffer).SkipPadBits();

                        if (stream.Position > (readStartPos + varSize))
                        {
                            if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                            {
                                NetworkLog.LogWarning($"Var delta read too far. {stream.Position - (readStartPos + varSize)} bytes. => {(logInstance != null ? ($"{nameof(NetworkObjectId)}: {logInstance.NetworkObjectId} - {nameof(NetworkObject.GetNetworkBehaviourOrderIndex)}(): {logInstance.NetworkObject.GetNetworkBehaviourOrderIndex(logInstance)} - VariableIndex: {i}") : string.Empty)}");
                            }

                            stream.Position = readStartPos + varSize;
                        }
                        else if (stream.Position < (readStartPos + varSize))
                        {
                            if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                            {
                                NetworkLog.LogWarning($"Var delta read too little. {(readStartPos + varSize) - stream.Position} bytes. => {(logInstance != null ? ($"{nameof(NetworkObjectId)}: {logInstance.NetworkObjectId} - {nameof(NetworkObject.GetNetworkBehaviourOrderIndex)}(): {logInstance.NetworkObject.GetNetworkBehaviourOrderIndex(logInstance)} - VariableIndex: {i}") : string.Empty)}");
                            }

                            stream.Position = readStartPos + varSize;
                        }
                    }
                }
            }
        }

        internal static void HandleNetworkVariableUpdate(List<INetworkVariable> networkVariableList, Stream stream, ulong clientId, NetworkBehaviour logInstance, NetworkManager networkManager)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                for (int i = 0; i < networkVariableList.Count; i++)
                {
                    ushort varSize = 0;

                    if (networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                    {
                        varSize = reader.ReadUInt16Packed();

                        if (varSize == 0)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (!reader.ReadBool())
                        {
                            continue;
                        }
                    }

                    if (networkManager.IsServer && !networkVariableList[i].CanClientWrite(clientId))
                    {
                        if (networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                        {
                            if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                            {
                                NetworkLog.LogWarning($"Client wrote to {nameof(NetworkVariable)} without permission. => {(logInstance != null ? ($"{nameof(NetworkObjectId)}: {logInstance.NetworkObjectId} - {nameof(NetworkObject.GetNetworkBehaviourOrderIndex)}(): {logInstance.NetworkObject.GetNetworkBehaviourOrderIndex(logInstance)} - VariableIndex: {i}") : string.Empty)}");
                            }

                            stream.Position += varSize;
                            continue;
                        }

                        //This client wrote somewhere they are not allowed. This is critical
                        //We can't just skip this field. Because we don't actually know how to dummy read
                        //That is, we don't know how many bytes to skip. Because the interface doesn't have a
                        //Read that gives us the value. Only a Read that applies the value straight away
                        //A dummy read COULD be added to the interface for this situation, but it's just being too nice.
                        //This is after all a developer fault. A critical error should be fine.
                        // - TwoTen
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                        {
                            NetworkLog.LogError($"Client wrote to {nameof(NetworkVariable)} without permission. No more variables can be read. This is critical. => {(logInstance != null ? ($"{nameof(NetworkObjectId)}: {logInstance.NetworkObjectId} - {nameof(NetworkObject.GetNetworkBehaviourOrderIndex)}(): {logInstance.NetworkObject.GetNetworkBehaviourOrderIndex(logInstance)} - VariableIndex: {i}") : string.Empty)}");
                        }

                        return;
                    }

                    long readStartPos = stream.Position;

                    networkVariableList[i].ReadField(stream);
                    PerformanceDataManager.Increment(ProfilerConstants.NetworkVarUpdates);

                    if (networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                    {
                        if (stream is NetworkBuffer networkBuffer)
                        {
                            networkBuffer.SkipPadBits();
                        }

                        if (stream.Position > (readStartPos + varSize))
                        {
                            if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                            {
                                NetworkLog.LogWarning($"Var update read too far. {stream.Position - (readStartPos + varSize)} bytes. => {(logInstance != null ? ($"{nameof(NetworkObjectId)}: {logInstance.NetworkObjectId} - {nameof(NetworkObject.GetNetworkBehaviourOrderIndex)}(): {logInstance.NetworkObject.GetNetworkBehaviourOrderIndex(logInstance)} - VariableIndex: {i}") : string.Empty)}");
                            }

                            stream.Position = readStartPos + varSize;
                        }
                        else if (stream.Position < (readStartPos + varSize))
                        {
                            if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                            {
                                NetworkLog.LogWarning($"Var update read too little. {(readStartPos + varSize) - stream.Position} bytes. => {(logInstance != null ? ($"{nameof(NetworkObjectId)}: {logInstance.NetworkObjectId} - {nameof(NetworkObject.GetNetworkBehaviourOrderIndex)}(): {logInstance.NetworkObject.GetNetworkBehaviourOrderIndex(logInstance)} - VariableIndex: {i}") : string.Empty)}");
                            }

                            stream.Position = readStartPos + varSize;
                        }
                    }
                }
            }
        }


        internal static void WriteNetworkVariableData(List<INetworkVariable> networkVariableList, Stream stream, ulong clientId, NetworkManager networkManager)
        {
            if (networkVariableList.Count == 0)
            {
                return;
            }

            using (var writer = PooledNetworkWriter.Get(stream))
            {
                for (int j = 0; j < networkVariableList.Count; j++)
                {
                    bool canClientRead = networkVariableList[j].CanClientRead(clientId);

                    if (networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                    {
                        if (!canClientRead)
                        {
                            writer.WriteUInt16Packed(0);
                        }
                    }
                    else
                    {
                        writer.WriteBool(canClientRead);
                    }

                    if (canClientRead)
                    {
                        if (networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                        {
                            using (var varBuffer = PooledNetworkBuffer.Get())
                            {
                                networkVariableList[j].WriteField(varBuffer);
                                varBuffer.PadBuffer();

                                writer.WriteUInt16Packed((ushort)varBuffer.Length);
                                varBuffer.CopyTo(stream);
                            }
                        }
                        else
                        {
                            networkVariableList[j].WriteField(stream);
                        }
                    }
                }
            }
        }

        internal static void SetNetworkVariableData(List<INetworkVariable> networkVariableList, Stream stream, NetworkManager networkManager)
        {
            if (networkVariableList.Count == 0)
            {
                return;
            }

            using (var reader = PooledNetworkReader.Get(stream))
            {
                for (int j = 0; j < networkVariableList.Count; j++)
                {
                    ushort varSize = 0;

                    if (networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                    {
                        varSize = reader.ReadUInt16Packed();

                        if (varSize == 0)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (!reader.ReadBool())
                        {
                            continue;
                        }
                    }

                    long readStartPos = stream.Position;

                    networkVariableList[j].ReadField(stream);

                    if (networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                    {
                        if (stream is NetworkBuffer networkBuffer)
                        {
                            networkBuffer.SkipPadBits();
                        }

                        if (stream.Position > (readStartPos + varSize))
                        {
                            if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                            {
                                NetworkLog.LogWarning($"Var data read too far. {stream.Position - (readStartPos + varSize)} bytes.");
                            }

                            stream.Position = readStartPos + varSize;
                        }
                        else if (stream.Position < (readStartPos + varSize))
                        {
                            if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                            {
                                NetworkLog.LogWarning($"Var data read too little. {(readStartPos + varSize) - stream.Position} bytes.");
                            }

                            stream.Position = readStartPos + varSize;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the local instance of a object with a given NetworkId
        /// </summary>
        /// <param name="networkId"></param>
        /// <returns></returns>
        protected NetworkObject GetNetworkObject(ulong networkId)
        {
            return NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkId, out NetworkObject networkObject) ? networkObject : null;
        }
    }
}
