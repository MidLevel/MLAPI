using System;
using System.IO;
using MLAPI.Serialization;
using MLAPI.Transports;
using MLAPI.Serialization.Pooled;

namespace MLAPI.Messaging
{
    /// <summary>
    /// MessageFrameItem
    /// Container structure for messages written to the Queue Frame
    /// Used for both Inbound and Outbound messages
    /// NOTE: This structure will change in the near future and is in a state of flux.
    /// This will include removing specific properties or changing property types
    /// </summary>
    internal struct MessageFrameItem
    {
        public NetworkUpdateStage UpdateStage;
        public MessageQueueContainer.MessageType MessageType;
        public ulong NetworkId; //Sender's network Identifier, or recipient identifier for server RPCs
        public NetworkChannel NetworkChannel;
        public ulong[] ClientNetworkIds; //Everything other than server RPCs
        public long StreamSize;
        public float Timestamp;
        public PooledNetworkWriter NetworkWriter;
        public PooledNetworkReader NetworkReader;
        public Stream NetworkBuffer;
        public ArraySegment<byte> MessageData;
    }
}
