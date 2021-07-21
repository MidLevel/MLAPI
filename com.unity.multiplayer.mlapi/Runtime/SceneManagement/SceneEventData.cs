using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using MLAPI.Serialization.Pooled;
using UnityEngine;
using UnityEngine.SceneManagement;
using MLAPI.Serialization;


namespace MLAPI.SceneManagement
{
    [Serializable]
    public class SceneEventData : IDisposable
    {
        public enum SceneEventTypes
        {
            Event_Load,                 //Server to client load additive scene
            Event_Unload,               //Server to client unload additive scene
            Event_SetActive,            //Server to client make scene active
            Event_Sync,                 //Server to client late join approval synchronization
            Event_ReSync,               //Server to client update of objects that were destroyed during sync
            Event_Load_Complete,        //Client to server
            Event_Unload_Complete,      //Client to server
            Event_SetActiveComplete,    //Client to server
            Event_Sync_Complete,        //Client to server
        }

        public SceneEventTypes SceneEventType;
        public LoadSceneMode LoadSceneMode;
        public Guid SwitchSceneGuid;

        public int SceneIndex;
        public ulong TargetClientId;

        private Dictionary<int, List<NetworkObject>> m_SceneNetworkObjects;
        private Dictionary<int, long> m_SceneNetworkObjectDataOffsets;

        /// <summary>
        /// Client or Server Side:
        /// Client side: Generates a list of all NetworkObjects by their NetworkObjectId that was spawned during th synchronization process
        /// Server side: Compares list from client to make sure client didn't drop a message about a NetworkObject being despawned while it
        /// was synchronizing (if so server will send another message back to the client informing the client of NetworkObjects to remove)
        /// spawned during an initial synchronization.
        /// </summary>
        private List<NetworkObject> m_NetworkObjectsSync = new List<NetworkObject>();

        /// <summary>
        /// Server Side Re-Synchronization:
        /// If there happens to be NetworkObjects in the final Event_Sync_Complete message that are no longer spawned,
        /// the server will compile a list and send back an Event_ReSync message to the client.
        /// </summary>
        private List<ulong> m_NetworkObjectsToBeRemoved = new List<ulong>();

        internal PooledNetworkBuffer InternalBuffer;

        private NetworkManager m_NetworkManager;

        /// <summary>
        /// Client Side:
        /// Gets the next scene index to be loaded for approval and/or late joining
        /// </summary>
        /// <returns></returns>
        public int GetNextSceneSynchronizationIndex()
        {
            if (m_SceneNetworkObjectDataOffsets.ContainsKey(SceneIndex))
            {
                return SceneIndex;
            }
            return m_SceneNetworkObjectDataOffsets.First().Key;
        }

        /// <summary>
        /// Client Side:
        /// Determines if all scenes have been processed during the synchronization process
        /// </summary>
        /// <returns>true/false</returns>
        public bool IsDoneWithSynchronization()
        {
            return (m_SceneNetworkObjectDataOffsets.Count == 0);
        }

        /// <summary>
        /// Server Side:
        /// Called just before the synchronization process
        /// </summary>
        public void InitializeForSynch()
        {
            if (m_SceneNetworkObjects == null)
            {
                m_SceneNetworkObjects = new Dictionary<int, List<NetworkObject>>();
            }
            else
            {
                m_SceneNetworkObjects.Clear();
            }
        }

        /// <summary>
        /// Server Side:
        /// Used during the synchronization process to associate NetworkObjects with scenes
        /// </summary>
        /// <param name="sceneIndex"></param>
        /// <param name="networkObject"></param>
        public void AddNetworkObjectForSynch(int sceneIndex, NetworkObject networkObject)
        {
            if (!m_SceneNetworkObjects.ContainsKey(sceneIndex))
            {
                m_SceneNetworkObjects.Add(sceneIndex, new List<NetworkObject>());
            }

            m_SceneNetworkObjects[sceneIndex].Add(networkObject);
        }

        /// <summary>
        /// Determines if the scene event type was intended for the client ( or server )
        /// </summary>
        /// <returns>true (client should handle this message) false (server should handle this message)</returns>
        public bool IsSceneEventClientSide()
        {
            switch (SceneEventType)
            {
                case SceneEventTypes.Event_Load:
                case SceneEventTypes.Event_Unload:
                case SceneEventTypes.Event_Sync:
                case SceneEventTypes.Event_ReSync:
                    {
                        return true;
                    }
            }
            return false;
        }

