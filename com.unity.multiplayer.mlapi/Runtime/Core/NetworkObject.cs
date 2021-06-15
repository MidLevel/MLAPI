using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using MLAPI.Configuration;
using MLAPI.Exceptions;
using MLAPI.Hashing;
using MLAPI.Logging;
using MLAPI.Serialization.Pooled;
using MLAPI.Transports;
using MLAPI.Serialization;
using UnityEngine;

namespace MLAPI
{
    /// <summary>
    /// A component used to identify that a GameObject in the network
    /// </summary>
    [AddComponentMenu("MLAPI/NetworkObject", -99)]
    [DisallowMultipleComponent]
    public sealed class NetworkObject : MonoBehaviour
    {
        [HideInInspector] [SerializeField] internal uint GlobalObjectIdHash;

#if UNITY_EDITOR
        // HEAD: DO NOT USE! TEST ONLY TEMP IMPL, WILL BE REMOVED
        internal uint TempGlobalObjectIdHashOverride = 0;
        // TAIL: DO NOT USE! TEST ONLY TEMP IMPL, WILL BE REMOVED

        private void OnValidate()
        {
            GenerateGlobalObjectIdHash();
        }

        internal void GenerateGlobalObjectIdHash()
        {
            // HEAD: DO NOT USE! TEST ONLY TEMP IMPL, WILL BE REMOVED
            if (TempGlobalObjectIdHashOverride != 0)
            {
                GlobalObjectIdHash = TempGlobalObjectIdHashOverride;
                return;
            }
            // TAIL: DO NOT USE! TEST ONLY TEMP IMPL, WILL BE REMOVED

            // do NOT regenerate GlobalObjectIdHash for NetworkPrefabs while Editor is in PlayMode
            if (UnityEditor.EditorApplication.isPlaying && !string.IsNullOrEmpty(gameObject.scene.name))
            {
                return;
            }

            // do NOT regenerate GlobalObjectIdHash if Editor is transitining into or out of PlayMode
            if (!UnityEditor.EditorApplication.isPlaying && UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var globalObjectIdString = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(this).ToString();
            GlobalObjectIdHash = XXHash.Hash32(globalObjectIdString);
        }
#endif

        /// <summary>
        /// Gets the NetworkManager that owns this NetworkObject instance
        /// </summary>
        public NetworkManager NetworkManager => NetworkManagerOwner != null ? NetworkManagerOwner : NetworkManager.Singleton;

        /// <summary>
        /// The NetworkManager that owns this NetworkObject.
        /// This property controls where this NetworkObject belongs.
        /// This property is null by default currently, which means that the above NetworkManager getter will return the Singleton.
        /// In the future this is the path where alternative NetworkManagers should be injected for running multi NetworkManagers
        /// </summary>
        internal NetworkManager NetworkManagerOwner;

        /// <summary>
        /// Gets the unique Id of this object that is synced across the network
        /// </summary>
        public ulong NetworkObjectId { get; internal set; }

        /// <summary>
        /// Gets the ClientId of the owner of this NetworkObject
        /// </summary>
        public ulong OwnerClientId
        {
            get
            {
                if (OwnerClientIdInternal == null)
                {
                    return NetworkManager != null ? NetworkManager.ServerClientId : 0;
                }
                else
                {
                    return OwnerClientIdInternal.Value;
                }
            }
            internal set
            {
                if (NetworkManager != null && value == NetworkManager.ServerClientId)
                {
                    OwnerClientIdInternal = null;
                }
                else
                {
                    OwnerClientIdInternal = value;
                }
            }
        }

        internal ulong? OwnerClientIdInternal = null;

        /// <summary>
        /// If true, the object will always be replicated as root on clients and the parent will be ignored.
        /// </summary>
        public bool AlwaysReplicateAsRoot;

        /// <summary>
        /// Gets if this object is a player object
        /// </summary>
        public bool IsPlayerObject { get; internal set; }

        /// <summary>
        /// Gets if the object is the the personal clients player object
        /// </summary>
        public bool IsLocalPlayer => NetworkManager != null && IsPlayerObject && OwnerClientId == NetworkManager.LocalClientId;

        /// <summary>
        /// Gets if the object is owned by the local player or if the object is the local player object
        /// </summary>
        public bool IsOwner => NetworkManager != null && OwnerClientId == NetworkManager.LocalClientId;

        /// <summary>
        /// Gets Whether or not the object is owned by anyone
        /// </summary>
        public bool IsOwnedByServer => NetworkManager != null && OwnerClientId == NetworkManager.ServerClientId;

        /// <summary>
        /// Gets if the object has yet been spawned across the network
        /// </summary>
        public bool IsSpawned { get; internal set; }

        /// <summary>
        /// Gets if the object is a SceneObject, null if it's not yet spawned but is a scene object.
        /// </summary>
        public bool? IsSceneObject { get; internal set; }

        /// <summary>
        /// Gets whether or not the object should be automatically removed when the scene is unloaded.
        /// </summary>
        public bool DestroyWithScene { get; internal set; }

        /// <summary>
        /// Delegate type for checking visibility
        /// </summary>
        /// <param name="clientId">The clientId to check visibility for</param>
        public delegate bool VisibilityDelegate(ulong clientId);

        /// <summary>
        /// Delegate invoked when the MLAPI needs to know if the object should be visible to a client, if null it will assume true
        /// </summary>
        public VisibilityDelegate CheckObjectVisibility = null;

        /// <summary>
        /// Delegate type for checking spawn options
        /// </summary>
        /// <param name="clientId">The clientId to check spawn options for</param>
        public delegate bool SpawnDelegate(ulong clientId);

        /// <summary>
        /// Delegate invoked when the MLAPI needs to know if it should include the transform when spawning the object, if null it will assume true
        /// </summary>
        public SpawnDelegate IncludeTransformWhenSpawning = null;

        /// <summary>
        /// Whether or not to destroy this object if it's owner is destroyed.
        /// If false, the objects ownership will be given to the server.
        /// </summary>
        public bool DontDestroyWithOwner;

        internal readonly HashSet<ulong> Observers = new HashSet<ulong>();

        /// <summary>
        /// Returns Observers enumerator
        /// </summary>
        /// <returns>Observers enumerator</returns>
        public HashSet<ulong>.Enumerator GetObservers()
        {
            if (!IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            return Observers.GetEnumerator();
        }

        /// <summary>
        /// Whether or not this object is visible to a specific client
        /// </summary>
        /// <param name="clientId">The clientId of the client</param>
        /// <returns>True if the client knows about the object</returns>
        public bool IsNetworkVisibleTo(ulong clientId)
        {
            if (!IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            return Observers.Contains(clientId);
        }

        /// <summary>
        /// Shows a previously hidden object to a client
        /// </summary>
        /// <param name="clientId">The client to show the object to</param>
        /// <param name="payload">An optional payload to send as part of the spawn</param>
        public void NetworkShow(ulong clientId, Stream payload = null)
        {
            if (!IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            if (!NetworkManager.IsServer)
            {
                throw new NotServerException("Only server can change visibility");
            }

            if (Observers.Contains(clientId))
            {
                throw new VisibilityChangeException("The object is already visible");
            }

            // Send spawn call
            Observers.Add(clientId);

            NetworkManager.SpawnManager.SendSpawnCallForObject(clientId, this, payload);
        }

        /// <summary>
        /// Shows a list of previously hidden objects to a client
        /// </summary>
        /// <param name="networkObjects">The objects to show</param>
        /// <param name="clientId">The client to show the objects to</param>
        /// <param name="payload">An optional payload to send as part of the spawns</param>
        public static void NetworkShow(List<NetworkObject> networkObjects, ulong clientId, Stream payload = null)
        {
            if (networkObjects == null || networkObjects.Count == 0)
            {
                throw new ArgumentNullException("At least one " + nameof(NetworkObject) + " has to be provided");
            }

            NetworkManager networkManager = networkObjects[0].NetworkManager;

            if (!networkManager.IsServer)
            {
                throw new NotServerException("Only server can change visibility");
            }


            // Do the safety loop first to prevent putting the MLAPI in an invalid state.
            for (int i = 0; i < networkObjects.Count; i++)
            {
                if (!networkObjects[i].IsSpawned)
                {
                    throw new SpawnStateException("Object is not spawned");
                }

                if (networkObjects[i].Observers.Contains(clientId))
                {
                    throw new VisibilityChangeException($"{nameof(NetworkObject)} with NetworkId: {networkObjects[i].NetworkObjectId} is already visible");
                }

                if (networkObjects[i].NetworkManager != networkManager)
                {
                    throw new ArgumentNullException("All " + nameof(NetworkObject) + "s must belong to the same " + nameof(NetworkManager));
                }
            }

            using (var buffer = PooledNetworkBuffer.Get())
            using (var writer = PooledNetworkWriter.Get(buffer))
            {
                writer.WriteUInt16Packed((ushort) networkObjects.Count);

                for (int i = 0; i < networkObjects.Count; i++)
                {
                    // Send spawn call
                    networkObjects[i].Observers.Add(clientId);

                    networkManager.SpawnManager.WriteSpawnCallForObject(buffer, clientId, networkObjects[i], payload);
                }

                networkManager.MessageSender.Send(clientId, NetworkConstants.ADD_OBJECTS, NetworkChannel.Internal, buffer);
            }
        }

        /// <summary>
        /// Hides a object from a specific client
        /// </summary>
        /// <param name="clientId">The client to hide the object for</param>
        public void NetworkHide(ulong clientId)
        {
            if (!IsSpawned)
            {
                throw new SpawnStateException("Object is not spawned");
            }

            if (!NetworkManager.IsServer)
            {
                throw new NotServerException("Only server can change visibility");
            }

            if (!Observers.Contains(clientId))
            {
                throw new VisibilityChangeException("The object is already hidden");
            }

            if (clientId == NetworkManager.ServerClientId)
            {
                throw new VisibilityChangeException("Cannot hide an object from the server");
            }


            // Send destroy call
            Observers.Remove(clientId);

            using (var buffer = PooledNetworkBuffer.Get())
            using (var writer = PooledNetworkWriter.Get(buffer))
            {
                writer.WriteUInt64Packed(NetworkObjectId);

                NetworkManager.MessageSender.Send(clientId, NetworkConstants.DESTROY_OBJECT, NetworkChannel.Internal, buffer);
            }
        }

        /// <summary>
        /// Hides a list of objects from a client
        /// </summary>
        /// <param name="networkObjects">The objects to hide</param>
        /// <param name="clientId">The client to hide the objects from</param>
        public static void NetworkHide(List<NetworkObject> networkObjects, ulong clientId)
        {
            if (networkObjects == null || networkObjects.Count == 0)
            {
                throw new ArgumentNullException("At least one " + nameof(NetworkObject) + " has to be provided");
            }

            NetworkManager networkManager = networkObjects[0].NetworkManager;

            if (!networkManager.IsServer)
            {
                throw new NotServerException("Only server can change visibility");
            }

            if (clientId == networkManager.ServerClientId)
            {
                throw new VisibilityChangeException("Cannot hide an object from the server");
            }

            // Do the safety loop first to prevent putting the MLAPI in an invalid state.
            for (int i = 0; i < networkObjects.Count; i++)
            {
                if (!networkObjects[i].IsSpawned)
                {
                    throw new SpawnStateException("Object is not spawned");
                }

                if (!networkObjects[i].Observers.Contains(clientId))
                {
                    throw new VisibilityChangeException($"{nameof(NetworkObject)} with {nameof(NetworkObjectId)}: {networkObjects[i].NetworkObjectId} is already hidden");
                }

                if (networkObjects[i].NetworkManager != networkManager)
                {
                    throw new ArgumentNullException("All " + nameof(NetworkObject) + "s must belong to the same " + nameof(NetworkManager));
                }
            }

            using (var buffer = PooledNetworkBuffer.Get())
            using (var writer = PooledNetworkWriter.Get(buffer))
            {
                writer.WriteUInt16Packed((ushort) networkObjects.Count);

                for (int i = 0; i < networkObjects.Count; i++)
                {
                    // Send destroy call
                    networkObjects[i].Observers.Remove(clientId);

                    writer.WriteUInt64Packed(networkObjects[i].NetworkObjectId);
                }

                networkManager.MessageSender.Send(clientId, NetworkConstants.DESTROY_OBJECTS, NetworkChannel.Internal, buffer);
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager == null || NetworkManager.SpawnManager == null)
            {
                return;
            }

            if (!NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(NetworkObjectId))
            {
                Debug.LogWarning($"Trying to destroy object {NetworkObjectId} but it doesn't seem to exist anymore!");
                return;
            }

            NetworkManager.SpawnManager.OnDestroyObject(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SpawnInternal(Stream spawnPayload, bool destroyWithScene, ulong? ownerClientId, bool playerObject)
        {
            if (!NetworkManager.IsListening)
            {
                throw new NotListeningException($"{nameof(NetworkManager)} isn't listening, start a server or host before spawning objects.");
            }

            if (!NetworkManager.IsServer)
            {
                throw new NotServerException($"Only server can spawn {nameof(NetworkObject)}s");
            }

            if (spawnPayload != null)
            {
                spawnPayload.Position = 0;
            }

            NetworkManager.SpawnManager.SpawnNetworkObjectLocally(this, NetworkManager.SpawnManager.GetNetworkObjectId(), false, playerObject, ownerClientId, spawnPayload, spawnPayload != null, spawnPayload == null ? 0 : (int) spawnPayload.Length, false, destroyWithScene);

            for (int i = 0; i < NetworkManager.ConnectedClientsList.Count; i++)
            {
                if (Observers.Contains(NetworkManager.ConnectedClientsList[i].ClientId))
                {
                    NetworkManager.SpawnManager.SendSpawnCallForObject(NetworkManager.ConnectedClientsList[i].ClientId, this, spawnPayload);
                }
            }
        }

        /// <summary>
        /// Spawns this GameObject across the network. Can only be called from the Server
        /// </summary>
        /// <param name="spawnPayload">The writer containing the spawn payload</param>
        /// <param name="destroyWithScene">Should the object be destroyd when the scene is changed</param>
        public void Spawn(Stream spawnPayload = null, bool destroyWithScene = false)
        {
            SpawnInternal(spawnPayload, destroyWithScene, null, false);
        }

        /// <summary>
        /// Spawns an object across the network with a given owner. Can only be called from server
        /// </summary>
        /// <param name="clientId">The clientId to own the object</param>
        /// <param name="spawnPayload">The writer containing the spawn payload</param>
        /// <param name="destroyWithScene">Should the object be destroyd when the scene is changed</param>
        public void SpawnWithOwnership(ulong clientId, Stream spawnPayload = null, bool destroyWithScene = false)
        {
            SpawnInternal(spawnPayload, destroyWithScene, clientId, false);
        }

        /// <summary>
        /// Spawns an object across the network and makes it the player object for the given client
        /// </summary>
        /// <param name="clientId">The clientId whos player object this is</param>
        /// <param name="spawnPayload">The writer containing the spawn payload</param>
        /// <param name="destroyWithScene">Should the object be destroyd when the scene is changed</param>
        public void SpawnAsPlayerObject(ulong clientId, Stream spawnPayload = null, bool destroyWithScene = false)
        {
            SpawnInternal(spawnPayload, destroyWithScene, clientId, true);
        }

        /// <summary>
        /// Despawns this GameObject and destroys it for other clients. This should be used if the object should be kept on the server
        /// </summary>
        public void Despawn(bool destroy = false)
        {
            NetworkManager.SpawnManager.DespawnObject(this, destroy);
        }


        /// <summary>
        /// Removes all ownership of an object from any client. Can only be called from server
        /// </summary>
        public void RemoveOwnership()
        {
            NetworkManager.SpawnManager.RemoveOwnership(this);
        }

        /// <summary>
        /// Changes the owner of the object. Can only be called from server
        /// </summary>
        /// <param name="newOwnerClientId">The new owner clientId</param>
        public void ChangeOwnership(ulong newOwnerClientId)
        {
            NetworkManager.SpawnManager.ChangeOwnership(this, newOwnerClientId);
        }

        internal void InvokeBehaviourOnLostOwnership()
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].OnLostOwnership();
            }
        }

        internal void InvokeBehaviourOnGainedOwnership()
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].OnGainedOwnership();
            }
        }

