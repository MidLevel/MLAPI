using System;
using MLAPI.Timing;
using NUnit.Framework;
using UnityEngine;

namespace MLAPI.EditorTests.Timing
{
    /// <summary>
    /// Tests for <see cref="ClientNetworkTimeProvider"/>
    /// </summary>
    public class ClientNetworkTimeProviderTests
    {
        private const double k_AcceptableRttOffset = 0.04d; // 40ms offset is fine

        [Test]
        public void InitializeClientTest()
        {
            var serverTime = new NetworkTime(60);
            var localTime = new NetworkTime(60);

            var clientNetworkTimeProvider = new ClientNetworkTimeProvider(new DummyNetworkStats(), 60);
            clientNetworkTimeProvider.InitializeClient(ref localTime, ref serverTime);

            Assert.IsTrue(serverTime.Time > 0f);
            Assert.IsTrue(localTime.Time > serverTime.Time);
        }

        /// <summary>
        /// Tests whether time is stable if RTT is stable.
        /// </summary>
        [Test]
        public void StableRttTest()
        {
            var serverTime = new NetworkTime(60);
            var localTime = new NetworkTime(60);

            var networkStats = new DummyNetworkStats() { Rtt = 0.1f, LastReceivedSnapshotTick = serverTime };

            var clientNetworkTimeProvider = new ClientNetworkTimeProvider(networkStats, 60);
            clientNetworkTimeProvider.InitializeClient(ref localTime, ref serverTime);

            var steps = TimingTestHelper.GetRandomTimeSteps(100f, 0.01f, 0.1f, 42);
            var rttSteps = TimingTestHelper.GetRandomTimeSteps(1000f, 0.095f, 0.105f, 42); // 10ms jitter

            // run for a while so that we reach regular RTT offset
            TimingTestHelper.ApplySteps(clientNetworkTimeProvider, steps, ref localTime, ref serverTime, delegate (int step)
            {
                // increase last received server tick
                networkStats.LastReceivedSnapshotTick = networkStats.LastReceivedSnapshotTick + steps[step];

                // Update RTT
                networkStats.Rtt = rttSteps[step];
            });

            // check how we close we are to target time.
            var offsetToTarget = (localTime - serverTime).Time - 0.1f - clientNetworkTimeProvider.TargetServerBufferTime;
            Assert.IsTrue(offsetToTarget < k_AcceptableRttOffset);
            Debug.Log($"offset to target time after running for a while: {offsetToTarget}");

            // run again, test that we never need to speed up or slow down under stable RTT
            TimingTestHelper.ApplySteps(clientNetworkTimeProvider, steps, ref localTime, ref serverTime, delegate (int step)
            {
                networkStats.LastReceivedSnapshotTick = networkStats.LastReceivedSnapshotTick + steps[step];
                networkStats.Rtt = rttSteps[step];
            });

            // check again to ensure we are still close to the target
            var newOffsetToTarget = (localTime - serverTime).Time - 0.1f - clientNetworkTimeProvider.TargetServerBufferTime;
            Assert.IsTrue(newOffsetToTarget < k_AcceptableRttOffset);
            Debug.Log($"offset to target time after running longer: {newOffsetToTarget}");

            // difference between first and second offset should be minimal
            var dif = offsetToTarget - newOffsetToTarget;
            Assert.IsTrue(Math.Abs(dif) < 0.01d); // less than 10ms

        }

