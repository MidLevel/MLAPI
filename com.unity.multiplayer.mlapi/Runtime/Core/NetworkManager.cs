using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using UnityEngine;
using MLAPI.Logging;
using MLAPI.Configuration;
using MLAPI.Internal;
using MLAPI.Profiling;
using MLAPI.Serialization;
using MLAPI.Transports;
using MLAPI.Connection;
using MLAPI.Messaging;
using MLAPI.SceneManagement;
using MLAPI.Serialization.Pooled;
using MLAPI.Spawning;
using MLAPI.Exceptions;
using MLAPI.Transports.Tasks;
using MLAPI.Messaging.Buffering;
using Unity.Profiling;

namespace MLAPI
{
    /// <summary>
    /// The main component of the library
    /// </summary>
    [AddComponentMenu("MLAPI/NetworkManager", -100)]
    public class NetworkManager : MonoBehaviour, INetworkUpdateSystem, IProfilableTransportProvider
    {
#pragma warning disable IDE1006 // disable naming rule violation check
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
#if UNITY_2020_2_OR_NEWER
        // RuntimeAccessModifiersILPP will make this `public`
        internal static readonly Dictionary<uint, Action<NetworkBehaviour, NetworkSerializer, __RpcParams>> __ntable = new Dictionary<uint, Action<NetworkBehaviour, NetworkSerializer, __RpcParams>>();
#else
        [Obsolete("Please do not use, will no longer be exposed in the future versions (framework internal)")]
        public static readonly Dictionary<uint, Action<NetworkBehaviour, NetworkSerializer, __RpcParams>> __ntable = new Dictionary<uint, Action<NetworkBehaviour, NetworkSerializer, __RpcParams>>();
#endif
#pragma warning restore IDE1006 // restore naming rule violation check

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static ProfilerMarker s_EventTick = new ProfilerMarker($"{nameof(NetworkManager)}.EventTick");
        private static ProfilerMarker s_ReceiveTick = new ProfilerMarker($"{nameof(NetworkManager)}.ReceiveTick");
        private static ProfilerMarker s_SyncTime = new ProfilerMarker($"{nameof(NetworkManager)}.SyncTime");
        private static ProfilerMarker s_TransportConnect = new ProfilerMarker($"{nameof(NetworkManager)}.TransportConnect");
        private static ProfilerMarker s_HandleIncomingData = new ProfilerMarker($"{nameof(NetworkManager)}.{nameof(HandleIncomingData)}");
        private static ProfilerMarker s_TransportDisconnect = new ProfilerMarker($"{nameof(NetworkManager)}.TransportDisconnect");

        private static ProfilerMarker s_InvokeRpc = new ProfilerMarker($"{nameof(NetworkManager)}.{nameof(InvokeRpc)}");
#endif

        internal RpcQueueContainer RpcQueueContainer { get; private set; }
        internal NetworkTickSystem NetworkTickSystem { get; private set; }
        public NetworkPrefabHandler PrefabHandler { get; private set; }

        /// <summary>
        /// A synchronized time, represents the time in seconds since the server application started. Is replicated across all clients
        /// </summary>
        public float NetworkTime => Time.unscaledTime + m_CurrentNetworkTimeOffset;

        private float m_NetworkTimeOffset;
        private float m_CurrentNetworkTimeOffset;

        /// <summary>
        /// Gets or sets if the NetworkManager should be marked as DontDestroyOnLoad
        /// </summary>
        [HideInInspector]
        public bool DontDestroy = true;

        /// <summary>
        /// Gets or sets if the application should be set to run in background
        /// </summary>
        [HideInInspector]
        public bool RunInBackground = true;

        /// <summary>
        /// The log level to use
        /// </summary>
        [HideInInspector]
        public LogLevel LogLevel = LogLevel.Normal;

        /// <summary>
        /// The singleton instance of the NetworkManager
        /// </summary>
        public static NetworkManager Singleton { get; private set; }

        /// <summary>
        /// Gets the SpawnManager for this NetworkManager
        /// </summary>
        public NetworkSpawnManager SpawnManager { get; private set; }

        public CustomMessagingManager CustomMessagingManager { get; private set; }

        public NetworkSceneManager SceneManager { get; private set; }

        internal BufferManager BufferManager { get; private set; }

        // Has to have setter for tests
        internal IInternalMessageHandler MessageHandler { get; set; }

        internal InternalMessageSender MessageSender { get; set; }

        /// <summary>
        /// Gets the networkId of the server
        /// </summary>
        public ulong ServerClientId => NetworkConfig.NetworkTransport?.ServerClientId ?? throw new NullReferenceException($"The transport in the active {nameof(NetworkConfig)} is null");

        /// <summary>
        /// The clientId the server calls the local client by, only valid for clients
        /// </summary>
        public ulong LocalClientId
        {
            get => IsServer ? NetworkConfig.NetworkTransport.ServerClientId : m_LocalClientId;
            internal set => m_LocalClientId = value;
        }

        private ulong m_LocalClientId;

        /// <summary>
        /// Gets a dictionary of connected clients and their clientId keys. This is only populated on the server.
        /// </summary>
        public readonly Dictionary<ulong, NetworkClient> ConnectedClients = new Dictionary<ulong, NetworkClient>();

        /// <summary>
        /// Gets a list of connected clients. This is only populated on the server.
        /// </summary>
        public readonly List<NetworkClient> ConnectedClientsList = new List<NetworkClient>();

        /// <summary>
        /// Gets a dictionary of the clients that have been accepted by the transport but are still pending by the MLAPI. This is only populated on the server.
        /// </summary>
        public readonly Dictionary<ulong, PendingClient> PendingClients = new Dictionary<ulong, PendingClient>();

        /// <summary>
        /// Gets Whether or not a server is running
        /// </summary>
        public bool IsServer { get; internal set; }

        /// <summary>
        /// Gets Whether or not a client is running
        /// </summary>
        public bool IsClient { get; internal set; }

        /// <summary>
        /// Gets if we are running as host
        /// </summary>
        public bool IsHost => IsServer && IsClient;

        /// <summary>
        /// Gets Whether or not we are listening for connections
        /// </summary>
        public bool IsListening { get; internal set; }

        /// <summary>
        /// Gets if we are connected as a client
        /// </summary>
        public bool IsConnectedClient { get; internal set; }

        /// <summary>
        /// The callback to invoke once a client connects. This callback is only ran on the server and on the local client that connects.
        /// </summary>
        public event Action<ulong> OnClientConnectedCallback = null;

        internal void InvokeOnClientConnectedCallback(ulong clientId) => OnClientConnectedCallback?.Invoke(clientId);

        /// <summary>
        /// The callback to invoke when a client disconnects. This callback is only ran on the server and on the local client that disconnects.
        /// </summary>
        public event Action<ulong> OnClientDisconnectCallback = null;

        internal void InvokeOnClientDisconnectCallback(ulong clientId) => OnClientDisconnectCallback?.Invoke(clientId);

