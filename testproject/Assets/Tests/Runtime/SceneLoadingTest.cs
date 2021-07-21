using System.Collections;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using MLAPI;

namespace TestProject.RuntimeTests
{
#if IGNORETHISTEST
    /// <summary>
    /// This is nothing more than a template to follow in order to
    /// use a scene to configure your NetworkManager as well as how
    /// to switching between scenes without blowing away the temporary
    /// test runner scene.  This also shows how to switch scenes using
    /// NetworkSceneManager during a unit test.
    /// </summary>
    public class SceneLoadingTest
    {

        private NetworkManager m_NetworkManager;

        private bool m_SceneLoaded;
        private bool m_TimedOut;
        private string m_TargetSceneNameToLoad;
        private Scene m_LoadedScene;

        [UnityTest]
        public IEnumerator SceneLoading()
        {



            // Keep track of the original test scene
            Scene originalScene = SceneManager.GetActiveScene();

            // Load the first scene with the predefined NetworkManager
            m_TargetSceneNameToLoad = "SceneTransitioningTest";

            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.LoadScene(m_TargetSceneNameToLoad, LoadSceneMode.Additive);

            // Wait for the scene to load
            var timeOut = Time.realtimeSinceStartup + 5;
            m_TimedOut = false;
            while (!m_SceneLoaded)
            {
                yield return new WaitForSeconds(0.01f);
                if (timeOut < Time.realtimeSinceStartup)
                {
                    m_TimedOut = true;
                    break;
                }
            }

            // Verify it loaded
            Assert.IsFalse(m_TimedOut);
            Assert.IsTrue(m_SceneLoaded);
            Assert.NotNull(m_LoadedScene);

            // Keep track of the scene we just loaded
            Scene primaryScene = m_LoadedScene;

            // Set the scene to be active if it is not the active scene.
            // (This is to assure spawned objects instantiate in the newly loaded scene)
            if (SceneManager.GetActiveScene().name != m_LoadedScene.name)
            {
                primaryScene = m_LoadedScene;
                Debug.Log($"Loaded scene not active, activating scene {m_TargetSceneNameToLoad}");
                SceneManager.SetActiveScene(m_LoadedScene);
                Assert.IsTrue(SceneManager.GetActiveScene().name == m_LoadedScene.name);
            }

            // No longer need to be notified of this event
            SceneManager.sceneLoaded -= SceneManager_sceneLoaded;

            // Get the NetworkManager instantiated from the scene
            var gameObject = GameObject.Find("NetworkManager");
            Assert.IsNotNull(gameObject);

            m_NetworkManager = gameObject.GetComponent<NetworkManager>();



            Assert.IsNotNull(m_NetworkManager);

            // Start in host mode
            if (m_NetworkManager)
            {
                m_NetworkManager.StartHost();
            }


            // Next, we want to do a scene transition using NetworkSceneManager
            m_TargetSceneNameToLoad = "SecondSceneToLoad";

            // Reference code for adding scenes not included in build settings:
            // Assure we are allowing runtime scene changes
            // m_NetworkManager.NetworkConfig.AllowRuntimeSceneChanges = true;
            // m_NetworkManager.SceneManager.AddRuntimeSceneName(m_TargetSceneNameToLoad, (uint)m_NetworkManager.SceneManager.RegisteredSceneNames.Count);

            // Switch the scene using NetworkSceneManager
            var sceneSwitchProgress = m_NetworkManager.SceneManager.SwitchScene(m_TargetSceneNameToLoad, LoadSceneMode.Additive);

            sceneSwitchProgress.OnComplete += SwitchSceneProgress_OnComplete;
            m_SceneLoaded = false;

            // Wait for the scene to load
            timeOut = Time.realtimeSinceStartup + 30;
            m_TimedOut = false;
            while (!m_SceneLoaded)
            {
                yield return new WaitForSeconds(0.01f);
                if (timeOut < Time.realtimeSinceStartup)
                {
                    m_TimedOut = true;
                    break;
                }
            }

            // Make sure we didn't time out and the scene loaded
            Assert.IsFalse(m_TimedOut);
            Assert.IsTrue(m_SceneLoaded);
            Assert.NotNull(m_LoadedScene);

            sceneSwitchProgress.OnComplete -= SwitchSceneProgress_OnComplete;

            // Set the scene to be active if it is not the active scene
            // (This is to assure spawned objects instantiate in the newly loaded scene)
            if (!SceneManager.GetActiveScene().name.Contains(m_LoadedScene.name))
            {

                Debug.Log($"Loaded scene not active, activating scene {m_TargetSceneNameToLoad}");
                SceneManager.SetActiveScene(m_LoadedScene);
                Assert.IsTrue(SceneManager.GetActiveScene().name == m_LoadedScene.name);
            }

            m_SceneLoaded = false;
            primaryScene = m_LoadedScene;
            m_NetworkManager.SceneManager.OnAdditiveSceneEvent += SceneManager_OnAdditiveSceneEvent;
            m_NetworkManager.SceneManager.UnloadScene(primaryScene.name);

            //// Now unload the previous scene
            //SceneManager.UnloadSceneAsync(primaryScene).completed += UnloadAsync_completed;

            // Now track the newly loaded and currently active scene
            primaryScene = SceneManager.GetActiveScene();

            // Wait for the previous scene to unload
            timeOut = Time.realtimeSinceStartup + 5;
            while (!m_SceneLoaded)
            {
                yield return new WaitForSeconds(0.01f);
                if (timeOut < Time.realtimeSinceStartup)
                {
                    m_TimedOut = true;
                    break;
                }
            }
            Assert.IsFalse(m_TimedOut);

            m_NetworkManager.SceneManager.OnAdditiveSceneEvent -= SceneManager_OnAdditiveSceneEvent;

            // Set the original Test Runner Scene to be the active scene
            SceneManager.SetActiveScene(originalScene);
            // Unload the previously active scene
            SceneManager.UnloadSceneAsync(primaryScene).completed += UnloadAsync_completed;

            // Wait for the scene to unload
            timeOut = Time.realtimeSinceStartup + 5;
            while (!m_SceneLoaded)
            {
                yield return new WaitForSeconds(0.01f);
                if (timeOut < Time.realtimeSinceStartup)
                {
                    m_TimedOut = true;
                    break;
                }
            }
            Assert.IsFalse(m_TimedOut);

            m_NetworkManager.DontDestroy = false;
            SceneManager.MoveGameObjectToScene(m_NetworkManager.gameObject, originalScene);
            // We are now done with the NetworkSceneManager switch scene test so stop the host
            m_NetworkManager.StopHost();

            Object.DestroyImmediate(m_NetworkManager.gameObject);
        }