        /// <summary>
        /// Sorts the NetworkObjects to assure proper order of operations for custom Network Prefab handlers
        /// that implement the INetworkPrefabInstanceHandler interface.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        private int SortNetworkObjects(NetworkObject first, NetworkObject second)
        {
            var doesFirstHaveHandler = m_NetworkManager.PrefabHandler.ContainsHandler(first);
            var doesSecondHaveHandler = m_NetworkManager.PrefabHandler.ContainsHandler(second);
            if (doesFirstHaveHandler != doesSecondHaveHandler)
            {
                if (doesFirstHaveHandler)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }
            return 0;
        }

        /// <summary>
        /// Serializes data based on the SceneEvent type.
        /// </summary>
        /// <param name="writer"></param>
        public void OnWrite(NetworkWriter writer)
        {
            writer.WriteByte((byte)SceneEventType);

            writer.WriteByte((byte)LoadSceneMode);

            if (SceneEventType != SceneEventTypes.Event_Sync)
            {
                writer.WriteByteArray(SwitchSceneGuid.ToByteArray());
            }

            writer.WriteInt32Packed(SceneIndex);

            if (SceneEventType == SceneEventTypes.Event_Sync)
            {
                writer.WriteInt32Packed(m_SceneNetworkObjects.Count());

                if (m_SceneNetworkObjects.Count() > 0)
                {
                    string msg = "Scene Associated NetworkObjects Write:\n";
                    foreach (var keypair in m_SceneNetworkObjects)
                    {
                        writer.WriteInt32Packed(keypair.Key);
                        msg += $"Scene ID [{keypair.Key}] NumNetworkObjects:[{keypair.Value.Count}]\n";
                        writer.WriteInt32Packed(keypair.Value.Count);
                        var positionStart = writer.GetStream().Position;
                        // Size Place Holder (For offset purposes, needs to not be packed)
                        writer.WriteUInt32(0);
                        var totalBytes = 0;

                        // Sort NetworkObjects so any NetworkObjects with a PrefabHandler are sorted to be after all other NetworkObjects
                        // This will assure the INetworkPrefabInstanceHandler instance is registered before we try to spawn the NetworkObjects
                        // on the client side.
                        keypair.Value.Sort(SortNetworkObjects);

                        foreach (var networkObject in keypair.Value)
                        {
                            var noStart = writer.GetStream().Position;

                            networkObject.SerializeSceneObject(writer, TargetClientId);
                            var noStop = writer.GetStream().Position;
                            totalBytes += (int)(noStop - noStart);
                            msg += $"Included: {networkObject.name} Bytes: {totalBytes} \n";
                        }
                        var positionEnd = writer.GetStream().Position;
                        var bytesWritten = (uint)(positionEnd - (positionStart + sizeof(uint)));
                        writer.GetStream().Position = positionStart;
                        // Write the total size written to the stream by NetworkObjects being serialized
                        writer.WriteUInt32(bytesWritten);
                        writer.GetStream().Position = positionEnd;
                        msg += $"Wrote [{bytesWritten}] bytes of NetworkObject data. Verification: {totalBytes}\n";
                    }

                    Debug.Log(msg);
                }
            }

            if (SceneEventType == SceneEventTypes.Event_Sync_Complete)
            {
                WriteClientSynchronizationResults(writer);
            }

            if (SceneEventType == SceneEventTypes.Event_ReSync)
            {
                WriteClientReSynchronizationData(writer);
            }
        }

