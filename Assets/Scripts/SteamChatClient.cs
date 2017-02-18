using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Steamworks;

public class SteamChatClient : MonoBehaviour {
	enum EChatClientState {
		Init,
		SteamNotInitialized,
		Refreshing,
		NoLobbiesFound,
		DisplayResults,
		Joining,
		Creating,
		FailedToJoin,
		InLobby,
	}
	EChatClientState m_ChatClientState = EChatClientState.Init;

	struct Lobby {
		public CSteamID m_SteamID;
		public int m_MemberCount;
		public int m_MemberLimit;
		public string[] m_DataKeys;
		public string[] m_DataValues;
	}

	CallResult<LobbyEnter_t> m_LobbyEnterCallResult;
	CallResult<LobbyMatchList_t> m_LobbyMatchListCallResult;
	CallResult<LobbyCreated_t> m_LobbyCreatedCallResult;

	Lobby[] m_Lobbies;

	Lobby m_CurrentLobby;
	EChatRoomEnterResponse m_LobbyEnterResponse;

	public GameObject m_LobbyListPanel;
	public GameObject m_LobbyPanel;
	public Text m_StatusText;
	public Text m_InLobbyStatusText;
	public Text m_ChatText;
	public Text m_ServerInfoText;
	public GameObject itemPrefab;
	public GameObject scrollable;
	public Button m_JoinButton;
	GameObject[] m_items;
	int m_CurrentlySelected;

	void OnEnable() {
		if (m_LobbyListPanel == null || m_LobbyPanel == null) {
			throw new System.Exception("Stop being a dumbo ya' big dumbo.");
		}
		
		m_LobbyPanel.SetActive(false);
		m_LobbyListPanel.SetActive(true);

		if (!SteamManager.Initialized) {
			ChangeState(EChatClientState.SteamNotInitialized);
			return;
		}

		m_LobbyEnterCallResult = CallResult<LobbyEnter_t>.Create(OnLobbyEnter);
		m_LobbyMatchListCallResult = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
		m_LobbyCreatedCallResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);

		m_Lobbies = null;

