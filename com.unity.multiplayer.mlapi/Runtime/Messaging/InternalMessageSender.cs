using System;
using System.Collections.Generic;
using MLAPI.Configuration;
using MLAPI.Internal;
using MLAPI.Profiling;
using MLAPI.Serialization;
using MLAPI.Transports;

namespace MLAPI.Messaging
{
    internal class InternalMessageSender
    {
        private NetworkManager m_NetworkManager { get; }

        internal InternalMessageSender(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager;
        }

        internal void Send(ulong clientId, byte messageType, NetworkChannel networkChannel, NetworkBuffer messageBuffer)
        {
            messageBuffer.PadBuffer();

            if (m_NetworkManager.IsServer && clientId == m_NetworkManager.ServerClientId)
            {
                return;
            }

            using (var buffer = MessagePacker.WrapMessage(messageType, messageBuffer))
            {
#if !UNITY_2020_2_OR_NEWER
                NetworkProfiler.StartEvent(TickType.Send, (uint)buffer.Length, networkChannel, NetworkConstants.MESSAGE_NAMES[messageType]);
#endif

                m_NetworkManager.NetworkConfig.NetworkTransport.Send(clientId, new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), networkChannel);
                PerformanceDataManager.Increment(ProfilerConstants.ByteSent, (int)buffer.Length);

#if !UNITY_2020_2_OR_NEWER
                NetworkProfiler.EndEvent();
#endif
            }
        }

        internal void Send(byte messageType, NetworkChannel networkChannel, NetworkBuffer messageBuffer)
        {
            messageBuffer.PadBuffer();

            using (var buffer = MessagePacker.WrapMessage(messageType, messageBuffer))
            {
#if !UNITY_2020_2_OR_NEWER
                NetworkProfiler.StartEvent(TickType.Send, (uint)buffer.Length, networkChannel, NetworkConstants.MESSAGE_NAMES[messageType]);
#endif

                for (int i = 0; i < m_NetworkManager.ConnectedClientsList.Count; i++)
                {
                    if (m_NetworkManager.IsServer && m_NetworkManager.ConnectedClientsList[i].ClientId == m_NetworkManager.ServerClientId)
                    {
                        continue;
                    }

                    m_NetworkManager.NetworkConfig.NetworkTransport.Send(m_NetworkManager.ConnectedClientsList[i].ClientId, new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), networkChannel);
                    PerformanceDataManager.Increment(ProfilerConstants.ByteSent, (int)buffer.Length);
                }

#if !UNITY_2020_2_OR_NEWER
                NetworkProfiler.EndEvent();
#endif
            }
        }

        internal void Send(byte messageType, NetworkChannel networkChannel, List<ulong> clientIds, NetworkBuffer messageBuffer)
        {
            if (clientIds == null)
            {
                Send(messageType, networkChannel, messageBuffer);
                return;
            }

            messageBuffer.PadBuffer();

            using (var buffer = MessagePacker.WrapMessage(messageType, messageBuffer))
            {
#if !UNITY_2020_2_OR_NEWER
                NetworkProfiler.StartEvent(TickType.Send, (uint)buffer.Length, networkChannel, NetworkConstants.MESSAGE_NAMES[messageType]);
#endif

                for (int i = 0; i < clientIds.Count; i++)
                {
                    if (m_NetworkManager.IsServer && clientIds[i] == m_NetworkManager.ServerClientId)
                    {
                        continue;
                    }

                    m_NetworkManager.NetworkConfig.NetworkTransport.Send(clientIds[i], new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), networkChannel);
                    PerformanceDataManager.Increment(ProfilerConstants.ByteSent, (int)buffer.Length);
                }

#if !UNITY_2020_2_OR_NEWER
                NetworkProfiler.EndEvent();
#endif
            }
        }

        internal void Send(byte messageType, NetworkChannel networkChannel, ulong clientIdToIgnore, NetworkBuffer messageBuffer)
        {
            messageBuffer.PadBuffer();

            using (var buffer = MessagePacker.WrapMessage(messageType, messageBuffer))
            {
#if !UNITY_2020_2_OR_NEWER
                NetworkProfiler.StartEvent(TickType.Send, (uint)buffer.Length, networkChannel, NetworkConstants.MESSAGE_NAMES[messageType]);
#endif

                for (int i = 0; i < m_NetworkManager.ConnectedClientsList.Count; i++)
                {
                    if (m_NetworkManager.ConnectedClientsList[i].ClientId == clientIdToIgnore ||
                        (m_NetworkManager.IsServer && m_NetworkManager.ConnectedClientsList[i].ClientId == m_NetworkManager.ServerClientId))
                    {
                        continue;
                    }

                    m_NetworkManager.NetworkConfig.NetworkTransport.Send(m_NetworkManager.ConnectedClientsList[i].ClientId, new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), networkChannel);
                    PerformanceDataManager.Increment(ProfilerConstants.ByteSent, (int)buffer.Length);
                }

#if !UNITY_2020_2_OR_NEWER
                NetworkProfiler.EndEvent();
#endif
            }
        }
    }
}
