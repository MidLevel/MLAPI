using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MLAPI.Components;
#if !DISABLE_CRYPTOGRAPHY
using MLAPI.Cryptography;
#endif
using MLAPI.Data;
using MLAPI.Logging;
using MLAPI.Serialization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using BitStream = UnityEngine.BitStream;

namespace MLAPI.Internal
{
    internal static partial class InternalMessageHandler
    {
#if !DISABLE_CRYPTOGRAPHY
        // Runs on client
        internal static void HandleHailRequest(ulong clientId, Stream stream)
        {
            X509Certificate2 certificate = null;
            byte[] serverDiffieHellmanPublicPart = null;
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                if (NetworkingManager.Singleton.NetworkConfig.EnableEncryption)
                {
                    // Read the certificate
                    if (NetworkingManager.Singleton.NetworkConfig.SignKeyExchange)
                    {
                        // Allocation justification: This runs on client and only once, at initial connection
                        certificate = new X509Certificate2(reader.ReadByteArray());
                        if (CryptographyHelper.VerifyCertificate(certificate, NetworkingManager.Singleton.ConnectedHostname))
                        {
                            // The certificate is not valid :(
                            // Man in the middle.
                            if (LogHelper.CurrentLogLevel <= LogLevel.Normal) if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Invalid certificate. Disconnecting");
                            NetworkingManager.Singleton.StopClient();
                            return;
                        }
                        else
                        {
                            NetworkingManager.Singleton.NetworkConfig.ServerX509Certificate = certificate;
                        }
                    }

                    // Read the ECDH
                    // Allocation justification: This runs on client and only once, at initial connection
                    serverDiffieHellmanPublicPart = reader.ReadByteArray();
                    
                    // Verify the key exchange
                    if (NetworkingManager.Singleton.NetworkConfig.SignKeyExchange)
                    {
                        byte[] serverDiffieHellmanPublicPartSignature = reader.ReadByteArray();

                        RSACryptoServiceProvider rsa = certificate.PublicKey.Key as RSACryptoServiceProvider;

                        if (rsa != null)
                        {
                            using (SHA256Managed sha = new SHA256Managed())
                            {
                                if (!rsa.VerifyData(serverDiffieHellmanPublicPart, sha, serverDiffieHellmanPublicPartSignature))
                                {
                                    if (LogHelper.CurrentLogLevel <= LogLevel.Normal) if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Invalid signature. Disconnecting");
                                    NetworkingManager.Singleton.StopClient();
                                    return;
                                }   
                            }
                        }
                    }
                }
            }

            using (PooledBitStream outStream = PooledBitStream.Get())
            {
                using (PooledBitWriter writer = PooledBitWriter.Get(outStream))
                {
                    if (NetworkingManager.Singleton.NetworkConfig.EnableEncryption)
                    {
                        // Create a ECDH key
                        EllipticDiffieHellman diffieHellman = new EllipticDiffieHellman(EllipticDiffieHellman.DEFAULT_CURVE, EllipticDiffieHellman.DEFAULT_GENERATOR, EllipticDiffieHellman.DEFAULT_ORDER);
                        NetworkingManager.Singleton.clientAesKey = diffieHellman.GetSharedSecret(serverDiffieHellmanPublicPart);
                        byte[] diffieHellmanPublicKey = diffieHellman.GetPublicKey();
                        writer.WriteByteArray(diffieHellmanPublicKey);
                        if (NetworkingManager.Singleton.NetworkConfig.SignKeyExchange)
                        {
                            RSACryptoServiceProvider rsa = certificate.PublicKey.Key as RSACryptoServiceProvider;

                            if (rsa != null)
                            {
                                using (SHA256CryptoServiceProvider sha = new SHA256CryptoServiceProvider())
                                {
                                    writer.WriteByteArray(rsa.Encrypt(sha.ComputeHash(diffieHellmanPublicKey), false));   
                                }
                            }
                            else
                            {
                                throw new CryptographicException("[MLAPI] Only RSA certificates are supported. No valid RSA key was found");
                            }
                        }
                    }
                }
                // Send HailResponse
                InternalMessageHandler.Send(NetworkingManager.Singleton.ServerClientId, MLAPIConstants.MLAPI_CERTIFICATE_HAIL_RESPONSE, "MLAPI_INTERNAL", outStream, SecuritySendFlags.None, null, true);
            }
        }

