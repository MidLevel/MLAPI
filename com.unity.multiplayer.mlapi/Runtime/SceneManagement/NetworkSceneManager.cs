using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using MLAPI.Configuration;
using MLAPI.Exceptions;
using MLAPI.Logging;
using MLAPI.Serialization.Pooled;
using UnityEngine;
using UnityEngine.SceneManagement;
using MLAPI.Serialization;
using MLAPI.Transports;

namespace MLAPI.SceneManagement
{
    /// <summary>
    /// Main class for managing network scenes
    /// </summary>
    public class NetworkSceneManager
    {
        /// <summary>
        /// Delegate for when the scene has been switched
        /// </summary>
        public delegate void SceneSwitchedDelegate();

        /// <summary>
        /// Delegate for when a scene switch has been initiated
        /// </summary>
        public delegate void SceneSwitchStartedDelegate(AsyncOperation operation);

        public delegate void AdditiveSceneEventDelegate(AsyncOperation operation, string sceneName, bool isLoading);

        /// <summary>
        /// Delegate for when a client has reported to the server that it has completed scene transition
        /// <see cref='OnNotifyServerClientLoadedScene'/>
        /// </summary>
        public delegate void NotifyServerClientLoadedSceneDelegate(SceneSwitchProgress progress, ulong clientId);

        /// <summary>
        /// Delegate for when all clients have reported to the server that they have completed scene transition or timed out
        /// <see cref='OnNotifyServerAllClientsLoadedScene'/>
        /// </summary>
        public delegate void NotifyServerAllClientsLoadedSceneDelegate(SceneSwitchProgress progress, bool timedOut);

        /// <summary>
        /// Delegate for when the clients get notified by the server that all clients have completed their scene transitions.
        /// <see cref='OnNotifyClientAllClientsLoadedScene'/>
        /// </summary>
        public delegate void NotifyClientAllClientsLoadedSceneDelegate(ulong[] clientIds, ulong[] timedOutClientIds);

        /// <summary>
        /// Event that is invoked when the scene is switched
        /// </summary>
        public event SceneSwitchedDelegate OnSceneSwitched;

        public event AdditiveSceneEventDelegate OnAdditiveSceneEvent;

        /// <summary>
        /// Event that is invoked when a local scene switch has started
        /// </summary>
        public event SceneSwitchStartedDelegate OnSceneSwitchStarted;

        /// <summary>
        /// Event that is invoked on the server when a client completes scene transition
        /// </summary>
        public event NotifyServerClientLoadedSceneDelegate OnNotifyServerClientLoadedScene;

        /// <summary>
        /// Event that is invoked on the server when all clients have reported that they have completed scene transition
        /// </summary>
        public event NotifyServerAllClientsLoadedSceneDelegate OnNotifyServerAllClientsLoadedScene;

        /// <summary>
        /// Event that is invoked on the clients after all clients have successfully completed scene transition or timed out.
        /// <remarks>This event happens after <see cref="OnNotifyServerAllClientsLoadedScene"/> fires on the server and the <see cref="NetworkConstants.ALL_CLIENTS_LOADED_SCENE"/> message is sent to the clients.
        /// It relies on MessageSender, which doesn't send events from the server to itself (which is the case for a Host client).</remarks>
        /// </summary>
        public event NotifyClientAllClientsLoadedSceneDelegate OnNotifyClientAllClientsLoadedScene;

        internal readonly HashSet<string> RegisteredSceneNames = new HashSet<string>();
        internal readonly Dictionary<string, uint> SceneNameToIndex = new Dictionary<string, uint>();
        internal readonly Dictionary<uint, string> SceneIndexToString = new Dictionary<uint, string>();
        internal readonly Dictionary<Guid, SceneSwitchProgress> SceneSwitchProgresses = new Dictionary<Guid, SceneSwitchProgress>();
        internal readonly Dictionary<uint, NetworkObject> ScenePlacedObjects = new Dictionary<uint, NetworkObject>();

        // Used for observed object synchronization
        private readonly List<NetworkObject> m_ObservedObjects = new List<NetworkObject>();

        private List<string> m_ScenesLoaded = new List<string>();

        private static bool s_IsSceneEventActive = false;
        internal static bool IsSpawnedObjectsPendingInDontDestroyOnLoad = false;
        internal static bool IsRunningUnitTest = false;

        //Client and Server: used for all scene event processing exception for client synchronization
        internal SceneEventData SceneEventData;

        //Server Side: Used specifically for scene synchronization (late joining and newly approved client connections)
        internal SceneEventData ClientSynchEventData;

        private NetworkManager m_NetworkManager { get; }