        /// <summary>
        /// The callback to invoke once the server is ready
        /// </summary>
        public event Action OnServerStarted = null;

        /// <summary>
        /// Delegate type called when connection has been approved. This only has to be set on the server.
        /// </summary>
        /// <param name="createPlayerObject">If true, a player object will be created. Otherwise the client will have no object.</param>
        /// <param name="playerPrefabHash">The prefabHash to use for the client. If createPlayerObject is false, this is ignored. If playerPrefabHash is null, the default player prefab is used.</param>
        /// <param name="approved">Whether or not the client was approved</param>
        /// <param name="position">The position to spawn the client at. If null, the prefab position is used.</param>
        /// <param name="rotation">The rotation to spawn the client with. If null, the prefab position is used.</param>
        public delegate void ConnectionApprovedDelegate(bool createPlayerObject, uint? playerPrefabHash, bool approved, Vector3? position, Quaternion? rotation);

        /// <summary>
        /// The callback to invoke during connection approval
        /// </summary>
        public event Action<byte[], ulong, ConnectionApprovedDelegate> ConnectionApprovalCallback = null;

        internal void InvokeConnectionApproval(byte[] payload, ulong clientId, ConnectionApprovedDelegate action) => ConnectionApprovalCallback?.Invoke(payload, clientId, action);

        /// <summary>
        /// The current NetworkConfig
        /// </summary>
        [HideInInspector]
        public NetworkConfig NetworkConfig;

        /// <summary>
        /// The current hostname we are connected to, used to validate certificate
        /// </summary>
        public string ConnectedHostname { get; private set; }

        internal static event Action OnSingletonReady;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (NetworkConfig == null)
            {
                return; // May occur when the component is added
            }

            if (GetComponentInChildren<NetworkObject>() != null)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"{nameof(NetworkManager)} cannot be a {nameof(NetworkObject)}.");
                }
            }

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var activeSceneName = activeScene.name;
            if (!NetworkConfig.RegisteredScenes.Contains(activeSceneName))
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Active scene is not registered as a network scene. The MLAPI has added it");
                }

                NetworkConfig.RegisteredScenes.Add(activeSceneName);
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (!UnityEditor.EditorApplication.isPlaying)
                    {
                        UnityEditor.EditorUtility.SetDirty(this);
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                    }
                };
            }

            // During OnValidate we will always clear out NetworkPrefabOverrideLinks and rebuild it
            NetworkConfig.NetworkPrefabOverrideLinks.Clear();

            // Check network prefabs and assign to dictionary for quick look up
            for (int i = 0; i < NetworkConfig.NetworkPrefabs.Count; i++)
            {
                if (NetworkConfig.NetworkPrefabs[i] != null && NetworkConfig.NetworkPrefabs[i].Prefab != null)
                {
                    var networkObject = NetworkConfig.NetworkPrefabs[i].Prefab.GetComponent<NetworkObject>();
                    if (networkObject == null)
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                        {
                            NetworkLog.LogWarning($"{nameof(NetworkPrefab)} [{i}] does not have a {nameof(NetworkObject)} component");
                        }
                    }
                    else
                    {
                        // Default to the standard NetworkPrefab.Prefab's NetworkObject first
                        var globalObjectIdHash = networkObject.GlobalObjectIdHash;

                        // Now check to see if it has an override
                        switch (NetworkConfig.NetworkPrefabs[i].Override)
                        {
                            case NetworkPrefabOverride.Prefab:
                                {
                                    if (NetworkConfig.NetworkPrefabs[i].SourcePrefabToOverride == null && NetworkConfig.NetworkPrefabs[i].Prefab != null)
                                    {
                                        NetworkConfig.NetworkPrefabs[i].SourcePrefabToOverride = NetworkConfig.NetworkPrefabs[i].Prefab;
                                    }
                                    globalObjectIdHash = NetworkConfig.NetworkPrefabs[i].SourcePrefabToOverride.GetComponent<NetworkObject>().GlobalObjectIdHash;
                                }
                                break;
                            case NetworkPrefabOverride.Hash:
                                globalObjectIdHash = NetworkConfig.NetworkPrefabs[i].SourceHashToOverride;
                                break;
                        }

                        // Add to the NetworkPrefabOverrideLinks or handle a new (blank) entries
                        if (!NetworkConfig.NetworkPrefabOverrideLinks.ContainsKey(globalObjectIdHash))
                        {
                            NetworkConfig.NetworkPrefabOverrideLinks.Add(globalObjectIdHash, NetworkConfig.NetworkPrefabs[i]);
                        }
                        else
                        {
                            // Duplicate entries can happen when adding a new entry into a list of existing entries
                            // Either this is user error or a new entry, either case we replace it with a new, blank, NetworkPrefab under this condition
                            NetworkConfig.NetworkPrefabs[i] = new NetworkPrefab();
                        }
                    }
                }
            }
        }