        // Ran on server
        internal static void HandleHailResponse(ulong clientId, Stream stream)
        {
            if (!NetworkingManager.Singleton.PendingClients.ContainsKey(clientId) || NetworkingManager.Singleton.PendingClients[clientId].ConnectionState != PendingClient.State.PendingHail) return;
            if (!NetworkingManager.Singleton.NetworkConfig.EnableEncryption) return;

            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                if (NetworkingManager.Singleton.PendingClients[clientId].KeyExchange != null)
                {
                    byte[] diffieHellmanPublic = reader.ReadByteArray();
                    NetworkingManager.Singleton.PendingClients[clientId].AesKey = NetworkingManager.Singleton.PendingClients[clientId].KeyExchange.GetSharedSecret(diffieHellmanPublic);
                    if (NetworkingManager.Singleton.NetworkConfig.SignKeyExchange)
                    {
                        byte[] diffieHellmanPublicSignature = reader.ReadByteArray();
                        X509Certificate2 certificate = NetworkingManager.Singleton.NetworkConfig.ServerX509Certificate;
                        RSACryptoServiceProvider rsa = certificate.PrivateKey as RSACryptoServiceProvider;

                        if (rsa != null)
                        {
                            using (SHA256Managed sha = new SHA256Managed())
                            {
                                byte[] clientHash = rsa.Decrypt(diffieHellmanPublicSignature, false);
                                byte[] serverHash = sha.ComputeHash(diffieHellmanPublic);
                                
                                if (!CryptographyHelper.ConstTimeArrayEqual(clientHash, serverHash))
                                {
                                    //Man in the middle.
                                    if (LogHelper.CurrentLogLevel <= LogLevel.Normal) if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("Signature doesnt match for the key exchange public part. Disconnecting");
                                    NetworkingManager.Singleton.DisconnectClient(clientId);
                                    return;
                                }
                            }
                        }
                        else
                        {
                            throw new CryptographicException("[MLAPI] Only RSA certificates are supported. No valid RSA key was found");
                        }
                    }
                }
            }

            NetworkingManager.Singleton.PendingClients[clientId].ConnectionState = PendingClient.State.PendingConnection;
            NetworkingManager.Singleton.PendingClients[clientId].KeyExchange = null; // Give to GC
            
            // Send greetings, they have passed all the handshakes
            using (PooledBitStream outStream = PooledBitStream.Get())
            {
                using (PooledBitWriter writer = PooledBitWriter.Get(outStream))
                {
                    writer.WriteInt64Packed(DateTime.Now.Ticks); // This serves no purpose.
                }
                InternalMessageHandler.Send(clientId, MLAPIConstants.MLAPI_GREETINGS, "MLAPI_INTERNAL", outStream, SecuritySendFlags.None, null, true);
            }
        }

        internal static void HandleGreetings(ulong clientId, Stream stream)
        {
            // Server greeted us, we can now initiate our request to connect.
            NetworkingManager.Singleton.SendConnectionRequest();
        }
