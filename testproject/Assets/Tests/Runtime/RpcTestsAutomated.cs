using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TestProject.ManualTests;
using MLAPI.RuntimeTests;
using MLAPI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace TestProject.RuntimeTests
{
    public class RpcTestsAutomated : BaseMultiInstanceTest
    {
        private bool m_TimedOut;
        private int m_MaxFrames;

        protected override int NbClients => throw new NotSupportedException("Not implemented on purpose, setup is implementing this itself");

        [UnitySetUp]
        public override IEnumerator Setup()
        {
            yield break;
        }

        /// <summary>
        /// Default Mode (Batched RPCs Enabled)
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator ManualRpcTestsAutomated()
        {
            return AutomatedRpcTestsHandler(9);
        }

        /// <summary>
        /// Same test with Batched RPC turned off
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator ManualRpcTestsAutomatedNoBatching()
        {
            return AutomatedRpcTestsHandler(3, false);
        }

        /// <summary>
        /// This just helps to simplify any further tests that can leverage from
        /// the RpcQueueManualTests' wide array of RPC testing under different
        /// conditions.
        /// Currently this allows for the adjustment of client count and whether
        /// RPC Batching is enabled or not.
        /// </summary>
        /// <param name="numClients"></param>
        /// <param name="useBatching"></param>
        /// <returns></returns>
        private IEnumerator AutomatedRpcTestsHandler(int numClients, bool useBatching = true)
        {
            var startFrameCount = Time.frameCount;
            var startTime = Time.realtimeSinceStartup;

            // Set RpcQueueManualTests into unit testing mode
            RpcQueueManualTests.UnitTesting = true;

            yield return StartSomeClientsAndServerWithPlayers(useHost:true, numClients, playerPrefab =>
            {
                // Add our RpcQueueManualTests component
                playerPrefab.AddComponent<RpcQueueManualTests>();
            });

            // Set the RPC Batch sending mode
            m_ServerNetworkManager.MessageQueueContainer.EnableBatchedMessages(useBatching);

            for (int i = 0; i < m_ClientNetworkManagers.Length; i++)
            {
                m_ClientNetworkManagers[i].MessageQueueContainer.EnableBatchedMessages(useBatching);
            }

            // [Host-Side] Get the Host owned instance of the RpcQueueManualTests
            var serverClientPlayerResult = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation((x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId), m_ServerNetworkManager, serverClientPlayerResult));

            var serverRpcTests = serverClientPlayerResult.Result.GetComponent<RpcQueueManualTests>();
            Assert.IsNotNull(serverRpcTests);

            // [Host-Side] Set the (unit) testing mode
            serverRpcTests.SetTestingMode(true, 1);

            // [Client-Side] Get all of the RpcQueueManualTests instances relative to each client
            var clientRpcQueueManualTestInstsances = new List<RpcQueueManualTests>();
            foreach (var client in m_ClientNetworkManagers)
            {
                var clientClientPlayerResult = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();
                yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation((x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId), client, clientClientPlayerResult));
                var clientRpcTests = clientClientPlayerResult.Result.GetComponent<RpcQueueManualTests>();
                Assert.IsNotNull(clientRpcTests);
                clientRpcQueueManualTestInstsances.Add(clientRpcTests);
            }

            // [Client-Side] Set the (unit) testing mode for each client
            foreach (var rpcClientSideTest in clientRpcQueueManualTestInstsances)
            {
                rpcClientSideTest.SetTestingMode(true, 1);
            }

            // [Host-Side] Begin testing on the host
            serverRpcTests.BeginTest();

            // [Client-Side] Begin testing on each client
            foreach (var rpcClientSideTest in clientRpcQueueManualTestInstsances)
            {
                rpcClientSideTest.BeginTest();
            }

            m_MaxFrames = Time.frameCount + 1000;

            var doubleCheckTime = Time.realtimeSinceStartup + 10.0f;

            m_TimedOut = false;
            while (!serverRpcTests.IsFinishedWithTest())
            {
                if (Time.frameCount > m_MaxFrames)
                {
                    // This is here in the event a platform is running at a higher
                    // frame rate than expected
                    if (doubleCheckTime < Time.realtimeSinceStartup)
                    {
                        m_TimedOut = true;
                        break;
                    }
                }
                var nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            }

            // Verify we didn't time out (i.e. all tests ran and finished)
            Assert.IsFalse(m_TimedOut);

            // Log the output for visual confirmation (Acceptance Test for this test) that all RPC test types (tracked by counters) executed multiple times
            Debug.Log("Final Host-Server Status Info:");
            Debug.Log(serverRpcTests.GetCurrentServerStatusInfo());

            foreach (var rpcClientSideTest in clientRpcQueueManualTestInstsances)
            {
                Debug.Log($"Final Client {rpcClientSideTest.NetworkManager.LocalClientId} Status Info:");
                Debug.Log(rpcClientSideTest.GetCurrentClientStatusInfo());
            }

            Debug.Log($"Total frames updated = {Time.frameCount - startFrameCount} within {Time.realtimeSinceStartup - startTime} seconds.");
        }
    }
}
