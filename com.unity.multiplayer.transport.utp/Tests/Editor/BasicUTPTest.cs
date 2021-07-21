using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using Unity.Networking.Transport;
using MLAPI.Transports;

namespace MLAPI.UTP.RuntimeTests
{
    public class BasicUTPTest : MonoBehaviour
    {
        [Test]
        public void BasicUTPInitializationTest()
        {
            var o = new GameObject();
            var utpTransport = (UTPTransport)o.AddComponent(typeof(UTPTransport));
            utpTransport.Init();

            Assert.True(utpTransport.ServerClientId == 0);

            utpTransport.Shutdown();
        }
    }
}


