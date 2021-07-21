using System;
using System.IO;
using MLAPI.Messaging;
using MLAPI.Messaging.Buffering;
using Unity.Profiling;

namespace MLAPI.Profiling
{
    internal class InternalMessageHandlerProfilingDecorator : IInternalMessageHandler
    {
        private readonly ProfilerMarker m_HandleConnectionRequest = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleConnectionRequest)}");
        private readonly ProfilerMarker m_HandleConnectionApproved = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleConnectionApproved)}");
        private readonly ProfilerMarker m_HandleAddObject = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleAddObject)}");
        private readonly ProfilerMarker m_HandleDestroyObject = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleDestroyObject)}");
        private readonly ProfilerMarker m_HandleSceneEvent = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleSceneEvent)}");
        private readonly ProfilerMarker m_HandleChangeOwner = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleChangeOwner)}");
        private readonly ProfilerMarker m_HandleAddObjects = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleAddObjects)}");
        private readonly ProfilerMarker m_HandleDestroyObjects = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleDestroyObjects)}");
        private readonly ProfilerMarker m_HandleTimeSync = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleTimeSync)}");
        private readonly ProfilerMarker m_HandleNetworkVariableDelta = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleNetworkVariableDelta)}");
        private readonly ProfilerMarker m_HandleUnnamedMessage = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleUnnamedMessage)}");
        private readonly ProfilerMarker m_HandleNamedMessage = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleNamedMessage)}");
        private readonly ProfilerMarker m_HandleNetworkLog = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(HandleNetworkLog)}");
        private readonly ProfilerMarker m_RpcReceiveQueueItemServerRpc = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(RpcReceiveQueueItem)}.{nameof(RpcQueueContainer.QueueItemType.ServerRpc)}");
        private readonly ProfilerMarker m_RpcReceiveQueueItemClientRpc = new ProfilerMarker($"{nameof(InternalMessageHandler)}.{nameof(RpcReceiveQueueItem)}.{nameof(RpcQueueContainer.QueueItemType.ClientRpc)}");
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

        public void HandleNetworkVariableDelta(ulong clientId, Stream stream, Action<ulong, PreBufferPreset> bufferCallback, PreBufferPreset bufferPreset)
        {
            m_HandleNetworkVariableDelta.Begin();

            m_MessageHandler.HandleNetworkVariableDelta(clientId, stream, bufferCallback, bufferPreset);

            m_HandleNetworkVariableDelta.End();
        }

        public void RpcReceiveQueueItem(ulong clientId, Stream stream, float receiveTime, RpcQueueContainer.QueueItemType queueItemType)
        {
            switch (queueItemType)
            {
                case RpcQueueContainer.QueueItemType.ServerRpc:
                    m_RpcReceiveQueueItemServerRpc.Begin();
                    break;
                case RpcQueueContainer.QueueItemType.ClientRpc:
                    m_RpcReceiveQueueItemClientRpc.Begin();
                    break;
            }

            m_MessageHandler.RpcReceiveQueueItem(clientId, stream, receiveTime, queueItemType);

            switch (queueItemType)
            {
                case RpcQueueContainer.QueueItemType.ServerRpc:
                    m_RpcReceiveQueueItemServerRpc.End();
                    break;
                case RpcQueueContainer.QueueItemType.ClientRpc:
                    m_RpcReceiveQueueItemClientRpc.End();
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

        public void HandleSceneEvent(ulong clientId, Stream stream)
        {
            m_HandleSceneEvent.Begin();

            m_MessageHandler.HandleSceneEvent(clientId, stream);

            m_HandleSceneEvent.End();
        }

        public void HandleAllClientsSwitchSceneCompleted(ulong clientId, Stream stream)
        {
            m_HandleAllClientsSwitchSceneCompleted.Begin();

            m_MessageHandler.HandleAllClientsSwitchSceneCompleted(clientId, stream);

            m_HandleAllClientsSwitchSceneCompleted.End();
        }
    }
}
