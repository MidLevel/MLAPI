using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using MLAPI.Messaging;

namespace MLAPI.RuntimeTests
{
    /// <summary>
    /// The RpcQueue unit tests validate:
    /// Maximum buffer size that can be sent (currently 1MB is the default maximum RpcQueueHistoryFrame size)
    /// That all RPCs invoke at the appropriate NetworkUpdateStage (Client and Server)
    /// A lower level RpcQueueContainer test that validates RpcQueueFrameItems after they have been put into the queue
    /// </summary>
    public class RpcQueueTests
    {
        [SetUp]
        public void Setup()
        {
            // Create, instantiate, and host
            Assert.IsTrue(NetworkManagerHelper.StartNetworkManager(out _));
        }

        /// <summary>
        /// Tests to make sure providing different
        /// ** This does not include any of the MLAPI to Transport code **
        /// </summary>
        /// <returns>IEnumerator</returns>
        [UnityTest, Order(1)]
        public IEnumerator UpdateStagesInvocation()
        {
            Guid updateStagesTestId = NetworkManagerHelper.AddGameNetworkObject("UpdateStagesTest");
            var rpcPipelineTestComponent = NetworkManagerHelper.AddComponentToObject<NetworkUpdateStagesComponent>(updateStagesTestId);

            NetworkManagerHelper.SpawnNetworkObject(updateStagesTestId);

            var testsAreComplete = rpcPipelineTestComponent.IsTestComplete();
            var exceededMaximumStageIterations = rpcPipelineTestComponent.ExceededMaxIterations();

            // Start testing
            rpcPipelineTestComponent.EnableTesting = true;

            Debug.Log("Running TestNetworkUpdateStages: ");

            // Wait for the RPC pipeline test to complete or if we exceeded the maximum iterations bail
            while (!testsAreComplete && !exceededMaximumStageIterations)
            {
                yield return new WaitForSeconds(0.003f);

                testsAreComplete = rpcPipelineTestComponent.IsTestComplete();
                Assert.IsFalse(rpcPipelineTestComponent.ExceededMaxIterations());
            }
            var testsAreValidated = rpcPipelineTestComponent.ValidateUpdateStages();

            // Stop testing
            rpcPipelineTestComponent.EnableTesting = false;

            Debug.Log($"Exiting status => {nameof(testsAreComplete)}: {testsAreComplete} - {nameof(testsAreValidated)}: {testsAreValidated} -{nameof(exceededMaximumStageIterations)}: {exceededMaximumStageIterations}");

            Assert.IsTrue(testsAreComplete && testsAreValidated);

            // Disable this so it isn't running any longer.
            rpcPipelineTestComponent.gameObject.SetActive(false);
        }

        /// <summary>
        /// This tests the RPC Queue outbound and inbound buffer capabilities.        
        /// </summary>
        /// <returns>IEnumerator</returns>
        [UnityTest, Order(2)]
        public IEnumerator BufferDataValidation()
        {
            Guid gameObjectId = NetworkManagerHelper.AddGameNetworkObject("GrowingBufferObject");

            var growingRpcBufferSizeComponent = NetworkManagerHelper.AddComponentToObject<BufferDataValidationComponent>(gameObjectId);

            NetworkManagerHelper.SpawnNetworkObject(gameObjectId);

            // Start Testing
            growingRpcBufferSizeComponent.EnableTesting = true;

            var testsAreComplete = growingRpcBufferSizeComponent.IsTestComplete();

            // Wait for the RPC pipeline test to complete or if we exceeded the maximum iterations bail
            while (!testsAreComplete)
            {
                yield return new WaitForSeconds(0.003f);

                testsAreComplete = growingRpcBufferSizeComponent.IsTestComplete();
            }

            // Stop Testing
            growingRpcBufferSizeComponent.EnableTesting = false;

            // Just disable this once we are done.
            growingRpcBufferSizeComponent.gameObject.SetActive(false);

            Assert.IsTrue(testsAreComplete);
        }

        /// <summary>
        /// This tests the RpcQueueContainer and RpcQueueHistoryFrame
        /// ***NOTE: We want to run this test always LAST!
        /// </summary>
        [Test, Order(3)]
        public void RpcQueueContainerClass()
        {
            // Create a testing rpcQueueContainer that doesn't get added to the network update loop so we don't try to send or process during the test
            var rpcQueueContainer = new MessageQueueContainer(NetworkManagerHelper.NetworkManagerObject, 0, true);

            // Make sure we set testing mode so we don't try to invoke RPCs
            rpcQueueContainer.SetTestingState(true);

            var maxRpcEntries = 8;
            var messageChunkSize = 2048;
            var preCalculatedBufferValues = new List<byte>(messageChunkSize);


            for (int i = 0; i < messageChunkSize; i++)
            {
                preCalculatedBufferValues.AddRange(BitConverter.GetBytes(UnityEngine.Random.Range(0, ulong.MaxValue)));
            }

            var indexOffset = 0;
            ulong senderNetworkId = 1;

            // Create fictitious list of clients to send to
            ulong[] psuedoClients = new ulong[] { 0 };

            var randomGeneratedDataArray = preCalculatedBufferValues.ToArray();
            var maximumOffsetValue = preCalculatedBufferValues.Count;

            // Testing outbound side of the RpcQueueContainer
            for (int i = 0; i < maxRpcEntries; i++)
            {
                // Increment our offset into our randomly generated data for next entry;
                indexOffset = (i * messageChunkSize) % maximumOffsetValue;

                var writer = rpcQueueContainer.BeginAddQueueItemToFrame(MessageQueueContainer.MessageType.ServerRpc, Time.realtimeSinceStartup, Transports.NetworkChannel.DefaultMessage,
                        senderNetworkId, psuedoClients, MessageQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);


                writer.WriteByteArray(randomGeneratedDataArray, messageChunkSize);


                rpcQueueContainer.EndAddQueueItemToFrame(writer, MessageQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);
            }

            // Now verify the data by obtaining the RpcQueueHistoryFrame we just wrote to
            var currentFrame = rpcQueueContainer.GetLoopBackHistoryFrame(MessageQueueHistoryFrame.QueueFrameType.Outbound, NetworkUpdateStage.PostLateUpdate);

            // Reset our index offset
            indexOffset = 0;
            int queueEntryItemCount = 0;
            // Parse through the entries written to the current RpcQueueHistoryFrame
            var currentQueueItem = currentFrame.GetFirstQueueItem();
            while (currentQueueItem.MessageType != MessageQueueContainer.MessageType.None)
            {
                // Check to make sure the wrapper information is accurate for the entry
                Assert.AreEqual(currentQueueItem.NetworkId, senderNetworkId);
                Assert.AreEqual(currentQueueItem.MessageType, MessageQueueContainer.MessageType.ServerRpc);
                Assert.AreEqual(currentQueueItem.UpdateStage, NetworkUpdateStage.PostLateUpdate);
                Assert.AreEqual(currentQueueItem.NetworkChannel, Transports.NetworkChannel.DefaultMessage);

                // Validate the data in the queue
                Assert.IsTrue(NetworkManagerHelper.BuffersMatch(currentQueueItem.MessageData.Offset, messageChunkSize, currentQueueItem.MessageData.Array, randomGeneratedDataArray));

                // Prepare for next queue item
                queueEntryItemCount++;
                currentQueueItem = currentFrame.GetNextQueueItem();
            }

            rpcQueueContainer.Dispose();
        }


        [TearDown]
        public void TearDown()
        {
            // Stop, shutdown, and destroy
            NetworkManagerHelper.ShutdownNetworkManager();
        }
    }
}
