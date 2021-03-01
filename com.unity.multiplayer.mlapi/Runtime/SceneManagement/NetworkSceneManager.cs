using System.Collections.Generic;
using System;
using System.IO;
using MLAPI.Configuration;
using MLAPI.Exceptions;
using MLAPI.Logging;
using MLAPI.Messaging;
using MLAPI.Serialization.Pooled;
using MLAPI.Spawning;
using UnityEngine;
using UnityEngine.SceneManagement;
using MLAPI.Messaging.Buffering;
using MLAPI.Transports;

namespace MLAPI.SceneManagement
{
    /// <summary>
    /// Main class for managing network scenes
    /// </summary>
    public static class NetworkSceneManager
    {
        /// <summary>
        /// Delegate for when the scene has been switched
        /// </summary>
        public delegate void SceneSwitchedDelegate();

        /// <summary>
        /// Delegate for when a scene switch has been initiated
        /// </summary>
        public delegate void SceneSwitchStartedDelegate(AsyncOperation operation);

        /// <summary>
        /// Event that is invoked when the scene is switched
        /// </summary>
        public static event SceneSwitchedDelegate OnSceneSwitched;

        /// <summary>
        /// Event that is invoked when a local scene switch has started
        /// </summary>
        public static event SceneSwitchStartedDelegate OnSceneSwitchStarted;

        internal static readonly HashSet<string> k_RegisteredSceneNames = new HashSet<string>();
        internal static readonly Dictionary<string, uint> k_SceneNameToIndex = new Dictionary<string, uint>();
        internal static readonly Dictionary<uint, string> k_SceneIndexToString = new Dictionary<uint, string>();
        internal static readonly Dictionary<Guid, SceneSwitchProgress> k_SceneSwitchProgresses = new Dictionary<Guid, SceneSwitchProgress>();

        private static Scene s_LastScene;
        private static string s_NextSceneName;
        private static bool s_IsSwitching = false;
        internal static uint s_CurrentSceneIndex = 0;
        internal static Guid s_CurrentSceneSwitchProgressGuid = new Guid();
        internal static bool s_IsSpawnedObjectsPendingInDontDestroyOnLoad = false;

        internal static void SetCurrentSceneIndex()
        {
            if (!k_SceneNameToIndex.ContainsKey(SceneManager.GetActiveScene().name))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal) NetworkLog.LogWarning("The current scene (" + SceneManager.GetActiveScene().name + ") is not regisered as a network scene.");
                return;
            }