        /// <summary>
        /// Deserialize data based on the SceneEvent type.
        /// </summary>
        /// <param name="reader"></param>
        public void OnRead(NetworkReader reader)
        {
            var sceneEventTypeValue = reader.ReadByte();

            if (Enum.IsDefined(typeof(SceneEventTypes), sceneEventTypeValue))
            {
                SceneEventType = (SceneEventTypes)sceneEventTypeValue;
            }
            else
            {
                Debug.LogError($"Serialization Read Error: {nameof(SceneEventType)} vale {sceneEventTypeValue} is not within the range of the defined {nameof(SceneEventTypes)} enumerator!");
                // NSS TODO: Add to proposal's MTT discussion topics: Should we assert here?
            }

            var loadSceneModeValue = reader.ReadByte();

            if (Enum.IsDefined(typeof(LoadSceneMode), loadSceneModeValue))
            {
                LoadSceneMode = (LoadSceneMode)loadSceneModeValue;
            }
            else
            {
                Debug.LogError($"Serialization Read Error: {nameof(LoadSceneMode)} vale {loadSceneModeValue} is not within the range of the defined {nameof(LoadSceneMode)} enumerator!");
                // NSS TODO: Add to proposal's MTT discussion topics: Should we assert here?
            }

            if (SceneEventType != SceneEventTypes.Event_Sync)
            {
                SwitchSceneGuid = new Guid(reader.ReadByteArray());
            }

            SceneIndex = reader.ReadInt32Packed();

            if (SceneEventType == SceneEventTypes.Event_Sync)
            {
                m_NetworkObjectsSync.Clear();
                var keyPairCount = reader.ReadInt32Packed();

                if (m_SceneNetworkObjectDataOffsets == null)
                {
                    m_SceneNetworkObjectDataOffsets = new Dictionary<int, long>();
                }

                if (keyPairCount > 0)
                {
                    m_SceneNetworkObjectDataOffsets.Clear();

                    InternalBuffer.Position = 0;

                    using (var writer = PooledNetworkWriter.Get(InternalBuffer))
                    {
                        for (int i = 0; i < keyPairCount; i++)
                        {
                            var key = reader.ReadInt32Packed();
                            var count = reader.ReadInt32Packed();
                            // how many bytes to read for this scene set
                            var bytesToRead = (ulong)reader.ReadUInt32();
                            // We store off the current position of the stream as it pertains to the scene relative NetworkObjects
                            m_SceneNetworkObjectDataOffsets.Add(key, InternalBuffer.Position);
                            writer.WriteInt32Packed(count);
                            writer.ReadAndWrite(reader, (long)bytesToRead);
                        }
                    }
                }
            }

            if (SceneEventType == SceneEventTypes.Event_Sync_Complete)
            {
                CheckClientSynchronizationResults(reader);
            }

            if (SceneEventType == SceneEventTypes.Event_ReSync)
            {
                ReadClientReSynchronizationData(reader);
            }
        }

