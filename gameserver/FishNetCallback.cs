
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Connection;
using FishNet.Broadcast;

public struct FNetBcServerToClient : IBroadcast {
	public int senderId;
	public string command;
	public string strJson;
};

public struct FNetBcClientToServer : IBroadcast {
	public Channel channel;
	public int[] targets;
	public bool includeSender;
	public string command;
	public string strJson;
};

public class FishNetCallback : MonoBehaviour {
	[System.NonSerialized] public Action<ServerConnectionStateArgs> pfnServer = null;
	[System.NonSerialized] public Action<NetworkConnection, RemoteConnectionStateArgs> pfnRemote = null;
	[System.NonSerialized] public Action<ClientConnectionStateArgs> pfnClient = null;
	[System.NonSerialized] public Action<NetworkConnection, FNetBcClientToServer> pfnBcServer = null;
	[System.NonSerialized] public Action<FNetBcServerToClient> pfnBcClient = null;

	public void nullServerConStFn(ServerConnectionStateArgs obj) { }
	public void nullRemoteConStFn(NetworkConnection net, RemoteConnectionStateArgs obj) { }
	public void nullClientConStFn(ClientConnectionStateArgs obj) { }
	public void nullServerBcFn(NetworkConnection net, FNetBcClientToServer bc) { }
	public void nullClientBcFn(FNetBcServerToClient bc) { }
	
	public void setStatusCB(
		Action<ServerConnectionStateArgs> s,
		Action<NetworkConnection, RemoteConnectionStateArgs> r,
		Action<ClientConnectionStateArgs> c
	) {
		pfnServer = (s == null) ? nullServerConStFn : s;
		pfnRemote = (r == null) ? nullRemoteConStFn : r;
		pfnClient = (c == null) ? nullClientConStFn : c;
	}

	private void onServerConnectionState(ServerConnectionStateArgs obj) {
		pfnServer(obj);
	}

	private void onRemoteConnectionState(NetworkConnection net, RemoteConnectionStateArgs obj) {
		pfnRemote(net, obj);
	}

	private void onClientConnectionState(ClientConnectionStateArgs obj) {
		pfnClient(obj);
	}
	
	private void OnServerBroadcast(NetworkConnection net, FNetBcClientToServer bc) {
		pfnBcServer(net, bc);
	}
	
	private void OnClientBroadcast(FNetBcServerToClient bc) {
		pfnBcClient(bc);
	}

	void Start() {
		// connection state
		NetworkManager networkManager = GetComponent<NetworkManager>();
		networkManager.ServerManager.OnServerConnectionState += onServerConnectionState;
		networkManager.ServerManager.OnRemoteConnectionState += onRemoteConnectionState;
		networkManager.ClientManager.OnClientConnectionState += onClientConnectionState;
		pfnServer = nullServerConStFn;
		pfnRemote = nullRemoteConStFn;
		pfnClient = nullClientConStFn;
		// boardcast
        networkManager.ServerManager.RegisterBroadcast<FNetBcClientToServer>(OnServerBroadcast);
		networkManager.ClientManager.RegisterBroadcast<FNetBcServerToClient>(OnClientBroadcast);
		pfnBcServer = nullServerBcFn;
		pfnBcClient = nullClientBcFn;
	}
}

