using System.IO;
using MLAPI.Messaging;
using MLAPI.Serialization;
using MLAPI.Transports;
using UnityEngine;

namespace MLAPI.EditorTests
{
    internal class DummyMessageHandler : IInternalMessageHandler
    {
        public NetworkManager NetworkManager { get; }

        public DummyMessageHandler(NetworkManager networkManager)
        {
            NetworkManager = networkManager;
        }

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

        public void HandleNetworkVariableDelta(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleNetworkVariableDelta));

        public void MessageReceiveQueueItem(ulong clientId, Stream stream, float receiveTime,
            MessageQueueContainer.MessageType messageType,
            NetworkChannel receiveChannel)
        {
            VerifyCalled(nameof(MessageReceiveQueueItem));
            if (NetworkManager)
            {
                // To actually process the message we have to add it to the inbound frame queue for the current update stage
                // and then process and flush the queue for the current update stage to actually get it to run through
                // MessageQueueContainer.ProcessMessage, which is where the actual code handling the message lives.
                // That's what will then call back into this for the others.
                var messageQueueContainer = NetworkManager.MessageQueueContainer;
                messageQueueContainer.AddQueueItemToInboundFrame(messageType, receiveTime, clientId,
                    (NetworkBuffer) stream, receiveChannel);
                messageQueueContainer.ProcessAndFlushMessageQueue(
                    MessageQueueContainer.MessageQueueProcessingTypes.Receive, NetworkUpdateLoop.UpdateStage);
            }
        }

        public void HandleUnnamedMessage(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleUnnamedMessage));

        public void HandleNamedMessage(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleNamedMessage));

        public void HandleNetworkLog(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleNetworkLog));

        public void HandleAllClientsSwitchSceneCompleted(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleAllClientsSwitchSceneCompleted));

        private void VerifyCalled(string method)
        {
            Debug.Log(method);
        }
    }
}
