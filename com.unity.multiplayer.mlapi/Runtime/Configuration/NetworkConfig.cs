using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using MLAPI.Transports;
using MLAPI.Hashing;
using MLAPI.Serialization;
using MLAPI.Serialization.Pooled;

namespace MLAPI.Configuration
{
    /// <summary>
    /// The configuration object used to start server, client and hosts
    /// </summary>
    [Serializable]
    public class NetworkConfig
    {
        /// <summary>
        /// The protocol version. Different versions doesn't talk to each other.
        /// </summary>
        [Tooltip("Use this to make two builds incompatible with each other")]
        public ushort ProtocolVersion = 0;

        /// <summary>
        /// The transport hosts the sever uses
        /// </summary>
        [Tooltip("The NetworkTransport to use")]
        public NetworkTransport NetworkTransport = null;

        /// <summary>
        /// A list of SceneNames that can be used during networked games.
        /// </summary>
        [Tooltip("The Scenes that can be switched to by the server")]
        public List<string> RegisteredScenes = new List<string>();

        /// <summary>
        /// Whether or not runtime scene changes should be allowed and expected.
        /// If this is true, clients with different initial configurations will not work together.
        /// </summary>
        [Tooltip("Whether or not runtime scene changes should be allowed and expected.\n " +
                 "If this is true, clients with different initial configurations will not work together.")]
        public bool AllowRuntimeSceneChanges = false;

        /// <summary>
        /// The default player prefab
        /// </summary>
        [Tooltip("When set, NetworkManager will automatically create and spawn the assigned player prefab. This can be overridden by adding it to the NetworkPrefabs list and selecting override.")]
        public GameObject PlayerPrefab;

        /// <summary>
        /// A list of spawnable prefabs
        /// </summary>
        [SerializeField]
        [Tooltip("The prefabs that can be spawned across the network")]
        internal List<NetworkPrefab> NetworkPrefabs = new List<NetworkPrefab>();

        /// <summary>
        /// This dictionary provides a quick way to check and see if a NetworkPrefab has a NetworkPrefab override.
        /// Generated at runtime and OnValidate
        /// </summary>
        internal Dictionary<uint, NetworkPrefab> NetworkPrefabOverrideLinks = new Dictionary<uint, NetworkPrefab>();

        /// <summary>
        /// Amount of times per second the receive queue is emptied and all messages inside are processed.
        /// </summary>
        [Tooltip("The amount of times per second the receive queue is emptied from pending incoming messages")]
        public int ReceiveTickrate = 64;

        /// <summary>
        /// Duration in seconds between network ticks.
        /// </summary>
        [Tooltip("Duration in seconds between network ticks")]
        public float NetworkTickIntervalSec = 0.050f;

        /// <summary>
        /// The max amount of messages to process per ReceiveTickrate. This is to prevent flooding.
        /// </summary>
        [Tooltip("The maximum amount of Receive events to poll per Receive tick. This is to prevent flooding and freezing on the server")]
        public int MaxReceiveEventsPerTickRate = 500;

        /// <summary>
        /// The amount of times per second internal frame events will occur, e.g. send checking.
        /// </summary>
        [Tooltip("The amount of times per second the internal event loop will run. This includes for example NetworkVariable checking.")]
        public int EventTickrate = 64;

        /// <summary>
        /// The amount of seconds to wait for handshake to complete before timing out a client
        /// </summary>
        [Tooltip("The amount of seconds to wait for the handshake to complete before the client times out")]
        public int ClientConnectionBufferTimeout = 10;

        /// <summary>
        /// Whether or not to use connection approval
        /// </summary>
        [Tooltip("Whether or not to force clients to be approved before they connect")]
        public bool ConnectionApproval = false;

        /// <summary>
        /// The data to send during connection which can be used to decide on if a client should get accepted
        /// </summary>
        [Tooltip("The connection data sent along with connection requests")]
        public byte[] ConnectionData = new byte[0];

        /// <summary>
        /// If your logic uses the NetworkTime, this should probably be turned off. If however it's needed to maximize accuracy, this is recommended to be turned on
        /// </summary>
        [Tooltip("Enable this to re-sync the NetworkTime after the initial sync")]
        public bool EnableTimeResync = false;

        /// <summary>
        /// If time re-sync is turned on, this specifies the interval between syncs in seconds.
        /// </summary>
        [Tooltip("The amount of seconds between re-syncs of NetworkTime, if enabled")]
        public int TimeResyncInterval = 30;

