using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MLAPI;
using MLAPI.Messaging;


namespace TestProject.ManualTests
{
    public class StatsDisplay : NetworkBehaviour
    {
        [SerializeField]
        private GameObject m_ClientServerToggle;
        private bool m_ClientMode = true;
        private Rect m_Stats;
        private string m_LastStatsDump;
        private Text m_ClientServerToggleText;
        private List<ulong> m_ClientsToUpdate = new List<ulong>();

        private bool m_IsServer;

        private void Start()
        {
            m_Stats = new Rect(5, 10, 175, 300);
            GUI.contentColor = new Color(196, 196, 196, 196);
            GUI.backgroundColor = new Color(96, 96, 96, 96);
            if (m_ClientServerToggle != null)
            {
                m_ClientServerToggleText = m_ClientServerToggle.GetComponentInChildren<Text>();
            }

            if (NetworkManager && NetworkManager.IsListening && !NetworkManager.IsServer)
            {
                m_ClientServerToggle.SetActive(true);
            }
            else
            {
                m_ClientServerToggle.SetActive(false);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.IsServer)
            {
                m_IsServer = true;
                NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;
            }
            else
            {
                m_ClientServerToggle.SetActive(true);
                UpdateButton();
            }
            StartCoroutine("UpdateTextStatus");
        }

        /// <summary>
        /// Invoked when a client connects
        /// </summary>
        /// <param name="clientId"></param>
        private void OnClientConnectedCallback(ulong clientId)
        {
            if (!IsServer)
            {
                var networkObject = GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    networkObject.SpawnWithOwnership(clientId, null, true);
                }
            }
        }

        /// <summary>
        /// Remove our OnClientConnectedCallback registration when we are destroyed
        /// </summary>
        private void OnDestroy()
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnectedCallback;
        }

        /// <summary>
        /// Used by UI Button click event
        /// </summary>
        public void ToggleClientSever()
        {
            if (NetworkManager.Singleton.IsClient)
            {
                m_ClientMode = !m_ClientMode;
                GetStatsServerRPC(NetworkManager.Singleton.LocalClientId);
                UpdateButton();
            }
        }

        /// <summary>
        /// Draw the stats
        /// </summary>
        private void OnGUI()
        {
            if (NetworkManager && NetworkManager.IsListening)
            {
                GUI.TextArea(m_Stats, m_LastStatsDump);
            }
        }


        /// <summary>
        /// Updates the text of the button for switching between server and client stats
        /// </summary>
        private void UpdateButton()
        {
            if (m_ClientServerToggleText != null)
            {
                m_ClientServerToggleText.text = m_ClientMode ? "Show Server Stats" : "Show Client Stats";
            }
        }

        /// <summary>
        /// RPC Used for the server to send stats info to the client
        /// </summary>
        /// <param name="statsinfo"></param>
        [ClientRpc]
        private void ReceiveStatsClientRPC(StatsInfoContainer statsinfo)
        {
            m_LastStatsDump = "Server Stats";
            m_LastStatsDump += "\ndeltaTime: [" + Time.deltaTime.ToString() + "]";
            // TODO: Fill out NetStats related information here when ready
        }

        /// <summary>
        /// RPC used to notify server that a specific client wants to receive its stats info
        /// </summary>
        /// <param name="clientId"></param>
        [ServerRpc(RequireOwnership = false)]
        public void GetStatsServerRPC(ulong clientId)
        {
            if (!m_ClientsToUpdate.Contains(clientId))
            {
                m_ClientsToUpdate.Add(clientId);
            }
            else if (m_ClientsToUpdate.Contains(clientId))
            {
                m_ClientsToUpdate.Remove(clientId);
            }
        }


        private const int k_NumValues = 10;

        /// <summary>
        /// Coroutine to update the stats information
        /// </summary>
        /// <returns></returns>
        private IEnumerator UpdateTextStatus()
        {
            while (true)
            {
                if (NetworkManager.Singleton)
                {
                    if (m_ClientMode)
                    {
                        m_LastStatsDump = m_IsServer ? "Server Stats" : "Client Stats";
                        m_LastStatsDump += "\ndeltaTime: [" + Time.deltaTime.ToString() + "]";

                        // TODO: Fill out NetStats related information here when ready
                    }
                    if (NetworkManager.Singleton.IsServer && m_ClientsToUpdate.Count > 0)
                    {
                        var statsInfoContainer = new StatsInfoContainer();
                        statsInfoContainer.StatValues = new List<float>();
                        for(int i = 0; i < k_NumValues; i++ )
                        {
                            statsInfoContainer.StatValues.Add(Random.Range(1.0f,100.0f));
                        }
                        ReceiveStatsClientRPC(statsInfoContainer);
                    }
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
