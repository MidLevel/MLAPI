#pragma warning disable 618
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;

namespace MLAPI.Transports.UNET
{
    public static class RelayTransport
    {
        private enum MessageType
        {
            StartServer,
            ConnectToServer,
            Data,
            ClientDisconnect,
            AddressReport
        }

        private static byte s_DefaultChannelId;
        private static int s_RelayConnectionId;
        private static bool s_IsClient = false;
        private static string s_Address;
        private static ushort s_Port;
        private static List<ChannelQOS> s_Channels = new List<ChannelQOS>();

        public static bool Enabled { get; set; } = true;
        public static string RelayAddress { get; set; } = "127.0.0.1";
        public static ushort RelayPort { get; set; } = 8888;

        public static event Action<IPEndPoint> OnRemoteEndpointReported;

        public static int Connect(int hostId, string serverAddress, int serverPort, int exceptionConnectionId, out byte error)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.Connect(hostId, serverAddress, serverPort, exceptionConnectionId, out error);

            s_IsClient = true;

            s_Address = serverAddress;
            s_Port = (ushort)serverPort;

            s_RelayConnectionId = UnityEngine.Networking.NetworkTransport.Connect(hostId, RelayAddress, RelayPort, exceptionConnectionId, out error); // Requests connection

            return s_RelayConnectionId;
        }

        public static int ConnectWithSimulator(int hostId, string serverAddress, int serverPort, int exceptionConnectionId, out byte error, ConnectionSimulatorConfig conf)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.ConnectWithSimulator(hostId, serverAddress, serverPort, exceptionConnectionId, out error, conf);

            s_IsClient = true;

            s_Address = serverAddress;
            s_Port = (ushort)serverPort;

            s_RelayConnectionId = UnityEngine.Networking.NetworkTransport.ConnectWithSimulator(hostId, RelayAddress, RelayPort, exceptionConnectionId, out error, conf); // Requests connection

