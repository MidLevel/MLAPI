using System;
using System.Collections.Generic;
using System.Linq;
using MLAPI;
using MLAPI.Configuration;
using MLAPI.Logging;
using MLAPI.Messaging;
using MLAPI.Spawning;
using NUnit.Framework;
using UnityEngine;
using Object = System.Object;

namespace TestProject.RuntimeTests.Support
{
    public class SpawnRpcDespawnInstanceHandler : INetworkPrefabInstanceHandler
    {
        private uint m_PrefabHash;

        public bool WasSpawned = false;
        public bool WasDestroyed = false;

        public SpawnRpcDespawnInstanceHandler(uint prefabHash)
        {
            m_PrefabHash = prefabHash;
        }

        public NetworkObject HandleNetworkPrefabSpawn(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            WasSpawned = true;
            Assert.AreEqual(SpawnRpcDespawn.TestStage, NetworkUpdateLoop.UpdateStage);


            // See if there is a valid registered NetworkPrefabOverrideLink associated with the provided prefabHash
            GameObject networkPrefabReference = null;
            if (NetworkManager.Singleton.NetworkConfig.NetworkPrefabOverrideLinks.ContainsKey(m_PrefabHash))
            {
                switch (NetworkManager.Singleton.NetworkConfig.NetworkPrefabOverrideLinks[m_PrefabHash].Override)
                {
                    default:
                    case NetworkPrefabOverride.None:
                        networkPrefabReference = NetworkManager.Singleton.NetworkConfig.NetworkPrefabOverrideLinks[m_PrefabHash].Prefab;
                        break;
                    case NetworkPrefabOverride.Hash:
                    case NetworkPrefabOverride.Prefab:
                        networkPrefabReference = NetworkManager.Singleton.NetworkConfig.NetworkPrefabOverrideLinks[m_PrefabHash].OverridingTargetPrefab;
                        break;
                }
            }

            // If not, then there is an issue (user possibly didn't register the prefab properly?)
            if (networkPrefabReference == null)
            {
                if (NetworkLog.CurrentLogLevel <= LogLevel.Error)
                {
                    NetworkLog.LogError($"Failed to create object locally. [{nameof(m_PrefabHash)}={m_PrefabHash}]. {nameof(NetworkPrefab)} could not be found. Is the prefab registered with {nameof(NetworkManager)}?");
                }
                return null;
            }

            // Otherwise, instantiate an instance of the NetworkPrefab linked to the prefabHash
            var networkObject =  UnityEngine.Object.Instantiate(networkPrefabReference, position, rotation).GetComponent<NetworkObject>();

            return networkObject;
        }

        public void HandleNetworkPrefabDestroy(NetworkObject networkObject)
        {
            WasDestroyed = true;
            if (networkObject.NetworkManager.IsClient)
            {
                Assert.AreEqual(SpawnRpcDespawn.TestStage, NetworkUpdateLoop.UpdateStage);
            }

            GameObject.Destroy(networkObject.gameObject);
        }
    }
    public class SpawnRpcDespawn : NetworkBehaviour, INetworkUpdateSystem
    {
        public static NetworkUpdateStage TestStage;
        public static int ClientUpdateCount;
        public static int ServerUpdateCount;
        public static NetworkUpdateStage StageExecutedByReceiver;

        private bool m_Active = false;

        [ClientRpc]
        public void SendIncrementUpdateCountClientRpc()
        {
            Assert.AreEqual(TestStage, NetworkUpdateLoop.UpdateStage);

            StageExecutedByReceiver = NetworkUpdateLoop.UpdateStage;
            ++ClientUpdateCount;
            Debug.Log($"Client RPC executed at {NetworkUpdateLoop.UpdateStage}; client count to {ClientUpdateCount.ToString()}");
        }

        public void IncrementUpdateCount()
        {
            ++ServerUpdateCount;
            Debug.Log($"Server count to {ServerUpdateCount.ToString()}");
            SendIncrementUpdateCountClientRpc();
        }

        public void Activate()
        {
            Debug.Log("Activated");
            m_Active = true;
        }

        public void NetworkStart()
        {
            Debug.Log($"Network Start on client {NetworkManager.LocalClientId.ToString()}");
            Assert.AreEqual(TestStage, NetworkUpdateLoop.UpdateStage);
        }

        public void Awake()
        {
            foreach (NetworkUpdateStage stage in Enum.GetValues(typeof(NetworkUpdateStage)))
            {
                NetworkUpdateLoop.RegisterNetworkUpdate(this, stage);
            }
        }

        public void OnDestroy()
        {
            foreach (NetworkUpdateStage stage in Enum.GetValues(typeof(NetworkUpdateStage)))
            {
                NetworkUpdateLoop.UnregisterNetworkUpdate(this, stage);
            }
        }

        private void RunTest()
        {
            Debug.Log("Running test...");
            GetComponent<NetworkObject>().Spawn();
            IncrementUpdateCount();
            GetComponent<NetworkObject>().Despawn();
            m_Active = false;
        }

        public void NetworkUpdate(NetworkUpdateStage stage)
        {
            if (IsServer && m_Active && stage == TestStage)
            {
                RunTest();
            }
        }
    }
}