        /// <summary>
        /// Client Side:
        /// If there happens to be NetworkObjects in the final Event_Sync_Complete message that are no longer spawned,
        /// the server will compile a list and send back an Event_ReSync message to the client.
        /// </summary>
        /// <param name="reader"></param>
        internal void ReadClientReSynchronizationData(NetworkReader reader)
        {
            var networkObjectsToRemove = reader.ReadULongArrayPacked();

            if(networkObjectsToRemove.Length > 0)
            {
                Debug.Log($"Client {NetworkManager.Singleton.LocalClientId} is being re-synchronized.");
                var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>();
                var networkObjectIdToNetworkObject = new Dictionary<ulong, NetworkObject>();
                foreach(var networkObject in networkObjects)
                {
                    if(!networkObjectIdToNetworkObject.ContainsKey(networkObject.NetworkObjectId))
                    {
                        networkObjectIdToNetworkObject.Add(networkObject.NetworkObjectId, networkObject);
                    }
                }

                foreach(var networkObjectId in networkObjectsToRemove)
                {
                    if (networkObjectIdToNetworkObject.ContainsKey(networkObjectId))
                    {
                        var networkObject = networkObjectIdToNetworkObject[networkObjectId];
                        networkObjectIdToNetworkObject.Remove(networkObjectId);

                        networkObject.IsSpawned = false;
                        if (m_NetworkManager.PrefabHandler.ContainsHandler(networkObject))
                        {
                            Debug.Log($"NetworkObjectId {networkObjectId} marked as not spawned and is being destroyed via prefab handler.");
                            NetworkManager.Singleton.PrefabHandler.HandleNetworkPrefabDestroy(networkObject);
                        }
                        else
                        {
                            Debug.Log($"NetworkObjectId {networkObjectId} marked as not spawned and is being destroyed immediately.");
                            UnityEngine.Object.DestroyImmediate(networkObject.gameObject);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Server Side:
        /// If there happens to be NetworkObjects in the final Event_Sync_Complete message that are no longer spawned,
        /// the server will compile a list and send back an Event_ReSync message to the client.
        /// </summary>
        /// <param name="writer"></param>
        internal void WriteClientReSynchronizationData(NetworkWriter writer)
        {
            //Write how many objects need to be removed
            writer.WriteULongArrayPacked(m_NetworkObjectsToBeRemoved.ToArray());
        }

        /// <summary>
        /// Server Side:
        /// Determines if the client needs to be slightly re-synchronized if during the deserialization
        /// process the server finds NetworkObjects that the client still thinks are spawned.
        /// </summary>
        /// <returns></returns>
        public bool ClientNeedsReSynchronization()
        {
            return (m_NetworkObjectsToBeRemoved.Count > 0);
        }

        /// <summary>
        /// Server Side:
        /// Determines if the client needs to be re-synchronized if during the deserialization
        /// process the server finds NetworkObjects that the client still thinks are spawned but
        /// have since been despawned.
        /// </summary>
        /// <param name="reader"></param>
        internal void CheckClientSynchronizationResults(NetworkReader reader)
        {
            m_NetworkObjectsToBeRemoved.Clear();
            var networkObjectIdCount = reader.ReadUInt32Packed();
            for(int i = 0; i < networkObjectIdCount; i++)
            {
                var networkObjectId = (ulong)reader.ReadUInt32Packed();
                if(!m_NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(networkObjectId))
                {
                    m_NetworkObjectsToBeRemoved.Add(networkObjectId);
                }
            }
        }

        /// <summary>
        /// Client Side:
        /// During the deserialization process of the servers Event_Sync, the client builds a list of
        /// all NetworkObjectIds that were spawned.  Upon responding to the server with the Event_Sync_Complete
        /// this list is included for the server to review to determine if the client needs a minor resynchronization
        /// of NetworkObjects that might have been despawned while the client was processing the Event_Sync.
        /// </summary>
        /// <param name="writer"></param>
        internal void WriteClientSynchronizationResults(NetworkWriter writer)
        {
            //Write how many objects were spawned
            writer.WriteUInt32Packed((uint)m_NetworkObjectsSync.Count);
            foreach (var networkObject in m_NetworkObjectsSync)
            {
                writer.WriteUInt32Packed((uint)networkObject.NetworkObjectId);
            }
        }

        /// <summary>
        /// Client Side:
        /// During the processing of a server sent Event_Sync, this method will be called for each scene once
        /// it is finished loading.  The client will also build a list of NetworkObjects that it spawned during
        /// this process which will be used as part of the Event_Sync_Complete response.
        /// </summary>
        /// <param name="sceneId"></param>
        /// <param name="networkManager"></param>
        public void SynchronizeSceneNetworkObjects(int sceneId, NetworkManager networkManager)
        {
            if (m_SceneNetworkObjectDataOffsets.ContainsKey(sceneId))
            {
                // Point to the appropriate offset
                InternalBuffer.Position = m_SceneNetworkObjectDataOffsets[sceneId];

                using (var reader = PooledNetworkReader.Get(InternalBuffer))
                {
                    // Process all NetworkObjects for this scene
                    var newObjectsCount = reader.ReadInt32Packed();

                    for (int i = 0; i < newObjectsCount; i++)
                    {
                        var spawnedNetworkObject = NetworkObject.DeserializeSceneObject(InternalBuffer, reader, networkManager);
                        if(!m_NetworkObjectsSync.Contains(spawnedNetworkObject))
                        {
                            m_NetworkObjectsSync.Add(spawnedNetworkObject);
                        }
                    }
                }

                // Remove each entry after it is processed so we know when we are done
                m_SceneNetworkObjectDataOffsets.Remove(sceneId);
            }
        }

        /// <summary>
        /// Used to store data during an asynchronous scene loading event
        /// </summary>
        /// <param name="stream"></param>
        internal void CopyUnreadFromStream(Stream stream)
        {
            InternalBuffer.Position = 0;
            InternalBuffer.CopyUnreadFrom(stream);
            InternalBuffer.Position = 0;
        }

        /// <summary>
        /// Used to release the pooled network buffer
        /// </summary>
        public void Dispose()
        {
            if (InternalBuffer != null)
            {
                NetworkBufferPool.PutBackInPool(InternalBuffer);
                InternalBuffer = null;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public SceneEventData(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager;
            InternalBuffer = NetworkBufferPool.GetBuffer();
        }
    }
}