		if (m_items != null) {
			for (int i = 0; i < m_items.Length; ++i) {
				Destroy(m_items[i]);
			}
		}
		m_items = null;
		RefreshLobbyList();
	}

	void OnDisable() {
		if (m_ChatClientState == EChatClientState.InLobby) {
			SteamMatchmaking.LeaveLobby(m_CurrentLobby.m_SteamID); // TODO: Causes a Null exception, did Unity 5 change the order of these function calls?
		}
	}

	void ChangeState(EChatClientState newState) {
		switch (newState) {
			case EChatClientState.Init:
				m_StatusText.text = "Init.";
				break;
			case EChatClientState.SteamNotInitialized:
				m_StatusText.text = "Steamworks is not Initialized...";
				break;
			case EChatClientState.Refreshing:
				m_StatusText.text = "Refreshing...";
				m_LobbyMatchListCallResult.Set(SteamMatchmaking.RequestLobbyList());
				m_JoinButton.interactable = false;
				break;
			case EChatClientState.NoLobbiesFound:
				m_StatusText.text = "No Lobbies Found...";
				break;
			case EChatClientState.DisplayResults:
				int nLobbies = m_Lobbies.Length;
				m_StatusText.text = "Found " + nLobbies + " lobbies:";

				RectTransform itemRectT = itemPrefab.GetComponent<RectTransform>();
				RectTransform rectTransform = scrollable.GetComponent<RectTransform>();

				rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, itemRectT.rect.height * nLobbies);

				m_items = new GameObject[nLobbies];
				for (int i = 0; i < nLobbies; ++i) {
					m_items[i] = Instantiate(itemPrefab);
					m_items[i].name = gameObject.name + " item at (" + i + ")";
					m_items[i].transform.parent = scrollable.transform;

					RectTransform newRectT = m_items[i].GetComponent<RectTransform>();
					newRectT.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, rectTransform.rect.width);
					newRectT.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, i * itemRectT.rect.height, itemRectT.rect.height);

					Text text = m_items[i].GetComponentInChildren<Text>();
					text.text = i + " - SteamID: " + m_Lobbies[i].m_SteamID.ToString() + " - Players: " + m_Lobbies[i].m_MemberCount + "/" + m_Lobbies[i].m_MemberLimit;

					Button button = m_items[i].GetComponentInChildren<Button>();
					int ThisIsDumb = i;
					button.onClick.AddListener(delegate {
						m_CurrentlySelected = ThisIsDumb;
						System.Text.StringBuilder test = new System.Text.StringBuilder();
						for (int j = 0; j < m_Lobbies[m_CurrentlySelected].m_DataKeys.Length; ++j) {
							test.Append(m_Lobbies[m_CurrentlySelected].m_DataKeys[j]);
							test.Append(":");
							test.Append(m_Lobbies[m_CurrentlySelected].m_DataValues[j]);
							test.Append(", ");
						}

						m_ServerInfoText.text = test.ToString();
						
						m_JoinButton.interactable = true;
					});
				}

				break;
			case EChatClientState.Joining:
				m_StatusText.text = "Joining...";
				m_LobbyEnterCallResult.Set(SteamMatchmaking.JoinLobby(m_Lobbies[m_CurrentlySelected].m_SteamID));
				break;

			case EChatClientState.Creating:
				m_StatusText.text = "Creating...";
				m_LobbyCreatedCallResult.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePrivate, 4));
				break;
			case EChatClientState.FailedToJoin:
				m_StatusText.text = "Failed To Join...";
				break;
			case EChatClientState.InLobby:
				m_InLobbyStatusText.text = "SteamID: " + m_CurrentLobby.m_SteamID.ToString() + " - Slots: " + m_CurrentLobby.m_MemberCount + "/" + m_CurrentLobby.m_MemberLimit;
				m_LobbyListPanel.SetActive(false);
				m_LobbyPanel.SetActive(true);
				m_ChatText.text = "";
				break;
		}

		m_ChatClientState = newState;
	}

	public void JoinServer() {
		ChangeState(EChatClientState.Joining);
	}

	public void RefreshLobbyList() {
		ChangeState(EChatClientState.Refreshing);

		if (m_items != null) {
			for (int i = 0; i < m_items.Length; ++i) {
				Destroy(m_items[i]);
			}
		}
	}

	public void CreateLobby() {
		ChangeState(EChatClientState.Creating);
	}

	public void LeaveLobby() {
		SteamMatchmaking.LeaveLobby(m_CurrentLobby.m_SteamID);
		m_LobbyListPanel.SetActive(true);
		m_LobbyPanel.SetActive(false);
		RefreshLobbyList();
	}

	public void SubmitChatText(string text) {
		//m_ChatText.text += text + '\n';
		byte[] bytes = System.Text.Encoding.ASCII.GetBytes(text);
		print("Submitted: '" + text + "' Len: " + text.Length + " bLen: " + bytes.Length);
		SteamMatchmaking.SendLobbyChatMsg(m_CurrentLobby.m_SteamID, bytes, bytes.Length + 1);
	}

