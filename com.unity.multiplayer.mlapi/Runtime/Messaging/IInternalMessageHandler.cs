using System;
using System.IO;
using MLAPI.Transports;

namespace MLAPI.Messaging
{
    internal interface IInternalMessageHandler
    {
        NetworkManager NetworkManager { get; }
        void HandleConnectionRequest(ulong clientId, Stream stream);
        void HandleConnectionApproved(ulong clientId, Stream stream, float receiveTime);
        void HandleAddObject(ulong clientId, Stream stream);
        void HandleDestroyObject(ulong clientId, Stream stream);
        void HandleSwitchScene(ulong clientId, Stream stream);
        void HandleClientSwitchSceneCompleted(ulong clientId, Stream stream);
        void HandleChangeOwner(ulong clientId, Stream stream);
        void HandleAddObjects(ulong clientId, Stream stream);
        void HandleDestroyObjects(ulong clientId, Stream stream);
        void HandleTimeSync(ulong clientId, Stream stream, float receiveTime);
        void HandleNetworkVariableDelta(ulong clientId, Stream stream);
        void MessageReceiveQueueItem(ulong clientId, Stream stream, float receiveTime, MessageQueueContainer.MessageType messageType, NetworkChannel receiveChannel);
        void HandleUnnamedMessage(ulong clientId, Stream stream);
        void HandleNamedMessage(ulong clientId, Stream stream);
        void HandleNetworkLog(ulong clientId, Stream stream);
        void HandleAllClientsSwitchSceneCompleted(ulong clientId, Stream stream);
    }
}