        /// <summary>
        /// Whether or not to enable the NetworkVariable system. This system runs in the Update loop and will degrade performance, but it can be a huge convenience.
        /// Only turn it off if you have no need for the NetworkVariable system.
        /// </summary>
        [Tooltip("Whether or not to enable the NetworkVariable system")]
        public bool EnableNetworkVariable = true;

        /// <summary>
        /// Whether or not to ensure that NetworkVariables can be read even if a client accidentally writes where its not allowed to. This costs some CPU and bandwidth.
        /// </summary>
        [Tooltip("Ensures that NetworkVariables can be read even if a client accidental writes where its not allowed to. This will cost some CPU time and bandwidth")]
        public bool EnsureNetworkVariableLengthSafety = false;

        /// <summary>
        /// Enables scene management. This will allow network scene switches and automatic scene difference corrections upon connect.
        /// SoftSynced scene objects wont work with this disabled. That means that disabling SceneManagement also enables PrefabSync.
        /// </summary>
        [Tooltip("Enables scene management. This will allow network scene switches and automatic scene difference corrections upon connect.\n" +
                 "SoftSynced scene objects wont work with this disabled. That means that disabling SceneManagement also enables PrefabSync.")]
        public bool EnableSceneManagement = true;

        /// <summary>
        /// Whether or not the MLAPI should check for differences in the prefabs at connection.
        /// If you dynamically add prefabs at runtime, turn this OFF
        /// </summary>
        [Tooltip("Whether or not the MLAPI should check for differences in the prefab lists at connection")]
        public bool ForceSamePrefabs = true;

        /// <summary>
        /// If true, NetworkIds will be reused after the NetworkIdRecycleDelay.
        /// </summary>
        [Tooltip("If true, NetworkIds will be reused after the NetworkIdRecycleDelay")]
        public bool RecycleNetworkIds = true;

        /// <summary>
        /// The amount of seconds a NetworkId has to be unused in order for it to be reused.
        /// </summary>
        [Tooltip("The amount of seconds a NetworkId has to unused in order for it to be reused")]
        public float NetworkIdRecycleDelay = 120f;

        /// <summary>
        /// Decides how many bytes to use for Rpc messaging. Leave this to 2 bytes unless you are facing hash collisions
        /// </summary>
        [Tooltip("The maximum amount of bytes to use for RPC messages.")]
        public HashSize RpcHashSize = HashSize.VarIntFourBytes;

        /// <summary>
        /// The amount of seconds to wait on all clients to load requested scene before the SwitchSceneProgress onComplete callback, that waits for all clients to complete loading, is called anyway.
        /// </summary>
        [Tooltip("The amount of seconds to wait for all clients to load a requested scene")]
        public int LoadSceneTimeOut = 120;

        /// <summary>
        /// The amount of time a message should be buffered for without being consumed. If it is not consumed within this time, it will be dropped.
        /// </summary>
        [Tooltip("The amount of time a message should be buffered for without being consumed. If it is not consumed within this time, it will be dropped")]
        public float MessageBufferTimeout = 20f;

        /// <summary>
        /// Whether or not to enable network logs.
        /// </summary>
        public bool EnableNetworkLogs = true;

        private void Sort()
        {
            RegisteredScenes.Sort(StringComparer.Ordinal);
        }

        /// <summary>
        /// Returns a base64 encoded version of the configuration
        /// </summary>
        /// <returns></returns>
        public string ToBase64()
        {
            NetworkConfig config = this;
            using (var buffer = PooledNetworkBuffer.Get())
            using (var writer = PooledNetworkWriter.Get(buffer))
            {
                writer.WriteUInt16Packed(config.ProtocolVersion);
                writer.WriteUInt16Packed((ushort)config.RegisteredScenes.Count);

                for (int i = 0; i < config.RegisteredScenes.Count; i++)
                {
                    writer.WriteString(config.RegisteredScenes[i]);
                }

                writer.WriteInt32Packed(config.ReceiveTickrate);
                writer.WriteInt32Packed(config.MaxReceiveEventsPerTickRate);
                writer.WriteInt32Packed(config.EventTickrate);
                writer.WriteInt32Packed(config.ClientConnectionBufferTimeout);
                writer.WriteBool(config.ConnectionApproval);
                writer.WriteInt32Packed(config.LoadSceneTimeOut);
                writer.WriteBool(config.EnableTimeResync);
                writer.WriteBool(config.EnsureNetworkVariableLengthSafety);
                writer.WriteBits((byte)config.RpcHashSize, 2);
                writer.WriteBool(ForceSamePrefabs);
                writer.WriteBool(EnableSceneManagement);
                writer.WriteBool(RecycleNetworkIds);
                writer.WriteSinglePacked(NetworkIdRecycleDelay);
                writer.WriteBool(EnableNetworkVariable);
                writer.WriteBool(AllowRuntimeSceneChanges);
                writer.WriteBool(EnableNetworkLogs);
                buffer.PadBuffer();

                return Convert.ToBase64String(buffer.ToArray());
            }
        }

