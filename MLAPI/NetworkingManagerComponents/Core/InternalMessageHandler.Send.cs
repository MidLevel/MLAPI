﻿using System.Collections.Generic;
using MLAPI.Data;
using MLAPI.Data.NetworkProfiler;
using MLAPI.NetworkingManagerComponents.Binary;
using MLAPI.NetworkingManagerComponents.Cryptography;

namespace MLAPI.NetworkingManagerComponents.Core
{
    internal static partial class InternalMessageHandler
    {
        internal static byte[] FinalMessageBuffer;
        internal static void PassthroughSend(uint targetId, uint sourceId, ushort messageType, int channelId, BitReader reader, uint? networkId = null, ushort? orderId = null)
        {
            if (netManager.isHost && targetId == netManager.NetworkConfig.NetworkTransport.HostDummyId)
            {
                //Host trying to send data to it's own client
                return;
            }

            using (BitWriter writer = BitWriter.Get())
            {
                writer.WriteGenericMessageHeader(messageType, networkId != null, networkId.GetValueOrDefault(), orderId.GetValueOrDefault(), true, null, sourceId);

#if !DISABLE_CRYPTOGRAPHY
                if (netManager.NetworkConfig.EncryptedChannelsHashSet.Contains(MessageManager.reverseChannels[channelId]))
                    writer.WriteByteArray(CryptographyHelper.Encrypt(reader.ReadByteArray(), netManager.ConnectedClients[targetId].AesKey));
                else
#endif
                    writer.WriteByteArray(reader.ReadByteArray());

                writer.Finalize(ref FinalMessageBuffer);

                netManager.NetworkConfig.NetworkTransport.QueueMessageForSending(targetId, ref FinalMessageBuffer, (int)writer.GetFinalizeSize(), channelId, false, out byte error);
            }
        }

        internal static void Send(uint clientId, string messageType, string channelName, BitWriter messageWriter, uint? networkId = null, ushort? orderId = null, bool skipQueue = false)
        {
            uint targetClientId = clientId;
            if (netManager.isHost && targetClientId == netManager.NetworkConfig.NetworkTransport.HostDummyId)
            {
                //Don't invoke the message on our own machine. Instant stack overflow.
                return;
            }
            else if (targetClientId == netManager.NetworkConfig.NetworkTransport.HostDummyId)
            {
                //Client trying to send data to host
                targetClientId = netManager.NetworkConfig.NetworkTransport.ServerNetId;
            }

            bool isPassthrough = (!netManager.isServer && clientId != netManager.NetworkConfig.NetworkTransport.ServerNetId && netManager.NetworkConfig.AllowPassthroughMessages);
            if (isPassthrough && !netManager.NetworkConfig.PassthroughMessageHashSet.Contains(MessageManager.messageTypes[messageType]))
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("The The MessageType " + messageType + " is not registered as an allowed passthrough message type");
                return;
            }

            using (BitWriter writer = BitWriter.Get())
            {
                writer.WriteGenericMessageHeader(MessageManager.messageTypes[messageType], networkId != null, networkId.GetValueOrDefault(), orderId.GetValueOrDefault(), isPassthrough, clientId, null);

#if !DISABLE_CRYPTOGRAPHY
                if (netManager.NetworkConfig.EncryptedChannelsHashSet.Contains(channelName))
                {
                    //This is an encrypted message.
                    byte[] encrypted;
                    if (netManager.isServer)
                        encrypted = CryptographyHelper.Encrypt(messageWriter.Finalize(), netManager.ConnectedClients[clientId].AesKey);
                    else
                        encrypted = CryptographyHelper.Encrypt(messageWriter.Finalize(), netManager.clientAesKey);

                    writer.WriteByteArray(encrypted);
                }
                else
#endif
                    writer.WriteWriter(messageWriter);

                if (isPassthrough)
                    targetClientId = netManager.NetworkConfig.NetworkTransport.ServerNetId;

                writer.Finalize(ref FinalMessageBuffer);

                NetworkProfiler.StartEvent(TickType.Send, (uint)messageWriter.GetFinalizeSize(), channelName, messageType);
                byte error;
                if (skipQueue)
                    netManager.NetworkConfig.NetworkTransport.QueueMessageForSending(targetClientId, ref FinalMessageBuffer, (int)writer.GetFinalizeSize(), MessageManager.channels[channelName], true, out error);
                else
                    netManager.NetworkConfig.NetworkTransport.QueueMessageForSending(targetClientId, ref FinalMessageBuffer, (int)writer.GetFinalizeSize(), MessageManager.channels[channelName], false, out error);
                NetworkProfiler.EndEvent();
            }
        }