        internal void ResetNetworkStartInvoked()
        {
            if (ChildNetworkBehaviours != null)
            {
                for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
                {
                    ChildNetworkBehaviours[i].NetworkStartInvoked = false;
                }
            }
        }

        internal void InvokeBehaviourNetworkSpawn(Stream stream)
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                //We check if we are it's NetworkObject owner incase a NetworkObject exists as a child of our NetworkObject
                if (!ChildNetworkBehaviours[i].NetworkStartInvoked)
                {
                    if (!ChildNetworkBehaviours[i].InternalNetworkStartInvoked)
                    {
                        ChildNetworkBehaviours[i].InternalNetworkStart();
                        ChildNetworkBehaviours[i].InternalNetworkStartInvoked = true;
                    }

                    ChildNetworkBehaviours[i].NetworkStart(stream);
                    ChildNetworkBehaviours[i].NetworkStartInvoked = true;
                }
            }
        }

        private List<NetworkBehaviour> m_ChildNetworkBehaviours;

        internal List<NetworkBehaviour> ChildNetworkBehaviours
        {
            get
            {
                if (m_ChildNetworkBehaviours != null)
                {
                    return m_ChildNetworkBehaviours;
                }

                m_ChildNetworkBehaviours = new List<NetworkBehaviour>();
                var networkBehaviours = GetComponentsInChildren<NetworkBehaviour>(true);
                for (int i = 0; i < networkBehaviours.Length; i++)
                {
                    if (networkBehaviours[i].NetworkObject == this)
                    {
                        m_ChildNetworkBehaviours.Add(networkBehaviours[i]);
                    }
                }

                return m_ChildNetworkBehaviours;
            }
        }

        internal void WriteNetworkVariableData(Stream stream, ulong clientId)
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].InitializeVariables();
                NetworkBehaviour.WriteNetworkVariableData(ChildNetworkBehaviours[i].NetworkVariableFields, stream, clientId, NetworkManager);
            }
        }

        internal void SetNetworkVariableData(Stream stream)
        {
            for (int i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                ChildNetworkBehaviours[i].InitializeVariables();
                NetworkBehaviour.SetNetworkVariableData(ChildNetworkBehaviours[i].NetworkVariableFields, stream, NetworkManager);
            }
        }

        internal ushort GetNetworkBehaviourOrderIndex(NetworkBehaviour instance)
        {
            // read the cached index, and verify it first
            if (instance.NetworkBehaviourIdCache < ChildNetworkBehaviours.Count)
            {
                if (ChildNetworkBehaviours[instance.NetworkBehaviourIdCache] == instance)
                {
                    return instance.NetworkBehaviourIdCache;
                }

                // invalid cached id reset
                instance.NetworkBehaviourIdCache = default;
            }

            for (ushort i = 0; i < ChildNetworkBehaviours.Count; i++)
            {
                if (ChildNetworkBehaviours[i] == instance)
                {
                    // cache the id, for next query
                    instance.NetworkBehaviourIdCache = i;
                    return i;
                }
            }

            return 0;
        }

        internal NetworkBehaviour GetNetworkBehaviourAtOrderIndex(ushort index)
        {
            if (index >= ChildNetworkBehaviours.Count)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                {
                    NetworkLog.LogError($"Behaviour index was out of bounds. Did you mess up the order of your {nameof(NetworkBehaviour)}s?");
                }

                return null;
            }

            return ChildNetworkBehaviours[index];
        }

        /// <summary>
        /// Used to serialize a NetworkObjects during scene synchronization that occurs
        /// upon a client being approved or a scene transition.
        /// </summary>
        /// <param name="writer">writer into the outbound stream</param>
        /// <param name="targetClientId">clientid we are targeting</param>
        internal void SerializeSceneObject(NetworkWriter writer, ulong targetClientId)
        {
            writer.WriteBool(IsPlayerObject);
            writer.WriteUInt64Packed(NetworkObjectId);
            writer.WriteUInt64Packed(OwnerClientId);

            NetworkObject parentNetworkObject = null;

            if (!AlwaysReplicateAsRoot && transform.parent != null)
            {
                parentNetworkObject = transform.parent.GetComponent<NetworkObject>();
            }

            if (parentNetworkObject == null)
            {
                // We don't have a parent
                writer.WriteBool(false);
            }
            else
            {
                // We do have a parent
                writer.WriteBool(true);
                // Write the parent's NetworkObjectId to be used for linking back to the child
                writer.WriteUInt64Packed(parentNetworkObject.NetworkObjectId);
            }

            // Write if we are a scene object or not
            writer.WriteBool(IsSceneObject ?? true);

            // Write the hash for this NetworkObject
            writer.WriteUInt32Packed(GlobalObjectIdHash);

            if (IncludeTransformWhenSpawning == null || IncludeTransformWhenSpawning(OwnerClientId))
            {
                // Set the position and rotation data marker to true (i.e. flag to know, when reading from the stream, that position and rotation data follows).
                writer.WriteBool(true);

                // Write position
                writer.WriteSinglePacked(transform.position.x);
                writer.WriteSinglePacked(transform.position.y);
                writer.WriteSinglePacked(transform.position.z);

                // Write rotation
                writer.WriteSinglePacked(transform.rotation.eulerAngles.x);
                writer.WriteSinglePacked(transform.rotation.eulerAngles.y);
                writer.WriteSinglePacked(transform.rotation.eulerAngles.z);
            }
            else
            {
                // Set the position and rotation data marker to false (i.e. flag to know, when reading from the stream, that position and rotation data *was not included*)
                writer.WriteBool(false);
            }

            // Write whether we are including network variable data
            writer.WriteBool(NetworkManager.NetworkConfig.EnableNetworkVariable);

            //If we are including NetworkVariable data
            if (NetworkManager.NetworkConfig.EnableNetworkVariable)
            {
                var buffer = writer.GetStream() as NetworkBuffer;

                // Write placeholder size, NOT as a packed value, initially as zero (i.e. we do not know how much NetworkVariable data will be written yet)
                writer.WriteUInt32(0);

                // Mark our current position before we potentially write any NetworkVariable data
                var positionBeforeNetworkVariableData = buffer.Position;

                // Write network variable data
                WriteNetworkVariableData(buffer, targetClientId);

                // If our current buffer position is greater than our positionBeforeNetworkVariableData then we wrote NetworkVariable data
                // Part 1: This will include the total NetworkVariable data size, if there was NetworkVariable data written, to the stream
                // in order to be able to skip past this entry on the deserialization side in the event this NetworkObject fails to be
                // constructed (See Part 2 below in the DeserializeSceneObject method)
                if (buffer.Position > positionBeforeNetworkVariableData)
                {
                    // Store our current stream buffer position
                    var endOfNetworkVariableData = buffer.Position;

                    // Calculate the total NetworkVariable data size written
                    var networkVariableDataSize = endOfNetworkVariableData - positionBeforeNetworkVariableData;

                    // Move the stream position back to just before we wrote our size (we include the unpacked UInt32 data size placeholder)
                    buffer.Position = positionBeforeNetworkVariableData - sizeof(uint);

                    // Now write the actual data size written into our unpacked UInt32 placeholder position
                    writer.WriteUInt32((uint) (networkVariableDataSize));

                    // Finally, revert the buffer position back to the end of the network variable data written
                    buffer.Position = endOfNetworkVariableData;
                }
            }
        }

        /// <summary>
        /// Used to deserialize a serialized scene object which occurs
        /// when the client is approved or during a scene transition
        /// </summary>
        /// <param name="objectStream">inbound stream</param>
        /// <param name="reader">reader for the stream</param>
        /// <param name="networkManager">NetworkManager instance</param>
        /// <returns>optional to use NetworkObject deserialized</returns>
        internal static NetworkObject DeserializeSceneObject(NetworkBuffer objectStream, NetworkReader reader, NetworkManager networkManager)
        {
            var isPlayerObject = reader.ReadBool();
            var networkId = reader.ReadUInt64Packed();
            var ownerClientId = reader.ReadUInt64Packed();
            var hasParent = reader.ReadBool();
            ulong? parentNetworkId = null;

            if (hasParent)
            {
                parentNetworkId = reader.ReadUInt32Packed();
            }

            var isSceneObject = reader.ReadBool();

            var prefabHash = reader.ReadUInt32Packed();
            Vector3? position = null;
            Quaternion? rotation = null;

            // Check to see if we have position and rotation values that follows
            if (reader.ReadBool())
            {
                position = new Vector3(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                rotation = Quaternion.Euler(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
            }

            //Attempt to create a local NetworkObject
            var networkObject = networkManager.SpawnManager.CreateLocalNetworkObject(isSceneObject, prefabHash, ownerClientId, parentNetworkId, position, rotation);

            // Determine if this NetworkObject has NetworkVariable data to read
            var networkVariableDataIsIncluded = reader.ReadBool();

            if (networkVariableDataIsIncluded)
            {
                // (See Part 1 above in the NetworkObject.SerializeSceneObject method to better understand this)
                // Part 2: This makes sure that if one NetworkObject fails to construct (for whatever reason) then we can "skip past"
                // that specific NetworkObject but continue processing any remaining serialized NetworkObjects as opposed to just
                // throwing an exception and skipping the remaining (if any) NetworkObjects.  This will prevent one misconfigured
                // issue (or more) from breaking the entire loading process.
                var networkVariableDataSize = reader.ReadUInt32();
                if (networkObject == null)
                {
                    // Log the error that the NetworkObject failed to construct
                    Debug.LogError($"Failed to spawn {nameof(NetworkObject)} for Hash {prefabHash}.");

                    // If we failed to load this NetworkObject, then skip past the network variable data
                    objectStream.Position += networkVariableDataSize;

                    // We have nothing left to do here.
                    return null;
                }
            }

            // Spawn the NetworkObject
            networkManager.SpawnManager.SpawnNetworkObjectLocally(networkObject, networkId, isSceneObject, isPlayerObject, ownerClientId, objectStream, false, 0, true, false);

            var bufferQueue = networkManager.BufferManager.ConsumeBuffersForNetworkId(networkId);

            // Apply buffered messages
            if (bufferQueue != null)
            {
                while (bufferQueue.Count > 0)
                {
                    Messaging.Buffering.BufferManager.BufferedMessage message = bufferQueue.Dequeue();
                    networkManager.HandleIncomingData(message.SenderClientId, message.NetworkChannel, new ArraySegment<byte>(message.NetworkBuffer.GetBuffer(), (int) message.NetworkBuffer.Position, (int) message.NetworkBuffer.Length), message.ReceiveTime, false);
                    Messaging.Buffering.BufferManager.RecycleConsumedBufferedMessage(message);
                }
            }

            return networkObject;
        }
    }
}