namespace MLAPI.Profiling
{
    public static class ProfilerConstants
    {
        public const string Connections = nameof(Connections);
        public const string ReceiveTickRate = nameof(ReceiveTickRate);

        public const string NamedMessageReceived = nameof(NamedMessageReceived);
        public const string UnnamedMessageReceived = nameof(UnnamedMessageReceived);
        public const string NamedMessageSent = nameof(NamedMessageSent);
        public const string UnnamedMessageSent = nameof(UnnamedMessageSent);
        public const string ByteSent = nameof(ByteSent);
        public const string ByteReceived = nameof(ByteReceived);
        public const string NetworkVarDeltas = nameof(NetworkVarDeltas);
        public const string NetworkVarUpdates = nameof(NetworkVarUpdates);
        public const string MessagesSent = nameof(MessagesSent);
        public const string MessagesReceived = nameof(MessagesReceived);
        public const string MessageBatchesSent = nameof(MessageBatchesSent);
        public const string MessageBatchesReceived = nameof(MessageBatchesReceived);
        public const string MessageQueueProcessed = nameof(MessageQueueProcessed);
        public const string MessageInQueueSize = nameof(MessageInQueueSize);
        public const string MessageOutQueueSize = nameof(MessageOutQueueSize);
    }
}
