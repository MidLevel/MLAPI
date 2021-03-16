using System;
using System.Collections.Generic;
using System.ComponentModel;
using MLAPI.Collections;
using MLAPI.Configuration;
using UnityEngine;

namespace MLAPI.Profiling
{
    /// <summary>
    /// NetworkProfiler for profiling network traffic
    /// </summary>
    public static class NetworkProfiler
    {
        /// <summary>
        /// The ticks that has been recorded
        /// </summary>
        public static FixedQueue<ProfilerTick> Ticks { get; private set; }
        /// <summary>
        /// Whether or not the profiler is recording data
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use IsRunning instead", false)]
        public static bool isRunning => IsRunning;
        /// <summary>
        /// Whether or not the profiler is recording data
        /// </summary>
        public static bool IsRunning { get; private set; }

        private static int tickHistory = 1024;
        private static int EventIdCounter = 0;
        private static ProfilerTick CurrentTick;

        /// <summary>
        /// Starts recording data for the Profiler
        /// </summary>
        /// <param name="historyLength">The amount of ticks to keep in memory</param>
        public static void Start(int historyLength)
        {
            if (IsRunning)
                return;
            EventIdCounter = 0;
            Ticks = new FixedQueue<ProfilerTick>(historyLength);
            tickHistory = historyLength;
            CurrentTick = null;
            IsRunning = true;
        }

        /// <summary>
        /// Stops recording data
        /// </summary>
        public static void Stop()
        {
            Ticks = null; //leave to GC
            CurrentTick = null; //leave to GC
            IsRunning = false;
        }

        /// <summary>
        /// Stops recording data and fills the buffer with the recorded ticks and returns the length;
        /// </summary>
        /// <param name="tickBuffer">The buffer to fill with the ticks</param>
        /// <returns>The number of ticks recorded</returns>
        public static int Stop(ref ProfilerTick[] tickBuffer)
        {
            if (!IsRunning)
                return 0;
            int iteration = Ticks.Count > tickBuffer.Length ? tickBuffer.Length : Ticks.Count;
            for (int i = 0; i < iteration; i++) tickBuffer[i] = Ticks[i];

            Ticks = null; //leave to GC
            CurrentTick = null; //leave to GC
            IsRunning = false;

            return iteration;
        }

        /// <summary>
        /// Stops recording data and fills the buffer with the recorded ticks and returns the length;
        /// </summary>
        /// <param name="tickBuffer">The buffer to fill with the ticks</param>
        /// <returns>The number of ticks recorded</returns>
        public static int Stop(ref List<ProfilerTick> tickBuffer)
        {
            if (!IsRunning)
                return 0;
            int iteration = Ticks.Count > tickBuffer.Count ? tickBuffer.Count : Ticks.Count;
            for (int i = 0; i < iteration; i++) tickBuffer[i] = Ticks[i];

            Ticks = null; //leave to GC
            CurrentTick = null; //leave to GC
            IsRunning = false;

            return iteration;
        }

        internal static void StartTick(TickType type)
        {
            if (!IsRunning)
                return;
            if (Ticks.Count == tickHistory)
                Ticks.Dequeue();

            ProfilerTick tick = new ProfilerTick()
            {
                Type = type,
                Frame = Time.frameCount,
                EventId = EventIdCounter
            };
            EventIdCounter++;
            Ticks.Enqueue(tick);
            CurrentTick = tick;
        }

        internal static void EndTick()
        {
            if (!IsRunning)
                return;
            if (CurrentTick == null)
                return;
            CurrentTick = null;
        }

        internal static void StartEvent(TickType eventType, uint bytes, string channelName, byte messageType)
        {
            if (!IsRunning)
                return;
            if (CurrentTick == null)
                StartTick(eventType);

            string messageName = messageType < MLAPIConstants.MESSAGE_NAMES.Length ? MLAPIConstants.MESSAGE_NAMES[messageType] : "INVALID_MESSAGE_TYPE";

            CurrentTick.StartEvent(eventType, bytes, channelName, messageName);
        }

        internal static void StartEvent(TickType eventType, uint bytes, string channelName, string messageName)
        {
            if (!IsRunning)
                return;
            if (CurrentTick == null)
                StartTick(eventType);

            CurrentTick.StartEvent(eventType, bytes, channelName, messageName);
        }

        internal static void EndEvent()
        {
            if (!IsRunning)
                return;
            if (CurrentTick == null)
                return;
            CurrentTick.EndEvent();
        }
    }
}
