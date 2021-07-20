using System;
using System.Collections;
using System.Text.RegularExpressions;
using MLAPI.Prototyping;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static MLAPI.Prototyping.NetworkTransform;

namespace MLAPI.RuntimeTests
{
    [TestFixture(true)]
    [TestFixture(false)]
    public class NetworkTransformTests : BaseMultiInstanceTest
    {
        private NetworkObject m_ClientSideClientPlayer;
        private NetworkObject m_ServerSideClientPlayer;

        private bool m_TestWithHost;

        public NetworkTransformTests(bool testWithHost)
        {
            m_TestWithHost = testWithHost; // from test fixture
        }

        protected override int NbClients => 1;

        [UnitySetUp]
        public override IEnumerator Setup()
        {
            yield return StartSomeClientsAndServerWithPlayers(useHost: m_TestWithHost, nbClients: NbClients, updatePlayerPrefab: playerPrefab =>
            {
                var networkTransform = playerPrefab.AddComponent<NetworkTransform>();
            });

            // This is the *SERVER VERSION* of the *CLIENT PLAYER*
            var serverClientPlayerResult = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation((x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId), m_ServerNetworkManager, serverClientPlayerResult));

            // This is the *CLIENT VERSION* of the *CLIENT PLAYER*
            var clientClientPlayerResult = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation((x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId), m_ClientNetworkManagers[0], clientClientPlayerResult));

