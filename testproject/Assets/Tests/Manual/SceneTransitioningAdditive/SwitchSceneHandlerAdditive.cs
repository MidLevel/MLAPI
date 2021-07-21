using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using MLAPI;
using MLAPI.SceneManagement;

namespace TestProject.ManualTests
{
    public class SwitchSceneHandlerAdditive : NetworkBehaviour
    {
        public static bool ExitingNow { get; internal set; }

        [SerializeField]
        private GameObject m_SwitchSceneButtonObject;

        [HideInInspector]
        [SerializeField]
        private List<string> m_SceneToSwitchTo;

#if UNITY_EDITOR
        [SerializeField]
        private List<SceneAsset> m_SceneAssets;
        private void OnValidate()
        {
            m_SceneToSwitchTo = new List<string>();
            foreach(var sceneAsset in m_SceneAssets)
            {
                m_SceneToSwitchTo.Add(sceneAsset.name);
            }
        }
#endif

        private int m_CurrentSceneIndex;

        private void Awake()
        {
            m_CurrentSceneIndex = 0;
            ExitingNow = false;
        }

        private void Start()
        {
            m_SwitchSceneButtonObject.SetActive(false);
            StartCoroutine(CheckForVisibility());
        }

        private bool m_ExitingScene;
        private void OnDestroy()
        {
            m_ExitingScene = true;
            StopAllCoroutines();
        }

        private IEnumerator CheckForVisibility()
        {
            while (!m_ExitingScene)
            {
                if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
                {
                    if (m_SwitchSceneButtonObject)
                    {
                        m_SwitchSceneButtonObject.SetActive(true);
                    }
                }
                else
                {
                    m_SwitchSceneButtonObject.SetActive(false);
                }

                yield return new WaitForSeconds(0.5f);
            }

            yield return null;
        }

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
            {
                if (m_SwitchSceneButtonObject)
                {
                    m_SwitchSceneButtonObject.SetActive(true);
                }
            }
            else
            {
                m_SwitchSceneButtonObject.SetActive(false);
            }
            base.OnNetworkSpawn();
        }

        private SceneSwitchProgress m_CurrentSceneSwitchProgress;

        public delegate void OnSceneSwitchBeginDelegateHandler();

        public event OnSceneSwitchBeginDelegateHandler OnSceneSwitchBegin;

        private bool m_IsReversing;

        public void OnSwitchScene()
        {
            if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening)
            {
                m_ExitingScene = true;
                ExitingNow = true;

                if (!m_IsReversing)
                {
                    OnSceneSwitchBegin?.Invoke();

                    m_CurrentSceneSwitchProgress = NetworkManager.Singleton.SceneManager.LoadScene(m_SceneToSwitchTo[m_CurrentSceneIndex], UnityEngine.SceneManagement.LoadSceneMode.Additive);
                    m_CurrentSceneIndex++;
                    m_CurrentSceneSwitchProgress.OnComplete += CurrentSceneSwitchProgress_OnComplete;
                    if(m_CurrentSceneIndex == m_SceneToSwitchTo.Count)
                    {
                        m_IsReversing = true;
                        m_CurrentSceneIndex--;
                    }
                }
                else
                {
                    m_CurrentSceneSwitchProgress = NetworkManager.Singleton.SceneManager.UnloadScene(m_SceneToSwitchTo[m_CurrentSceneIndex]);
                    m_CurrentSceneIndex--;
                    m_CurrentSceneSwitchProgress.OnComplete += CurrentSceneSwitchProgress_OnComplete;
                    if(m_CurrentSceneIndex < 0)
                    {
                        m_IsReversing = false;
                        m_CurrentSceneIndex = 0;
                    }
                }
            }
        }

        public delegate void OnSceneSwitchCompletedDelegateHandler();

        public event OnSceneSwitchCompletedDelegateHandler OnSceneSwitchCompleted;

        private void CurrentSceneSwitchProgress_OnComplete(bool timedOut)
        {
            OnSceneSwitchCompleted?.Invoke();
        }
    }
}