#endif

        private void Init(bool server)
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(Init));
            }

            LocalClientId = 0;
            m_NetworkTimeOffset = 0f;
            m_CurrentNetworkTimeOffset = 0f;
            m_LastReceiveTickTime = 0f;
            m_LastReceiveTickTime = 0f;
            PendingClients.Clear();
            ConnectedClients.Clear();
            ConnectedClientsList.Clear();

            // Create spawn manager instance
            SpawnManager = new NetworkSpawnManager(this);

            CustomMessagingManager = new CustomMessagingManager(this);

            BufferManager = new BufferManager(this);

            SceneManager = new NetworkSceneManager(this);

            if (MessageHandler == null)
            {
                // Only create this if it's not already set (like in test cases)
                MessageHandler = new InternalMessageHandler(this);
            }

            MessageSender = new InternalMessageSender(this);

            if (NetworkConfig.NetworkTransport == null)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                {
                    NetworkLog.LogError("No transport has been selected!");
                }

                return;
            }

            // This 'if' should never enter
            if (NetworkTickSystem != null)
            {
                NetworkTickSystem.Dispose();
                NetworkTickSystem = null;
            }

            NetworkTickSystem = new NetworkTickSystem(NetworkConfig.NetworkTickIntervalSec);

            // This should never happen, but in the event that it does there should be (at a minimum) a unity error logged.
            if (RpcQueueContainer != null)
            {
                UnityEngine.Debug.LogError("Init was invoked, but rpcQueueContainer was already initialized! (destroying previous instance)");
                RpcQueueContainer.Dispose();
                RpcQueueContainer = null;
            }

            // The RpcQueueContainer must be initialized within the Init method ONLY
            // It should ONLY be shutdown and destroyed in the Shutdown method (other than just above)
            RpcQueueContainer = new RpcQueueContainer(this);

            // Register INetworkUpdateSystem (always register this after rpcQueueContainer has been instantiated)
            this.RegisterNetworkUpdate(NetworkUpdateStage.EarlyUpdate);
            this.RegisterNetworkUpdate(NetworkUpdateStage.PreUpdate);

            if (NetworkConfig.EnableSceneManagement)
            {
                NetworkConfig.RegisteredScenes.Sort(StringComparer.Ordinal);

                for (int i = 0; i < NetworkConfig.RegisteredScenes.Count; i++)
                {
                    SceneManager.RegisteredSceneNames.Add(NetworkConfig.RegisteredScenes[i]);
                    SceneManager.SceneIndexToString.Add((uint)i, NetworkConfig.RegisteredScenes[i]);
                    SceneManager.SceneNameToIndex.Add(NetworkConfig.RegisteredScenes[i], (uint)i);
                }

                SceneManager.SetCurrentSceneIndex();
            }

            // This is used to remove entries not needed or invalid
            var removeEmptyPrefabs = new List<int>();

            // Always clear our prefab override links before building
            NetworkConfig.NetworkPrefabOverrideLinks.Clear();

            // Build the NetworkPrefabOverrideLinks dictionary
            for (int i = 0; i < NetworkConfig.NetworkPrefabs.Count; i++)
            {
                if (NetworkConfig.NetworkPrefabs[i] == null || NetworkConfig.NetworkPrefabs[i].Prefab == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                    {
                        NetworkLog.LogWarning($"{nameof(NetworkPrefab)} cannot be null ({nameof(NetworkPrefab)} at index: {i})");
                    }

                    // Provide the name of the prefab with issues so the user can more easily find the prefab and fix it
                    UnityEngine.Debug.LogWarning($"{nameof(NetworkPrefab)} (\"{NetworkConfig.NetworkPrefabs[i].Prefab.name}\") will be removed and ignored.");
                    removeEmptyPrefabs.Add(i);

                    continue;
                }
                else if (NetworkConfig.NetworkPrefabs[i].Prefab.GetComponent<NetworkObject>() == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                    {
                        NetworkLog.LogWarning($"{nameof(NetworkPrefab)} (\"{NetworkConfig.NetworkPrefabs[i].Prefab.name}\") is missing a {nameof(NetworkObject)} component");
                    }

                    // Provide the name of the prefab with issues so the user can more easily find the prefab and fix it
                    UnityEngine.Debug.LogWarning($"{nameof(NetworkPrefab)} (\"{NetworkConfig.NetworkPrefabs[i].Prefab.name}\") will be removed and ignored.");
                    removeEmptyPrefabs.Add(i);

                    continue;
                }

                var networkObject = NetworkConfig.NetworkPrefabs[i].Prefab.GetComponent<NetworkObject>();

                // Assign the appropriate GlobalObjectIdHash to the appropriate NetworkPrefab
                if (!NetworkConfig.NetworkPrefabOverrideLinks.ContainsKey(networkObject.GlobalObjectIdHash))
                {
                    switch (NetworkConfig.NetworkPrefabs[i].Override)
                    {
                        default:
                        case NetworkPrefabOverride.None:
                            NetworkConfig.NetworkPrefabOverrideLinks.Add(networkObject.GlobalObjectIdHash, NetworkConfig.NetworkPrefabs[i]);
                            break;
                        case NetworkPrefabOverride.Prefab:
                            NetworkConfig.NetworkPrefabOverrideLinks.Add(NetworkConfig.NetworkPrefabs[i].SourcePrefabToOverride.GetComponent<NetworkObject>().GlobalObjectIdHash, NetworkConfig.NetworkPrefabs[i]);
                            break;
                        case NetworkPrefabOverride.Hash:
                            NetworkConfig.NetworkPrefabOverrideLinks.Add(NetworkConfig.NetworkPrefabs[i].SourceHashToOverride, NetworkConfig.NetworkPrefabs[i]);
                            break;
                    }
                }
                else
                {
                    // This should never happen, but in the case it somehow does log an error and remove the duplicate entry
                    UnityEngine.Debug.LogError($"{nameof(NetworkPrefab)} (\"{NetworkConfig.NetworkPrefabs[i].Prefab.name}\") has a duplicate {nameof(NetworkObject.GlobalObjectIdHash)} {networkObject.GlobalObjectIdHash} entry! Removing entry from list!");
                    removeEmptyPrefabs.Add(i);
                }
            }

            // If we have a player prefab, then we need to verify it is in the list of NetworkPrefabOverrideLinks for client side spawning.
            if (NetworkConfig.PlayerPrefab != null)
            {
                var playerPrefabNetworkObject = NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>();
                if (playerPrefabNetworkObject != null)
                {
                    //In the event there is no NetworkPrefab entry (i.e. no override for default player prefab)
                    if (!NetworkConfig.NetworkPrefabOverrideLinks.ContainsKey(playerPrefabNetworkObject.GlobalObjectIdHash))
                    {
                        //Then add a new entry for the player prefab
                        var playerNetworkPrefab = new NetworkPrefab();
                        playerNetworkPrefab.Prefab = NetworkConfig.PlayerPrefab;
                        NetworkConfig.NetworkPrefabs.Insert(0, playerNetworkPrefab);
                        NetworkConfig.NetworkPrefabOverrideLinks.Add(playerPrefabNetworkObject.GlobalObjectIdHash, playerNetworkPrefab);
                    }
                }
                else
                {
                    // Provide the name of the prefab with issues so the user can more easily find the prefab and fix it
                    UnityEngine.Debug.LogError($"{nameof(NetworkConfig.PlayerPrefab)} (\"{NetworkConfig.PlayerPrefab.name}\") has no NetworkObject assigned to it!.");
                }
            }

            // Clear out anything that is invalid or not used (for invalid entries we already logged warnings to the user earlier)
            foreach (var networkPrefabIndexToRemove in removeEmptyPrefabs)
            {
                NetworkConfig.NetworkPrefabs.RemoveAt(networkPrefabIndexToRemove);
            }
            removeEmptyPrefabs.Clear();

            NetworkConfig.NetworkTransport.OnTransportEvent += HandleRawTransportPoll;

            NetworkConfig.NetworkTransport.ResetChannelCache();

            NetworkConfig.NetworkTransport.Init();

            ProfilerNotifier.Initialize(this);
        }

        /// <summary>
        /// Starts a server
        /// </summary>
        public SocketTasks StartServer()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo("StartServer()");
            }

            if (IsServer || IsClient)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Cannot start server while an instance is already running");
                }

                return SocketTask.Fault.AsTasks();
            }

            if (NetworkConfig.ConnectionApproval)
            {
                if (ConnectionApprovalCallback == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning("No ConnectionApproval callback defined. Connection approval will timeout");
                    }
                }
            }

            Init(true);

            var socketTasks = NetworkConfig.NetworkTransport.StartServer();

            IsServer = true;
            IsClient = false;
            IsListening = true;

            SpawnManager.ServerSpawnSceneObjectsOnStartSweep();

            OnServerStarted?.Invoke();

            return socketTasks;
        }

        /// <summary>
        /// Starts a client
        /// </summary>
        public SocketTasks StartClient()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(StartClient));
            }

            if (IsServer || IsClient)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Cannot start client while an instance is already running");
                }

                return SocketTask.Fault.AsTasks();
            }

            Init(false);

            var socketTasks = NetworkConfig.NetworkTransport.StartClient();

            IsServer = false;
            IsClient = true;
            IsListening = true;

            return socketTasks;
        }

        /// <summary>
        /// Starts a Host
        /// </summary>
        public SocketTasks StartHost()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(StartHost));
            }

            if (IsServer || IsClient)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning("Cannot start host while an instance is already running");
                }

                return SocketTask.Fault.AsTasks();
            }

            if (NetworkConfig.ConnectionApproval)
            {
                if (ConnectionApprovalCallback == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning("No ConnectionApproval callback defined. Connection approval will timeout");
                    }
                }
            }

            Init(true);

            var socketTasks = NetworkConfig.NetworkTransport.StartServer();

            IsServer = true;
            IsClient = true;
            IsListening = true;

            if (NetworkConfig.ConnectionApproval)
            {
                InvokeConnectionApproval(NetworkConfig.ConnectionData, ServerClientId, (createPlayerObject, playerPrefabHash, approved, position, rotation) =>
                {
                    // You cannot decline the local server. Force approved to true
                    if (!approved)
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                        {
                            NetworkLog.LogWarning("You cannot decline the host connection. The connection was automatically approved.");
                        }
                    }

                    HandleApproval(ServerClientId, createPlayerObject, playerPrefabHash, true, position, rotation);
                });
            }
            else
            {
                HandleApproval(ServerClientId, NetworkConfig.PlayerPrefab != null, null, true, null, null);
            }

            SpawnManager.ServerSpawnSceneObjectsOnStartSweep();

            OnServerStarted?.Invoke();

            return socketTasks;
        }

        public void SetSingleton()
        {
            Singleton = this;

            OnSingletonReady?.Invoke();
        }

        private void Awake()
        {
            PrefabHandler = new NetworkPrefabHandler();
        }

        private void OnEnable()
        {
            if (DontDestroy)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (RunInBackground)
            {
                Application.runInBackground = true;
            }

            if (Singleton == null)
            {
                SetSingleton();
            }
        }

        private void OnDestroy()
        {
            Shutdown();

            if (Singleton == this)
            {
                Singleton = null;
            }
        }

        public void Shutdown()
        {
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo(nameof(Shutdown));
            }

            if (IsServer)
            {
                // Before any actual shutdown work is done. Notify the transport of the disconnects to give clients a chance to receive a disconnect message.

                foreach (KeyValuePair<ulong, NetworkClient> pair in ConnectedClients)
                {
                    if (pair.Key == NetworkConfig.NetworkTransport.ServerClientId)
                    {
                        continue;
                    }

                    NetworkConfig.NetworkTransport.DisconnectRemoteClient(pair.Key);
                }

                foreach (KeyValuePair<ulong, PendingClient> pair in PendingClients)
                {
                    if (pair.Key == NetworkConfig.NetworkTransport.ServerClientId)
                    {
                        continue;
                    }

                    NetworkConfig.NetworkTransport.DisconnectRemoteClient(pair.Key);
                }
            }
            else
            {
                // Disconnect the local client cleanly
                NetworkConfig.NetworkTransport.DisconnectLocalClient();
            }

            // Unregister INetworkUpdateSystem before shutting down the RpcQueueContainer
            this.UnregisterAllNetworkUpdates();

            //If an instance of the RpcQueueContainer is still around, then shut it down and remove the reference
            if (RpcQueueContainer != null)
            {
                RpcQueueContainer.Dispose();
                RpcQueueContainer = null;
            }

            if (NetworkTickSystem != null)
            {
                NetworkTickSystem.Dispose();
                NetworkTickSystem = null;
            }