        private void SceneManager_OnAdditiveSceneEvent(AsyncOperation operation, string sceneName, bool isLoading)
        {
            if(!isLoading)
            {
                m_SceneLoaded = true;
            }
        }

        private void SceneManager_sceneUnloaded(Scene arg0)
        {
            m_SceneLoaded = true;
        }

        /// <summary>
        /// Checks to make sure the scene unloaded
        /// </summary>
        /// <param name="obj"></param>
        private void UnloadAsync_completed(AsyncOperation obj)
        {
            m_SceneLoaded = true;
        }

        /// <summary>
        /// NetworkSceneManager switch scene progress OnComplete event
        /// </summary>
        /// <param name="timedOut"></param>
        private void SwitchSceneProgress_OnComplete(bool timedOut)
        {
            m_TimedOut = timedOut;
            if (!m_TimedOut)
            {
                var scene = SceneManager.GetActiveScene();

                if (scene != null && m_TargetSceneNameToLoad.Contains(scene.name))
                {
                    m_SceneLoaded = true;
                    m_LoadedScene = scene;
                }
            }
        }

        /// <summary>
        /// When invoked, makes sure the scene loaded is the correct scene
        /// and then set our scene loaded to true and keep a reference to the loaded scene
        /// </summary>
        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene != null && m_TargetSceneNameToLoad.Contains(scene.name))
            {
                m_SceneLoaded = true;
                m_LoadedScene = scene;
            }
        }

        [UnitySetUp]
        private IEnumerator SetUp()
        {
            MLAPI.SceneManagement.NetworkSceneManager.IsRunningUnitTest = true;
            yield return null;
        }

        [UnityTearDown]
        private IEnumerator TearDown()
        {
            MLAPI.SceneManagement.NetworkSceneManager.IsRunningUnitTest = false;


            yield return null;
        }
    }
#endif
}