        internal static void Send(uint[] clientIds, string messageType, string channelName, BitWriter messageWriter, uint? networkId = null, ushort? orderId = null)
        {
            if (netManager.NetworkConfig.EncryptedChannelsHashSet.Contains(channelName))
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Cannot send messages over encrypted channel to multiple clients");
                return;
            }

            using (BitWriter writer = BitWriter.Get())
            {
                writer.WriteGenericMessageHeader(MessageManager.messageTypes[messageType], networkId != null, networkId.GetValueOrDefault(), orderId.GetValueOrDefault(), false, 0, 0);

                writer.WriteWriter(messageWriter);

                int channel = MessageManager.channels[channelName];
                for (int i = 0; i < clientIds.Length; i++)
                {
                    uint targetClientId = clientIds[i];
                    if (netManager.isHost && targetClientId == netManager.NetworkConfig.NetworkTransport.HostDummyId)
                    {
                        //Don't invoke the message on our own machine. Instant stack overflow.
                        continue;
                    }
                    else if (targetClientId == netManager.NetworkConfig.NetworkTransport.HostDummyId)
                    {
                        //Client trying to send data to host
                        targetClientId = netManager.NetworkConfig.NetworkTransport.ServerNetId;
                    }

                    writer.Finalize(ref FinalMessageBuffer);

                    NetworkProfiler.StartEvent(TickType.Send, (uint)messageWriter.GetFinalizeSize(), channelName, messageType);
                    netManager.NetworkConfig.NetworkTransport.QueueMessageForSending(targetClientId, ref FinalMessageBuffer, (int)writer.GetFinalizeSize(), channel, false, out byte error);
                    NetworkProfiler.EndEvent();
                }
            }
        }

        internal static void Send(List<uint> clientIds, string messageType, string channelName, BitWriter messageWriter, uint? networkId = null, ushort? orderId = null)
        {
            if (netManager.NetworkConfig.EncryptedChannelsHashSet.Contains(channelName))
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Cannot send messages over encrypted channel to multiple clients");
                return;
            }

            using (BitWriter writer = BitWriter.Get())
            {
                writer.WriteGenericMessageHeader(MessageManager.messageTypes[messageType], networkId != null, networkId.GetValueOrDefault(), orderId.GetValueOrDefault(), false, 0, 0);

                writer.WriteWriter(messageWriter);

                int channel = MessageManager.channels[channelName];
                for (int i = 0; i < clientIds.Count; i++)
                {
                    uint targetClientId = clientIds[i];
                    if (netManager.isHost && targetClientId == netManager.NetworkConfig.NetworkTransport.HostDummyId)
                    {
                        //Don't invoke the message on our own machine. Instant stack overflow.
                        continue;
                    }
                    else if (targetClientId == netManager.NetworkConfig.NetworkTransport.HostDummyId)
                    {
                        //Client trying to send data to host
                        targetClientId = netManager.NetworkConfig.NetworkTransport.ServerNetId;
                    }

                    writer.Finalize(ref FinalMessageBuffer);

                    NetworkProfiler.StartEvent(TickType.Send, (uint)messageWriter.GetFinalizeSize(), channelName, messageType);
                    netManager.NetworkConfig.NetworkTransport.QueueMessageForSending(targetClientId, ref FinalMessageBuffer, (int)writer.GetFinalizeSize(), channel, false, out byte error);
                    NetworkProfiler.EndEvent();
                }
            }
        }

        internal static void Send(string messageType, string channelName, BitWriter messageWriter,  uint? networkId = null, ushort? orderId = null)
        {
            if (netManager.NetworkConfig.EncryptedChannels.Contains(channelName))
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Cannot send messages over encrypted channel to multiple clients");
                return;
            }

            using (BitWriter writer = BitWriter.Get())
            {
                writer.WriteGenericMessageHeader(MessageManager.messageTypes[messageType], networkId != null, networkId.GetValueOrDefault(), orderId.GetValueOrDefault(), false, 0, 0);

                writer.WriteWriter(messageWriter);

                int channel = MessageManager.channels[channelName];
                foreach (KeyValuePair<uint, NetworkedClient> pair in netManager.ConnectedClients)
                {
                    uint targetClientId = pair.Key;
                    if (netManager.isHost && targetClientId == netManager.NetworkConfig.NetworkTransport.HostDummyId)
                    {
                        //Don't invoke the message on our own machine. Instant stack overflow.
                        continue;
                    }
                    else if (targetClientId == netManager.NetworkConfig.NetworkTransport.HostDummyId)
                    {
                        //Client trying to send data to host
                        targetClientId = netManager.NetworkConfig.NetworkTransport.ServerNetId;
                    }

                    writer.Finalize(ref FinalMessageBuffer);

                    NetworkProfiler.StartEvent(TickType.Send, (uint)messageWriter.GetFinalizeSize(), channelName, messageType);
                    netManager.NetworkConfig.NetworkTransport.QueueMessageForSending(targetClientId, ref FinalMessageBuffer, (int)writer.GetFinalizeSize(), channel, false, out byte error);
                    NetworkProfiler.EndEvent();
                }
            }
        }

        internal static void Send(string messageType, string channelName, BitWriter messageWriter, uint clientIdToIgnore, uint? networkId = null, ushort? orderId = null)
        {
            if (netManager.NetworkConfig.EncryptedChannels.Contains(channelName))
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Cannot send messages over encrypted channel to multiple clients");
                return;
            }

            using (BitWriter writer = BitWriter.Get())
            {
                writer.WriteGenericMessageHeader(MessageManager.messageTypes[messageType], networkId != null, networkId.GetValueOrDefault(), orderId.GetValueOrDefault(), false, 0, 0);

                writer.WriteWriter(messageWriter);

                int channel = MessageManager.channels[channelName];
                foreach (KeyValuePair<uint, NetworkedClient> pair in netManager.ConnectedClients)
                {
                    if (pair.Key == clientIdToIgnore)
                        continue;

                    uint targetClientId = pair.Key;
                    if (netManager.isHost && targetClientId == netManager.NetworkConfig.NetworkTransport.HostDummyId)
                    {
                        //Don't invoke the message on our own machine. Instant stack overflow.
                        continue;
                    }
                    else if (targetClientId == netManager.NetworkConfig.NetworkTransport.HostDummyId)
                    {
                        //Client trying to send data to host
                        targetClientId = netManager.NetworkConfig.NetworkTransport.ServerNetId;
                    }

                    writer.Finalize(ref FinalMessageBuffer);

                    NetworkProfiler.StartEvent(TickType.Send, (uint)messageWriter.GetFinalizeSize(), channelName, messageType);
                    netManager.NetworkConfig.NetworkTransport.QueueMessageForSending(targetClientId, ref FinalMessageBuffer, (int)writer.GetFinalizeSize(), channel, false, out byte error);
                    NetworkProfiler.EndEvent();
                }
            }
        }

        private static void WriteGenericMessageHeader(this BitWriter writer, ushort messageType, bool isTargeted, uint targetNetworkId, ushort behaviourIndex, bool isPassthrough, uint? passthroughTarget, uint? passthroughOrigin)
        {
            writer.WriteUShort(messageType);
            writer.WriteBool(isTargeted);
            if (isTargeted)
            {
                writer.WriteUInt(targetNetworkId);
                writer.WriteUShort(behaviourIndex);
            }
            writer.WriteBool(isPassthrough);
            if (isPassthrough)
            {
                if (passthroughTarget != null) writer.WriteUInt(passthroughTarget.Value);
                if (passthroughOrigin != null) writer.WriteUInt(passthroughOrigin.Value);
            }
            writer.WriteAlignBits();
        }
    }
}