            return s_RelayConnectionId;
        }

        public static int ConnectEndPoint(int hostId, EndPoint endPoint, int exceptionConnectionId, out byte error)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.ConnectEndPoint(hostId, endPoint, exceptionConnectionId, out error);

            s_IsClient = true;

            s_Address = ((IPEndPoint)endPoint).Address.ToString();
            s_Port = (ushort)((IPEndPoint)endPoint).Port;

            s_RelayConnectionId = UnityEngine.Networking.NetworkTransport.Connect(hostId, RelayAddress, RelayPort, exceptionConnectionId, out error); // Requests connection

            return s_RelayConnectionId;
        }

        private static void SetChannelsFromTopology(HostTopology topology) => s_Channels = topology.DefaultConfig.Channels;

        public static int AddHost(HostTopology topology, bool createServer)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.AddHost(topology, 0, null);

            s_IsClient = !createServer;

            s_DefaultChannelId = topology.DefaultConfig.AddChannel(QosType.ReliableSequenced);

            SetChannelsFromTopology(topology);

            int ret = UnityEngine.Networking.NetworkTransport.AddHost(topology, 0, null);

            if (createServer) s_RelayConnectionId = UnityEngine.Networking.NetworkTransport.Connect(ret, RelayAddress, RelayPort, 0, out byte b);

            return ret;
        }

        public static int AddHost(HostTopology topology, int port, bool createServer)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.AddHost(topology, port);

            s_IsClient = !createServer;

            s_DefaultChannelId = topology.DefaultConfig.AddChannel(QosType.ReliableSequenced);

            SetChannelsFromTopology(topology);

            int ret = UnityEngine.Networking.NetworkTransport.AddHost(topology, port);

            if (createServer) s_RelayConnectionId = UnityEngine.Networking.NetworkTransport.Connect(ret, RelayAddress, RelayPort, 0, out byte b);

            return ret;
        }

        public static int AddHost(HostTopology topology, int port, string ip, bool createServer)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.AddHost(topology, port, ip);

            s_IsClient = !createServer;

            s_DefaultChannelId = topology.DefaultConfig.AddChannel(QosType.ReliableSequenced);

            SetChannelsFromTopology(topology);

            int ret = UnityEngine.Networking.NetworkTransport.AddHost(topology, port, ip);

            if (createServer) s_RelayConnectionId = UnityEngine.Networking.NetworkTransport.Connect(ret, RelayAddress, RelayPort, 0, out byte b);

            return ret;
        }

        public static int AddHostWithSimulator(HostTopology topology, int minTimeout, int maxTimeout, int port, string ip, bool createServer)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.AddHostWithSimulator(topology, minTimeout, maxTimeout);

            s_IsClient = !createServer;

            s_DefaultChannelId = topology.DefaultConfig.AddChannel(QosType.ReliableSequenced);

            SetChannelsFromTopology(topology);

            int ret = UnityEngine.Networking.NetworkTransport.AddHostWithSimulator(topology, minTimeout, maxTimeout, port, ip);

            if (createServer) s_RelayConnectionId = UnityEngine.Networking.NetworkTransport.Connect(ret, RelayAddress, RelayPort, 0, out byte b);

            return ret;
        }

        public static int AddHostWithSimulator(HostTopology topology, int minTimeout, int maxTimeout, bool createServer)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.AddHostWithSimulator(topology, minTimeout, maxTimeout);

            s_IsClient = !createServer;

            s_DefaultChannelId = topology.DefaultConfig.AddChannel(QosType.ReliableSequenced);

            SetChannelsFromTopology(topology);

            int ret = UnityEngine.Networking.NetworkTransport.AddHostWithSimulator(topology, minTimeout, maxTimeout);

            if (createServer) s_RelayConnectionId = UnityEngine.Networking.NetworkTransport.Connect(ret, RelayAddress, RelayPort, 0, out byte b);

            return ret;
        }

        public static int AddHostWithSimulator(HostTopology topology, int minTimeout, int maxTimeout, int port, bool createServer)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.AddHostWithSimulator(topology, minTimeout, maxTimeout, port);

            s_IsClient = !createServer;

            SetChannelsFromTopology(topology);

            int ret = UnityEngine.Networking.NetworkTransport.AddHostWithSimulator(topology, minTimeout, maxTimeout, port);

            if (createServer) s_RelayConnectionId = UnityEngine.Networking.NetworkTransport.Connect(ret, RelayAddress, RelayPort, 0, out byte b);

            return ret;
        }

        public static int AddWebsocketHost(HostTopology topology, int port, bool createServer)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.AddWebsocketHost(topology, port);

            s_IsClient = !createServer;

            s_DefaultChannelId = topology.DefaultConfig.AddChannel(QosType.ReliableSequenced);

            SetChannelsFromTopology(topology);

            int ret = UnityEngine.Networking.NetworkTransport.AddWebsocketHost(topology, port);

            if (createServer) s_RelayConnectionId = UnityEngine.Networking.NetworkTransport.Connect(ret, RelayAddress, RelayPort, 0, out byte b);

            return ret;
        }

        public static int AddWebsocketHost(HostTopology topology, int port, string ip, bool createServer)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.AddWebsocketHost(topology, port, ip);

            s_IsClient = !createServer;

            s_DefaultChannelId = topology.DefaultConfig.AddChannel(QosType.ReliableSequenced);

            SetChannelsFromTopology(topology);

            int ret = UnityEngine.Networking.NetworkTransport.AddWebsocketHost(topology, port, ip);

            if (createServer) s_RelayConnectionId = UnityEngine.Networking.NetworkTransport.Connect(ret, RelayAddress, RelayPort, 0, out byte b);

            return ret;
        }

        private static readonly byte[] disconnectBuffer = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, (byte)MessageType.ClientDisconnect };

        public static bool Disconnect(int hostId, int connectionId, out byte error)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.Disconnect(hostId, connectionId, out error);

            if (!s_IsClient)
            {
                for (byte i = 0; i < sizeof(ulong); i++) disconnectBuffer[i] = ((byte)((ulong)connectionId >> (i * 8)));

                return UnityEngine.Networking.NetworkTransport.Send(hostId, s_RelayConnectionId, s_DefaultChannelId, disconnectBuffer, 9, out error);
            }

            return UnityEngine.Networking.NetworkTransport.Disconnect(hostId, connectionId, out error);
        }

        public static bool Send(int hostId, int connectionId, int channelId, byte[] buffer, int size, out byte error)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.Send(hostId, connectionId, channelId, buffer, size, out error);

            ++size;

            if (!s_IsClient)
            {
                size += 8;

                if (buffer.Length < size)
                {
                    byte[] extendedBuffer = new byte[size];
                    Buffer.BlockCopy(buffer, 0, extendedBuffer, 0, buffer.Length);
                    buffer = extendedBuffer;
                }

                int connectionIdOffset = size - 9;

                for (byte i = 0; i < sizeof(ulong); i++) buffer[connectionIdOffset + i] = ((byte)((ulong)connectionId >> (i * 8)));
            }

            buffer[size - 1] = (byte)MessageType.Data;

            return UnityEngine.Networking.NetworkTransport.Send(hostId, s_RelayConnectionId, channelId, buffer, size, out error);
        }

        public static bool QueueMessageForSending(int hostId, int connectionId, int channelId, byte[] buffer, int size, out byte error)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.QueueMessageForSending(hostId, connectionId, channelId, buffer, size, out error);

            ++size;

            if (!s_IsClient)
            {
                size += 8;

                int connectionIdOffset = size - 9;

                for (byte i = 0; i < sizeof(ulong); i++) buffer[connectionIdOffset + i] = ((byte)((ulong)connectionId >> (i * 8)));
            }

            buffer[size - 1] = (byte)MessageType.Data;

            return UnityEngine.Networking.NetworkTransport.QueueMessageForSending(hostId, s_RelayConnectionId, channelId, buffer, size, out error);
        }

        public static bool SendQueuedMessages(int hostId, int connectionId, out byte error)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.SendQueuedMessages(hostId, connectionId, out error);

            return UnityEngine.Networking.NetworkTransport.SendQueuedMessages(hostId, s_RelayConnectionId, out error);
        }

        public static NetworkEventType ReceiveFromHost(int hostId, out int connectionId, out int channelId, byte[] buffer, int bufferSize, out int receivedSize, out byte error)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.ReceiveFromHost(hostId, out connectionId, out channelId, buffer, bufferSize, out receivedSize, out error);

            var eventType = UnityEngine.Networking.NetworkTransport.ReceiveFromHost(hostId, out connectionId, out channelId, buffer, bufferSize, out receivedSize, out error);

            return BaseReceive(eventType, hostId, ref connectionId, ref channelId, buffer, bufferSize, ref receivedSize, ref error);
        }

        public static NetworkEventType Receive(out int hostId, out int connectionId, out int channelId, byte[] buffer, int bufferSize, out int receivedSize, out byte error)
        {
            if (!Enabled) return UnityEngine.Networking.NetworkTransport.Receive(out hostId, out connectionId, out channelId, buffer, bufferSize, out receivedSize, out error);

            var eventType = UnityEngine.Networking.NetworkTransport.Receive(out hostId, out connectionId, out channelId, buffer, bufferSize, out receivedSize, out error);

            return BaseReceive(eventType, hostId, ref connectionId, ref channelId, buffer, bufferSize, ref receivedSize, ref error);
        }

        private static NetworkEventType BaseReceive(NetworkEventType netEvent, int hostId, ref int connectionId, ref int channelId, byte[] buffer, int bufferSize, ref int receivedSize, ref byte error)
        {
            switch (netEvent)
            {
                case NetworkEventType.DataEvent:
                {
                    var messageType = (MessageType)buffer[receivedSize - 1];

                    switch (messageType)
                    {
                        case MessageType.AddressReport:
                        {
                            byte[] addressBytes = new byte[16];

                            for (int i = 0; i < addressBytes.Length; i++)
                            {
                                addressBytes[i] = buffer[i];
                            }

                            ushort remotePort = (ushort)(((ushort)buffer[16]) |
                                                         ((ushort)buffer[17] << 8));

                            var remoteEndPoint = new IPEndPoint(new IPAddress(addressBytes), remotePort);

                            OnRemoteEndpointReported?.Invoke(remoteEndPoint);

                            break;
                        }
                        case MessageType.ConnectToServer: // Connection approved
                        {
                            if (!s_IsClient)
                            {
                                ulong _connectionId = (((ulong)buffer[receivedSize - 9]) |
                                                       ((ulong)buffer[receivedSize - 8] << 8) |
                                                       ((ulong)buffer[receivedSize - 7] << 16) |
                                                       ((ulong)buffer[receivedSize - 6] << 24) |
                                                       ((ulong)buffer[receivedSize - 5] << 32) |
                                                       ((ulong)buffer[receivedSize - 4] << 40) |
                                                       ((ulong)buffer[receivedSize - 3] << 48) |
                                                       ((ulong)buffer[receivedSize - 2] << 56));

                                connectionId = (int)_connectionId;
                            }

                            return NetworkEventType.ConnectEvent;
                        }
                        case MessageType.Data:
                        {
                            // Implicitly remove header
                            if (s_IsClient) --receivedSize;
                            else
                            {
                                receivedSize -= 9;

                                ulong _connectionId = (((ulong)buffer[receivedSize]) |
                                                       ((ulong)buffer[receivedSize + 1] << 8) |
                                                       ((ulong)buffer[receivedSize + 2] << 16) |
                                                       ((ulong)buffer[receivedSize + 3] << 24) |
                                                       ((ulong)buffer[receivedSize + 4] << 32) |
                                                       ((ulong)buffer[receivedSize + 5] << 40) |
                                                       ((ulong)buffer[receivedSize + 6] << 48) |
                                                       ((ulong)buffer[receivedSize + 7] << 56));

                                connectionId = (int)_connectionId;
                            }

                            return NetworkEventType.DataEvent;
                        }
                        case MessageType.ClientDisconnect:
                        {
                            ulong _connectionId = (((ulong)buffer[0]) |
                                                   ((ulong)buffer[1] << 8) |
                                                   ((ulong)buffer[2] << 16) |
                                                   ((ulong)buffer[3] << 24) |
                                                   ((ulong)buffer[4] << 32) |
                                                   ((ulong)buffer[5] << 40) |
                                                   ((ulong)buffer[6] << 48) |
                                                   ((ulong)buffer[7] << 56));

                            connectionId = (int)_connectionId;

                            return NetworkEventType.DisconnectEvent;
                        }
                    }
                }
                    break;
                case NetworkEventType.ConnectEvent:
                {
                    if (s_IsClient)
                    {
                        //Connect via relay

                        byte[] ipv6AddressBuffer;
                        var ipAddress = IPAddress.Parse(s_Address);

                        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        {
                            ipv6AddressBuffer = ipAddress.GetAddressBytes();
                        }
                        else if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            byte[] ipv4Address = ipAddress.GetAddressBytes();
                            ipv6AddressBuffer = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 255, 255, ipv4Address[0], ipv4Address[1], ipv4Address[2], ipv4Address[3] };
                        }
                        else
                        {
                            // TODO: Throw wrong type
                            ipv6AddressBuffer = null;
                        }

                        // TODO: Throw if address is not 16 bytes. It should always be
                        for (int i = 0; i < ipv6AddressBuffer.Length; i++) buffer[i] = ipv6AddressBuffer[i];

                        for (byte i = 0; i < sizeof(ushort); i++) buffer[16 + i] = ((byte)(s_Port >> (i * 8)));

                        buffer[16 + 2] = (byte)MessageType.ConnectToServer;

                        UnityEngine.Networking.NetworkTransport.Send(hostId, connectionId, s_DefaultChannelId, buffer, 16 + 2 + 1, out error);
                    }
                    else
                    {
                        //Register us as a server
                        buffer[0] = (byte)MessageType.StartServer;

                        UnityEngine.Networking.NetworkTransport.Send(hostId, connectionId, s_DefaultChannelId, buffer, 1, out error);
                    }

                    return NetworkEventType.Nothing; // Connect event is ignored
                }
                case NetworkEventType.DisconnectEvent:
                {
                    if ((NetworkError)error == NetworkError.CRCMismatch) Debug.LogError("[MLAPI.Relay] The MLAPI Relay detected a CRC mismatch. This could be due to the maxClients or other connectionConfig settings not being the same");

                    return NetworkEventType.DisconnectEvent;
                }
            }

            return netEvent;
        }
    }

    public class InvalidConfigException : SystemException
    {
        public InvalidConfigException() { }
        public InvalidConfigException(string issue) : base(issue) { }
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore 618