            s_CurrentSceneIndex = k_SceneNameToIndex[SceneManager.GetActiveScene().name];
            s_CurrentActiveSceneIndex = s_CurrentSceneIndex;
        }

        internal static uint s_CurrentActiveSceneIndex { get; private set; } = 0;

        /// <summary>
        /// Adds a scene during runtime.
        /// The index is REQUIRED to be unique AND the same across all instances.
        /// </summary>
        /// <param name="sceneName">Scene name.</param>
        /// <param name="index">Index.</param>
        public static void AddRuntimeSceneName(string sceneName, uint index)
        {
            if (!NetworkManager.Singleton.NetworkConfig.AllowRuntimeSceneChanges)
            {
                throw new NetworkConfigurationException("Cannot change the scene configuration when AllowRuntimeSceneChanges is false");
            }

            k_RegisteredSceneNames.Add(sceneName);
            k_SceneIndexToString.Add(index, sceneName);
            k_SceneNameToIndex.Add(sceneName, index);
        }

        /// <summary>
        /// Switches to a scene with a given name. Can only be called from Server
        /// </summary>
        /// <param name="sceneName">The name of the scene to switch to</param>
        public static SceneSwitchProgress SwitchScene(string sceneName)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                throw new NotServerException("Only server can start a scene switch");
            }
            else if (s_IsSwitching)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal) NetworkLog.LogWarning("Scene switch already in progress");
                return null;
            }
            else if (!k_RegisteredSceneNames.Contains(sceneName))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal) NetworkLog.LogWarning("The scene " + sceneName + " is not registered as a switchable scene.");
                return null;
            }

            NetworkSpawnManager.ServerDestroySpawnedSceneObjects(); //Destroy current scene objects before switching.
            s_IsSwitching = true;
            s_LastScene = SceneManager.GetActiveScene();

            SceneSwitchProgress switchSceneProgress = new SceneSwitchProgress();
            k_SceneSwitchProgresses.Add(switchSceneProgress.Guid, switchSceneProgress);
            s_CurrentSceneSwitchProgressGuid = switchSceneProgress.Guid;

            // Move ALL NetworkObjects to the temp scene
            MoveObjectsToDontDestroyOnLoad();

            s_IsSpawnedObjectsPendingInDontDestroyOnLoad = true;

            // Switch scene
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            s_NextSceneName = sceneName;

            sceneLoad.completed += (AsyncOperation asyncOp2) => { OnSceneLoaded(switchSceneProgress.Guid, null); };

            switchSceneProgress.SetSceneLoadOperation(sceneLoad);

            if (OnSceneSwitchStarted != null)
            {
                OnSceneSwitchStarted(sceneLoad);
            }

            return switchSceneProgress;
        }

        // Called on client
        internal static void OnSceneSwitch(uint sceneIndex, Guid switchSceneGuid, Stream objectStream)
        {
            if (!k_SceneIndexToString.ContainsKey(sceneIndex) || !k_RegisteredSceneNames.Contains(k_SceneIndexToString[sceneIndex]))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal) NetworkLog.LogWarning("Server requested a scene switch to a non registered scene");
                return;
            }

            s_LastScene = SceneManager.GetActiveScene();

            // Move ALL NetworkObjects to the temp scene
            MoveObjectsToDontDestroyOnLoad();

            s_IsSpawnedObjectsPendingInDontDestroyOnLoad = true;

            string sceneName = k_SceneIndexToString[sceneIndex];

            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            s_NextSceneName = sceneName;

            sceneLoad.completed += (AsyncOperation asyncOp2) => { OnSceneLoaded(switchSceneGuid, objectStream); };

            if (OnSceneSwitchStarted != null)
            {
                OnSceneSwitchStarted(sceneLoad);
            }
        }

        internal static void OnFirstSceneSwitchSync(uint sceneIndex, Guid switchSceneGuid)
        {
            if (!k_SceneIndexToString.ContainsKey(sceneIndex) || !k_RegisteredSceneNames.Contains(k_SceneIndexToString[sceneIndex]))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal) NetworkLog.LogWarning("Server requested a scene switch to a non registered scene");
                return;
            }

            if (SceneManager.GetActiveScene().name == k_SceneIndexToString[sceneIndex])
            {
                return; //This scene is already loaded. This usually happends at first load
            }

            s_LastScene = SceneManager.GetActiveScene();
            string sceneName = k_SceneIndexToString[sceneIndex];
            s_NextSceneName = sceneName;
            s_CurrentActiveSceneIndex = k_SceneNameToIndex[sceneName];

            s_IsSpawnedObjectsPendingInDontDestroyOnLoad = true;
            SceneManager.LoadScene(sceneName);

            using (var stream = PooledNetworkStream.Get())
            {
                using (var writer = PooledNetworkWriter.Get(stream))
                {
                    writer.WriteByteArray(switchSceneGuid.ToByteArray());
                    InternalMessageSender.Send(NetworkManager.Singleton.ServerClientId, NetworkConstants.k_CLIENT_SWITCH_SCENE_COMPLETED, NetworkChannel.Internal, stream);
                }
            }

            s_IsSwitching = false;
        }

        private static void OnSceneLoaded(Guid switchSceneGuid, Stream objectStream)
        {
            s_CurrentActiveSceneIndex = k_SceneNameToIndex[s_NextSceneName];
            Scene nextScene = SceneManager.GetSceneByName(s_NextSceneName);
            SceneManager.SetActiveScene(nextScene);

            // Move all objects to the new scene
            MoveObjectsToScene(nextScene);

            s_IsSpawnedObjectsPendingInDontDestroyOnLoad = false;

            s_CurrentSceneIndex = s_CurrentActiveSceneIndex;

            if (NetworkManager.Singleton.IsServer)
            {
                OnSceneUnloadServer(switchSceneGuid);
            }
            else
            {
                OnSceneUnloadClient(switchSceneGuid, objectStream);
            }
        }

        private static void OnSceneUnloadServer(Guid switchSceneGuid)
        {
            // Justification: Rare alloc, could(should?) reuse
            var newSceneObjects = new List<NetworkObject>();
            {
                var networkObjects = MonoBehaviour.FindObjectsOfType<NetworkObject>();
                for (int i = 0; i < networkObjects.Length; i++)
                {
                    if (networkObjects[i].IsSceneObject == null)
                    {
                        NetworkSpawnManager.SpawnNetworkObjectLocally(networkObjects[i], NetworkSpawnManager.GetNetworkObjectId(), true, false, null, null, false, 0, false, true);
                        newSceneObjects.Add(networkObjects[i]);
                    }
                }
            }

            for (int j = 0; j < NetworkManager.Singleton.ConnectedClientsList.Count; j++)
            {
                if (NetworkManager.Singleton.ConnectedClientsList[j].ClientId != NetworkManager.Singleton.ServerClientId)
                {
                    using (var stream = PooledNetworkStream.Get())
                    {
                        using (var writer = PooledNetworkWriter.Get(stream))
                        {
                            writer.WriteUInt32Packed(s_CurrentActiveSceneIndex);
                            writer.WriteByteArray(switchSceneGuid.ToByteArray());

                            uint sceneObjectsToSpawn = 0;
                            for (int i = 0; i < newSceneObjects.Count; i++)
                            {
                                if (newSceneObjects[i].m_Observers.Contains(NetworkManager.Singleton.ConnectedClientsList[j].ClientId))
                                    sceneObjectsToSpawn++;
                            }

                            writer.WriteUInt32Packed(sceneObjectsToSpawn);

                            for (int i = 0; i < newSceneObjects.Count; i++)
                            {
                                if (newSceneObjects[i].m_Observers.Contains(NetworkManager.Singleton.ConnectedClientsList[j].ClientId))
                                {
                                    writer.WriteBool(newSceneObjects[i].IsPlayerObject);
                                    writer.WriteUInt64Packed(newSceneObjects[i].NetworkId);
                                    writer.WriteUInt64Packed(newSceneObjects[i].OwnerClientId);

                                    NetworkObject parent = null;

                                    if (!newSceneObjects[i].AlwaysReplicateAsRoot && newSceneObjects[i].transform.parent != null)
                                    {
                                        parent = newSceneObjects[i].transform.parent.GetComponent<NetworkObject>();
                                    }

                                    if (parent == null)
                                    {
                                        writer.WriteBool(false);
                                    }
                                    else
                                    {
                                        writer.WriteBool(true);
                                        writer.WriteUInt64Packed(parent.NetworkId);
                                    }

                                    if (!NetworkManager.Singleton.NetworkConfig.EnableSceneManagement || NetworkManager.Singleton.NetworkConfig.UsePrefabSync)
                                    {
                                        writer.WriteUInt64Packed(newSceneObjects[i].PrefabHash);

                                        writer.WriteSinglePacked(newSceneObjects[i].transform.position.x);
                                        writer.WriteSinglePacked(newSceneObjects[i].transform.position.y);
                                        writer.WriteSinglePacked(newSceneObjects[i].transform.position.z);

                                        writer.WriteSinglePacked(newSceneObjects[i].transform.rotation.eulerAngles.x);
                                        writer.WriteSinglePacked(newSceneObjects[i].transform.rotation.eulerAngles.y);
                                        writer.WriteSinglePacked(newSceneObjects[i].transform.rotation.eulerAngles.z);
                                    }
                                    else
                                    {
                                        writer.WriteUInt64Packed(newSceneObjects[i].NetworkInstanceId);
                                    }

                                    if (NetworkManager.Singleton.NetworkConfig.EnableNetworkVariable)
                                    {
                                        newSceneObjects[i].WriteNetworkVariableData(stream, NetworkManager.Singleton.ConnectedClientsList[j].ClientId);
                                    }
                                }
                            }
                        }

                        InternalMessageSender.Send(NetworkManager.Singleton.ConnectedClientsList[j].ClientId, NetworkConstants.k_SWITCH_SCENE, NetworkChannel.Internal, stream);
                    }
                }
            }

            //Tell server that scene load is completed
            if (NetworkManager.Singleton.IsHost)
            {
                OnClientSwitchSceneCompleted(NetworkManager.Singleton.LocalClientId, switchSceneGuid);
            }

            s_IsSwitching = false;

            if (OnSceneSwitched != null)
            {
                OnSceneSwitched();
            }
        }

        private static void OnSceneUnloadClient(Guid switchSceneGuid, Stream objectStream)
        {
            if (!NetworkManager.Singleton.NetworkConfig.EnableSceneManagement || NetworkManager.Singleton.NetworkConfig.UsePrefabSync)
            {
                NetworkSpawnManager.DestroySceneObjects();

                using (var reader = PooledNetworkReader.Get(objectStream))
                {
                    uint newObjectsCount = reader.ReadUInt32Packed();

                    for (int i = 0; i < newObjectsCount; i++)
                    {
                        bool isPlayerObject = reader.ReadBool();
                        ulong networkId = reader.ReadUInt64Packed();
                        ulong owner = reader.ReadUInt64Packed();
                        bool hasParent = reader.ReadBool();
                        ulong? parentNetworkId = null;

                        if (hasParent)
                        {
                            parentNetworkId = reader.ReadUInt64Packed();
                        }

                        ulong prefabHash = reader.ReadUInt64Packed();

                        Vector3? position = null;
                        Quaternion? rotation = null;
                        if (reader.ReadBool())
                        {
                            position = new Vector3(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                            rotation = Quaternion.Euler(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                        }

                        NetworkObject networkObject = NetworkSpawnManager.CreateLocalNetworkObject(false, 0, prefabHash, parentNetworkId, position, rotation);
                        NetworkSpawnManager.SpawnNetworkObjectLocally(networkObject, networkId, true, isPlayerObject, owner, objectStream, false, 0, true, false);

                        Queue<BufferManager.BufferedMessage> bufferQueue = BufferManager.ConsumeBuffersForNetworkId(networkId);

                        // Apply buffered messages
                        if (bufferQueue != null)
                        {
                            while (bufferQueue.Count > 0)
                            {
                                BufferManager.BufferedMessage message = bufferQueue.Dequeue();

                                NetworkManager.Singleton.HandleIncomingData(message.Sender, message.NetworkChannel, new ArraySegment<byte>(message.Payload.GetBuffer(), (int)message.Payload.Position, (int)message.Payload.Length), message.ReceiveTime, false);

                                BufferManager.RecycleConsumedBufferedMessage(message);
                            }
                        }
                    }
                }
            }
            else
            {
                var networkObjects = MonoBehaviour.FindObjectsOfType<NetworkObject>();
                NetworkSpawnManager.ClientCollectSoftSyncSceneObjectSweep(networkObjects);

                using (var reader = PooledNetworkReader.Get(objectStream))
                {
                    uint newObjectsCount = reader.ReadUInt32Packed();

                    for (int i = 0; i < newObjectsCount; i++)
                    {
                        bool isPlayerObject = reader.ReadBool();
                        ulong networkId = reader.ReadUInt64Packed();
                        ulong owner = reader.ReadUInt64Packed();
                        bool hasParent = reader.ReadBool();
                        ulong? parentNetworkId = null;

                        if (hasParent)
                        {
                            parentNetworkId = reader.ReadUInt64Packed();
                        }

                        ulong instanceId = reader.ReadUInt64Packed();

                        NetworkObject networkObject = NetworkSpawnManager.CreateLocalNetworkObject(true, instanceId, 0, parentNetworkId, null, null);
                        NetworkSpawnManager.SpawnNetworkObjectLocally(networkObject, networkId, true, isPlayerObject, owner, objectStream, false, 0, true, false);

                        Queue<BufferManager.BufferedMessage> bufferQueue = BufferManager.ConsumeBuffersForNetworkId(networkId);

                        // Apply buffered messages
                        if (bufferQueue != null)
                        {
                            while (bufferQueue.Count > 0)
                            {
                                BufferManager.BufferedMessage message = bufferQueue.Dequeue();

                                NetworkManager.Singleton.HandleIncomingData(message.Sender, message.NetworkChannel, new ArraySegment<byte>(message.Payload.GetBuffer(), (int)message.Payload.Position, (int)message.Payload.Length), message.ReceiveTime, false);

                                BufferManager.RecycleConsumedBufferedMessage(message);
                            }
                        }
                    }
                }
            }

            using (var stream = PooledNetworkStream.Get())
            {
                using (var writer = PooledNetworkWriter.Get(stream))
                {
                    writer.WriteByteArray(switchSceneGuid.ToByteArray());
                    InternalMessageSender.Send(NetworkManager.Singleton.ServerClientId, NetworkConstants.k_CLIENT_SWITCH_SCENE_COMPLETED, NetworkChannel.Internal, stream);
                }
            }

            s_IsSwitching = false;

            OnSceneSwitched?.Invoke();
        }

        internal static bool HasSceneMismatch(uint sceneIndex)
        {
            return SceneManager.GetActiveScene().name != k_SceneIndexToString[sceneIndex];
        }

        // Called on server
        internal static void OnClientSwitchSceneCompleted(ulong clientId, Guid switchSceneGuid)
        {
            if (switchSceneGuid == Guid.Empty)
            {
                //If Guid is empty it means the client has loaded the start scene of the server and the server would never have a switchSceneProgresses created for the start scene.
                return;
            }

            if (!k_SceneSwitchProgresses.ContainsKey(switchSceneGuid))
            {
                return;
            }

            k_SceneSwitchProgresses[switchSceneGuid].AddClientAsDone(clientId);
        }


        internal static void RemoveClientFromSceneSwitchProgresses(ulong clientId)
        {
            foreach (SceneSwitchProgress switchSceneProgress in k_SceneSwitchProgresses.Values)
            {
                switchSceneProgress.RemoveClientAsDone(clientId);
            }
        }

        private static void MoveObjectsToDontDestroyOnLoad()
        {
            // Move ALL NetworkObjects to the temp scene
            HashSet<NetworkObject> objectsToKeep = NetworkSpawnManager.SpawnedObjectsList;

            foreach (var sobj in objectsToKeep)
            {
                //In case an object has been set as a child of another object it has to be unchilded in order to be moved from one scene to another.
                if (sobj.gameObject.transform.parent != null)
                {
                    sobj.gameObject.transform.parent = null;
                }

                MonoBehaviour.DontDestroyOnLoad(sobj.gameObject);
            }
        }

        private static void MoveObjectsToScene(Scene scene)
        {
            // Move ALL NetworkObjects to the temp scene
            HashSet<NetworkObject> objectsToKeep = NetworkSpawnManager.SpawnedObjectsList;

            foreach (var sobj in objectsToKeep)
            {
                //In case an object has been set as a child of another object it has to be unchilded in order to be moved from one scene to another.
                if (sobj.gameObject.transform.parent != null)
                {
                    sobj.gameObject.transform.parent = null;
                }

                SceneManager.MoveGameObjectToScene(sobj.gameObject, scene);
            }
        }
    }
}