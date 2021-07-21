using System;
using System.IO;
using MLAPI.Messaging.Buffering;

namespace MLAPI.Messaging
{
    internal interface IInternalMessageHandler
    {
        NetworkManager NetworkManager { get; }
        void HandleConnectionRequest(ulong clientId, Stream stream);
        void HandleConnectionApproved(ulong clientId, Stream stream, float receiveTime);
        void HandleAddObject(ulong clientId, Stream stream);
        void HandleDestroyObject(ulong clientId, Stream stream);
        void HandleSceneEvent(ulong clientId, Stream stream);
        void HandleChangeOwner(ulong clientId, Stream stream);
        void HandleAddObjects(ulong clientId, Stream stream);
        void HandleDestroyObjects(ulong clientId, Stream stream);
        void HandleTimeSync(ulong clientId, Stream stream, float receiveTime);
        void HandleNetworkVariableDelta(ulong clientId, Stream stream, Action<ulong, PreBufferPreset> bufferCallback, PreBufferPreset bufferPreset);
        void RpcReceiveQueueItem(ulong clientId, Stream stream, float receiveTime, RpcQueueContainer.QueueItemType queueItemType);
        void HandleUnnamedMessage(ulong clientId, Stream stream);
        void HandleNamedMessage(ulong clientId, Stream stream);
        void HandleNetworkLog(ulong clientId, Stream stream);
        void HandleAllClientsSwitchSceneCompleted(ulong clientId, Stream stream);
    }
}