#endif

        internal static void HandleConnectionRequest(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong configHash = reader.ReadUInt64Packed();
                if (!NetworkingManager.Singleton.NetworkConfig.CompareConfig(configHash))
                {
                    if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("NetworkConfiguration mismatch. The configuration between the server and client does not match");
                    NetworkingManager.Singleton.DisconnectClient(clientId);
                    return;
                }

                if (NetworkingManager.Singleton.NetworkConfig.ConnectionApproval)
                {
                    byte[] connectionBuffer = reader.ReadByteArray();
                    NetworkingManager.Singleton.ConnectionApprovalCallback(connectionBuffer, clientId, NetworkingManager.Singleton.HandleApproval);
                }
                else
                {
                    NetworkingManager.Singleton.HandleApproval(clientId, null, true, null, null);
                }
            }
        }

        internal static void HandleConnectionApproved(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                NetworkingManager.Singleton.LocalClientId = reader.ReadUInt64Packed();
                
                uint sceneIndex = reader.ReadUInt32Packed();
                Guid sceneSwitchProgressGuid = new Guid(reader.ReadByteArray());

                float netTime = reader.ReadSinglePacked();
                ulong msDelay = NetworkingManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId);
                
                NetworkingManager.Singleton.NetworkTime = netTime + (msDelay / 1000f);

                NetworkingManager.Singleton.ConnectedClients.Add(NetworkingManager.Singleton.LocalClientId, new NetworkedClient() { ClientId = NetworkingManager.Singleton.LocalClientId });

                bool sceneSwitch = NetworkSceneManager.HasSceneMismatch(sceneIndex);

                void DelayedSpawnAction(Stream continuationStream)
                {
                    using (PooledBitReader continuationReader = PooledBitReader.Get(continuationStream))
                    {
                        if (NetworkingManager.Singleton.NetworkConfig.UsePrefabSync)
                        {
                            SpawnManager.DestroySceneObjects();
                        }
                        else
                        {
                            SpawnManager.ClientCollectSoftSyncSceneObjectSweep(null);
                        }

                        uint objectCount = continuationReader.ReadUInt32Packed();
                        for (int i = 0; i < objectCount; i++)
                        {
                            bool isPlayerObject = continuationReader.ReadBool();
                            ulong networkId = continuationReader.ReadUInt64Packed();
                            ulong ownerId = continuationReader.ReadUInt64Packed();

                            ulong prefabHash;
                            ulong instanceId;
                            bool softSync;

                            if (NetworkingManager.Singleton.NetworkConfig.UsePrefabSync)
                            {
                                softSync = false;
                                instanceId = 0;
                                prefabHash = continuationReader.ReadUInt64Packed();
                            }
                            else
                            {
                                softSync = continuationReader.ReadBool();

                                if (softSync)
                                {
                                    instanceId = continuationReader.ReadUInt64Packed();
                                    prefabHash = 0;
                                }
                                else
                                {
                                    prefabHash = continuationReader.ReadUInt64Packed();
                                    instanceId = 0;
                                }
                            }

                            Vector3 pos = new Vector3(continuationReader.ReadSinglePacked(), continuationReader.ReadSinglePacked(), continuationReader.ReadSinglePacked());
                            Quaternion rot = Quaternion.Euler(continuationReader.ReadSinglePacked(), continuationReader.ReadSinglePacked(), continuationReader.ReadSinglePacked());

                            NetworkedObject netObject = SpawnManager.CreateLocalNetworkedObject(softSync, instanceId, prefabHash, pos, rot);
                            SpawnManager.SpawnNetworkedObjectLocally(netObject, networkId, softSync, isPlayerObject, ownerId, continuationStream, false, 0, true, false);
                        }

                        NetworkingManager.Singleton.IsConnectedClient = true;

                        if (NetworkingManager.Singleton.OnClientConnectedCallback != null) NetworkingManager.Singleton.OnClientConnectedCallback.Invoke(NetworkingManager.Singleton.LocalClientId);
                    }
                }

                if (sceneSwitch)
                {
                    UnityAction<Scene, Scene> onSceneLoaded = null;

                    Serialization.BitStream continuationStream = new Serialization.BitStream();
                    continuationStream.CopyUnreadFrom(stream);
                    continuationStream.Position = 0;

                    void OnSceneLoadComplete()
                    {
                        SceneManager.activeSceneChanged -= onSceneLoaded;
                        DelayedSpawnAction(continuationStream);
                    }

                    onSceneLoaded = (oldScene, newScene) => { OnSceneLoadComplete(); };
                    
                    SceneManager.activeSceneChanged += onSceneLoaded;

                    NetworkSceneManager.OnFirstSceneSwitchSync(sceneIndex, sceneSwitchProgressGuid);
                }
                else
                {
                    DelayedSpawnAction(stream);
                }
            }
        }

        internal static void HandleAddObject(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                bool isPlayerObject = reader.ReadBool();
                ulong networkId = reader.ReadUInt64Packed();
                ulong ownerId = reader.ReadUInt64Packed();
                
                ulong prefabHash;
                ulong instanceId;
                bool softSync;
                    
                if (NetworkingManager.Singleton.NetworkConfig.UsePrefabSync)
                {
                    softSync = false;
                    instanceId = 0;
                    prefabHash = reader.ReadUInt64Packed();
                }
                else
                {
                    softSync = reader.ReadBool();

                    if (softSync)
                    {
                        instanceId = reader.ReadUInt64Packed();
                        prefabHash = 0;
                    }
                    else
                    {
                        prefabHash = reader.ReadUInt64Packed();
                        instanceId = 0;
                    }
                }

                Vector3 pos = new Vector3(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                Quaternion rot = Quaternion.Euler(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());

                bool hasPayload = reader.ReadBool();
                int payLoadLength = hasPayload ? reader.ReadInt32Packed() : 0;
                
                NetworkedObject netObject = SpawnManager.CreateLocalNetworkedObject(softSync, instanceId, prefabHash, pos, rot);
                SpawnManager.SpawnNetworkedObjectLocally(netObject, networkId, softSync, isPlayerObject, ownerId, stream, hasPayload, payLoadLength, true, false);
            }
        }

        internal static void HandleDestroyObject(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkId = reader.ReadUInt64Packed();
                SpawnManager.OnDestroyObject(networkId, true);
            }
        }

        internal static void HandleSwitchScene(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                uint sceneIndex = reader.ReadUInt32Packed();
                Guid switchSceneGuid = new Guid(reader.ReadByteArray());
                
                Serialization.BitStream objectStream = new Serialization.BitStream();
                objectStream.CopyUnreadFrom(stream);
                objectStream.Position = 0;
                
                NetworkSceneManager.OnSceneSwitch(sceneIndex, switchSceneGuid, objectStream);
            }
        }

        internal static void HandleClientSwitchSceneCompleted(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream)) 
            {
                NetworkSceneManager.OnClientSwitchSceneCompleted(clientId, new Guid(reader.ReadByteArray()));
            }
        }

        internal static void HandleChangeOwner(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkId = reader.ReadUInt64Packed();
                ulong ownerClientId = reader.ReadUInt64Packed();
                
                if (SpawnManager.SpawnedObjects[networkId].OwnerClientId == NetworkingManager.Singleton.LocalClientId)
                {
                    //We are current owner.
                    SpawnManager.SpawnedObjects[networkId].InvokeBehaviourOnLostOwnership();
                }
                if (ownerClientId == NetworkingManager.Singleton.LocalClientId)
                {
                    //We are new owner.
                    SpawnManager.SpawnedObjects[networkId].InvokeBehaviourOnGainedOwnership();
                }
                SpawnManager.SpawnedObjects[networkId].OwnerClientId = ownerClientId;
            }
        }

        internal static void HandleAddObjects(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ushort objectCount = reader.ReadUInt16Packed();
                for (int i = 0; i < objectCount; i++)
                {
                    bool isPlayerObject = reader.ReadBool();
                    ulong networkId = reader.ReadUInt64Packed();
                    ulong ownerId = reader.ReadUInt64Packed();

                    ulong prefabHash;
                    ulong instanceId;
                    bool softSync;
                    
                    if (NetworkingManager.Singleton.NetworkConfig.UsePrefabSync)
                    {
                        softSync = false;
                        instanceId = 0;
                        prefabHash = reader.ReadUInt64Packed();
                    }
                    else
                    {
                        softSync = reader.ReadBool();

                        if (softSync)
                        {
                            instanceId = reader.ReadUInt64Packed();
                            prefabHash = 0;
                        }
                        else
                        {
                            prefabHash = reader.ReadUInt64Packed();
                            instanceId = 0;
                        }
                    }

                    Vector3 pos = new Vector3(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                    Quaternion rot = Quaternion.Euler(reader.ReadSinglePacked(), reader.ReadSinglePacked(), reader.ReadSinglePacked());
                    
                    NetworkedObject netObject = SpawnManager.CreateLocalNetworkedObject(softSync, instanceId, prefabHash, pos, rot);
                    SpawnManager.SpawnNetworkedObjectLocally(netObject, networkId, softSync, isPlayerObject, ownerId, stream, false, 0, true, false);
                }
            }
        }

        internal static void HandleTimeSync(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                float netTime = reader.ReadSinglePacked();
                ulong msDelay = NetworkingManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId);
                
                NetworkingManager.Singleton.NetworkTime = netTime + (msDelay / 1000f);
            }
        }

        internal static void HandleNetworkedVarDelta(ulong clientId, Stream stream)
        {
            if (!NetworkingManager.Singleton.NetworkConfig.EnableNetworkedVar)
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("NetworkedVar delta received but EnableNetworkedVar is false");
                return;
            }
            
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkId = reader.ReadUInt64Packed();
                ushort orderIndex = reader.ReadUInt16Packed();

                if (SpawnManager.SpawnedObjects.ContainsKey(networkId))
                {
                    NetworkedBehaviour instance = SpawnManager.SpawnedObjects[networkId].GetBehaviourAtOrderIndex(orderIndex);
                    if (instance == null)
                    {
                        if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("NetworkedVar message recieved for a non existant behaviour");
                        return;
                    }
                    NetworkedBehaviour.HandleNetworkedVarDeltas(instance.networkedVarFields, stream, clientId, instance);
                }
                else
                {
                    if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("NetworkedVar message recieved for a non existant object with id: " + networkId);
                    return;
                }
            }
        }

        internal static void HandleNetworkedVarUpdate(ulong clientId, Stream stream)
        {
            if (!NetworkingManager.Singleton.NetworkConfig.EnableNetworkedVar)
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("NetworkedVar update received but EnableNetworkedVar is false");
                return;
            }
            
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkId = reader.ReadUInt64Packed();
                ushort orderIndex = reader.ReadUInt16Packed();

                if (SpawnManager.SpawnedObjects.ContainsKey(networkId))
                {
                    NetworkedBehaviour instance = SpawnManager.SpawnedObjects[networkId].GetBehaviourAtOrderIndex(orderIndex);
                    if (instance == null)
                    {
                        if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("NetworkedVar message recieved for a non existant behaviour");
                        return;
                    }
                    NetworkedBehaviour.HandleNetworkedVarUpdate(instance.networkedVarFields, stream, clientId, instance);
                }
                else
                {
                    if (LogHelper.CurrentLogLevel <= LogLevel.Normal) LogHelper.LogWarning("NetworkedVar message recieved for a non existant object with id: " + networkId);
                    return;
                }
            }
        }
        
        internal static void HandleServerRPC(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkId = reader.ReadUInt64Packed();
                ushort behaviourId = reader.ReadUInt16Packed();
                ulong hash = reader.ReadUInt64Packed();

                if (SpawnManager.SpawnedObjects.ContainsKey(networkId)) 
                { 
                    NetworkedBehaviour behaviour = SpawnManager.SpawnedObjects[networkId].GetBehaviourAtOrderIndex(behaviourId);
                    if (behaviour != null)
                    {
                        behaviour.OnRemoteServerRPC(hash, clientId, stream);
                    }
                }
            }
        }
        
        internal static void HandleServerRPCRequest(ulong clientId, Stream stream, string channelName, SecuritySendFlags security)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkId = reader.ReadUInt64Packed();
                ushort behaviourId = reader.ReadUInt16Packed();
                ulong hash = reader.ReadUInt64Packed();
                ulong responseId = reader.ReadUInt64Packed();

                if (SpawnManager.SpawnedObjects.ContainsKey(networkId)) 
                { 
                    NetworkedBehaviour behaviour = SpawnManager.SpawnedObjects[networkId].GetBehaviourAtOrderIndex(behaviourId);
                    if (behaviour != null)
                    {
                        object result = behaviour.OnRemoteServerRPC(hash, clientId, stream);

                        using (PooledBitStream responseStream = PooledBitStream.Get())
                        {
                            using (PooledBitWriter responseWriter = PooledBitWriter.Get(responseStream))
                            {
                                responseWriter.WriteUInt64Packed(responseId);
                                responseWriter.WriteObjectPacked(result);
                            }
                            
                            InternalMessageHandler.Send(clientId, MLAPIConstants.MLAPI_SERVER_RPC_RESPONSE, channelName, responseStream, security, SpawnManager.SpawnedObjects[networkId]);
                        }
                    }
                }
            }
        }
        
        internal static void HandleServerRPCResponse(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong responseId = reader.ReadUInt64Packed();

                if (ResponseMessageManager.ContainsKey(responseId))
                {
                    RpcResponseBase responseBase = ResponseMessageManager.GetByKey(responseId);

                    if (responseBase.ClientId != clientId) return;
                    
                    ResponseMessageManager.Remove(responseId);
                    
                    responseBase.IsDone = true;
                    responseBase.Result = reader.ReadObjectPacked(responseBase.Type);
                    responseBase.IsSuccessful = true;
                }
            }
        }
        
        internal static void HandleClientRPC(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkId = reader.ReadUInt64Packed();
                ushort behaviourId = reader.ReadUInt16Packed();
                ulong hash = reader.ReadUInt64Packed();
                
                if (SpawnManager.SpawnedObjects.ContainsKey(networkId)) 
                {
                    NetworkedBehaviour behaviour = SpawnManager.SpawnedObjects[networkId].GetBehaviourAtOrderIndex(behaviourId);
                    if (behaviour != null)
                    {
                        behaviour.OnRemoteClientRPC(hash, clientId, stream);
                    }
                }
            }
        }
        
        internal static void HandleClientRPCRequest(ulong clientId, Stream stream, string channelName, SecuritySendFlags security)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong networkId = reader.ReadUInt64Packed();
                ushort behaviourId = reader.ReadUInt16Packed();
                ulong hash = reader.ReadUInt64Packed();
                ulong responseId = reader.ReadUInt64Packed();
                
                if (SpawnManager.SpawnedObjects.ContainsKey(networkId)) 
                {
                    NetworkedBehaviour behaviour = SpawnManager.SpawnedObjects[networkId].GetBehaviourAtOrderIndex(behaviourId);
                    if (behaviour != null)
                    {
                        object result = behaviour.OnRemoteClientRPC(hash, clientId, stream);
                        
                        using (PooledBitStream responseStream = PooledBitStream.Get())
                        {
                            using (PooledBitWriter responseWriter = PooledBitWriter.Get(responseStream))
                            {
                                responseWriter.WriteUInt64Packed(responseId);
                                responseWriter.WriteObjectPacked(result);
                            }
                            
                            InternalMessageHandler.Send(clientId, MLAPIConstants.MLAPI_CLIENT_RPC_RESPONSE, channelName, responseStream, security, null);
                        }
                    }
                }
            }
        }
        
        internal static void HandleClientRPCResponse(ulong clientId, Stream stream)
        {
            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                ulong responseId = reader.ReadUInt64Packed();

                if (ResponseMessageManager.ContainsKey(responseId))
                {
                    RpcResponseBase responseBase = ResponseMessageManager.GetByKey(responseId);
                    
                    if (responseBase.ClientId != clientId) return;
                    
                    ResponseMessageManager.Remove(responseId);
                    
                    responseBase.IsDone = true;
                    responseBase.Result = reader.ReadObjectPacked(responseBase.Type);
                    responseBase.IsSuccessful = true;
                }
            }
        }
        
        internal static void HandleCustomMessage(ulong clientId, Stream stream)
        {
            NetworkingManager.Singleton.InvokeOnIncomingCustomMessage(clientId, stream);
        }
    }
}