        /// <summary>
        /// Sets the NetworkConfig data with that from a base64 encoded version
        /// </summary>
        /// <param name="base64">The base64 encoded version</param>
        public void FromBase64(string base64)
        {
            NetworkConfig config = this;
            byte[] binary = Convert.FromBase64String(base64);
            using (var buffer = new NetworkBuffer(binary))
            using (var reader = PooledNetworkReader.Get(buffer))
            {
                config.ProtocolVersion = reader.ReadUInt16Packed();

                ushort sceneCount = reader.ReadUInt16Packed();
                config.RegisteredScenes.Clear();

                for (int i = 0; i < sceneCount; i++)
                {
                    config.RegisteredScenes.Add(reader.ReadString().ToString());
                }

                config.ReceiveTickrate = reader.ReadInt32Packed();
                config.MaxReceiveEventsPerTickRate = reader.ReadInt32Packed();
                config.EventTickrate = reader.ReadInt32Packed();
                config.ClientConnectionBufferTimeout = reader.ReadInt32Packed();
                config.ConnectionApproval = reader.ReadBool();
                config.LoadSceneTimeOut = reader.ReadInt32Packed();
                config.EnableTimeResync = reader.ReadBool();
                config.EnsureNetworkVariableLengthSafety = reader.ReadBool();
                config.RpcHashSize = (HashSize)reader.ReadBits(2);
                config.ForceSamePrefabs = reader.ReadBool();
                config.EnableSceneManagement = reader.ReadBool();
                config.RecycleNetworkIds = reader.ReadBool();
                config.NetworkIdRecycleDelay = reader.ReadSinglePacked();
                config.EnableNetworkVariable = reader.ReadBool();
                config.AllowRuntimeSceneChanges = reader.ReadBool();
                config.EnableNetworkLogs = reader.ReadBool();
            }
        }


        private ulong? m_ConfigHash = null;

        /// <summary>
        /// Gets a SHA256 hash of parts of the NetworkConfig instance
        /// </summary>
        /// <param name="cache"></param>
        /// <returns></returns>
        public ulong GetConfig(bool cache = true)
        {
            if (m_ConfigHash != null && cache)
            {
                return m_ConfigHash.Value;
            }

            Sort();

            using (var buffer = PooledNetworkBuffer.Get())
            using (var writer = PooledNetworkWriter.Get(buffer))
            {
                writer.WriteUInt16Packed(ProtocolVersion);
                writer.WriteString(NetworkConstants.PROTOCOL_VERSION);

                if (EnableSceneManagement && !AllowRuntimeSceneChanges)
                {
                    for (int i = 0; i < RegisteredScenes.Count; i++)
                    {
                        writer.WriteString(RegisteredScenes[i]);
                    }
                }

                if (ForceSamePrefabs)
                {
                    var sortedDictionary = NetworkPrefabOverrideLinks.OrderBy(x => x.Key);
                    foreach (var sortedEntry in sortedDictionary)
                    {
                        writer.WriteUInt32Packed(sortedEntry.Key);
                    }
                }

                writer.WriteBool(EnableNetworkVariable);
                writer.WriteBool(ForceSamePrefabs);
                writer.WriteBool(EnableSceneManagement);
                writer.WriteBool(EnsureNetworkVariableLengthSafety);
                writer.WriteBits((byte)RpcHashSize, 2);
                buffer.PadBuffer();

                if (cache)
                {
                    m_ConfigHash = XXHash.Hash64(buffer.ToArray());
                    return m_ConfigHash.Value;
                }

                return XXHash.Hash64(buffer.ToArray());
            }
        }

        /// <summary>
        /// Compares a SHA256 hash with the current NetworkConfig instances hash
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public bool CompareConfig(ulong hash)
        {
            return hash == GetConfig();
        }
    }
}