        internal NetworkSceneManager(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager;
            SceneEventData = new SceneEventData(networkManager);
            ClientSynchEventData = new SceneEventData(networkManager);
        }

        /// <summary>
        /// Returns the MLAPI scene index from a scene
        /// </summary>
        /// <param name="scene"></param>
        /// <returns>MLAPI Scene Index</returns>
        internal uint GetMLAPISceneIndexFromScene(Scene scene)
        {
            uint index = 0;
            if (!SceneNameToIndex.TryGetValue(scene.name, out index))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"The current scene ({scene.name}) is not registered as a network scene.");
                }
                //MaxValue denotes an error
                return uint.MaxValue;
            }
            return index;
        }

        /// <summary>
        /// Returns the scene name from the MLAPI scene index
        /// Note: This is not the same as the Build Settings Scenes in Build index
        /// </summary>
        /// <param name="sceneIndex">MLAPI Scene Index</param>
        /// <returns>scene name</returns>
        internal string GetSceneNameFromMLAPISceneIndex(uint sceneIndex)
        {
            var sceneName = string.Empty;
            if (!SceneIndexToString.TryGetValue(sceneIndex, out sceneName))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"The current scene index ({sceneIndex}) is not registered as a network scene.");
                }
            }
            return sceneName;
        }

        /// <summary>
        /// Adds a scene during runtime.
        /// The index is REQUIRED to be unique AND the same across all instances.
        /// </summary>
        /// <param name="sceneName">Scene name.</param>
        /// <param name="index">Index.</param>
        public void AddRuntimeSceneName(string sceneName, uint index)
        {
            if (!m_NetworkManager.NetworkConfig.AllowRuntimeSceneChanges)
            {
                throw new NetworkConfigurationException($"Cannot change the scene configuration when {nameof(NetworkConfig.AllowRuntimeSceneChanges)} is false");
            }

            RegisteredSceneNames.Add(sceneName);
            SceneIndexToString.Add(index, sceneName);
            SceneNameToIndex.Add(sceneName, index);
        }

        /// <summary>
        /// Validates the new scene event request by the server-side code.
        /// This also initializes some commonly shared values as well as switchSceneProgress
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns>SceneSwitchProgress (if null it failed)</returns>
        private SceneSwitchProgress ValidateServerSceneEvent(string sceneName, bool isUnloading = false)
        {
            if (!m_NetworkManager.IsServer)
            {
                throw new NotServerException("Only server can start a scene switch");
            }

            if (!m_NetworkManager.NetworkConfig.EnableSceneManagement)
            {
                //Log message about enabling SceneManagement
                throw new Exception($"{nameof(NetworkConfig.EnableSceneManagement)} flag is not enabled in the {nameof(NetworkManager)}'s {nameof(NetworkConfig)}. Please set {nameof(NetworkConfig.EnableSceneManagement)} flag to true before calling this method.");
            }

            if (s_IsSceneEventActive)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Scene event is already in progress");
                }

                return null;
            }

            if (!RegisteredSceneNames.Contains(sceneName))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"The scene {sceneName} is not registered as a switchable scene.");
                }

                return null;
            }

            var switchSceneProgress = new SceneSwitchProgress(m_NetworkManager);
            SceneSwitchProgresses.Add(switchSceneProgress.Guid, switchSceneProgress);

            if (!isUnloading)
            {
                // The Condition: While a scene is asynchronously loaded in single loading scene mode, if any new NetworkObjects are spawned they need to be moved into the do not destroy temporary scene
                // When it is set: Just before starting the asynchronous loading call
                // When it is unset: After the scene has loaded, the PopulateScenePlacedObjects is called, and all NetworkObjects in the do not destroy temporary scene are moved into the active scene
                IsSpawnedObjectsPendingInDontDestroyOnLoad = true;
            }

            s_IsSceneEventActive = true;

            // NSS TODO: switchSceneProgress needs to be re-factored
            switchSceneProgress.OnClientLoadedScene += clientId => { OnNotifyServerClientLoadedScene?.Invoke(switchSceneProgress, clientId); };
            switchSceneProgress.OnComplete += timedOut =>
            {
                OnNotifyServerAllClientsLoadedScene?.Invoke(switchSceneProgress, timedOut);

                using (var buffer = PooledNetworkBuffer.Get())
                using (var writer = PooledNetworkWriter.Get(buffer))
                {
                    var doneClientIds = switchSceneProgress.DoneClients.ToArray();
                    var timedOutClientIds = m_NetworkManager.ConnectedClients.Keys.Except(doneClientIds).ToArray();

                    writer.WriteULongArray(doneClientIds, doneClientIds.Length);
                    writer.WriteULongArray(timedOutClientIds, timedOutClientIds.Length);

                    m_NetworkManager.MessageSender.Send(NetworkManager.Singleton.ServerClientId, NetworkConstants.ALL_CLIENTS_LOADED_SCENE, NetworkChannel.Internal, buffer);
                }
            };

            return switchSceneProgress;
        }

        /// <summary>
        /// Unloads an additively loaded scene
        /// </summary>
        /// <param name="sceneName">scene name to unload</param>
        /// <returns></returns>
        public SceneSwitchProgress UnloadScene(string sceneName)
        {
            // Make sure the scene is actually loaded
            var sceneToUnload = SceneManager.GetSceneByName(sceneName);
            if (sceneToUnload == null)
            {
                Debug.LogWarning($"{nameof(UnloadScene)} was called, but the scene {sceneName} is not currently loaded!");
                return null;
            }

            var switchSceneProgress = ValidateServerSceneEvent(sceneName, true);
            if (switchSceneProgress == null)
            {
                return null;
            }

            SceneEventData.SwitchSceneGuid = switchSceneProgress.Guid;
            SceneEventData.SceneEventType = SceneEventData.SceneEventTypes.Event_Unload;
            SceneEventData.SceneIndex = SceneNameToIndex[sceneName];

            for (int j = 0; j < m_NetworkManager.ConnectedClientsList.Count; j++)
            {
                using (var buffer = PooledNetworkBuffer.Get())
                {
                    using (var writer = PooledNetworkWriter.Get(buffer))
                    {
                        SceneEventData.OnWrite(writer);
                        m_NetworkManager.MessageSender.Send(m_NetworkManager.ConnectedClientsList[j].ClientId, NetworkConstants.SCENE_EVENT, NetworkChannel.Internal, buffer);
                    }
                }
            }

            // start loading the scene
            AsyncOperation sceneUnload = SceneManager.UnloadSceneAsync(sceneToUnload);
            sceneUnload.completed += (AsyncOperation asyncOp2) => { OnSceneUnloaded(); };
            switchSceneProgress.SetSceneLoadOperation(sceneUnload);
            if(m_ScenesLoaded.Contains(sceneName))
            {
                m_ScenesLoaded.Remove(sceneName);
            }
            OnAdditiveSceneEvent?.Invoke(sceneUnload, sceneName, false);

            //Return our scene progress instance
            return switchSceneProgress;
        }

        private void OnClientUnloadScene()
        {
            if (!SceneIndexToString.TryGetValue(SceneEventData.SceneIndex, out string sceneName) || !RegisteredSceneNames.Contains(sceneName))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Server requested a scene switch to a non-registered scene");
                }

                return;
            }
            s_IsSceneEventActive = true;

            var sceneUnload = SceneManager.UnloadSceneAsync(sceneName);

            if (m_ScenesLoaded.Contains(sceneName))
            {
                m_ScenesLoaded.Remove(sceneName);
            }

            sceneUnload.completed += asyncOp2 => OnSceneUnloaded();

            OnAdditiveSceneEvent?.Invoke(sceneUnload, sceneName, false);
        }

        /// <summary>
        /// Invoked when the additively loaded scene is unloaded
        /// </summary>
        private void OnSceneUnloaded()
        {
            using (var buffer = PooledNetworkBuffer.Get())
            using (var writer = PooledNetworkWriter.Get(buffer))
            {
                SceneEventData.SceneEventType = SceneEventData.SceneEventTypes.Event_Unload_Complete;
                SceneEventData.OnWrite(writer);
                m_NetworkManager.MessageSender.Send(m_NetworkManager.ServerClientId, NetworkConstants.SCENE_EVENT, NetworkChannel.Internal, buffer);
            }

            s_IsSceneEventActive = false;
        }

        /// <summary>
        /// Loads the scene name in question as either additive or single.
        /// </summary>
        /// <param name="sceneName"></param>
        /// NSS TODO: This could probably stand to have some form of "scene event status" class/structure that will
        /// include any error types/messages and the SceneSwitchProgress class associated with the status (if success)
        /// <returns>SceneSwitchProgress  (if null this call failed)</returns>
        public SceneSwitchProgress LoadScene(string sceneName, LoadSceneMode loadSceneMode)
        {
            var switchSceneProgress = ValidateServerSceneEvent(sceneName);
            if (switchSceneProgress == null)
            {
                return null;
            }

            SceneEventData.SwitchSceneGuid = switchSceneProgress.Guid;
            SceneEventData.SceneEventType = SceneEventData.SceneEventTypes.Event_Load;
            SceneEventData.SceneIndex = SceneNameToIndex[sceneName];
            SceneEventData.LoadSceneMode = loadSceneMode;

            if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                // Destroy current scene objects before switching.
                m_NetworkManager.SpawnManager.ServerDestroySpawnedSceneObjects();

                // Preserve the objects that should not be destroyed during the scene event
                MoveObjectsToDontDestroyOnLoad();
            }

            // Begin the scene event
            OnBeginSceneEvent(sceneName, switchSceneProgress, loadSceneMode);

            //Return our scene progress instance
            return switchSceneProgress;
        }

        /// <summary>
        /// Switches to a scene with a given name. Can only be called from Server
        /// </summary>
        /// <param name="sceneName">The name of the scene to switch to</param>
        /// <param name="loadSceneMode">The mode to load the scene (Additive vs Single)</param>
        /// NSS TODO: This is a topic for MTT discussion.  Do we want to keep this divergence from Unity's SceneManager API?
        /// It is proposed we completely remove this call or mark it as deprecated with message to use Load and Unload
        /// <returns>SceneSwitchProgress</returns>
        public SceneSwitchProgress SwitchScene(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Single)
        {
            return LoadScene(sceneName, loadSceneMode);
        }

        /// <summary>
        /// Commonly shared code between switching and additively loading a scene
        /// </summary>
        /// <param name="sceneName">name of the scene to be loaded</param>
        /// <param name="switchSceneProgress">SceneSwitchProgress class instance</param>
        /// <param name="loadSceneMode">how the scene will be loaded</param>
        private void OnBeginSceneEvent(string sceneName, SceneSwitchProgress switchSceneProgress, LoadSceneMode loadSceneMode)
        {
            // start loading the scene
            // NSS TODO: This is a temporary place holder check to make sure we don't try to unload a scene loaded in single mode.
            var currentActiveScene = SceneManager.GetActiveScene();
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);
            sceneLoad.completed += (AsyncOperation asyncOp2) => { OnSceneLoaded(sceneName); };
            switchSceneProgress.SetSceneLoadOperation(sceneLoad);



            if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                foreach (var additiveSceneName in m_ScenesLoaded)
                {
                    if(currentActiveScene.name != additiveSceneName)
                    {
                        SceneManager.UnloadSceneAsync(additiveSceneName);
                    }
                }
                m_ScenesLoaded.Clear();
                // NSS TODO: Make a single unified notification callback
                OnSceneSwitchStarted?.Invoke(sceneLoad);
            }
            else
            {
                if(!m_ScenesLoaded.Contains(sceneName))
                {
                    m_ScenesLoaded.Add(sceneName);
                }
                else
                {
                    throw new Exception($"{sceneName} is being loaded twice?!");
                }
                // NSS TODO: Make a single unified notification callback
                OnAdditiveSceneEvent?.Invoke(sceneLoad, sceneName, true);
            }
        }

        /// <summary>
        /// Client Side: handles both forms of scene loading
        /// </summary>
        /// <param name="objectStream">Stream data associated with the event </param>
        internal void OnClientSceneLoadingEvent(Stream objectStream)
        {
            SceneEventData.CopyUnreadFromStream(objectStream);

            if (!SceneIndexToString.TryGetValue(SceneEventData.SceneIndex, out string sceneName) || !RegisteredSceneNames.Contains(sceneName))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Server requested a scene switch to a non-registered scene");
                }

                return;
            }

            // NSS TODO: This is a temporary place holder check to make sure we don't try to unload a scene loaded in single mode.
            var currentActiveScene = SceneManager.GetActiveScene();
            if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                foreach(var loadedSceneName in m_ScenesLoaded)
                {
                    if(currentActiveScene.name != loadedSceneName)
                    {
                        SceneManager.UnloadSceneAsync(loadedSceneName);
                    }

                }
                m_ScenesLoaded.Clear();

                // Move ALL NetworkObjects to the temp scene
                MoveObjectsToDontDestroyOnLoad();
            }
            else
            {
                if (!m_ScenesLoaded.Contains(sceneName))
                {
                    m_ScenesLoaded.Add(sceneName);
                }
                else
                {
                    throw new Exception($"{sceneName} is being loaded twice?!");
                }
            }

            // The Condition: While a scene is asynchronously loaded in single loading scene mode, if any new NetworkObjects are spawned they need to be moved into the do not destroy temporary scene
            // When it is set: Just before starting the asynchronous loading call
            // When it is unset: After the scene has loaded, the PopulateScenePlacedObjects is called, and all NetworkObjects in the do not destroy temporary scene are moved into the active scene
            if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                IsSpawnedObjectsPendingInDontDestroyOnLoad = true;
            }

            var sceneLoad = SceneManager.LoadSceneAsync(sceneName, SceneEventData.LoadSceneMode);
            sceneLoad.completed += asyncOp2 => OnSceneLoaded(sceneName);

            if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                OnSceneSwitchStarted?.Invoke(sceneLoad);
            }
            else
            {
                OnAdditiveSceneEvent?.Invoke(sceneLoad, sceneName, true);
            }
        }

        /// <summary>
        /// Client and Server: Generic on scene loaded callback method to be called upon a scene loading
        /// </summary>
        private void OnSceneLoaded(string sceneName)
        {
            var nextScene = SceneManager.GetSceneByName(sceneName);
            if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                SceneManager.SetActiveScene(nextScene);
                m_ScenesLoaded.Add(sceneName);
            }

            //Get all NetworkObjects loaded by the scene
            PopulateScenePlacedObjects(nextScene);

            if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
            {
                // Move all objects to the new scene
                MoveObjectsToScene(nextScene);

            }

            // The Condition: While a scene is asynchronously loaded in single loading scene mode, if any new NetworkObjects are spawned they need to be moved into the do not destroy temporary scene
            // When it is set: Just before starting the asynchronous loading call
            // When it is unset: After the scene has loaded, the PopulateScenePlacedObjects is called, and all NetworkObjects in the do not destroy temporary scene are moved into the active scene
            IsSpawnedObjectsPendingInDontDestroyOnLoad = false;

            if (m_NetworkManager.IsServer)
            {
                OnServerLoadedScene();
            }
            else
            {
                OnClientLoadedScene();
            }
        }

        /// <summary>
        /// Server specific on scene loaded callback method invoked by OnSceneLoading only
        /// </summary>
        private void OnServerLoadedScene()
        {
            // Register in-scene placed NetworkObjects with MLAPI
            foreach (var keyValuePair in ScenePlacedObjects)
            {
                if (!keyValuePair.Value.IsPlayerObject)
                {
                    m_NetworkManager.SpawnManager.SpawnNetworkObjectLocally(keyValuePair.Value, m_NetworkManager.SpawnManager.GetNetworkObjectId(), true, false, null, null, false, 0, false, true);
                }
            }

            for (int j = 0; j < m_NetworkManager.ConnectedClientsList.Count; j++)
            {
                var clientId = m_NetworkManager.ConnectedClientsList[j].ClientId;
                if (clientId != m_NetworkManager.ServerClientId)
                {
                    using (var buffer = PooledNetworkBuffer.Get())
                    {
                        using (var writer = PooledNetworkWriter.Get(buffer))
                        {
                            SceneEventData.OnWrite(writer);

                            uint sceneObjectsToSpawn = 0;

                            foreach (var keyValuePair in ScenePlacedObjects)
                            {
                                if (keyValuePair.Value.Observers.Contains(clientId))
                                {
                                    sceneObjectsToSpawn++;
                                }
                            }

                            // Write number of scene objects to spawn
                            writer.WriteUInt32Packed(sceneObjectsToSpawn);
                            foreach (var keyValuePair in ScenePlacedObjects)
                            {
                                if (keyValuePair.Value.Observers.Contains(clientId))
                                {
                                    keyValuePair.Value.SerializeSceneObject(writer, clientId);
                                }
                            }

                            m_NetworkManager.MessageSender.Send(m_NetworkManager.ConnectedClientsList[j].ClientId, NetworkConstants.SCENE_EVENT, NetworkChannel.Internal, buffer);
                        }
                    }
                }
            }

            // Tell server that scene load is completed
            if (m_NetworkManager.IsServer)
            {
                OnClientSceneLoadingEventCompleted(m_NetworkManager.LocalClientId, SceneEventData.SwitchSceneGuid);
            }

            s_IsSceneEventActive = false;
            OnSceneSwitched?.Invoke();
        }

        /// <summary>
        /// Client specific on scene loaded callback method invoked by OnSceneLoading only
        /// </summary>
        private void OnClientLoadedScene()
        {
            using (var reader = PooledNetworkReader.Get(SceneEventData.InternalBuffer))
            {
                var newObjectsCount = reader.ReadUInt32Packed();

                for (int i = 0; i < newObjectsCount; i++)
                {
                    NetworkObject.DeserializeSceneObject(SceneEventData.InternalBuffer as NetworkBuffer, reader, m_NetworkManager);
                }
            }

            using (var buffer = PooledNetworkBuffer.Get())
            using (var writer = PooledNetworkWriter.Get(buffer))
            {
                SceneEventData.SceneEventType = SceneEventData.SceneEventTypes.Event_Load_Complete;
                SceneEventData.OnWrite(writer);
                m_NetworkManager.MessageSender.Send(m_NetworkManager.ServerClientId, NetworkConstants.SCENE_EVENT, NetworkChannel.Internal, buffer);
            }

            s_IsSceneEventActive = false;

            OnSceneSwitched?.Invoke();
        }

        /// <summary>
        /// Server Side Only:
        /// This is used for late joining players and players that have just had their connection approved
        /// </summary>
        /// <param name="ownerClientId">newly joined client identifier</param>
        internal void SynchronizeNetworkObjects(ulong ownerClientId)
        {
            m_ObservedObjects.Clear();

            foreach (var sobj in m_NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (sobj.CheckObjectVisibility == null || sobj.CheckObjectVisibility(ownerClientId))
                {
                    m_ObservedObjects.Add(sobj);
                    sobj.Observers.Add(ownerClientId);
                }
            }

            ClientSynchEventData.InitializeForSynch();
            ClientSynchEventData.LoadSceneMode = LoadSceneMode.Single;
            var activeScene = SceneManager.GetActiveScene();
            ClientSynchEventData.SceneEventType = SceneEventData.SceneEventTypes.Event_Sync;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                var malpiSceneIndex = GetMLAPISceneIndexFromScene(scene);

                if( malpiSceneIndex == uint.MaxValue)
                {
                    continue;
                }
                // This would depend upon whether we are additive or note
                if (activeScene == scene)
                {
                    ClientSynchEventData.SceneIndex = malpiSceneIndex;
                }

                // Separate NetworkObjects by scene
                foreach (var networkObject in m_ObservedObjects)
                {
                    // If the current scene we are matching NetworkObjects to does not match and this NetworkObject has no dependent scene, then continue.
                    if (networkObject.gameObject.scene != scene && (networkObject.DependentSceneName == null || networkObject.DependentSceneName == string.Empty))
                    {
                        continue;
                    }
                    else // If this NetworkObject has a dependent scene and the current scene is not the dependent scene, then continue
                    if (networkObject.DependentSceneName != null && networkObject.DependentSceneName != string.Empty && networkObject.DependentSceneName != scene.name)
                    {
                        continue;
                    }
                    ClientSynchEventData.AddNetworkObjectForSynch(malpiSceneIndex, networkObject);
                }
            }

            // Send the scene event
            using (var buffer = PooledNetworkBuffer.Get())
            using (var writer = PooledNetworkWriter.Get(buffer))
            {
                ClientSynchEventData.OnWrite(writer);
                m_NetworkManager.MessageSender.Send(ownerClientId, NetworkConstants.SCENE_EVENT, NetworkChannel.Internal, buffer);
            }

        }

        /// <summary>
        /// This is called when the client receives the SCENE_EVENT of type SceneEventData.SceneEventTypes.SYNC
        /// Note: This can recurse one additional time by the client if the current scene loaded by the client
        /// is already loaded.
        /// </summary>
        /// <param name="sceneIndex">MLAPI sceneIndex to load</param>
        private void OnClientBeginSync(uint sceneIndex)
        {
            if (!SceneIndexToString.TryGetValue(sceneIndex, out string sceneName) || !RegisteredSceneNames.Contains(sceneName))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Server requested a scene switch to a non-registered scene");
                }

                return;
            }

            var activeScene = SceneManager.GetActiveScene();

            if (sceneName != activeScene.name)
            {
                // NSS TODO: Add to proposal's MTT discussion topics: Should we cover switching the active scene for V1.0.0?
                var sceneLoad = SceneManager.LoadSceneAsync(sceneName, sceneIndex == SceneEventData.SceneIndex ? SceneEventData.LoadSceneMode : LoadSceneMode.Additive);
                sceneLoad.completed += asyncOp2 => ClientLoadedSynchronization(sceneIndex);
            }
            else
            {
                ClientLoadedSynchronization(sceneIndex);
            }
        }

        /// <summary>
        /// Once a scene is loaded ( or if it was already loaded) this gets called.
        /// This handles all of the in-scene and dynamically spawned NetworkObject synchronization
        /// </summary>
        /// <param name="sceneIndex">MLAPI scene index that was loaded</param>
        private void ClientLoadedSynchronization(uint sceneIndex)
        {
            var nextScene = SceneManager.GetSceneByName(GetSceneNameFromMLAPISceneIndex(sceneIndex));
            if (nextScene == null)
            {
                Debug.LogError($"Client was trying to load {sceneIndex} which does not appear to be a valid registered scene index!");
                return;
            }

            // NSS TODO: Add to proposal's MTT discussion topics: Should we cover switching the active scene for V1.0.0?
            if ((sceneIndex == SceneEventData.SceneIndex ? SceneEventData.LoadSceneMode : LoadSceneMode.Additive) == LoadSceneMode.Single)
            {
                SceneManager.SetActiveScene(nextScene);
            }

            // Get all NetworkObjects loaded by the scene  (in-scene NetworkObjects)
            PopulateScenePlacedObjects(nextScene);

            // Synchronize the NetworkObjects for this scene
            SceneEventData.SynchronizeSceneNetworkObjects(sceneIndex, m_NetworkManager);

            // Check to see if we still have scenes to load and synchronize with
            HandleClientSceneEvent(null);
        }

        #region General Methods
        internal bool HasSceneMismatch(uint sceneIndex) => SceneManager.GetActiveScene().name != SceneIndexToString[sceneIndex];

        internal void RemoveClientFromSceneSwitchProgresses(ulong clientId)
        {
            foreach (var switchSceneProgress in SceneSwitchProgresses.Values)
            {
                switchSceneProgress.RemoveClientAsDone(clientId);
            }
        }

        private void MoveObjectsToDontDestroyOnLoad()
        {
            // Move ALL NetworkObjects to the temp scene
            var objectsToKeep = m_NetworkManager.SpawnManager.SpawnedObjectsList;

            foreach (var sobj in objectsToKeep)
            {
                //In case an object has been set as a child of another object it has to be removed from the parent in order to be moved from one scene to another.
                if (sobj.gameObject.transform.parent != null)
                {
                    sobj.gameObject.transform.parent = null;
                }

                UnityEngine.Object.DontDestroyOnLoad(sobj.gameObject);
            }
        }

        /// <summary>
        /// Should be invoked on both the client and server side after:
        /// -- A new scene has been loaded
        /// -- Before any "DontDestroyOnLoad" NetworkObjects have been added back into the scene.
        /// Added the ability to choose not to clear the scene placed objects for additive scene loading.
        /// </summary>
        internal void PopulateScenePlacedObjects(Scene sceneToFilterBy, bool clearScenePlacedObjects = true)
        {
            if (clearScenePlacedObjects)
            {
                ScenePlacedObjects.Clear();
            }

            var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>();

            // Just add every NetworkObject found that isn't already in the list
            // If any "non-in-scene placed NetworkObjects" are added to this list it shouldn't matter
            // The only thing that matters is making sure each NetworkObject is keyed off of their GlobalObjectIdHash
            foreach (var networkObjectInstance in networkObjects)
            {
                if (!ScenePlacedObjects.ContainsKey(networkObjectInstance.GlobalObjectIdHash))
                {
                    // We check to make sure the NetworkManager instance is the same one to be "MultiInstanceHelpers" compatible and filter the list on a per scene basis (additive scenes)
                    if (networkObjectInstance.IsSceneObject == null && networkObjectInstance.NetworkManager == m_NetworkManager && networkObjectInstance.gameObject.scene == sceneToFilterBy)
                    {
                        ScenePlacedObjects.Add(networkObjectInstance.GlobalObjectIdHash, networkObjectInstance);
                    }
                }
            }
        }

        private void MoveObjectsToScene(Scene scene)
        {
            // Move ALL NetworkObjects to the temp scene
            var objectsToKeep = m_NetworkManager.SpawnManager.SpawnedObjectsList;

            foreach (var sobj in objectsToKeep)
            {
                //In case an object has been set as a child of another object it has to be removed from the parent in order to be moved from one scene to another.
                if (sobj.gameObject.transform.parent != null)
                {
                    sobj.gameObject.transform.parent = null;
                }

                SceneManager.MoveGameObjectToScene(sobj.gameObject, scene);
            }
        }

        internal void AllClientsReady(ulong[] clientIds, ulong[] timedOutClientIds)
        {
            OnNotifyClientAllClientsLoadedScene?.Invoke(clientIds, timedOutClientIds);
        }

        // Called on server
        internal void OnClientSceneLoadingEventCompleted(ulong clientId, Guid switchSceneGuid)
        {
            if (switchSceneGuid == Guid.Empty)
            {
                // If Guid is empty it means the client has loaded the start scene of the server and the server would never have a switchSceneProgresses created for the start scene.
                return;
            }

            if (SceneSwitchProgresses.TryGetValue(switchSceneGuid, out SceneSwitchProgress progress))
            {
                SceneSwitchProgresses[switchSceneGuid].AddClientAsDone(clientId);
            }
        }
        #endregion

        /// <summary>
        /// Client Side: Handles incoming SCENE_EVENT messages
        /// </summary>
        /// <param name="stream">data associated with the event</param>
        private void HandleClientSceneEvent(Stream stream)
        {
            switch (SceneEventData.SceneEventType)
            {
                // Both events are basically the same with some minor differences
                //case SceneEventData.SceneEventTypes.EventSwitch:
                case SceneEventData.SceneEventTypes.Event_Load:
                    {
                        OnClientSceneLoadingEvent(stream);
                        break;
                    }
                case SceneEventData.SceneEventTypes.Event_Unload:
                    {
                        OnClientUnloadScene();
                        break;
                    }
                case SceneEventData.SceneEventTypes.Event_Sync:
                    {
                        if (!SceneEventData.IsDoneWithSynchronization())
                        {
                            OnClientBeginSync(SceneEventData.GetNextSceneSynchronizationIndex());
                        }
                        else
                        {
                            // All scenes are synchronized, let the server know we are done synchronizing
                            m_NetworkManager.IsConnectedClient = true;
                            m_NetworkManager.InvokeOnClientConnectedCallback(m_NetworkManager.LocalClientId);
                            using (var buffer = PooledNetworkBuffer.Get())
                            using (var writer = PooledNetworkWriter.Get(buffer))
                            {
                                SceneEventData.SceneEventType = SceneEventData.SceneEventTypes.Event_Sync_Complete;
                                SceneEventData.OnWrite(writer);
                                m_NetworkManager.MessageSender.Send(m_NetworkManager.ServerClientId, NetworkConstants.SCENE_EVENT, NetworkChannel.Internal, buffer);
                            }
                        }
                        break;
                    }
                case SceneEventData.SceneEventTypes.Event_ReSync:
                    {
                        break;
                    }
                default:
                    {
                        Debug.LogWarning($"{SceneEventData.SceneEventType} is not currently supported!");
                        break;
                    }
            }
        }

        /// <summary>
        /// Server Side: Handles incoming scene events
        /// </summary>
        /// <param name="clientId">client who sent the event</param>
        /// <param name="stream">data associated with the event</param>
        private void HandleServerSceneEvent(ulong clientId, Stream stream)
        {
            switch (SceneEventData.SceneEventType)
            {
                case SceneEventData.SceneEventTypes.Event_Load_Complete:
                    {
                        Debug.Log($"[{nameof(SceneEventData.SceneEventTypes.Event_Load_Complete)}] Client Id {clientId} finished loading additive scene.");
                        if (SceneEventData.LoadSceneMode == LoadSceneMode.Single)
                        {
                            OnClientSceneLoadingEventCompleted(clientId, SceneEventData.SwitchSceneGuid);
                        }
                        else
                        {
                            //Other message?
                        }
                        break;
                    }
                case SceneEventData.SceneEventTypes.Event_Unload_Complete:
                    {
                        Debug.Log($"[{nameof(SceneEventData.SceneEventTypes.Event_Unload_Complete)}] Client Id {clientId} finished unloading additive scene.");
                        break;
                    }
                case SceneEventData.SceneEventTypes.Event_Sync_Complete:
                    {
                        if(SceneEventData.ClientNeedsReSynchronization())
                        {
                            Debug.Log($"Re-Synchronizing client {clientId} for missed destroyed NetworkObjects.");
                            using (var buffer = PooledNetworkBuffer.Get())
                            using (var writer = PooledNetworkWriter.Get(buffer))
                            {
                                SceneEventData.SceneEventType = SceneEventData.SceneEventTypes.Event_ReSync;
                                SceneEventData.OnWrite(writer);
                                m_NetworkManager.MessageSender.Send(clientId, NetworkConstants.SCENE_EVENT, NetworkChannel.Internal, buffer);
                            }
                        }
                        // NSS TOOD: The scene event local notification needs to be called
                        //m_NetworkManager.NotifyPlayerConnected(clientId);
                        break;
                    }
                default:
                    {
                        Debug.LogWarning($"{SceneEventData.SceneEventType} is not currently supported!");
                        break;
                    }
            }
        }

        /// <summary>
        /// Both Client and Server: Incoming scene event entry point
        /// </summary>
        /// <param name="clientId">client who sent the scene event</param>
        /// <param name="stream">data associated with the scene event</param>
        public void HandleSceneEvent(ulong clientId, Stream stream)
        {
            if (m_NetworkManager != null)
            {
                if (stream != null)
                {
                    var reader = NetworkReaderPool.GetReader(stream);
                    SceneEventData.OnRead(reader);
                    NetworkReaderPool.PutBackInPool(reader);
                    if (SceneEventData.IsSceneEventClientSide())
                    {
                        HandleClientSceneEvent(stream);
                    }
                    else
                    {
                        HandleServerSceneEvent(clientId, stream);
                    }
                }
                else
                {
                    Debug.LogError($"Scene Event {nameof(OnClientSceneLoadingEvent)} was invoked with a null stream!");
                    return;
                }
            }
            else
            {
                Debug.LogError($"{nameof(NetworkSceneManager.HandleSceneEvent)} was invoked but {nameof(NetworkManager)} reference was null!");
            }
        }
    }
}
