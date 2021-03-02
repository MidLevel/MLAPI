using System;
using System.Collections.Generic;
using MLAPI.Configuration;
using MLAPI.Internal;
using MLAPI.Profiling;
using MLAPI.Serialization;
using MLAPI.Transports;

namespace MLAPI.Messaging
{
    internal static class InternalMessageSender
    {
        internal static void Send(ulong clientId, byte messageType, NetworkChannel networkChannel, NetworkBuffer messageBuffer)
        {
            messageBuffer.PadStream();

            if (NetworkManager.Singleton.IsServer && clientId == NetworkManager.Singleton.ServerClientId) return;

            using (NetworkBuffer buffer = MessagePacker.WrapMessage(messageType, messageBuffer))
            {
                NetworkProfiler.StartEvent(TickType.Send, (uint)buffer.Length, networkChannel, NetworkConstants.k_MESSAGE_NAMES[messageType]);

                NetworkManager.Singleton.NetworkConfig.NetworkTransport.Send(clientId, new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), networkChannel);
                ProfilerStatManager.bytesSent.Record((int)buffer.Length);
                PerformanceDataManager.Increment(ProfilerConstants.NumberBytesSent, (int)buffer.Length);

                NetworkProfiler.EndEvent();
            }
        }

        internal static void Send(byte messageType, NetworkChannel networkChannel, NetworkBuffer messageBuffer)
        {
            messageBuffer.PadStream();

            using (NetworkBuffer buffer = MessagePacker.WrapMessage(messageType, messageBuffer))
            {
                NetworkProfiler.StartEvent(TickType.Send, (uint)buffer.Length, networkChannel, NetworkConstants.k_MESSAGE_NAMES[messageType]);
                for (int i = 0; i < NetworkManager.Singleton.ConnectedClientsList.Count; i++)
                {
                    if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.ConnectedClientsList[i].ClientId == NetworkManager.Singleton.ServerClientId)
                        continue;

                    NetworkManager.Singleton.NetworkConfig.NetworkTransport.Send(NetworkManager.Singleton.ConnectedClientsList[i].ClientId, new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), networkChannel);
                    ProfilerStatManager.bytesSent.Record((int)buffer.Length);
                    PerformanceDataManager.Increment(ProfilerConstants.NumberBytesSent, (int)buffer.Length);
                }

                NetworkProfiler.EndEvent();
            }
        }

        internal static void Send(byte messageType, NetworkChannel networkChannel, List<ulong> clientIds, NetworkBuffer messageBuffer)
        {
            if (clientIds == null)
            {
                Send(messageType, networkChannel, messageBuffer);
                return;
            }

            messageBuffer.PadStream();

            using (NetworkBuffer buffer = MessagePacker.WrapMessage(messageType, messageBuffer))
            {
                NetworkProfiler.StartEvent(TickType.Send, (uint)buffer.Length, networkChannel, NetworkConstants.k_MESSAGE_NAMES[messageType]);
                for (int i = 0; i < clientIds.Count; i++)
                {
                    if (NetworkManager.Singleton.IsServer && clientIds[i] == NetworkManager.Singleton.ServerClientId)
                        continue;

                    NetworkManager.Singleton.NetworkConfig.NetworkTransport.Send(clientIds[i], new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), networkChannel);
                    ProfilerStatManager.bytesSent.Record((int)buffer.Length);
                    PerformanceDataManager.Increment(ProfilerConstants.NumberBytesSent, (int)buffer.Length);
                }

                NetworkProfiler.EndEvent();
            }
        }

        internal static void Send(byte messageType, NetworkChannel networkChannel, ulong clientIdToIgnore, NetworkBuffer messageBuffer)
        {
            messageBuffer.PadStream();

            using (NetworkBuffer buffer = MessagePacker.WrapMessage(messageType, messageBuffer))
            {
                NetworkProfiler.StartEvent(TickType.Send, (uint)buffer.Length, networkChannel, NetworkConstants.k_MESSAGE_NAMES[messageType]);
                for (int i = 0; i < NetworkManager.Singleton.ConnectedClientsList.Count; i++)
                {
                    if (NetworkManager.Singleton.ConnectedClientsList[i].ClientId == clientIdToIgnore ||
                        (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.ConnectedClientsList[i].ClientId == NetworkManager.Singleton.ServerClientId))
                        continue;

                    NetworkManager.Singleton.NetworkConfig.NetworkTransport.Send(NetworkManager.Singleton.ConnectedClientsList[i].ClientId, new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), networkChannel);
                    ProfilerStatManager.bytesSent.Record((int)buffer.Length);
                    PerformanceDataManager.Increment(ProfilerConstants.NumberBytesSent, (int)buffer.Length);
                }

                NetworkProfiler.EndEvent();
            }
        }
    }
}