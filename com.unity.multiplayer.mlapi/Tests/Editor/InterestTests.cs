using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using MLAPI.Connection;
using MLAPI.Configuration;
using MLAPI.Interest;

namespace MLAPI.EditorTests
{
    public class TestInterestSettings : InterestSettings
    {
        public int SomeSetting;
    }

    public class InterestTests
    {
        public class OddsEvensNode : InterestNode
        {
            public void OnEnable()
            {
                m_Odds = ScriptableObject.CreateInstance<InterestNodeStatic>();
                m_Evens = ScriptableObject.CreateInstance<InterestNodeStatic>();
            }

            public override void AddObject(in NetworkObject obj)
            {
                if (obj.NetworkObjectId % 2 == 0)
                {
                    m_Evens.AddObject(obj);
                }
                else
                {
                    m_Odds.AddObject(obj);
                }
            }

            public override void RemoveObject(in NetworkObject obj)
            {
                if (obj.NetworkObjectId % 2 == 0)
                {
                    m_Evens.RemoveObject(obj);
                }
                else
                {
                    m_Odds.RemoveObject(obj);
                }
            }

            public override void QueryFor(in NetworkClient client, HashSet<NetworkObject> results)
            {
                // if a client with an odd NetworkObjectID queries, we return objects with odd NetworkObjectIDs
                if (client.PlayerObject.NetworkObjectId % 2 == 0)
                {
                    m_Evens.QueryFor(client, results);
                }
                else
                {
                    m_Odds.QueryFor(client, results);
                }
            }

            public override void UpdateObject(in NetworkObject obj)
            {
                m_Odds.RemoveObject(obj);
                m_Evens.RemoveObject(obj);
                AddObject(obj);
            }

            private InterestNodeStatic m_Odds;
            private InterestNodeStatic m_Evens;
        }

        private NetworkObject MakeGameObjectHelper()
        {
            var o = new GameObject();
            var no = (NetworkObject)o.AddComponent(typeof(NetworkObject));
            return no;
        }

        private NetworkObject MakeGameInterestObjectHelper(Vector3 coords, InterestNode comn)
        {
            var o = new GameObject();
            var no = MakeGameObjectHelper();
            if (comn != null)
            {
                no.InterestNodes.Add(comn);
                no.transform.position = coords;
            }

            return no;
        }

        private NetworkManager SetUpNetworkingManager()
        {
            var o = new GameObject();
            var nm = (NetworkManager)o.AddComponent(typeof(NetworkManager));
            nm.NetworkConfig = new NetworkConfig();
            nm.SetSingleton();
            var transport = o.AddComponent<DummyTransport>();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.StartServer();
            return nm;
        }

        [Test]
        // Start is called before the first frame update
        public void InterestCustomStorageTests()
        {
            var nm = SetUpNetworkingManager();

            var oddsEvensNode = ScriptableObject.CreateInstance<OddsEvensNode>();

            var results = new HashSet<NetworkObject>();
            var nc = new NetworkClient()
            {
                ClientId = 1,
            };
            nm.InterestManager.QueryFor(nc, results);
            var objectsBeforeAdd = results.Count;

            var numNodes = 4;
            var nodes = new NetworkObject[4];
            for (var i = 0; i < numNodes; i++)
            {
                nodes[i] = MakeGameInterestObjectHelper(new Vector3(0.0f, 0.0f, 0.0f), oddsEvensNode);
                nodes[i].NetworkObjectId = (ulong)(i + 100);
                nm.SpawnManager.SpawnNetworkObjectLocally(nodes[i]);
            }

            nc.PlayerObject = MakeGameInterestObjectHelper(new Vector3(0.0f, 0.0f, 0.0f), null);

            results.Clear();
            nc.PlayerObject.NetworkObjectId = 100;
            nm.InterestManager.QueryFor(nc, results);
            Assert.True(results.Count - objectsBeforeAdd == numNodes / 2);

            results.Clear();
            nc.PlayerObject.NetworkObjectId = 101;
            nm.InterestManager.QueryFor(nc, results);
            Assert.True(results.Count - objectsBeforeAdd == numNodes / 2);

            // now re-number the nodes
            for (var i = 0; i < numNodes; i++)
            {
                nodes[i].NetworkObjectId = (ulong)(2 * i + 101); // 101, 103, 105, 107
            }

            results.Clear();
            nc.PlayerObject.NetworkObjectId = 100;
            nm.InterestManager.QueryFor(nc, results);
            Assert.True(results.Count - objectsBeforeAdd == numNodes / 2); // should be unchanged

            results.Clear();
            nc.PlayerObject.NetworkObjectId = 101;
            nm.InterestManager.QueryFor(nc, results);
            Assert.True(results.Count - objectsBeforeAdd == numNodes / 2); // should be unchanged

            // now update the nodes
            for (var i = 0; i < numNodes; i++)
            {
                nodes[i].UpdateInterest();
            }

            results.Clear();
            nc.PlayerObject.NetworkObjectId = 100;
            nm.InterestManager.QueryFor(nc, results);
            Assert.True(results.Count - objectsBeforeAdd == 0); // now zero, all are odd

            results.Clear();
            nc.PlayerObject.NetworkObjectId = 101;
            nm.InterestManager.QueryFor(nc, results);
            Assert.True(results.Count - objectsBeforeAdd == nodes.Length); // now 4, all are odd
        }