#if DIS
	void OnGUI() {
		GUILayout.Label(m_ChatClientState.ToString());

		switch (m_ChatClientState) {
			case EChatClientState.Init:
				if (GUILayout.Button("Refresh")) {
					RefreshLobbyList();
				}
				break;
			case EChatClientState.SteamworksNotInitialized:
				GUILayout.Label("Steamworks failed to Initialize...");
				break;
			case EChatClientState.Refreshing:
				GUILayout.Label("Refreshing...");
				break;
			case EChatClientState.NoLobbiesFound:
				if (GUILayout.Button("Refresh")) {
					RefreshLobbyList();
				}
				GUILayout.Label("No Lobbies Available!");
				break;
			case EChatClientState.DisplayResults:
				if (GUILayout.Button("Create Lobby")) {
					CreateLobby();
				}
				if (GUILayout.Button("Refresh")) {
					RefreshLobbyList();
				}

				if (m_Lobbies != null) {
					int nLobbies = m_Lobbies.Length;
					GUILayout.Label("Found " + nLobbies + " lobbies:");
					for (int i = 0; i < nLobbies; ++i) {
						GUILayout.Label(i + " - SteamID: " + m_Lobbies[i].m_SteamID.ToString() + " - Slots: " + m_Lobbies[i].m_MemberCount + "/" + m_Lobbies[i].m_MemberLimit);

						if (GUILayout.Button("Join")) {
							m_LobbyEnterCallResult.Set(SteamMatchmaking.JoinLobby(m_Lobbies[i].m_SteamID));
							ChangeState(EChatClientState.Joining;
						}

						System.Text.StringBuilder test = new System.Text.StringBuilder();
						for (int j = 0; j < m_Lobbies[i].m_DataKeys.Length; ++j) {
							test.Append(m_Lobbies[i].m_DataKeys[j]);
							test.Append(":");
							test.Append(m_Lobbies[i].m_DataValues[j]);
							test.Append(", ");
						}
						GUILayout.Label(test.ToString());
						/*if (GUILayout.Button("Test")) { // Test sending a message from outside of the lobby!
							byte[] MsgBody = System.Text.Encoding.UTF8.GetBytes("Test Message!");
							bool ret = SteamMatchmaking.SendLobbyChatMsg(m_Lobbies[i].m_SteamID, MsgBody, MsgBody.Length);
							print("Test: " + ret);
						}*/
					}
				}
				break;
			case EChatClientState.Joining:
				GUILayout.Label("Joining...");
				break;
			case EChatClientState.Creating:
				GUILayout.Label("Creating...");
				break;
			case EChatClientState.FailedToJoin:
				GUILayout.Label("Failed to Join Lobby: " + m_LobbyEnterResponse);
				if (GUILayout.Button("OK")) {
					RefreshLobbyList();
				}
				break;
			case EChatClientState.InLobby:
				GUILayout.Label("SteamID: " + m_CurrentLobby.m_SteamID.ToString() + " - Slots: " + m_CurrentLobby.m_MemberCount + "/" + m_CurrentLobby.m_MemberLimit);
				if (GUILayout.Button("Leave")) {
					SteamMatchmaking.LeaveLobby(m_CurrentLobby.m_SteamID);
					RefreshLobbyList();
				}
				break;
		}
	}
#endif


	void OnFavoritesListChanged(FavoritesListChanged_t pCallback) {
		Debug.Log("[" + FavoritesListChanged_t.k_iCallback + " - FavoritesListChanged] - " + pCallback.m_nIP + " -- " + pCallback.m_nQueryPort + " -- " + pCallback.m_nConnPort + " -- " + pCallback.m_nAppID + " -- " + pCallback.m_nFlags + " -- " + pCallback.m_bAdd + " -- " + pCallback.m_unAccountId);
	}

	void OnLobbyInvite(LobbyInvite_t pCallback) {
		Debug.Log("[" + LobbyInvite_t.k_iCallback + " - LobbyInvite] - " + pCallback.m_ulSteamIDUser + " -- " + pCallback.m_ulSteamIDLobby + " -- " + pCallback.m_ulGameID);
	}

	void OnLobbyEnter(LobbyEnter_t pCallback) {
		Debug.Log("[" + LobbyEnter_t.k_iCallback + " - LobbyEnter] - " + pCallback.m_ulSteamIDLobby + " -- " + pCallback.m_rgfChatPermissions + " -- " + pCallback.m_bLocked + " -- " + pCallback.m_EChatRoomEnterResponse);
	}

	void OnLobbyEnter(LobbyEnter_t pCallback, bool bIOFailure) {
		if (bIOFailure) {
			ESteamAPICallFailure reason = SteamUtils.GetAPICallFailureReason(m_LobbyEnterCallResult.Handle);
			Debug.LogError("OnLobbyEnter encountered an IOFailure due to: " + reason);
			return; // TODO: Recovery
		}

		Debug.Log("[" + LobbyEnter_t.k_iCallback + " - LobbyEnter] - " + pCallback.m_ulSteamIDLobby + " -- " + pCallback.m_rgfChatPermissions + " -- " + pCallback.m_bLocked + " -- " + (EChatRoomEnterResponse)pCallback.m_EChatRoomEnterResponse);
		
		m_LobbyEnterResponse = (EChatRoomEnterResponse)pCallback.m_EChatRoomEnterResponse;
		
		if(m_LobbyEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess) {
			ChangeState(EChatClientState.FailedToJoin);
			return;
		}
		
		m_CurrentLobby.m_SteamID = (CSteamID)pCallback.m_ulSteamIDLobby;
		m_CurrentLobby.m_MemberCount = SteamMatchmaking.GetNumLobbyMembers(m_CurrentLobby.m_SteamID);
		m_CurrentLobby.m_MemberLimit = SteamMatchmaking.GetLobbyMemberLimit(m_CurrentLobby.m_SteamID);

		ChangeState(EChatClientState.InLobby);
	}

	void OnLobbyDataUpdate(LobbyDataUpdate_t pCallback) {
		Debug.Log("[" + LobbyDataUpdate_t.k_iCallback + " - LobbyDataUpdate] - " + pCallback.m_ulSteamIDLobby + " -- " + pCallback.m_ulSteamIDMember + " -- " + pCallback.m_bSuccess);
	}

	void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback) {
		Debug.Log("[" + LobbyChatUpdate_t.k_iCallback + " - LobbyChatUpdate] - " + pCallback.m_ulSteamIDLobby + " -- " + pCallback.m_ulSteamIDUserChanged + " -- " + pCallback.m_ulSteamIDMakingChange + " -- " + pCallback.m_rgfChatMemberStateChange);
	}

	void OnLobbyChatMsg(LobbyChatMsg_t pCallback) {
		Debug.Log("[" + LobbyChatMsg_t.k_iCallback + " - LobbyChatMsg] - " + pCallback.m_ulSteamIDLobby + " -- " + pCallback.m_ulSteamIDUser + " -- " + pCallback.m_eChatEntryType + " -- " + pCallback.m_iChatID);
		CSteamID SteamIDUser;
		byte[] Data = new byte[4096];
		EChatEntryType ChatEntryType;
		int ret = SteamMatchmaking.GetLobbyChatEntry((CSteamID)pCallback.m_ulSteamIDLobby, (int)pCallback.m_iChatID, out SteamIDUser, Data, Data.Length, out ChatEntryType);
		Debug.Log("SteamMatchmaking.GetLobbyChatEntry(" + (CSteamID)pCallback.m_ulSteamIDLobby + ", " + (int)pCallback.m_iChatID + ", out SteamIDUser, Data, Data.Length, out ChatEntryType) : " + ret + " -- " + SteamIDUser + " -- " + System.Text.Encoding.UTF8.GetString(Data) + " -- " + ChatEntryType);
	}

	void OnLobbyGameCreated(LobbyGameCreated_t pCallback) {
		Debug.Log("[" + LobbyGameCreated_t.k_iCallback + " - LobbyGameCreated] - " + pCallback.m_ulSteamIDLobby + " -- " + pCallback.m_ulSteamIDGameServer + " -- " + pCallback.m_unIP + " -- " + pCallback.m_usPort);
	}

	void OnLobbyMatchList(LobbyMatchList_t pCallback, bool bIOFailure) {
		if (bIOFailure) {
			ESteamAPICallFailure reason = SteamUtils.GetAPICallFailureReason(m_LobbyMatchListCallResult.Handle);
			Debug.LogError("OnLobbyMatchList encountered an IOFailure due to: " + reason);
			return; // TODO: Recovery
		}

		Debug.Log("[" + LobbyMatchList_t.k_iCallback + " - LobbyMatchList] - " + pCallback.m_nLobbiesMatching);

		if (pCallback.m_nLobbiesMatching == 0) {
			ChangeState(EChatClientState.NoLobbiesFound);
			return;
		}

		m_Lobbies = new Lobby[pCallback.m_nLobbiesMatching];
		for (int i = 0; i < pCallback.m_nLobbiesMatching; ++i) {
			m_Lobbies[i].m_SteamID = SteamMatchmaking.GetLobbyByIndex(i);
			m_Lobbies[i].m_MemberCount = SteamMatchmaking.GetNumLobbyMembers(m_Lobbies[i].m_SteamID);
			m_Lobbies[i].m_MemberLimit = SteamMatchmaking.GetLobbyMemberLimit(m_Lobbies[i].m_SteamID);

			/*uint IP;
			ushort Port;
			CSteamID GameServerSteamID;
			bool lobbyGameServerRet = SteamMatchmaking.GetLobbyGameServer(m_Lobbies[i].m_SteamID, out IP, out Port, out GameServerSteamID);
			print("IP: " + IP);
			print("Port: " + Port);
			print("GSID: " + GameServerSteamID);*/

			int nDataCount = SteamMatchmaking.GetLobbyDataCount(m_Lobbies[i].m_SteamID);
			m_Lobbies[i].m_DataKeys = new string[nDataCount];
			m_Lobbies[i].m_DataValues = new string[nDataCount];
			for(int j = 0; j < nDataCount; ++j) {
				string key;
				string value;
				bool lobbyDataRet = SteamMatchmaking.GetLobbyDataByIndex(m_Lobbies[i].m_SteamID, j, out key, 255, out value, 255);
				if(!lobbyDataRet) {
					Debug.LogError("SteamMatchmaking.GetLobbyDataByIndex returned false.");
					continue;
				}

				m_Lobbies[i].m_DataKeys[j] = key;
				m_Lobbies[i].m_DataValues[j] = value;
			}
		}

		ChangeState(EChatClientState.DisplayResults);
	}

	void OnLobbyKicked(LobbyKicked_t pCallback) {
		Debug.Log("[" + LobbyKicked_t.k_iCallback + " - LobbyKicked] - " + pCallback.m_ulSteamIDLobby + " -- " + pCallback.m_ulSteamIDAdmin + " -- " + pCallback.m_bKickedDueToDisconnect);
	}

	void OnLobbyCreated(LobbyCreated_t pCallback, bool bIOFailure) {
		if (bIOFailure) {
			ESteamAPICallFailure reason = SteamUtils.GetAPICallFailureReason(m_LobbyCreatedCallResult.Handle);
			Debug.LogError("OnLobbyCreated encountered an IOFailure due to: " + reason);
			return; // TODO: Recovery
		}

		Debug.Log("[" + LobbyCreated_t.k_iCallback + " - LobbyCreated] - " + pCallback.m_eResult + " -- " + pCallback.m_ulSteamIDLobby);
		
		m_CurrentLobby.m_SteamID = (CSteamID)pCallback.m_ulSteamIDLobby;
		m_CurrentLobby.m_MemberCount = SteamMatchmaking.GetNumLobbyMembers(m_CurrentLobby.m_SteamID);
		m_CurrentLobby.m_MemberLimit = SteamMatchmaking.GetLobbyMemberLimit(m_CurrentLobby.m_SteamID);

		ChangeState(EChatClientState.InLobby);
	}

	void OnFavoritesListAccountsUpdated(FavoritesListAccountsUpdated_t pCallback) {
		Debug.Log("[" + FavoritesListAccountsUpdated_t.k_iCallback + " - FavoritesListAccountsUpdated] - " + pCallback.m_eResult);
	}
}
