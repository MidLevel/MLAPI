using System;
using MLAPI;
using MLAPI.Configuration;
using MLAPI.Transports;
using NUnit.Framework;
using UnityEngine;
using MLAPI.Transports.UNET;
using UnityEngine.Networking;

public class TransportTest : MonoBehaviour
{
    // hack, remove any NetworkObject's from the scene to avoid spawning scene objects
    //  which themselves might not be able initialize themselves properly
    private void ForceNetworkObjectShutdown()
    {
        NetworkObject[] networkObjects = FindObjectsOfType<NetworkObject>();
        for (int i = 0; i < networkObjects.Length; i++)
        {
            DestroyImmediate(networkObjects[i]);
        }
    }

    // A Test behaves as an ordinary method
    [Test]
    public void UNetCustomChannelRegistrationTest()
    {
        ForceNetworkObjectShutdown();

        var o = new GameObject();
        var nm = (NetworkManager)o.AddComponent(typeof(NetworkManager));
        nm.SetSingleton();
        nm.NetworkConfig = new NetworkConfig();
        var ut = (UNetTransport)o.AddComponent(typeof(UNetTransport));

        ut.ServerListenPort = 7777;
        nm.NetworkConfig.NetworkTransport = ut;

        byte customChannel = 0;

        // test 1: add a legit channel.
        ut.Channels.Add(new UNetChannel { Id = (byte)(NetworkChannel.ChannelUnused + customChannel), Type = QosType.Unreliable });

        try
        {
            nm.StartServer();
        }
        catch
        {
            Assert.Fail("The UNet transport won't allow registration of a legit user channel");
        }

        nm.Shutdown();

        ut.Channels.Clear();
        // test 2: add a bogus channel (one that intersects with the MLAPI built-in ones.)  Expect failure
        ut.Channels.Add(new UNetChannel { Id = (byte)NetworkChannel.Internal, Type = QosType.Unreliable });

        try
        {
            nm.StartServer();
            Assert.Fail("The UNet transport allowed registration of an MLAPI-reserved channel");
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }

        nm.Shutdown();
    }
}
