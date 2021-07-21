using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MLAPI.RuntimeTests
{
    public class NetworkPrefabGlobalObjectIdHashTests
    {
        private Scene m_TestScene;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == nameof(NetworkPrefabGlobalObjectIdHashTests))
            {
                m_TestScene = scene;
            }
        }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            ScenesInBuild.IsTesting = true;
            SceneManager.sceneLoaded += OnSceneLoaded;

            var execAssembly = Assembly.GetExecutingAssembly();
            var packagePath = PackageInfo.FindForAssembly(execAssembly).assetPath;
            var scenePath = Path.Combine(packagePath, $"Tests/Runtime/GlobalObjectIdHash/{nameof(NetworkPrefabGlobalObjectIdHashTests)}.unity");

            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            ScenesInBuild.IsTesting = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (m_TestScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(m_TestScene);
            }
        }

        [Test]
        public void VerifyUniquenessOfNetworkPrefabs()
        {
            Assert.That(m_TestScene.isLoaded, Is.True);

            var networkManager = NetworkManager.Singleton;
            Assert.That(networkManager, Is.Not.Null);
            Assert.That(networkManager.NetworkConfig, Is.Not.Null);
            Assert.That(networkManager.NetworkConfig.NetworkPrefabs, Is.Not.Null);
            Assert.That(networkManager.NetworkConfig.NetworkPrefabs.Count, Is.GreaterThan(1));

            var hashSet = new HashSet<uint>();
            foreach (var networkPrefab in networkManager.NetworkConfig.NetworkPrefabOverrideLinks)
            {
                var idHash = networkPrefab.Key;
                Assert.That(idHash, Is.Not.EqualTo(0));
                Assert.That(hashSet.Contains(idHash), Is.False);
                hashSet.Add(idHash);
            }
        }
    }
}