#if !UNITY_2020_2_OR_NEWER
            NetworkProfiler.Stop();
#endif
            IsListening = false;
            IsServer = false;
            IsClient = false;
            IsConnectedClient = false;

            NetworkConfig.NetworkTransport.OnTransportEvent -= HandleRawTransportPoll;

            if (BufferManager != null)
            {
                BufferManager = null;
            }

            if (SpawnManager != null)
            {
                SpawnManager.DestroyNonSceneObjects();
                SpawnManager.ServerResetShudownStateForSceneObjects();

                SpawnManager = null;
            }

            if (SceneManager != null)
            {
                SceneManager = null;
            }

            if (MessageHandler != null)
            {
                MessageHandler = null;
            }

            if (MessageSender != null)
            {
                MessageSender = null;
            }

            if (CustomMessagingManager != null)
            {
                CustomMessagingManager = null;
            }

            //The Transport is set during Init time, thus it is possible for the Transport to be null
            NetworkConfig?.NetworkTransport?.Shutdown();
        }

        // INetworkUpdateSystem
        public void NetworkUpdate(NetworkUpdateStage updateStage)
        {
            switch (updateStage)
            {
                case NetworkUpdateStage.EarlyUpdate:
                    OnNetworkEarlyUpdate();
                    break;
                case NetworkUpdateStage.PreUpdate:
                    OnNetworkPreUpdate();
                    break;
            }
        }

        private float m_LastReceiveTickTime;
        private float m_LastEventTickTime;
        private float m_LastTimeSyncTime;

        private void OnNetworkEarlyUpdate()
        {
            NotifyProfilerListeners();
            ProfilerBeginTick();

            if (IsListening)
            {
                // Process received data
                if ((NetworkTime - m_LastReceiveTickTime >= (1f / NetworkConfig.ReceiveTickrate)) || NetworkConfig.ReceiveTickrate <= 0)
                {
                    PerformanceDataManager.Increment(ProfilerConstants.ReceiveTickRate);
                    ProfilerStatManager.RcvTickRate.Record();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    s_ReceiveTick.Begin();
#endif
                    var isLoopBack = false;

#if !UNITY_2020_2_OR_NEWER
                    NetworkProfiler.StartTick(TickType.Receive);
#endif

                    //If we are in loopback mode, we don't need to touch the transport
                    if (!isLoopBack)
                    {
                        NetworkEvent networkEvent;
                        int processedEvents = 0;
                        do
                        {
                            processedEvents++;
                            networkEvent = NetworkConfig.NetworkTransport.PollEvent(out ulong clientId, out NetworkChannel networkChannel, out ArraySegment<byte> payload, out float receiveTime);
                            HandleRawTransportPoll(networkEvent, clientId, networkChannel, payload, receiveTime);

                            // Only do another iteration if: there are no more messages AND (there is no limit to max events or we have processed less than the maximum)
                        } while (IsListening && (networkEvent != NetworkEvent.Nothing) && (NetworkConfig.MaxReceiveEventsPerTickRate <= 0 || processedEvents < NetworkConfig.MaxReceiveEventsPerTickRate));
                    }

                    m_LastReceiveTickTime = NetworkTime;

#if !UNITY_2020_2_OR_NEWER
                    NetworkProfiler.EndTick();
#endif

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    s_ReceiveTick.End();
#endif
                }
            }
        }

        private void OnNetworkPreUpdate()
        {
            if (IsListening)
            {
                if (((NetworkTime - m_LastEventTickTime >= (1f / NetworkConfig.EventTickrate))))
                {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    s_EventTick.Begin();
#endif
#if UNITY_EDITOR && !UNITY_2020_2_OR_NEWER
                    NetworkProfiler.StartTick(TickType.Event);
#endif

                    if (NetworkConfig.EnableNetworkVariable)
                    {
                        // Do NetworkVariable updates
                        NetworkBehaviour.NetworkBehaviourUpdate(this);
                    }

                    if (!IsServer && NetworkConfig.EnableMessageBuffering)
                    {
                        BufferManager.CleanBuffer();
                    }

                    if (IsServer)
                    {
                        m_LastEventTickTime = NetworkTime;
                    }
#if UNITY_EDITOR && !UNITY_2020_2_OR_NEWER
                    NetworkProfiler.EndTick();
#endif

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    s_EventTick.End();
#endif
                }

                if (IsServer && NetworkConfig.EnableTimeResync && NetworkTime - m_LastTimeSyncTime >= NetworkConfig.TimeResyncInterval)
                {
#if UNITY_EDITOR && !UNITY_2020_2_OR_NEWER
                    NetworkProfiler.StartTick(TickType.Event);
#endif
                    SyncTime();
                    m_LastTimeSyncTime = NetworkTime;
#if UNITY_EDITOR && !UNITY_2020_2_OR_NEWER
                    NetworkProfiler.EndTick();
#endif
                }

                if (!Mathf.Approximately(m_NetworkTimeOffset, m_CurrentNetworkTimeOffset))
                {
                    // Smear network time adjustments by no more than 200ms per second.  This should help code deal with
                    // changes more gracefully, since the network time will always flow forward at a reasonable pace.
                    float maxDelta = Mathf.Max(0.001f, 0.2f * Time.unscaledDeltaTime);
                    m_CurrentNetworkTimeOffset += Mathf.Clamp(m_NetworkTimeOffset - m_CurrentNetworkTimeOffset, -maxDelta, maxDelta);
                }
            }
        }

        internal void UpdateNetworkTime(ulong clientId, float netTime, float receiveTime, bool warp = false)
        {
            float rtt = NetworkConfig.NetworkTransport.GetCurrentRtt(clientId) / 1000f;
            m_NetworkTimeOffset = netTime - receiveTime + rtt / 2f;

            if (warp)
            {
                m_CurrentNetworkTimeOffset = m_NetworkTimeOffset;
            }

            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo($"Received network time {netTime}, RTT to server is {rtt}, {(warp ? "setting" : "smearing")} offset to {m_NetworkTimeOffset} (delta {m_NetworkTimeOffset - m_CurrentNetworkTimeOffset})");
            }
        }

        private void SendConnectionRequest()
        {
            using (var buffer = PooledNetworkBuffer.Get())
            using (var writer = PooledNetworkWriter.Get(buffer))
            {
                writer.WriteUInt64Packed(NetworkConfig.GetConfig());

                if (NetworkConfig.ConnectionApproval)
                {
                    writer.WriteByteArray(NetworkConfig.ConnectionData);
                }

                MessageSender.Send(ServerClientId, NetworkConstants.CONNECTION_REQUEST, NetworkChannel.Internal, buffer);
            }
        }

        private IEnumerator ApprovalTimeout(ulong clientId)
        {
            float timeStarted = NetworkTime;

            //We yield every frame incase a pending client disconnects and someone else gets its connection id
            while (NetworkTime - timeStarted < NetworkConfig.ClientConnectionBufferTimeout && PendingClients.ContainsKey(clientId))
            {
                yield return null;
            }

            if (PendingClients.ContainsKey(clientId) && !ConnectedClients.ContainsKey(clientId))
            {
                // Timeout
                if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                {
                    NetworkLog.LogInfo($"Client {clientId} Handshake Timed Out");
                }

                DisconnectClient(clientId);
            }
        }

        internal IEnumerator TimeOutSwitchSceneProgress(SceneSwitchProgress switchSceneProgress)
        {
            yield return new WaitForSecondsRealtime(NetworkConfig.LoadSceneTimeOut);
            switchSceneProgress.SetTimedOut();
        }

        private void HandleRawTransportPoll(NetworkEvent networkEvent, ulong clientId, NetworkChannel networkChannel, ArraySegment<byte> payload, float receiveTime)
        {
            PerformanceDataManager.Increment(ProfilerConstants.ByteReceived, payload.Count);
            ProfilerStatManager.BytesRcvd.Record(payload.Count);

            switch (networkEvent)
            {
                case NetworkEvent.Connect:
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    s_TransportConnect.Begin();
#endif
#if !UNITY_2020_2_OR_NEWER
                    NetworkProfiler.StartEvent(TickType.Receive, (uint)payload.Count, networkChannel, "TRANSPORT_CONNECT");
#endif
                    if (IsServer)
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                        {
                            NetworkLog.LogInfo("Client Connected");
                        }

                        PendingClients.Add(clientId, new PendingClient()
                        {
                            ClientId = clientId,
                            ConnectionState = PendingClient.State.PendingConnection
                        });

                        StartCoroutine(ApprovalTimeout(clientId));
                    }
                    else
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                        {
                            NetworkLog.LogInfo("Connected");
                        }

                        SendConnectionRequest();
                        StartCoroutine(ApprovalTimeout(clientId));
                    }

