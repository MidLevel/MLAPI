---
title: NetworkConfig
permalink: /api/network-config/
---

<div style="line-height: 1;">
	<h2 markdown="1">NetworkConfig ``class``</h2>
	<p style="font-size: 20px;"><b>Namespace:</b> MLAPI.Configuration</p>
	<p style="font-size: 20px;"><b>Assembly:</b> MLAPI.dll</p>
</div>
<p>The configuration object used to start server, client and hosts</p>

<div>
	<h3 markdown="1">Public Properties</h3>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``X509Certificate2`` ServerX509Certificate { get; set; }</b></h4>
		<p>Gets the currently in use certificate</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``byte[]`` ServerX509CertificateBytes { get; }</b></h4>
		<p>Gets the cached binary representation of the server certificate that's used for handshaking</p>
	</div>
</div>
<br>
<div>
	<h3 markdown="1">Public Fields</h3>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``ushort`` ProtocolVersion;</b></h4>
		<p>The protocol version. Different versions doesn't talk to each other.</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public [``DefaultTransport``](/MLAPI/api/default-transport/) Transport;</b></h4>
		<p>The transport to be used</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``IUDPTransport`` NetworkTransport;</b></h4>
		<p>The transport hosts the sever uses</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``string`` RelayAddress;</b></h4>
		<p>Only used if the transport is MLPAI-Relay</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``ushort`` RelayPort;</b></h4>
		<p>Only used if the transport is MLPAI-Relay</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``bool`` RelayEnabled;</b></h4>
		<p>Wheter or not to use the relay</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``List<Channel>`` Channels;</b></h4>
		<p>Channels used by the NetworkedTransport</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``List<string>`` RegisteredScenes;</b></h4>
		<p>A list of SceneNames that can be used during networked games.</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``List<NetworkedPrefab>`` NetworkedPrefabs;</b></h4>
		<p>A list of spawnable prefabs</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``int`` MessageBufferSize;</b></h4>
		<p>The size of the receive message buffer. This is the max message size including any MLAPI overheads.</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``int`` ReceiveTickrate;</b></h4>
		<p>Amount of times per second the receive queue is emptied and all messages inside are processed.</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``int`` MaxReceiveEventsPerTickRate;</b></h4>
		<p>The max amount of messages to process per ReceiveTickrate. This is to prevent flooding.</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``int`` SendTickrate;</b></h4>
		<p>The amount of times per second every pending message will be sent away.</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``int`` EventTickrate;</b></h4>
		<p>The amount of times per second internal frame events will occur, examples include SyncedVar send checking.</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``int`` MaxConnections;</b></h4>
		<p>The max amount of Clients that can connect.</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``int`` ConnectPort;</b></h4>
		<p>The port for the NetworkTransport to use when connecting</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``string`` ConnectAddress;</b></h4>
		<p>The address to connect to</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``int`` ClientConnectionBufferTimeout;</b></h4>
		<p>The amount of seconds to wait for handshake to complete before timing out a client</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``bool`` ConnectionApproval;</b></h4>
		<p>Wheter or not to use connection approval</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``byte[]`` ConnectionData;</b></h4>
		<p>The data to send during connection which can be used to decide on if a client should get accepted</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``int`` SecondsHistory;</b></h4>
		<p>The amount of seconds to keep a lag compensation position history</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``bool`` HandleObjectSpawning;</b></h4>
		<p>Wheter or not to make the library handle object spawning</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``bool`` EnableSceneSwitching;</b></h4>
		<p>Wheter or not to enable scene switching</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``bool`` EnableTimeResync;</b></h4>
		<p>If your logic uses the NetworkedTime, this should probably be turned off. If however it's needed to maximize accuracy, this is recommended to be turned on</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``bool`` ForceSamePrefabs;</b></h4>
		<p>Wheter or not the MLAPI should check for differences in the prefabs at connection. 
            If you dynamically add prefabs at runtime, turn this OFF</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public [``HashSize``](/MLAPI/api/hash-size/) RpcHashSize;</b></h4>
		<p>Decides how many bytes to use for Rpc messaging. Leave this to 2 bytes unless you are facing hash collisions</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public [``HashSize``](/MLAPI/api/hash-size/) PrefabHashSize;</b></h4>
		<p>Decides how many bytes to use for Prefab names. Leave this to 2 bytes unless you are facing hash collisions</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``int`` LoadSceneTimeOut;</b></h4>
		<p>Wheter or not to enable encryption
            The amount of seconds to wait on all clients to load requested scene before the SwitchSceneProgress onComplete callback, that waits for all clients to complete loading, is called anyway.</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``bool`` EnableEncryption;</b></h4>
		<p>Wheter or not to enable the ECDHE key exchange to allow for encryption and authentication of messages</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``bool`` SignKeyExchange;</b></h4>
		<p>Wheter or not to enable signed diffie hellman key exchange.</p>
	</div>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``string`` ServerBase64PfxCertificate;</b></h4>
		<p>Pfx file in base64 encoding containing private and public key</p>
	</div>
