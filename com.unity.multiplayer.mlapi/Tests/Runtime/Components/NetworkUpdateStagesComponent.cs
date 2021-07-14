using System;
using System.Collections.Generic;
using UnityEngine;
using MLAPI.Messaging;

namespace MLAPI.RuntimeTests
{
    /// <summary>
    /// Used in conjunction with the RpcQueueTest to validate that Rpcs are being invoked at the proper NetworkUpdateStage
    /// </summary>
    public class NetworkUpdateStagesComponent : NetworkBehaviour
    {
        /// <summary>
        /// Allows the external RPCQueueTest to begin testing or stop it
        /// </summary>
        public bool EnableTesting;

        /// <summary>
        /// How many times will we iterate through the various NetworkUpdateStage values?
        /// (defaults to 1)
        /// </summary>
        public int MaxIterations = 1;

        private bool m_ClientReceivedRpc;
        private int m_Counter;
        private int m_MaxStagesSent;
        private int m_MaxStages;
        private ServerRpcParams m_ServerParams;
        private ClientRpcParams m_ClientParams;
        private NetworkUpdateStage m_LastUpdateStage;

        // Start is called before the first frame update
        private void Start()
        {
            m_ServerParams.Send.UpdateStage = NetworkUpdateStage.EarlyUpdate;
            m_ClientParams.Send.UpdateStage = NetworkUpdateStage.Update;

            //This assures the ILPP Codegen side of things is working by making sure that the Receive.UpdateStage
            //is not always NetworkUpdateStage.EarlyUpdate as it should be whatever NetworkUpdateStage the RPC
            //was assigned within the Send.UpdateStage
            m_ServerParams.Receive.UpdateStage = NetworkUpdateStage.EarlyUpdate;
            m_ClientParams.Receive.UpdateStage = NetworkUpdateStage.EarlyUpdate;

            m_MaxStages = (Enum.GetValues(typeof(NetworkUpdateStage)).Length);
            m_MaxStagesSent = m_MaxStages * MaxIterations;

            //Start out with this being true (for first sequence)
            m_ClientReceivedRpc = true;
        }

        /// <summary>
        /// Determine if we have iterated over more than our maximum stages we want to test
        /// </summary>
        /// <returns>true or false (did we exceed the max iterations or not?)</returns>
        public bool ExceededMaxIterations()
        {
            if (m_StagesSent.Count > m_MaxStagesSent && m_MaxStagesSent > 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns back whether the test has completed the total number of iterations
        /// </summary>
        /// <returns></returns>
        public bool IsTestComplete()
        {
            if (m_Counter >= MaxIterations)
            {
                return true;
            }

            return false;
        }

        // Update is called once per frame
        private void Update()
        {
            if (NetworkManager.Singleton.IsListening && EnableTesting && m_ClientReceivedRpc)
            {
                //Reset this for the next sequence of rpcs
                m_ClientReceivedRpc = false;

                //As long as testing isn't completed, keep testing
                if (!IsTestComplete() && m_StagesSent.Count < m_MaxStagesSent)
                {
                    m_LastUpdateStage = m_ServerParams.Send.UpdateStage;
                    m_StagesSent.Add(m_LastUpdateStage);

                    PingMySelfServerRpc(m_StagesSent.Count, m_ServerParams);

                    switch (m_ServerParams.Send.UpdateStage)
                    {
                        case NetworkUpdateStage.EarlyUpdate:
                            m_ServerParams.Send.UpdateStage = NetworkUpdateStage.FixedUpdate;
                            break;
                        case NetworkUpdateStage.FixedUpdate:
                            m_ServerParams.Send.UpdateStage = NetworkUpdateStage.PreUpdate;
                            break;
                        case NetworkUpdateStage.PreUpdate:
                            m_ServerParams.Send.UpdateStage = NetworkUpdateStage.Update;
                            break;
                        case NetworkUpdateStage.Update:
                            m_ServerParams.Send.UpdateStage = NetworkUpdateStage.PreLateUpdate;
                            break;
                        case NetworkUpdateStage.PreLateUpdate:
                            m_ServerParams.Send.UpdateStage = NetworkUpdateStage.PostLateUpdate;
                            break;
                        case NetworkUpdateStage.PostLateUpdate:
                            m_ServerParams.Send.UpdateStage = NetworkUpdateStage.EarlyUpdate;
                            break;
                    }
                }
            }
        }

        private readonly List<NetworkUpdateStage> m_ServerStagesReceived = new List<NetworkUpdateStage>();
        private readonly List<NetworkUpdateStage> m_ClientStagesReceived = new List<NetworkUpdateStage>();
        private readonly List<NetworkUpdateStage> m_StagesSent = new List<NetworkUpdateStage>();

        /// <summary>
        /// Assures all update stages were in alginment with one another
        /// </summary>
        /// <returns>true or false</returns>
        public bool ValidateUpdateStages()
        {
            var validated = false;
            if (m_ServerStagesReceived.Count == m_ClientStagesReceived.Count && m_ClientStagesReceived.Count == m_StagesSent.Count)
            {
                for (int i = 0; i < m_StagesSent.Count; i++)
                {
                    var currentStage = m_StagesSent[i];
                    if (m_ServerStagesReceived[i] != currentStage)
                    {
                        Debug.Log($"ServerRpc Update Stage ({m_ServerStagesReceived[i]}) is not equal to the current update stage ({currentStage})");

                        return validated;
                    }

                    if (m_ClientStagesReceived[i] != currentStage)
                    {
                        Debug.Log($"ClientRpc Update Stage ({m_ClientStagesReceived[i]}) is not equal to the current update stage ({currentStage})");

                        return validated;
                    }
                }

                validated = true;
            }

            return validated;
        }

        /// <summary>
        /// Server side RPC for testing
        /// </summary>
        /// <param name="parameters">server rpc parameters</param>
        [ServerRpc]
        private void PingMySelfServerRpc(int currentCount, ServerRpcParams parameters = default)
        {
            Debug.Log($"{nameof(PingMySelfServerRpc)}: [HostClient][ServerRpc][{currentCount}] invoked during the {parameters.Receive.UpdateStage} stage.");

            m_ClientParams.Send.UpdateStage = parameters.Receive.UpdateStage;
            m_ServerStagesReceived.Add(parameters.Receive.UpdateStage);

            PingMySelfClientRpc(currentCount, m_ClientParams);
        }

        /// <summary>
        /// Client Side RPC called by PingMySelfServerRPC to validate both Client->Server and Server-Client pipeline is working
        /// </summary>
        /// <param name="parameters">client rpc parameters</param>
        [ClientRpc]
        private void PingMySelfClientRpc(int currentCount, ClientRpcParams parameters = default)
        {
            Debug.Log($"{nameof(PingMySelfClientRpc)}: [HostServer][ClientRpc][{currentCount}]  invoked during the {parameters.Receive.UpdateStage} stage. (previous output line should confirm this)");

            m_ClientStagesReceived.Add(parameters.Receive.UpdateStage);

            //If we reached the last update state, then go ahead and increment our iteration counter
            if (parameters.Receive.UpdateStage == NetworkUpdateStage.PostLateUpdate)
            {
                m_Counter++;
            }

            m_ClientReceivedRpc = true;
        }
    }
}