#if !UNITY_2020_2_OR_NEWER
                    NetworkProfiler.EndEvent();
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    s_TransportConnect.End();
#endif
                    break;
                case NetworkEvent.Data:
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                        {
                            NetworkLog.LogInfo($"Incoming Data From {clientId}: {payload.Count} bytes");
                        }

                        HandleIncomingData(clientId, networkChannel, payload, receiveTime, true);
                        break;
                    }
                case NetworkEvent.Disconnect:
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    s_TransportDisconnect.Begin();
#endif
#if !UNITY_2020_2_OR_NEWER
                    NetworkProfiler.StartEvent(TickType.Receive, 0, NetworkChannel.Internal, "TRANSPORT_DISCONNECT");
#endif

                    if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                    {
                        NetworkLog.LogInfo($"Disconnect Event From {clientId}");
                    }

                    if (IsServer)
                    {
                        OnClientDisconnectFromServer(clientId);
                    }
                    else
                    {
                        Shutdown();
                    }

                    OnClientDisconnectCallback?.Invoke(clientId);

#if !UNITY_2020_2_OR_NEWER
                    NetworkProfiler.EndEvent();
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    s_TransportDisconnect.End();
#endif
                    break;
            }
        }

        private readonly NetworkBuffer m_InputBufferWrapper = new NetworkBuffer(new byte[0]);
        private readonly RpcBatcher m_RpcBatcher = new RpcBatcher();

        internal void HandleIncomingData(ulong clientId, NetworkChannel networkChannel, ArraySegment<byte> data, float receiveTime, bool allowBuffer)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_HandleIncomingData.Begin();
