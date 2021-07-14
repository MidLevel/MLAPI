#if UNITY_2020_2_OR_NEWER
using UnityEngine;
using Unity.Profiling;
#endif

namespace MLAPI.Profiling
{
    internal static class ProfilerCountersInfo
    {
#if UNITY_2020_2_OR_NEWER && ENABLE_PROFILER
        // Operations
        private static readonly ProfilerCounterValue<int> k_ConnectionsCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.Connections,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame);

        private static readonly ProfilerCounterValue<int> k_TickRateCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.ReceiveTickRate,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        // Messages
        private static readonly ProfilerCounterValue<int> k_NamedMessageReceivedCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.NamedMessageReceived,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_UnnamedMessageReceivedCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.UnnamedMessageReceived,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_NamedMessageSentCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.NamedMessageSent,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_UnnamedMessageSentCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.UnnamedMessageSent,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_BytesSentCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.ByteSent,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_BytesReceivedCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.ByteReceived,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_NetworkVarDeltasCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.NetworkVarDeltas,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_NetworkVarUpdatesCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.NetworkVarUpdates,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        // Queue Stats
        private static readonly ProfilerCounterValue<int> k_MessagesSentCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.MessagesSent,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_MessagesReceivedCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.MessagesReceived,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_MessageBatchesSentCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.MessageBatchesSent,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_MessageBatchesReceivedCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.MessageBatchesReceived,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_MessageQueueProcessedCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.MessageQueueProcessed,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_MessagesInQueueSizeCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.MessageInQueueSize,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static readonly ProfilerCounterValue<int> k_MessagesOutQueueSizeCounterValue =
            new ProfilerCounterValue<int>(ProfilerCategory.Network, ProfilerConstants.MessageOutQueueSize,
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        [RuntimeInitializeOnLoadMethod]
        private static void RegisterMLAPIPerformanceEvent()
        {
            InitializeCounters();
            ProfilerNotifier.OnPerformanceDataEvent += OnPerformanceTickData;
            ProfilerNotifier.OnNoTickDataEvent += OnNoTickData;
        }

        private static void OnNoTickData()
        {
            Debug.LogWarning("There was a profiler event that was not captured in a tick");
        }

        private static void InitializeCounters()
        {
            k_ConnectionsCounterValue.Value = 0;
            k_TickRateCounterValue.Value = 0;

            k_NamedMessageReceivedCounterValue.Value = 0;
            k_UnnamedMessageReceivedCounterValue.Value = 0;
            k_NamedMessageSentCounterValue.Value = 0;
            k_UnnamedMessageSentCounterValue.Value = 0;
            k_BytesSentCounterValue.Value = 0;
            k_BytesReceivedCounterValue.Value = 0;
            k_NetworkVarDeltasCounterValue.Value = 0;
            k_NetworkVarUpdatesCounterValue.Value = 0;

            k_MessagesSentCounterValue.Value = 0;
            k_MessagesReceivedCounterValue.Value = 0;
            k_MessageBatchesSentCounterValue.Value = 0;
            k_MessageBatchesReceivedCounterValue.Value = 0;
            k_MessageQueueProcessedCounterValue.Value = 0;
            k_MessagesInQueueSizeCounterValue.Value = 0;
            k_MessagesOutQueueSizeCounterValue.Value = 0;
        }

        private static void OnPerformanceTickData(PerformanceTickData tickData)
        {
            // Operations
            UpdateIntCounter(tickData, k_ConnectionsCounterValue, ProfilerConstants.Connections);
            UpdateIntCounter(tickData, k_TickRateCounterValue, ProfilerConstants.ReceiveTickRate);

            // Messages
            UpdateIntCounter(tickData, k_NamedMessageReceivedCounterValue, ProfilerConstants.NamedMessageReceived);
            UpdateIntCounter(tickData, k_UnnamedMessageReceivedCounterValue, ProfilerConstants.UnnamedMessageReceived);
            UpdateIntCounter(tickData, k_NamedMessageSentCounterValue, ProfilerConstants.NamedMessageSent);
            UpdateIntCounter(tickData, k_UnnamedMessageSentCounterValue, ProfilerConstants.UnnamedMessageSent);
            UpdateIntCounter(tickData, k_BytesSentCounterValue, ProfilerConstants.ByteSent);
            UpdateIntCounter(tickData, k_BytesReceivedCounterValue, ProfilerConstants.ByteReceived);
            UpdateIntCounter(tickData, k_NetworkVarDeltasCounterValue, ProfilerConstants.NetworkVarDeltas);
            UpdateIntCounter(tickData, k_NetworkVarUpdatesCounterValue, ProfilerConstants.NetworkVarUpdates);

            // Queue stats
            UpdateIntCounter(tickData, k_MessagesSentCounterValue, ProfilerConstants.MessagesSent);
            UpdateIntCounter(tickData, k_MessagesReceivedCounterValue, ProfilerConstants.MessagesReceived);
            UpdateIntCounter(tickData, k_MessageBatchesSentCounterValue, ProfilerConstants.MessageBatchesSent);
            UpdateIntCounter(tickData, k_MessageBatchesReceivedCounterValue, ProfilerConstants.MessageBatchesReceived);
            UpdateIntCounter(tickData, k_MessageBatchesReceivedCounterValue, ProfilerConstants.MessageQueueProcessed);
            UpdateIntCounter(tickData, k_MessageQueueProcessedCounterValue, ProfilerConstants.MessageInQueueSize);
            UpdateIntCounter(tickData, k_MessagesInQueueSizeCounterValue, ProfilerConstants.MessageOutQueueSize);
        }

        private static void UpdateIntCounter(PerformanceTickData tickData, ProfilerCounterValue<int> counter, string fieldName)
        {
            if (tickData.HasData(fieldName))
            {
                counter.Value += tickData.GetData(fieldName);
            }
        }
#endif
    }
}
