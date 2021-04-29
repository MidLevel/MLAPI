using System;
using System.Collections.Generic;
using System.IO;
using MLAPI.Configuration;
using MLAPI.Internal;
using MLAPI.Messaging;
using MLAPI.Messaging.Buffering;
using MLAPI.Serialization;
using MLAPI.Transports;
using MLAPI.Transports.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MLAPI.EditorTests
{
    public class NetworkManagerMessageHandlerTests
    {
        [Test]
        public void MessageHandlerReceivedMessageServerClient()
        {
            // Init
            var gameObject = new GameObject(nameof(MessageHandlerReceivedMessageServerClient));
            var networkManager = gameObject.AddComponent<NetworkManager>();
            var transport = gameObject.AddComponent<DummyTransport>();

            // MLAPI sets this in validate
            networkManager.NetworkConfig = new NetworkConfig()
            {
                // Set the current scene to prevent unexpected log messages which would trigger a failure
                RegisteredScenes = new List<string>() { SceneManager.GetActiveScene().name }
            };

            // Set dummy transport that does nothing
            networkManager.NetworkConfig.NetworkTransport = transport;

            // Replace the real message handler with a dummy one that just prints a result
            networkManager.MessageHandler = new DummyMessageHandler();

            using (var inputBuffer = new NetworkBuffer())
            {
                // Start server since pre-message-handler passes IsServer & IsClient checks
                networkManager.StartServer();

                // Disable batching to make the RPCs come straight through
                // This has to be done post start
                networkManager.RpcQueueContainer.EnableBatchedRpcs(false);

                // Should cause log (server only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleConnectionRequest));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.CONNECTION_REQUEST, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should not cause log (client only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.CONNECTION_APPROVED, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should not cause log (client only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.ADD_OBJECT, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should not cause log (client only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.DESTROY_OBJECT, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should not cause log (client only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.SWITCH_SCENE, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should not cause log (client only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.CHANGE_OWNER, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should not cause log (client only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.ADD_OBJECTS, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should not cause log (client only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.DESTROY_OBJECTS, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should not cause log (client only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.TIME_SYNC, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (server and client)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleNetworkVariableDelta));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.NETWORK_VARIABLE_DELTA, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (server and client)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleNetworkVariableUpdate));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.NETWORK_VARIABLE_UPDATE, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (server and client)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleUnnamedMessage));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.UNNAMED_MESSAGE, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (server and client)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleNamedMessage));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.NAMED_MESSAGE, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (server only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleClientSwitchSceneCompleted));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.CLIENT_SWITCH_SCENE_COMPLETED, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (server only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleNetworkLog));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.SERVER_LOG, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (server only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.RpcReceiveQueueItem));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.SERVER_RPC, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should not cause log (client only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.CLIENT_RPC, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Stop server to trigger full shutdown
                networkManager.Shutdown();

                // Replace the real message handler with a dummy one that just prints a result
                networkManager.MessageHandler = new DummyMessageHandler();

                // Start client since pre-message-handler passes IsServer & IsClient checks
                networkManager.StartClient();

                // Disable batching to make the RPCs come straight through
                // This has to be done post start (and post restart since the queue container is reset)
                networkManager.RpcQueueContainer.EnableBatchedRpcs(false);

                // Should not cause log (server only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.CONNECTION_REQUEST, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (client only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleConnectionApproved));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.CONNECTION_APPROVED, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (client only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleAddObject));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.ADD_OBJECT, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (client only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleDestroyObject));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.DESTROY_OBJECT, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (client only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleSwitchScene));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.SWITCH_SCENE, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (client only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleChangeOwner));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.CHANGE_OWNER, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (client only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleAddObjects));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.ADD_OBJECTS, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (client only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleDestroyObjects));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.DESTROY_OBJECTS, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (client only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleTimeSync));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.TIME_SYNC, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (server and client)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleNetworkVariableDelta));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.NETWORK_VARIABLE_DELTA, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (server and client)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleNetworkVariableUpdate));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.NETWORK_VARIABLE_UPDATE, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (server and client)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleUnnamedMessage));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.UNNAMED_MESSAGE, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (server and client)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.HandleNamedMessage));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.NAMED_MESSAGE, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should not cause log (server only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.CLIENT_SWITCH_SCENE_COMPLETED, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should not cause log (server only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.SERVER_LOG, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should not cause log (server only)
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.SERVER_RPC, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Should cause log (client only)
                LogAssert.Expect(LogType.Log, nameof(MessageHandlerReceivedMessageServerClient) + " " + nameof(DummyMessageHandler.RpcReceiveQueueItem));
                using (var messageStream = MessagePacker.WrapMessage(NetworkConstants.CLIENT_RPC, inputBuffer))
                {
                    networkManager.HandleIncomingData(0, NetworkChannel.Internal, new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length), 0, true);
                }

                // Full cleanup
                networkManager.Shutdown();
            }

            // Ensure no missmatches with expectations
            LogAssert.NoUnexpectedReceived();

            // Cleanup
            Object.DestroyImmediate(gameObject);
        }
    }

    internal class DummyMessageHandler : IInternalMessageHandler
    {
        public NetworkManager NetworkManager { get; }

        public void HandleConnectionRequest(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleConnectionRequest));

        public void HandleConnectionApproved(ulong clientId, Stream stream, float receiveTime) => VerifyCalled(nameof(HandleConnectionApproved));

        public void HandleAddObject(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleAddObject));

        public void HandleDestroyObject(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleDestroyObject));

        public void HandleSwitchScene(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleSwitchScene));

        public void HandleClientSwitchSceneCompleted(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleClientSwitchSceneCompleted));

        public void HandleChangeOwner(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleChangeOwner));

        public void HandleAddObjects(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleAddObjects));

        public void HandleDestroyObjects(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleDestroyObjects));

        public void HandleTimeSync(ulong clientId, Stream stream, float receiveTime) => VerifyCalled(nameof(HandleTimeSync));

        public void HandleNetworkVariableDelta(ulong clientId, Stream stream, Action<ulong, PreBufferPreset> bufferCallback, PreBufferPreset bufferPreset) => VerifyCalled(nameof(HandleNetworkVariableDelta));

        public void HandleNetworkVariableUpdate(ulong clientId, Stream stream, Action<ulong, PreBufferPreset> bufferCallback, PreBufferPreset bufferPreset) => VerifyCalled(nameof(HandleNetworkVariableUpdate));

        public void RpcReceiveQueueItem(ulong clientId, Stream stream, float receiveTime, RpcQueueContainer.QueueItemType queueItemType) => VerifyCalled(nameof(RpcReceiveQueueItem));

        public void HandleUnnamedMessage(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleUnnamedMessage));

        public void HandleNamedMessage(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleNamedMessage));

        public void HandleNetworkLog(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleNetworkLog));

        private void VerifyCalled(string method)
        {
            Debug.Log(nameof(NetworkManagerMessageHandlerTests.MessageHandlerReceivedMessageServerClient) + " " + method);
        }
    }

    // Should probably have one of these for more files? In the future we could use the SIPTransport?
    internal class DummyTransport : NetworkTransport
    {
        public override ulong ServerClientId { get; } = 0;
        public override void Send(ulong clientId, ArraySegment<byte> data, NetworkChannel networkChannel)
        {
        }

        public override NetworkEvent PollEvent(out ulong clientId, out NetworkChannel networkChannel, out ArraySegment<byte> payload, out float receiveTime)
        {
            clientId = 0;
            networkChannel = NetworkChannel.Internal;
            payload = new ArraySegment<byte>();
            receiveTime = 0;
            return NetworkEvent.Nothing;
        }

        public override SocketTasks StartClient()
        {
            return SocketTask.Done.AsTasks();
        }

        public override SocketTasks StartServer()
        {
            return SocketTask.Done.AsTasks();

        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
        }

        public override void DisconnectLocalClient()
        {
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            return 0;
        }

        public override void Shutdown()
        {
        }

        public override void Init()
        {
        }
    }
}