#endif
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo("Unwrapping Data Header");
            }

            m_InputBufferWrapper.SetTarget(data.Array);
            m_InputBufferWrapper.SetLength(data.Count + data.Offset);
            m_InputBufferWrapper.Position = data.Offset;

            using (var messageStream = MessagePacker.UnwrapMessage(m_InputBufferWrapper, out byte messageType))
            {
                if (messageStream == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                    {
                        NetworkLog.LogError("Message unwrap could not be completed. Was the header corrupt?");
                    }

                    return;
                }

                if (messageType == NetworkConstants.INVALID)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                    {
                        NetworkLog.LogError($"Message unwrap read an invalid {nameof(messageType)}");
                    }

                    return;
                }

                uint headerByteSize = (uint)Arithmetic.VarIntSize(messageType);

#if !UNITY_2020_2_OR_NEWER
                NetworkProfiler.StartEvent(TickType.Receive, (uint)(data.Count - headerByteSize), networkChannel, messageType);
#endif

                if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                {
                    NetworkLog.LogInfo($"Data Header: {nameof(messageType)}={messageType}");
                }

                // Client tried to send a network message that was not the connection request before he was accepted.
                if (PendingClients.ContainsKey(clientId) && (PendingClients[clientId].ConnectionState == PendingClient.State.PendingApproval || (PendingClients[clientId].ConnectionState == PendingClient.State.PendingConnection && messageType != NetworkConstants.CONNECTION_REQUEST)))
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning($"Message received from {nameof(clientId)}={clientId} before it has been accepted");
                    }

                    return;
                }

                switch (messageType)
                {
                    case NetworkConstants.CONNECTION_REQUEST:
                        if (IsServer)
                        {
                            MessageHandler.HandleConnectionRequest(clientId, messageStream);
                        }

                        break;
                    case NetworkConstants.CONNECTION_APPROVED:
                        if (IsClient)
                        {
                            MessageHandler.HandleConnectionApproved(clientId, messageStream, receiveTime);
                        }

                        break;
                    case NetworkConstants.ADD_OBJECT:
                        if (IsClient)
                        {
                            MessageHandler.HandleAddObject(clientId, messageStream);
                        }

                        break;
                    case NetworkConstants.DESTROY_OBJECT:
                        if (IsClient)
                        {
                            MessageHandler.HandleDestroyObject(clientId, messageStream);
                        }

                        break;
                    case NetworkConstants.SWITCH_SCENE:
                        if (IsClient)
                        {
                            MessageHandler.HandleSwitchScene(clientId, messageStream);
                        }

                        break;
                    case NetworkConstants.CHANGE_OWNER:
                        if (IsClient)
                        {
                            MessageHandler.HandleChangeOwner(clientId, messageStream);
                        }

                        break;
                    case NetworkConstants.ADD_OBJECTS:
                        if (IsClient)
                        {
                            MessageHandler.HandleAddObjects(clientId, messageStream);
                        }

                        break;
                    case NetworkConstants.DESTROY_OBJECTS:
                        if (IsClient)
                        {
                            MessageHandler.HandleDestroyObjects(clientId, messageStream);
                        }

                        break;
                    case NetworkConstants.TIME_SYNC:
                        if (IsClient)
                        {
                            MessageHandler.HandleTimeSync(clientId, messageStream, receiveTime);
                        }

                        break;
                    case NetworkConstants.NETWORK_VARIABLE_DELTA:
                        MessageHandler.HandleNetworkVariableDelta(clientId, messageStream, BufferCallback, new PreBufferPreset()
                        {
                            AllowBuffer = allowBuffer,
                            NetworkChannel = networkChannel,
                            ClientId = clientId,
                            Data = data,
                            MessageType = messageType,
                            ReceiveTime = receiveTime
                        });
                        break;
                    case NetworkConstants.NETWORK_VARIABLE_UPDATE:
                        MessageHandler.HandleNetworkVariableUpdate(clientId, messageStream, BufferCallback, new PreBufferPreset()
                        {
                            AllowBuffer = allowBuffer,
                            NetworkChannel = networkChannel,
                            ClientId = clientId,
                            Data = data,
                            MessageType = messageType,
                            ReceiveTime = receiveTime
                        });
                        break;
                    case NetworkConstants.UNNAMED_MESSAGE:
                        MessageHandler.HandleUnnamedMessage(clientId, messageStream);
                        break;
                    case NetworkConstants.NAMED_MESSAGE:
                        MessageHandler.HandleNamedMessage(clientId, messageStream);
                        break;
                    case NetworkConstants.CLIENT_SWITCH_SCENE_COMPLETED:
                        if (IsServer && NetworkConfig.EnableSceneManagement)
                        {
                            MessageHandler.HandleClientSwitchSceneCompleted(clientId, messageStream);
                        }
                        else if (!NetworkConfig.EnableSceneManagement)
                        {
                            NetworkLog.LogWarning($"Server received {nameof(NetworkConstants.CLIENT_SWITCH_SCENE_COMPLETED)} from client id {clientId}");
                        }

                        break;
                    case NetworkConstants.SERVER_LOG:
                        if (IsServer && NetworkConfig.EnableNetworkLogs)
                        {
                            MessageHandler.HandleNetworkLog(clientId, messageStream);
                        }

                        break;
                    case NetworkConstants.SERVER_RPC:
                        {
                            if (IsServer)
                            {
                                if (RpcQueueContainer.IsUsingBatching())
                                {
                                    m_RpcBatcher.ReceiveItems(messageStream, ReceiveCallback, RpcQueueContainer.QueueItemType.ServerRpc, clientId, receiveTime);
                                    ProfilerStatManager.RpcBatchesRcvd.Record();
                                    PerformanceDataManager.Increment(ProfilerConstants.RpcBatchesReceived);
                                }
                                else
                                {
                                    MessageHandler.RpcReceiveQueueItem(clientId, messageStream, receiveTime, RpcQueueContainer.QueueItemType.ServerRpc);
                                }
                            }

                            break;
                        }
                    case NetworkConstants.CLIENT_RPC:
                        {
                            if (IsClient)
                            {
                                if (RpcQueueContainer.IsUsingBatching())
                                {
                                    m_RpcBatcher.ReceiveItems(messageStream, ReceiveCallback, RpcQueueContainer.QueueItemType.ClientRpc, clientId, receiveTime);
                                    ProfilerStatManager.RpcBatchesRcvd.Record();
                                    PerformanceDataManager.Increment(ProfilerConstants.RpcBatchesReceived);
                                }
                                else
                                {
                                    MessageHandler.RpcReceiveQueueItem(clientId, messageStream, receiveTime, RpcQueueContainer.QueueItemType.ClientRpc);
                                }
                            }

                            break;
                        }
                    default:
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                        {
                            NetworkLog.LogError($"Read unrecognized {nameof(messageType)}={messageType}");
                        }

                        break;
                }

