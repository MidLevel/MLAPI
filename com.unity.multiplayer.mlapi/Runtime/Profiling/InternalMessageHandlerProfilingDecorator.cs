using System.IO;
using MLAPI.Messaging;
using MLAPI.Transports;
using Unity.Profiling;

namespace MLAPI.Profiling
{
    internal class InternalMessageHandlerProfilingDecorator : IInternalMessageHandler
    {
        private readonly ProfilerMarker m_HandleConnectionRequest = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleConnectionRequest)}");
        private readonly ProfilerMarker m_HandleConnectionApproved = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleConnectionApproved)}");
        private readonly ProfilerMarker m_HandleAddObject = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleAddObject)}");
        private readonly ProfilerMarker m_HandleDestroyObject = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleDestroyObject)}");
        private readonly ProfilerMarker m_HandleSwitchScene = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleSwitchScene)}");
        private readonly ProfilerMarker m_HandleClientSwitchSceneCompleted = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleClientSwitchSceneCompleted)}");
        private readonly ProfilerMarker m_HandleChangeOwner = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleChangeOwner)}");
        private readonly ProfilerMarker m_HandleAddObjects = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleAddObjects)}");
        private readonly ProfilerMarker m_HandleDestroyObjects = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleDestroyObjects)}");
        private readonly ProfilerMarker m_HandleTimeSync = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleTimeSync)}");
        private readonly ProfilerMarker m_HandleNetworkVariableDelta = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleNetworkVariableDelta)}");
        private readonly ProfilerMarker m_HandleUnnamedMessage = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleUnnamedMessage)}");
        private readonly ProfilerMarker m_HandleNamedMessage = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleNamedMessage)}");
        private readonly ProfilerMarker m_HandleNetworkLog = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleNetworkLog)}");
        private readonly ProfilerMarker m_MessageReceiveQueueItemServerRpc = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(MessageReceiveQueueItem)}.{nameof(MessageQueueContainer.MessageType.ServerRpc)}");
        private readonly ProfilerMarker m_MessageReceiveQueueItemClientRpc = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(MessageReceiveQueueItem)}.{nameof(MessageQueueContainer.MessageType.ClientRpc)}");
        private readonly ProfilerMarker m_MessageReceiveQueueItemInternalMessage = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(MessageReceiveQueueItem)}.InternalMessage");
        private readonly ProfilerMarker m_HandleAllClientsSwitchSceneCompleted = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleAllClientsSwitchSceneCompleted)}");

        private readonly IInternalMessageHandler m_MessageHandler;

        internal InternalMessageHandlerProfilingDecorator(IInternalMessageHandler messageHandler)
        {
            m_MessageHandler = messageHandler;
        }

        public NetworkManager NetworkManager => m_MessageHandler.NetworkManager;

        public void HandleConnectionRequest(ulong clientId, Stream stream)
        {
            m_HandleConnectionRequest.Begin();

            m_MessageHandler.HandleConnectionRequest(clientId, stream);

            m_HandleConnectionRequest.End();
        }

        public void HandleConnectionApproved(ulong clientId, Stream stream, float receiveTime)
        {
            m_HandleConnectionApproved.Begin();

            m_MessageHandler.HandleConnectionApproved(clientId, stream, receiveTime);

            m_HandleConnectionApproved.End();
        }

        public void HandleAddObject(ulong clientId, Stream stream)
        {
            m_HandleAddObject.Begin();

            m_MessageHandler.HandleAddObject(clientId, stream);

            m_HandleAddObject.End();
        }

        public void HandleDestroyObject(ulong clientId, Stream stream)
        {
            m_HandleDestroyObject.Begin();

            m_MessageHandler.HandleDestroyObject(clientId, stream);

            m_HandleDestroyObject.End();
        }

        public void HandleSwitchScene(ulong clientId, Stream stream)
        {
            m_HandleSwitchScene.Begin();

            m_MessageHandler.HandleSwitchScene(clientId, stream);

            m_HandleSwitchScene.End();
        }

        public void HandleClientSwitchSceneCompleted(ulong clientId, Stream stream)
        {
            m_HandleClientSwitchSceneCompleted.Begin();

            m_MessageHandler.HandleClientSwitchSceneCompleted(clientId, stream);

            m_HandleClientSwitchSceneCompleted.End();
        }

        public void HandleChangeOwner(ulong clientId, Stream stream)
        {
            m_HandleChangeOwner.Begin();

            m_MessageHandler.HandleChangeOwner(clientId, stream);

            m_HandleChangeOwner.End();
        }

        public void HandleAddObjects(ulong clientId, Stream stream)
        {
            m_HandleAddObjects.Begin();

            m_MessageHandler.HandleAddObjects(clientId, stream);

            m_HandleAddObjects.End();
        }

        public void HandleDestroyObjects(ulong clientId, Stream stream)
        {
            m_HandleDestroyObjects.Begin();

            m_MessageHandler.HandleDestroyObjects(clientId, stream);

            m_HandleDestroyObjects.End();
        }

        public void HandleTimeSync(ulong clientId, Stream stream, float receiveTime)
        {
            m_HandleTimeSync.Begin();

            m_MessageHandler.HandleTimeSync(clientId, stream, receiveTime);

            m_HandleTimeSync.End();
        }

        public void HandleNetworkVariableDelta(ulong clientId, Stream stream)
        {
            m_HandleNetworkVariableDelta.Begin();

            m_MessageHandler.HandleNetworkVariableDelta(clientId, stream);

            m_HandleNetworkVariableDelta.End();
        }

        public void MessageReceiveQueueItem(ulong clientId, Stream stream, float receiveTime, MessageQueueContainer.MessageType messageType, NetworkChannel receiveChannel)
        {
            switch (messageType)
            {
                case MessageQueueContainer.MessageType.ServerRpc:
                    m_MessageReceiveQueueItemServerRpc.Begin();
                    break;
                case MessageQueueContainer.MessageType.ClientRpc:
                    m_MessageReceiveQueueItemClientRpc.Begin();
                    break;
                default:
                    m_MessageReceiveQueueItemInternalMessage.Begin();
                    break;
            }

            m_MessageHandler.MessageReceiveQueueItem(clientId, stream, receiveTime, messageType, receiveChannel);

            switch (messageType)
            {
                case MessageQueueContainer.MessageType.ServerRpc:
                    m_MessageReceiveQueueItemServerRpc.End();
                    break;
                case MessageQueueContainer.MessageType.ClientRpc:
                    m_MessageReceiveQueueItemClientRpc.End();
                    break;
                default:
                    m_MessageReceiveQueueItemInternalMessage.End();
                    break;
            }
        }

        public void HandleUnnamedMessage(ulong clientId, Stream stream)
        {
            m_HandleUnnamedMessage.Begin();

            m_MessageHandler.HandleUnnamedMessage(clientId, stream);

            m_HandleUnnamedMessage.End();
        }

        public void HandleNamedMessage(ulong clientId, Stream stream)
        {
            m_HandleNamedMessage.Begin();

            m_MessageHandler.HandleNamedMessage(clientId, stream);

            m_HandleNamedMessage.End();
        }

        public void HandleNetworkLog(ulong clientId, Stream stream)
        {
            m_HandleNetworkLog.Begin();

            m_MessageHandler.HandleNetworkLog(clientId, stream);

            m_HandleNetworkLog.End();
        }

        public void HandleAllClientsSwitchSceneCompleted(ulong clientId, Stream stream)
        {
            m_HandleAllClientsSwitchSceneCompleted.Begin();

            m_MessageHandler.HandleAllClientsSwitchSceneCompleted(clientId, stream);

            m_HandleAllClientsSwitchSceneCompleted.End();
        }
    }
}