        /// <summary>
        /// Tests whether local time can speed up and slow down to catch up when RTT changes.
        /// </summary>
        [Test]
        public void RttCatchupSlowdownTest()
        {
            var serverTime = new NetworkTime(60);
            var localTime = new NetworkTime(60);

            var networkStats = new DummyNetworkStats() { Rtt = 0.1f, LastReceivedSnapshotTick = serverTime };

            var clientNetworkTimeProvider = new ClientNetworkTimeProvider(networkStats, 60);
            clientNetworkTimeProvider.InitializeClient(ref localTime, ref serverTime);

            var steps = TimingTestHelper.GetRandomTimeSteps(100f, 0.01f, 0.1f, 42);
            var rttSteps = TimingTestHelper.GetRandomTimeSteps(1000f, 0.095f, 0.105f, 42); // 10ms jitter

            // run for a while so that we reach regular RTT offset
            TimingTestHelper.ApplySteps(clientNetworkTimeProvider, steps, ref localTime, ref serverTime, delegate (int step)
            {
                networkStats.LastReceivedSnapshotTick = networkStats.LastReceivedSnapshotTick + steps[step];
                networkStats.Rtt = rttSteps[step];
            });

            // increase RTT to ~200ms from ~100ms
            var rttSteps2 = TimingTestHelper.GetRandomTimeSteps(1000f, 0.195f, 0.205f, 42);

            // we run again and check how much speed up is done. In theory this should be around 0.1f at the end because the local time is trying to catch up.
            float totalLocalSpeedUpTime = 0f;

            TimingTestHelper.ApplySteps(clientNetworkTimeProvider, steps, ref localTime, ref serverTime, delegate (int step)
            {
                networkStats.LastReceivedSnapshotTick = networkStats.LastReceivedSnapshotTick + steps[step];
                networkStats.Rtt = rttSteps2[step]; // note; uses new rtt steps

                if (step < steps.Count - 2)
                {
                    totalLocalSpeedUpTime += (clientNetworkTimeProvider.LocalTimeScale - 1f) * steps[step + 1]; // +1 because the scale will be applied to the next time
                }
            });

            // speed up of 0.1f expected
            Assert.True(Mathf.Abs(totalLocalSpeedUpTime - 0.1f) < k_AcceptableRttOffset);
            Debug.Log($"Total local speed up time catch up: {totalLocalSpeedUpTime}");

            // run again with RTT ~100ms and see whether we slow down by -0.1f
            totalLocalSpeedUpTime = 0f;

            TimingTestHelper.ApplySteps(clientNetworkTimeProvider, steps, ref localTime, ref serverTime, delegate (int step)
            {
                networkStats.LastReceivedSnapshotTick = networkStats.LastReceivedSnapshotTick + steps[step];
                networkStats.Rtt = rttSteps[step];

                if (step < steps.Count - 2)
                {
                    totalLocalSpeedUpTime += (clientNetworkTimeProvider.LocalTimeScale - 1f) * steps[step + 1]; // +1 because the scale will be applied to the next time
                }
            });

            // slow down of 0.1f expected
            Assert.True(Mathf.Abs(totalLocalSpeedUpTime + 0.1f) < k_AcceptableRttOffset);
            Debug.Log($"Total local speed up time slow down: {totalLocalSpeedUpTime}");

        }

        /// <summary>
        /// Tests whether time resets when there is a huge spike in RTT and is able to stabilize again.
        /// </summary>
        [Test]
        public void ResetTest()
        {
            var serverTime = new NetworkTime(60);
            var localTime = new NetworkTime(60);

            var networkStats = new DummyNetworkStats() { Rtt = 0.1f, LastReceivedSnapshotTick = serverTime };

            var clientNetworkTimeProvider = new ClientNetworkTimeProvider(networkStats, 60);
            clientNetworkTimeProvider.InitializeClient(ref localTime, ref serverTime);

            var steps = TimingTestHelper.GetRandomTimeSteps(100f, 0.01f, 0.1f, 42);
            var rttSteps = TimingTestHelper.GetRandomTimeSteps(1000f, 0.095f, 0.105f, 42); // 10ms jitter

            // run for a while so that we reach regular RTT offset
            TimingTestHelper.ApplySteps(clientNetworkTimeProvider, steps, ref localTime, ref serverTime, delegate (int step)
            {
                networkStats.LastReceivedSnapshotTick = networkStats.LastReceivedSnapshotTick + steps[step];
                networkStats.Rtt = rttSteps[step];
            });

            // increase RTT to ~500ms from ~100ms
            networkStats.Rtt = 0.5f;
            var rttSteps2 = TimingTestHelper.GetRandomTimeSteps(1000f, 0.495f, 0.505f, 42);

            // run a single advance expect a hard rest
            bool reset = clientNetworkTimeProvider.AdvanceTime(ref localTime, ref serverTime, 0.02f);
            Assert.IsTrue(reset);


            TimingTestHelper.ApplySteps(clientNetworkTimeProvider, steps, ref localTime, ref serverTime, delegate (int step, bool reset)
            {
                Assert.IsFalse(reset);

                networkStats.LastReceivedSnapshotTick = networkStats.LastReceivedSnapshotTick + steps[step];
                networkStats.Rtt = rttSteps2[step]; // note; uses new rtt steps

                // after hard reset time should stay close to rtt
                Assert.IsTrue(Math.Abs((localTime - serverTime).Time - networkStats.Rtt - clientNetworkTimeProvider.TargetServerBufferTime) < k_AcceptableRttOffset);
            });
        }

    }
}