#if !UNITY_2020_2_OR_NEWER
                NetworkProfiler.EndEvent();
#endif
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_HandleIncomingData.End();
#endif
        }

        private void ReceiveCallback(NetworkBuffer messageBuffer, RpcQueueContainer.QueueItemType messageType, ulong clientId, float receiveTime)
        {
            MessageHandler.RpcReceiveQueueItem(clientId, messageBuffer, receiveTime, messageType);
        }

        /// <summary>
        /// InvokeRPC
        /// Called when an inbound queued RPC is invoked
        /// </summary>
        /// <param name="queueItem">frame queue item to invoke</param>
#pragma warning disable 618
        internal void InvokeRpc(RpcFrameQueueItem queueItem)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_InvokeRpc.Begin();
#endif
            var networkObjectId = queueItem.NetworkReader.ReadUInt64Packed();
            var networkBehaviourId = queueItem.NetworkReader.ReadUInt16Packed();
            var networkUpdateStage = queueItem.NetworkReader.ReadByteDirect();
            var networkMethodId = queueItem.NetworkReader.ReadUInt32Packed();

            if (__ntable.ContainsKey(networkMethodId))
            {
                if (!SpawnManager.SpawnedObjects.ContainsKey(networkObjectId))
                {
                    return;
                }

                var networkObject = SpawnManager.SpawnedObjects[networkObjectId];

                var networkBehaviour = networkObject.GetNetworkBehaviourAtOrderIndex(networkBehaviourId);
                if (networkBehaviour == null)
                {
                    return;
                }

                var rpcParams = new __RpcParams();
                switch (queueItem.QueueItemType)
                {
                    case RpcQueueContainer.QueueItemType.ServerRpc:
                        rpcParams.Server = new ServerRpcParams
                        {
                            Receive = new ServerRpcReceiveParams
                            {
                                UpdateStage = (NetworkUpdateStage)networkUpdateStage,
                                SenderClientId = queueItem.NetworkId
                            }
                        };
                        break;
                    case RpcQueueContainer.QueueItemType.ClientRpc:
                        rpcParams.Client = new ClientRpcParams
                        {
                            Receive = new ClientRpcReceiveParams
                            {
                                UpdateStage = (NetworkUpdateStage)networkUpdateStage
                            }
                        };
                        break;
                }

                __ntable[networkMethodId](networkBehaviour, new NetworkSerializer(queueItem.NetworkReader), rpcParams);
            }
#pragma warning restore 618

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_InvokeRpc.End();
#endif
        }

        private void BufferCallback(ulong networkId, PreBufferPreset preset)
        {
            if (!preset.AllowBuffer)
            {
                // This is to prevent recursive buffering
                if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                {
                    NetworkLog.LogError($"A message of type {NetworkConstants.MESSAGE_NAMES[preset.MessageType]} was recursivley buffered. It has been dropped.");
                }

                return;
            }

            if (!NetworkConfig.EnableMessageBuffering)
            {
                throw new InvalidOperationException("Cannot buffer with buffering disabled.");
            }

            if (IsServer)
            {
                throw new InvalidOperationException("Cannot buffer on server.");
            }

            BufferManager.BufferMessageForNetworkId(networkId, preset.ClientId, preset.NetworkChannel, preset.ReceiveTime, preset.Data);
        }

        /// <summary>
        /// Disconnects the remote client.
        /// </summary>
        /// <param name="clientId">The ClientId to disconnect</param>
        public void DisconnectClient(ulong clientId)
        {
            if (!IsServer)
            {
                throw new NotServerException("Only server can disconnect remote clients. Use StopClient instead.");
            }

            if (ConnectedClients.ContainsKey(clientId))
            {
                ConnectedClients.Remove(clientId);
            }

            if (PendingClients.ContainsKey(clientId))
            {
                PendingClients.Remove(clientId);
            }

            for (int i = ConnectedClientsList.Count - 1; i > -1; i--)
            {
                if (ConnectedClientsList[i].ClientId == clientId)
                {
                    ConnectedClientsList.RemoveAt(i);
                    PerformanceDataManager.Increment(ProfilerConstants.Connections, -1);
                    ProfilerStatManager.Connections.Record(-1);
                }
            }

            NetworkConfig.NetworkTransport.DisconnectRemoteClient(clientId);
        }

        internal void OnClientDisconnectFromServer(ulong clientId)
        {
            if (PendingClients.ContainsKey(clientId))
            {
                PendingClients.Remove(clientId);
            }

            if (ConnectedClients.ContainsKey(clientId))
            {
                if (IsServer)
                {
                    if (ConnectedClients[clientId].PlayerObject != null)
                    {
                        if (PrefabHandler.ContainsHandler(ConnectedClients[clientId].PlayerObject.GlobalObjectIdHash))
                        {
                            PrefabHandler.HandleNetworkPrefabDestroy(ConnectedClients[clientId].PlayerObject);
                            SpawnManager.OnDestroyObject(ConnectedClients[clientId].PlayerObject.NetworkObjectId, false);
                        }
                        else
                        {
                            Destroy(ConnectedClients[clientId].PlayerObject.gameObject);
                        }
                    }

                    for (int i = 0; i < ConnectedClients[clientId].OwnedObjects.Count; i++)
                    {
                        if (ConnectedClients[clientId].OwnedObjects[i] != null)
                        {
                            if (!ConnectedClients[clientId].OwnedObjects[i].DontDestroyWithOwner)
                            {

                                if (PrefabHandler.ContainsHandler(ConnectedClients[clientId].OwnedObjects[i].GlobalObjectIdHash))
                                {
                                    PrefabHandler.HandleNetworkPrefabDestroy(ConnectedClients[clientId].OwnedObjects[i]);
                                    SpawnManager.OnDestroyObject(ConnectedClients[clientId].OwnedObjects[i].NetworkObjectId, false);
                                }
                                else
                                {
                                    Destroy(ConnectedClients[clientId].OwnedObjects[i].gameObject);
                                }
                            }
                            else
                            {
                                ConnectedClients[clientId].OwnedObjects[i].RemoveOwnership();
                            }
                        }
                    }

                    // TODO: Could(should?) be replaced with more memory per client, by storing the visiblity

                    foreach (var sobj in SpawnManager.SpawnedObjectsList)
                    {
                        sobj.Observers.Remove(clientId);
                    }
                }

                for (int i = 0; i < ConnectedClientsList.Count; i++)
                {
                    if (ConnectedClientsList[i].ClientId == clientId)
                    {
                        ConnectedClientsList.RemoveAt(i);
                        PerformanceDataManager.Increment(ProfilerConstants.Connections, -1);
                        ProfilerStatManager.Connections.Record(-1);
                        break;
                    }
                }

                ConnectedClients.Remove(clientId);
            }
        }

        private void SyncTime()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_SyncTime.Begin();
