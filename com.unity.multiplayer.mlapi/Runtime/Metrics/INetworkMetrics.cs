﻿using System.Collections.Generic;

namespace MLAPI.Metrics
{
    public interface INetworkMetrics
    {
        void TrackNetworkObject(NetworkObject networkObject);

        void TrackNamedMessageSent(ulong receiverClientId, string messageName, ulong bytesCount);

        void TrackNamedMessageSent(IReadOnlyCollection<ulong> receiverClientIds, string messageName, ulong bytesCount);

        void TrackNamedMessageReceived(ulong senderClientId, string messageName, ulong bytesCount);

        void TrackUnnamedMessageSent(ulong receiverClientId, ulong bytesCount);

        void TrackUnnamedMessageSent(IReadOnlyCollection<ulong> receiverClientIds, ulong bytesCount);

        void TrackUnnamedMessageReceived(ulong senderClientId, ulong bytesCount);

        void TrackNetworkVariableDeltaSent(ulong receiverClientId, ulong networkObjectId, string gameObjectName,string variableName, ulong bytesCount);

        void TrackNetworkVariableDeltaReceived(ulong senderClientId, ulong networkObjectId, string gameObjectName,string variableName, ulong bytesCount);

        void DispatchFrame();
    }
}