        [Test]
        // Start is called before the first frame update
        public void InterestRadiusCheck()
        {
            var nm = SetUpNetworkingManager();

            var naiveRadiusNode = ScriptableObject.CreateInstance<RadiusInterestNode>();
//            var ris = ScriptableObject.CreateInstance<RadiusInterestStorage>();
            naiveRadiusNode.Radius = 1.5f;
//            naiveRadiusNode.InterestNode = ris;
            var staticNode = ScriptableObject.CreateInstance<InterestNodeStatic>();

            var results = new HashSet<NetworkObject>();
            var nc = new NetworkClient()
            {
                ClientId = 1,
            };

            nm.InterestManager.QueryFor(nc, results);
            int objectsBeforeAdd = results.Count;

            var ok1 = MakeGameInterestObjectHelper(new Vector3(0.5f, 0.0f, 0.0f), naiveRadiusNode);
            nm.SpawnManager.SpawnNetworkObjectLocally(ok1);

            var ok2 = MakeGameInterestObjectHelper(new Vector3(1.0f, 0.0f, 0.0f), naiveRadiusNode);
            nm.SpawnManager.SpawnNetworkObjectLocally(ok2);

            var tooFar = MakeGameInterestObjectHelper(new Vector3(3.0f, 0.0f, 0.0f), naiveRadiusNode);
            nm.SpawnManager.SpawnNetworkObjectLocally(tooFar);

            var always = MakeGameInterestObjectHelper(new Vector3(99.0f, 99.0f, 99.0f), staticNode);
            nm.SpawnManager.SpawnNetworkObjectLocally(always);

            nc.PlayerObject = MakeGameInterestObjectHelper(new Vector3(0.0f, 0.0f, 0.0f), null);
            nm.SpawnManager.SpawnNetworkObjectLocally(nc.PlayerObject);

            results.Clear();
            nm.InterestManager.QueryFor(nc, results);
            var hits = results.Count - objectsBeforeAdd;
            Assert.True(results.Contains(ok1));
            Assert.True(results.Contains(ok2));
            Assert.True(results.Contains(nc.PlayerObject));
            Assert.True(results.Contains(always));
            Assert.False(results.Contains(tooFar));
            Assert.True(hits == 4);

            // remove an object, should not be in replication manager
            nm.SpawnManager.DespawnObject(always);
            results.Clear();
            nm.InterestManager.QueryFor(nc, results);
            hits = results.Count - objectsBeforeAdd;
            Assert.True(results.Contains(ok1));
            Assert.True(results.Contains(ok2));
            Assert.True(results.Contains(nc.PlayerObject));
            Assert.False(results.Contains(always));
            Assert.False(results.Contains(tooFar));
            Assert.True(hits == 3);
        }

        [Test]
        public void CheckPlainReplication()
        {
            var nm = SetUpNetworkingManager();

            var results = new HashSet<NetworkObject>();
            var nc = new NetworkClient()
            {
                ClientId = 1,
            };

            nm.InterestManager.QueryFor(nc, results);
            var objectsBeforeAdd = results.Count;

            var object1 = MakeGameInterestObjectHelper(new Vector3(2.0f, 0.0f, 0.0f), null);
            nm.SpawnManager.SpawnNetworkObjectLocally(object1);

            var object2 = MakeGameInterestObjectHelper(new Vector3(2.0f, 0.0f, 0.0f), null);
            nm.SpawnManager.SpawnNetworkObjectLocally(object2);

            nc.PlayerObject = MakeGameInterestObjectHelper(new Vector3(0.0f, 0.0f, 0.0f), null);
            nm.SpawnManager.SpawnNetworkObjectLocally(nc.PlayerObject);

            results.Clear();
            nm.InterestManager.QueryFor(nc, results);
            var hits = results.Count;
            Assert.True(hits == (3 + objectsBeforeAdd));
            Assert.True(results.Contains(object1));
            Assert.True(results.Contains(object2));
            Assert.True(results.Contains(nc.PlayerObject));

            // remove an object, should not be in replication manager
            nm.SpawnManager.DespawnObject(object1);
            nm.SpawnManager.DespawnObject(object2);
            nm.SpawnManager.DespawnObject(nc.PlayerObject);
            results.Clear();
            nm.InterestManager.QueryFor(nc, results);
            hits = results.Count;
            Assert.True(hits == (objectsBeforeAdd));
        }

        [Test]
        public void TestInterestSettings()
        {
            var nm = SetUpNetworkingManager();
            var rootSettings = ScriptableObject.CreateInstance<TestInterestSettings>();
            rootSettings.SomeSetting = 1;
            nm.InterestSettings = rootSettings;

            var objSettings = ScriptableObject.CreateInstance<TestInterestSettings>();
            objSettings.SomeSetting = 2;
            var object1 = MakeGameInterestObjectHelper(new Vector3(0.0f, 0.0f, 0.0f), null);
            object1.InterestSettings = objSettings;

            // no override settings, should receive from NetworkManager
            var object2 = MakeGameInterestObjectHelper(new Vector3(0.0f, 0.0f, 0.0f), null);

            var checkObj1 = (TestInterestSettings)object1.InterestSettings;
            var checkObj2 = (TestInterestSettings)object2.InterestSettings;

            Assert.True(checkObj1.SomeSetting == 2);
            Assert.True(checkObj2.SomeSetting == 1);
        }
    }
}