            m_ServerSideClientPlayer = serverClientPlayerResult.Result;
            m_ClientSideClientPlayer = clientClientPlayerResult.Result;
        }

        [UnityTest]
        [TestCase(true, Authority.Owner, ExpectedResult = null)]
        [TestCase(true, Authority.Server, ExpectedResult = null)]
        [TestCase(false, Authority.Owner, ExpectedResult = null)]
        [TestCase(false, Authority.Server, ExpectedResult = null)]
        public IEnumerator TestAuthoritativeTransformChangeOneAtATime(bool testLocalTransform, Authority authorityToTest)
        {
            var waitResult = new MultiInstanceHelpers.CoroutineResultWrapper<bool>();

            var networkTransform = (authorityToTest == Authority.Owner ? m_ClientSideClientPlayer : m_ServerSideClientPlayer).GetComponent<NetworkTransform>();
            networkTransform.SetAuthority(authorityToTest);

            var otherSideNetworkTransform = (authorityToTest == Authority.Owner ? m_ServerSideClientPlayer : m_ClientSideClientPlayer).GetComponent<NetworkTransform>();
            otherSideNetworkTransform.SetAuthority(authorityToTest);

            bool HasAuthority(NetworkTransform transform)
            {
                return transform.NetworkObject.NetworkManager.IsServer && transform.TransformAuthority == Authority.Server ||
                    transform.NetworkObject.NetworkManager.IsClient && transform.TransformAuthority == Authority.Owner;
            }

            if (HasAuthority(networkTransform))
            {
                networkTransform.UseLocal = testLocalTransform;
            }

            if (HasAuthority(otherSideNetworkTransform))
            {
                otherSideNetworkTransform.UseLocal = testLocalTransform;
            }

            float approximation = 0.05f;

            // test position
            var playerTransform = networkTransform.transform;
            playerTransform.position = new Vector3(10, 20, 30);
            Assert.AreEqual(Vector3.zero, otherSideNetworkTransform.transform.position, "server side pos should be zero at first"); // sanity check
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.WaitForCondition(() => otherSideNetworkTransform.transform.position.x > approximation, waitResult, maxFrames: 30));
            if (!waitResult.Result)
            {
                throw new Exception("timeout while waiting for position change");
            }
            Assert.True(new Vector3(10, 20, 30) == otherSideNetworkTransform.transform.position, $"wrong position on ghost, {otherSideNetworkTransform.transform.position}"); // Vector3 already does float approximation with ==

            // test rotation
            playerTransform.rotation = Quaternion.Euler(45, 40, 35); // using euler angles instead of quaternions directly to really see issues users might encounter
            Assert.AreEqual(Quaternion.identity, otherSideNetworkTransform.transform.rotation, "wrong initial value for rotation"); // sanity check
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.WaitForCondition(() => otherSideNetworkTransform.transform.rotation.eulerAngles.x > approximation, waitResult, maxFrames: 30));
            if (!waitResult.Result)
            {
                throw new Exception("timeout while waiting for position change");
            }
            // approximation needed here since eulerAngles isn't super precise.
            Assert.LessOrEqual(Math.Abs(45 - otherSideNetworkTransform.transform.rotation.eulerAngles.x), approximation, $"wrong rotation on ghost on x, got {otherSideNetworkTransform.transform.rotation.eulerAngles.x}");
            Assert.LessOrEqual(Math.Abs(40 - otherSideNetworkTransform.transform.rotation.eulerAngles.y), approximation, $"wrong rotation on ghost on y, got {otherSideNetworkTransform.transform.rotation.eulerAngles.y}");
            Assert.LessOrEqual(Math.Abs(35 - otherSideNetworkTransform.transform.rotation.eulerAngles.z), approximation, $"wrong rotation on ghost on z, got {otherSideNetworkTransform.transform.rotation.eulerAngles.z}");

            // test scale
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(1f, otherSideNetworkTransform.transform.lossyScale.x, "wrong initial value for scale"); // sanity check
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(1f, otherSideNetworkTransform.transform.lossyScale.y, "wrong initial value for scale"); // sanity check
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(1f, otherSideNetworkTransform.transform.lossyScale.z, "wrong initial value for scale"); // sanity check
            playerTransform.localScale = new Vector3(2, 3, 4);
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.WaitForCondition(() => otherSideNetworkTransform.transform.lossyScale.x > 1f + approximation, waitResult, maxFrames: 30));
            if (!waitResult.Result)
            {
                throw new Exception("timeout while waiting for position change");
            }
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(2f, otherSideNetworkTransform.transform.lossyScale.x, "wrong scale on ghost");
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(3f, otherSideNetworkTransform.transform.lossyScale.y, "wrong scale on ghost");
            UnityEngine.Assertions.Assert.AreApproximatelyEqual(4f, otherSideNetworkTransform.transform.lossyScale.z, "wrong scale on ghost");

            // todo reparent and test
            // todo test all public API
        }

        [UnityTest]
        [TestCase(Authority.Owner, ExpectedResult = null)]
        [TestCase(Authority.Server, ExpectedResult = null)]
        public IEnumerator TestCantChangeTransformFromOtherSideAuthority(Authority authorityToTest)
        {
            // test server can't change client authoritative transform
            var networkTransform = (authorityToTest == Authority.Owner ? m_ClientSideClientPlayer : m_ServerSideClientPlayer).GetComponent<NetworkTransform>();
            networkTransform.SetAuthority(authorityToTest);

            var otherSideNetworkTransform = (authorityToTest == Authority.Owner ? m_ServerSideClientPlayer : m_ClientSideClientPlayer).GetComponent<NetworkTransform>();
            otherSideNetworkTransform.SetAuthority(authorityToTest);

            Assert.AreEqual(Vector3.zero, otherSideNetworkTransform.transform.position, "other side pos should be zero at first"); // sanity check
            otherSideNetworkTransform.transform.position = new Vector3(4, 5, 6);

            yield return new WaitForFixedUpdate();

            LogAssert.Expect(LogType.Error, new Regex(".*[Aa]uthority.*"));
            Assert.AreEqual(Vector3.zero, otherSideNetworkTransform.transform.position, "got authority error, but other side still moved!");
        }

        [UnityTearDown]
        public override IEnumerator Teardown()
        {
            yield return base.Teardown();
            UnityEngine.Object.Destroy(m_PlayerPrefab);
        }
    }
}