#endif
            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
            {
                NetworkLog.LogInfo("Syncing Time To Clients");
            }

            using (var buffer = PooledNetworkBuffer.Get())
            using (var writer = PooledNetworkWriter.Get(buffer))
            {
                writer.WriteSinglePacked(Time.realtimeSinceStartup);
                MessageSender.Send(NetworkConstants.TIME_SYNC, NetworkChannel.SyncChannel, buffer);
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_SyncTime.End();
#endif
        }

        private readonly List<NetworkObject> m_ObservedObjects = new List<NetworkObject>();

        internal void HandleApproval(ulong ownerClientId, bool createPlayerObject, uint? playerPrefabHash, bool approved, Vector3? position, Quaternion? rotation)
        {
            if (approved)
            {
                // Inform new client it got approved
                if (PendingClients.ContainsKey(ownerClientId))
                {
                    PendingClients.Remove(ownerClientId);
                }

                var client = new NetworkClient { ClientId = ownerClientId, };
                ConnectedClients.Add(ownerClientId, client);
                ConnectedClientsList.Add(client);

                PerformanceDataManager.Increment(ProfilerConstants.Connections);
                ProfilerStatManager.Connections.Record();

                // This packet is unreliable, but if it gets through it should provide a much better sync than the potentially huge approval message.
                SyncTime();

                if (createPlayerObject)
                {
                    var networkObject = SpawnManager.CreateLocalNetworkObject(false, playerPrefabHash ?? NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash, ownerClientId, null, position, rotation);
                    SpawnManager.SpawnNetworkObjectLocally(networkObject, SpawnManager.GetNetworkObjectId(), false, true, ownerClientId, null, false, 0, false, false);

                    ConnectedClients[ownerClientId].PlayerObject = networkObject;
                }

                m_ObservedObjects.Clear();

                foreach (var sobj in SpawnManager.SpawnedObjectsList)
                {
                    if (ownerClientId == ServerClientId || sobj.CheckObjectVisibility == null || sobj.CheckObjectVisibility(ownerClientId))
                    {
                        m_ObservedObjects.Add(sobj);
                        sobj.Observers.Add(ownerClientId);
                    }
                }

                if (ownerClientId != ServerClientId)
                {
                    // Don't send any data over the wire if the host "connected"
                    using (var buffer = PooledNetworkBuffer.Get())
                    using (var writer = PooledNetworkWriter.Get(buffer))
                    {
                        writer.WriteUInt64Packed(ownerClientId);

                        if (NetworkConfig.EnableSceneManagement)
                        {
                            writer.WriteUInt32Packed(NetworkSceneManager.CurrentSceneIndex);
                            writer.WriteByteArray(NetworkSceneManager.CurrentSceneSwitchProgressGuid.ToByteArray());
                        }

                        writer.WriteSinglePacked(Time.realtimeSinceStartup);
                        writer.WriteUInt32Packed((uint)m_ObservedObjects.Count);

                        for (int i = 0; i < m_ObservedObjects.Count; i++)
                        {
                            m_ObservedObjects[i].SerializeSceneObject(writer, ownerClientId);
                        }

                        MessageSender.Send(ownerClientId, NetworkConstants.CONNECTION_APPROVED, NetworkChannel.Internal, buffer);
                    }
                }

                OnClientConnectedCallback?.Invoke(ownerClientId);

                if (!createPlayerObject || (playerPrefabHash == null && NetworkConfig.PlayerPrefab == null))
                {
                    return;
                }

                // Inform old clients of the new player
                foreach (KeyValuePair<ulong, NetworkClient> clientPair in ConnectedClients)
                {
                    if (clientPair.Key == ownerClientId ||
                        ConnectedClients[ownerClientId].PlayerObject == null ||
                        !ConnectedClients[ownerClientId].PlayerObject.Observers.Contains(clientPair.Key))
                    {
                        continue; //The new client.
                    }

                    using (var buffer = PooledNetworkBuffer.Get())
                    using (var writer = PooledNetworkWriter.Get(buffer))
                    {
                        writer.WriteBool(true);
                        writer.WriteUInt64Packed(ConnectedClients[ownerClientId].PlayerObject.NetworkObjectId);
                        writer.WriteUInt64Packed(ownerClientId);

                        //Does not have a parent
                        writer.WriteBool(false);

                        // This is not a scene object
                        writer.WriteBool(false);

                        writer.WriteUInt32Packed(playerPrefabHash ?? NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>().GlobalObjectIdHash);

                        if (ConnectedClients[ownerClientId].PlayerObject.IncludeTransformWhenSpawning == null || ConnectedClients[ownerClientId].PlayerObject.IncludeTransformWhenSpawning(ownerClientId))
                        {
                            writer.WriteBool(true);
                            writer.WriteSinglePacked(ConnectedClients[ownerClientId].PlayerObject.transform.position.x);
                            writer.WriteSinglePacked(ConnectedClients[ownerClientId].PlayerObject.transform.position.y);
                            writer.WriteSinglePacked(ConnectedClients[ownerClientId].PlayerObject.transform.position.z);

                            writer.WriteSinglePacked(ConnectedClients[ownerClientId].PlayerObject.transform.rotation.eulerAngles.x);
                            writer.WriteSinglePacked(ConnectedClients[ownerClientId].PlayerObject.transform.rotation.eulerAngles.y);
                            writer.WriteSinglePacked(ConnectedClients[ownerClientId].PlayerObject.transform.rotation.eulerAngles.z);
                        }
                        else
                        {
                            writer.WriteBool(false);
                        }

                        writer.WriteBool(false); //No payload data

                        if (NetworkConfig.EnableNetworkVariable)
                        {
                            ConnectedClients[ownerClientId].PlayerObject.WriteNetworkVariableData(buffer, clientPair.Key);
                        }

                        MessageSender.Send(clientPair.Key, NetworkConstants.ADD_OBJECT, NetworkChannel.Internal, buffer);
                    }
                }
            }
            else
            {
                if (PendingClients.ContainsKey(ownerClientId))
                {
                    PendingClients.Remove(ownerClientId);
                }

                NetworkConfig.NetworkTransport.DisconnectRemoteClient(ownerClientId);
            }
        }

        private void ProfilerBeginTick()
        {
            ProfilerNotifier.ProfilerBeginTick();
        }

        private void NotifyProfilerListeners()
        {
            ProfilerNotifier.NotifyProfilerListeners();
        }

        public ITransportProfilerData Transport
        {
            get { return NetworkConfig.NetworkTransport as ITransportProfilerData; }
        }
    }
}
