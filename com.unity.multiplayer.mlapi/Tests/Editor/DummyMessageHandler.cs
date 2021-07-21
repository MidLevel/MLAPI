using System;
using System.IO;
using MLAPI.Messaging;
using MLAPI.Messaging.Buffering;
using UnityEngine;

namespace MLAPI.EditorTests
{
    internal class DummyMessageHandler : IInternalMessageHandler
    {
        public NetworkManager NetworkManager { get; }

        public void HandleConnectionRequest(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleConnectionRequest));

        public void HandleConnectionApproved(ulong clientId, Stream stream, float receiveTime) => VerifyCalled(nameof(HandleConnectionApproved));

        public void HandleAddObject(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleAddObject));

        public void HandleDestroyObject(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleDestroyObject));

        public void HandleSceneEvent(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleSceneEvent));

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

        public void HandleAllClientsSwitchSceneCompleted(ulong clientId, Stream stream) => VerifyCalled(nameof(HandleAllClientsSwitchSceneCompleted));

        private void VerifyCalled(string method)
        {
            Debug.Log(method);
        }
    }
}