</div>
<br>
<div>
	<h3>Public Constructors</h3>
	<div style="line-height: 1; ">
		<h4 markdown="1"><b>public [``NetworkConfig``](/MLAPI/api/network-config/)();</b></h4>
	</div>
</div>
<br>
<div>
	<h3 markdown="1">Public Methods</h3>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``string`` ToBase64();</b></h4>
		<p>Returns a base64 encoded version of the config</p>
		<h5 markdown="1"><b>Returns ``string``</b></h5>
		<div>
			<p></p>
		</div>
	</div>
	<br>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``void`` FromBase64(``string`` base64, ``bool`` createDummyObject);</b></h4>
		<p>Sets the NetworkConfig data with that from a base64 encoded version</p>
		<h5><b>Parameters</b></h5>
		<div>
			<p style="font-size: 20px; color: #444;" markdown="1">``string`` base64</p>
			<p>The base64 encoded version</p>
		</div>
		<div>
			<p style="font-size: 20px; color: #444;" markdown="1">``bool`` createDummyObject</p>
			<p>Wheter or not to create dummy objects for NetworkedPrefabs</p>
		</div>
	</div>
	<br>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``ulong`` GetConfig(``bool`` cache);</b></h4>
		<p>Gets a SHA256 hash of parts of the NetworkingConfiguration instance</p>
		<h5><b>Parameters</b></h5>
		<div>
			<p style="font-size: 20px; color: #444;" markdown="1">``bool`` cache</p>
			<p></p>
		</div>
		<h5 markdown="1"><b>Returns ``ulong``</b></h5>
		<div>
			<p></p>
		</div>
	</div>
	<br>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``bool`` CompareConfig(``ulong`` hash);</b></h4>
		<p>Compares a SHA256 hash with the current NetworkingConfiguration instances hash</p>
		<h5><b>Parameters</b></h5>
		<div>
			<p style="font-size: 20px; color: #444;" markdown="1">``ulong`` hash</p>
			<p></p>
		</div>
		<h5 markdown="1"><b>Returns ``bool``</b></h5>
		<div>
			<p></p>
		</div>
	</div>
	<br>
</div>
<br>
<div>
	<h3 markdown="1">Inherited Methods</h3>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``string`` ToString();</b></h4>
		<h5 markdown="1">Inherited from: ``object``</h5>
	</div>
	<br>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``bool`` Equals(``object`` obj);</b></h4>
		<h5 markdown="1">Inherited from: ``object``</h5>
		<h5><b>Parameters</b></h5>
		<div>
			<p style="font-size: 20px; color: #444;" markdown="1">``object`` obj</p>
		</div>
	</div>
	<br>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``int`` GetHashCode();</b></h4>
		<h5 markdown="1">Inherited from: ``object``</h5>
	</div>
	<br>
	<div style="line-height: 1;">
		<h4 markdown="1"><b>public ``Type`` GetType();</b></h4>
		<h5 markdown="1">Inherited from: ``object``</h5>
	</div>
</div>
<br>
