using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

using MLAPI;
using MLAPI.SceneManagement;

namespace TestProject.ManualTests
{
    public class AdditiveSceneToggleHandler : NetworkBehaviour
    {
        [SerializeField]
        private bool m_ActivateOnLoad = false;

        private Toggle m_ToggleObject;

        [HideInInspector]
        [SerializeField]
        private string m_SceneToLoad;



#if UNITY_EDITOR
        [SerializeField]
        private SceneAsset m_SceneAsset;
        private void OnValidate()
        {
            if (m_SceneAsset != null && m_SceneAsset.name != m_SceneToLoad)
            {
                m_SceneToLoad = m_SceneAsset.name;
            }
        }
#endif

        private void Start()
        {
            m_ToggleObject = gameObject.GetComponentInChildren<Toggle>();
            StartCoroutine(CheckForVisibility());
        }

        private bool m_ExitingScene;
        private void OnDestroy()
        {
            m_ExitingScene = true;
            StopCoroutine(CheckForVisibility());
        }

        private IEnumerator CheckForVisibility()
        {
            while (!m_ExitingScene)
            {
                if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening )
                {
                    if (m_ToggleObject)
                    {
                        if(NetworkManager.Singleton.IsServer)
                        {
                            m_ToggleObject.gameObject.SetActive(true);
                            if (m_ActivateOnLoad)
                            {
                                StartCoroutine(DelayedActivate());
                            }
                        }
                        else
                        {
                            m_ToggleObject.gameObject.SetActive(false);
                        }
                    }
                    break;
                }
                else
                {
                    if (m_ToggleObject && m_ToggleObject.gameObject.activeInHierarchy)
                    {
                        m_ToggleObject.gameObject.SetActive(false);
                    }
                }

                yield return new WaitForSeconds(0.1f);
            }

            yield return null;
        }

        private IEnumerator DelayedActivate()
        {
            yield return new WaitForSeconds(0.5f);
            if (m_ToggleObject)
            {
                m_ToggleObject.isOn = true;
            }
            yield return null;
        }

        private SceneSwitchProgress m_CurrentSceneSwitchProgress;


        public void OnToggle()
        {
            if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening)
            {
                if (m_ToggleObject)
                {
                    m_ToggleObject.enabled = false;
                    StartCoroutine(SceneEventCoroutine(m_ToggleObject.isOn));
                }
            }
        }

        private IEnumerator SceneEventCoroutine(bool isLoading)
        {
            while (m_CurrentSceneSwitchProgress == null)
            {
                if (isLoading)
                {
                    m_CurrentSceneSwitchProgress = NetworkManager.Singleton.SceneManager.LoadScene(m_SceneToLoad,UnityEngine.SceneManagement.LoadSceneMode.Additive);
                }
                else
                {
                    m_CurrentSceneSwitchProgress = NetworkManager.Singleton.SceneManager.UnloadScene(m_SceneToLoad);
                }
                if (m_CurrentSceneSwitchProgress == null)
                {
                    yield return new WaitForSeconds(0.25f);
                }
            }
            m_ToggleObject.isOn = isLoading;
            m_ToggleObject.enabled = true;
            m_CurrentSceneSwitchProgress = null;
            yield return null;
        }



        public delegate void OnSceneSwitchCompletedDelegateHandler();

        public event OnSceneSwitchCompletedDelegateHandler OnSceneSwitchCompleted;

        private void CurrentSceneSwitchProgress_OnComplete(bool timedOut)
        {
            OnSceneSwitchCompleted?.Invoke();
        }
    }
}
