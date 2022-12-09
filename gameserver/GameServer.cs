
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
//using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using FishNet.Connection;
using Debug=UnityEngine.Debug;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Proyecto26;
using System.Linq;
using Random = UnityEngine.Random;

namespace TestMultiplayer {
public class GameServer : MonoBehaviour {
	//
	private NetworkManager networkManager;
	private FishNetCallback fishnetCB;
	private Tugboat tugboat;
	private string lobbyUrl = "localhost:3001";
	private string lobbyPassword = "";
	private ushort portConnect = 3002;
	private string lobbyServerGroup = "noname";
	private string lobbyServerId;
	private IEnumerator lobbyRefreshCoroutine = null;
	private UtUid chanUid = new UtUid();
	//
	private class ChannelData {
		public string channelId;
		public string password;
		public int hostId;
		//public int userNum;
		public int userMax;
		public bool isClosedChannel; // If true, new user can't join.
		public int uid;
		public string strArg;
	};
	//
	private class UserData {
		public NetworkConnection net;
		public ChannelData channel;
	};
	//
	private ChannelData channelDefault = new ChannelData {
		channelId = null, password = null, hostId = 0, userMax = 1000000,
		isClosedChannel = false, uid = 0, strArg = "",
	};
	private List<UserData> lUserData = new List<UserData>();
	//
	void Start() {
		//
		void assignArgConfigs() {
			string[] args = System.Environment.GetCommandLineArgs();
			for(int i=0; i<(args.Length-1); i++) {
				if(args[i] == "-port") {
					ushort valueDefault = portConnect;
					try { portConnect = ushort.Parse(args[i+1]); }
					catch { portConnect = valueDefault; }
				} else if(args[i] == "-lobbyUrl") {
					string valueDefault = lobbyUrl;
					try { lobbyUrl = args[i+1]; }
					catch { lobbyUrl = valueDefault; }
				} else if(args[i] == "-lobbyPassword") {
					string valueDefault = lobbyPassword;
					try { lobbyPassword = args[i+1]; }
					catch { lobbyPassword = valueDefault; }
				} else if(args[i] == "-lobbyGroup") {
					string valueDefault = lobbyServerGroup;
					try { lobbyServerGroup = args[i+1]; }
					catch { lobbyServerGroup = valueDefault; }
				}
			}
		}
		//
		assignArgConfigs();
		networkManager = FindObjectOfType<NetworkManager>();
		fishnetCB = networkManager.GetComponent<FishNetCallback>();
		tugboat = GameObject.Find("/NetworkManager").GetComponent<Tugboat>();
		fishnetCB.setStatusCB(onServerState, onRemoteState, null);
		fishnetCB.pfnBcServer = fnHostBoardcast;
		tugboat.SetPort(portConnect);
		tugboat.SetMaximumClients(9999);
		networkManager.ServerManager.StartConnection();
		Debug.Log($"lobbyUrl = {lobbyUrl}");
		Debug.Log($"port = {portConnect}");
	}
	//
	class CreateServerParam { public ushort port; public string serverPass; public string serverGroup; public int maxClient; }
	class RefreshServerParam { public string id; public int clientNum; public int maxClient; }
	class DelServerParam { public string id; }
	//
	async void serverLobbyStart() {
		string strRes = await Util.httpPost(lobbyUrl+"/createServer", new CreateServerParam {
			port = tugboat.GetPort(),
			serverPass = lobbyPassword,
			serverGroup = lobbyServerGroup,
			maxClient = tugboat.GetMaximumClients(),
		});
		JObject jRes = JObject.Parse(strRes);
		string errMsg = (string)jRes["errMsg"];
		if(errMsg == null) {
			lobbyServerId = (string)jRes["id"];
			Debug.Log($"/createServer ok lobbyServerId = {lobbyServerId}");
			// refresh timer
			lobbyRefreshCoroutine = waitLobbyRefresh();
			StartCoroutine(lobbyRefreshCoroutine);
		} else {
			Debug.Log($"/createServer post error: {errMsg}");
		}
	}
	//
	IEnumerator waitLobbyRefresh() {
		while(true) {
			yield return new WaitForSeconds(1.0f * 60.0f);
			Debug.Log("refreshServer na");
			async void fn() {
				string strRes = await Util.httpPost(lobbyUrl+"/refreshServer", new RefreshServerParam {
					id = lobbyServerId,
					clientNum = lUserData.Count(),
					maxClient = tugboat.GetMaximumClients(),
				});
				JObject jRes = JObject.Parse(strRes);
				string errMsg = (string)jRes["errMsg"];
				if(errMsg != null) { // reconnect
						serverLobbyStart();
				}
			}
			fn();
        }
    }
	//
	private void onServerState(ServerConnectionStateArgs obj) {
		try {
			//
			async void serverLobbyStop() {
				if(lobbyRefreshCoroutine != null) {
					await Util.httpPost(lobbyUrl+"/delServer", new DelServerParam { id = lobbyServerId, });
					StopCoroutine(lobbyRefreshCoroutine);
					lobbyRefreshCoroutine = null;
				}
			}
			//
			Debug.Log($"server: {obj.ConnectionState}");
			if(obj.ConnectionState == LocalConnectionState.Started) {
				networkManager.ClientManager.StartConnection();
				serverLobbyStart();
			} else if(obj.ConnectionState == LocalConnectionState.Stopped) { // create host failed or close
				serverLobbyStop();
			}
		} catch {}
	}
	//
	private void boardcastNet(NetworkConnection net, int senderId, string command, object data) {
		try {
			networkManager.ServerManager.Broadcast(net, new FNetBcServerToClient() {
				senderId = senderId,
				command = command,
				strJson = JsonConvert.SerializeObject(data),
			});
		} catch {}
	}
	//
	private void boardcastNet(NetworkConnection net, int senderId, string command, string strData) {
		try {
			networkManager.ServerManager.Broadcast(net, new FNetBcServerToClient() {
				senderId = senderId,
				command = command,
				strJson = strData,
			});
		} catch {}
	}
	//
	/*private void boardcastLNet(HashSet<NetworkConnection> lNet, int senderId, string command, object data) {
		try {
			networkManager.ServerManager.Broadcast(lNet, new FNetBcServerToClient() {
				senderId = senderId,
				command = command,
				strJson = JsonConvert.SerializeObject(data),
			});
		} catch {}
	}*/
	//
	private void boardcastLUserData(List<UserData> lUser, int senderId, string command, object data) {
		try {
			HashSet<NetworkConnection> lNet = new HashSet<NetworkConnection>();
			foreach(UserData user in lUser) { lNet.Add(user.net); }
			//
			networkManager.ServerManager.Broadcast(lNet, new FNetBcServerToClient() {
				senderId = senderId,
				command = command,
				strJson = JsonConvert.SerializeObject(data),
			});
		} catch {}
	}
	//
	int userLeaveChannel(NetworkConnection net, bool isSendLeaveSuccessMsg) {
		int index = lUserData.FindIndex((UserData user) => user.net.ClientId == net.ClientId);
		UserData userData = lUserData[index];
		ChannelData channel = userData.channel;
		if(channel.channelId == null) { // channel default
			if(isSendLeaveSuccessMsg) { boardcastNet(userData.net, -1, "leftChannel", new { errMsg = "channel = null", }); }
			return index;
		}
		userData.channel = channelDefault;
		if(isSendLeaveSuccessMsg) { boardcastNet(userData.net, -1, "leftChannel", new { channelId = channel.channelId, }); }
		List<UserData> lOtherUser = lUserData.FindAll((UserData user) => { return user.channel.channelId == channel.channelId; });
		//channel.userNum -= 1;
		if(lOtherUser.Count() <= 0) { chanUid.remove(channel.uid); return index; } // channel is empty now
		int hostId = channel.hostId;
		if(hostId == net.ClientId) { hostId = lOtherUser[0].net.ClientId; channel.hostId = hostId; }
		boardcastLUserData(lOtherUser, -1, "userLeftChannel", new { id = net.ClientId, hostId = hostId, });
		return index;
	}
	//
	void userJoinChannel(NetworkConnection net, string channelId, string channelPassword) {
		if(channelId == null) {
			boardcastNet(net, -1, "joinedChannel", new {errMsg = "channel = null",});
			return;
		}
		int index = lUserData.FindIndex((UserData user) => user.net.ClientId == net.ClientId);
		UserData userData = lUserData[index];
		List<UserData> lOtherUser = lUserData.FindAll((UserData user) => { return user.channel.channelId == channelId; });
		if(lOtherUser.Count() <= 0) {
			boardcastNet(net, -1, "joinedChannel", new {errMsg = "no channel",});
			return;
		}
		ChannelData channel = lOtherUser[0].channel;
		if(channel.isClosedChannel) {
			boardcastNet(net, -1, "joinedChannel", new {errMsg = "channel is closed",});
			return;
		}
		if(lOtherUser.Count() >= channel.userMax) {
			boardcastNet(net, -1, "joinedChannel", new {errMsg = "channel is full",});
			return;
		}
		if((channel.password != null) && (channel.password != channelPassword)) {
			boardcastNet(net, -1, "joinedChannel", new {errMsg = "wrong password",});
			return;
		}
		//Debug.Log($"^3^");
		// otherIds
		int[] otherIds = new int[lOtherUser.Count()];
		for(int i=0; i<lOtherUser.Count(); i++) { otherIds[i] = lOtherUser[i].net.ClientId; }
		//
		//channel.userNum += 1;
		boardcastNet(net, -1, "joinedChannel", new {id = net.ClientId, hostId = channel.hostId, otherIds = otherIds});
		boardcastLUserData(lOtherUser, -1, "userJoinedChannel", new {id = net.ClientId, hostId = channel.hostId});
		userData.channel = channel;
	}
	//
	void userCreateChannel(NetworkConnection net, string channelId, int userMax, string strArg, string channelPassword = null) {
		if(channelId == null) {
			boardcastNet(net, -1, "createdChannel", new {errMsg = "channelId = null",});
			return;
		}
		int index = lUserData.FindIndex((UserData user) => user.net.ClientId == net.ClientId);
		UserData userData = lUserData[index];
		List<UserData> lOtherUser = lUserData.FindAll((UserData user) => { return user.channel.channelId == channelId; });
		if(lOtherUser.Count() > 0) {
			boardcastNet(net, -1, "createdChannel", new {errMsg = "channel is used",});
			return;
		}
		ChannelData channel = new ChannelData {
			channelId = channelId,
			password = channelPassword,
			hostId = net.ClientId,
			//userNum = 1,
			userMax = userMax,
			uid = chanUid.gen(),
			strArg = strArg,
		};
		boardcastNet(net, -1, "createdChannel", new {hostId = channel.hostId, uid = channel.uid,});
		userData.channel = channel;
	}
	//
	private void onRemoteState(NetworkConnection net, RemoteConnectionStateArgs obj) {
		try {
			Debug.Log($"remote {net.ClientId}: {obj.ConnectionState}");
			if(obj.ConnectionState == RemoteConnectionState.Started) {
				lUserData.Add(new UserData { net = net, channel = channelDefault, });
			} else if(obj.ConnectionState == RemoteConnectionState.Stopped) {
				int index = userLeaveChannel(net, false);
				lUserData.RemoveAt(index);
			}
		} catch {}
	}
	//
	HashSet<NetworkConnection> getLNet(string channelId, int[] targets) {
		HashSet<NetworkConnection> lNet = new HashSet<NetworkConnection>();
		if(channelId == null) { return lNet; }
		if(targets == null) {
			foreach(var user in lUserData) { if(user.channel.channelId == channelId) { lNet.Add(user.net); } }
			return lNet;
		}
		//
		for(int i=0; i<targets.Length; i++) {
			UserData userData = lUserData.Find((UserData user) => user.net.ClientId == targets[i]);
			if(userData != null) { lNet.Add(userData.net); }
		}
		return lNet;
	}
	// return dict: channel => userNum
	Dictionary<ChannelData, int> getLChanNumUser(bool incNullId, bool incPassword, bool incUnavail) {
		var lChanNumUserAll = new Dictionary<ChannelData, int>();
		foreach(var userData in lUserData) {
			ChannelData channel = userData.channel;
			if((channel.channelId == null) && !incNullId) { continue; }
			if(lChanNumUserAll.ContainsKey(channel)) { lChanNumUserAll[channel] += 1; }
			else { lChanNumUserAll[channel] = 1; }
		}
		//
		var lChanNumUser = new Dictionary<ChannelData, int>();
		foreach(var dict in lChanNumUserAll) {
			var channel = dict.Key;
			var num = dict.Value;
			if(!incPassword && (channel.password != null)) { continue; }
			if(!incUnavail && channel.isClosedChannel) { continue; }
			if(!incUnavail && (num >= channel.userMax)) { continue; }
			lChanNumUser[channel] = num;
		}
		//
		return lChanNumUser;
	}
	//
	class ChanListData {
		public string channelId;
		public int userNum;
		public int userMax;
		public int uid;
		public bool isClosedChannel;
		public string strArg;
	}
	ChanListData[] getListChannel(bool incPassword, bool incFull) {
		var lChanNumUser = getLChanNumUser(false, incPassword, incFull);
		var arr = new ChanListData[lChanNumUser.Count];
		int i = 0;
		foreach(var dict in lChanNumUser) {
			var channel = dict.Key;
			var num = dict.Value;
			arr[i] = new ChanListData {
				channelId = channel.channelId,
				userNum = num,
				userMax = channel.userMax,
				uid = channel.uid,
				isClosedChannel = channel.isClosedChannel,
				strArg = channel.strArg,
			};
			i++;
		}
		return arr;
	}
	//
	string[] cacheListChan = new string[]{null, null, null, null};
	string getCacheListChannel(bool incPassword, bool incFull) {
		int id = (incPassword ? 0 : 1) * 2 + (incFull ? 0 : 1);
		float test = Random.value;
		IEnumerator enuFn() {
			yield return new WaitForSeconds(2);
			cacheListChan[id] = null;
		}
		if(cacheListChan[id] == null) {
			cacheListChan[id] = JsonConvert.SerializeObject(new {list = getListChannel(incPassword, incFull),});
			StartCoroutine(enuFn());
		}
		return cacheListChan[id];
	}
	//
	void fnHostBoardcast(NetworkConnection net, FNetBcClientToServer bc) {
		//
		ChannelData checkErrHostConfig(string commandRet) {
			UserData userData = lUserData.Find((UserData user) => user.net.ClientId == net.ClientId);
			if(userData == null) {Debug.Log($"No user id: {net.ClientId}"); return null;}
			ChannelData channel = userData.channel;
			if(channel.channelId == null) { boardcastNet(net, -1, commandRet, new {errMsg = "channel = null"}); return null; }
			if(net.ClientId != channel.hostId) { boardcastNet(net, -1, commandRet, new {errMsg = "no permission"}); return null; }
			return channel;
		}
		//
		try {
			switch(bc.command) {
				case "createChannel": {
					JObject jobj = JObject.Parse(bc.strJson);
					string channelId = (string)jobj["channelId"];
					int userMax = (int)jobj["userMax"];
					string strArg = (string)jobj["strArg"];
					string channelPassword = (string)jobj["password"];
					Debug.Log($"{net.ClientId} create channelId = {channelId}, userMax = {userMax}, password = {channelPassword}");
					userLeaveChannel(net, false);
					userCreateChannel(net, channelId, userMax, strArg, channelPassword);
					break;
				}
				//
				case "joinChannel": {
					JObject jobj = JObject.Parse(bc.strJson);
					string channelId = (string)jobj["channelId"];
					string channelPassword = (string)jobj["password"];
					Debug.Log($"{net.ClientId} join channelId = {channelId}");
					userLeaveChannel(net, false);
					userJoinChannel(net, channelId, channelPassword);
					break;
				}
				//
				case "leaveChannel": {
					userLeaveChannel(net, true);
					break;
				}
				//
				case "listChannel": {
					JObject jobj = JObject.Parse(bc.strJson);
					boardcastNet(net, -1, "listChannelRet", getCacheListChannel((bool)jobj["incPassword"], (bool)jobj["incUnavail"]));
					break;
				}
				//
				case "setUserMax": {
					void fn() {
						ChannelData channel = checkErrHostConfig("setUserMaxRet");
						JObject jobj = JObject.Parse(bc.strJson);
						channel.userMax = (int)jobj["userMax"];
						boardcastNet(net, -1, "setUserMaxRet", new {userMax = channel.userMax});
					}
					fn();
					break;
				}
				//
				case "setClosedChannel": {
					void fn() {
						ChannelData channel = checkErrHostConfig("setClosedChannelRet");
						JObject jobj = JObject.Parse(bc.strJson);
						channel.isClosedChannel = (bool)jobj["isClosed"];
						boardcastNet(net, -1, "setClosedChannelRet", new {isClosed = channel.isClosedChannel});
					}
					fn();
					break;
				}
				//
				default: {
					void fn() {
						UserData userData = lUserData.Find((UserData user) => user.net.ClientId == net.ClientId);
						if(userData == null) {Debug.Log($"No user id: {net.ClientId}"); return;}
						HashSet<NetworkConnection> lNet = getLNet(userData.channel.channelId, bc.targets);
						if((bc.targets == null) && !bc.includeSender) {
							lNet.RemoveWhere(netCheck => netCheck.ClientId == net.ClientId);
						}
						networkManager.ServerManager.Broadcast(lNet, new FNetBcServerToClient() {
							senderId = net.ClientId,
							command = bc.command,
							strJson = bc.strJson,
						}, true, bc.channel);
					}
					fn();
					break;
				}
			}
		} catch {}
	}
}};
