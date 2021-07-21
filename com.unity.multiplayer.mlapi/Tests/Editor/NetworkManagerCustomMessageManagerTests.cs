using System.Collections.Generic;
using MLAPI.Configuration;
using MLAPI.Messaging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MLAPI.EditorTests
{
    public class NetworkManagerCustomMessageManagerTests
    {
        [Test]
        public void CustomMessageManagerAssigned()
        {
            var gameObject = new GameObject(nameof(CustomMessageManagerAssigned));
            var networkManager = gameObject.AddComponent<NetworkManager>();
            var transport = gameObject.AddComponent<DummyTransport>();

            networkManager.PopulateScenesInBuild();
            networkManager.ScenesInBuild.Scenes.Add(SceneManager.GetActiveScene().name);
            networkManager.NetworkConfig = new NetworkConfig();
            // Set dummy transport that does nothing
            networkManager.NetworkConfig.NetworkTransport = transport;

            CustomMessagingManager preManager = networkManager.CustomMessagingManager;

            // Start server to cause init
            networkManager.StartServer();

            Debug.Assert(preManager == null);
            Debug.Assert(networkManager.CustomMessagingManager != null);

            Object.DestroyImmediate(gameObject);
        }
    }
}
