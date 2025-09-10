using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionManagerUI : MonoBehaviour
{
    [SerializeField] private RelayManager.ConnectionType connectionType;

    [Header("Stage 1 : Decide")]
    [SerializeField] private Transform stage1Transform;
    [SerializeField] private Button createGameButton;
    [SerializeField] private Button joinGameButton;
    [SerializeField] private Button quitGameButton;

    [Header("Stage 2 : Host")]
    [SerializeField] private Transform stage2Transform;
    [SerializeField] private Input_Button roomName;
    [SerializeField] private Input_Button roomSize;
    [SerializeField] private Input_Button playerName;
    [SerializeField] private Transform creatingLobbyVisual;

    [SerializeField] private Button createPublicRoomButton;
    [SerializeField] private Button createPrivateRoomButton;
    [SerializeField] private Button backButtonHost;

    [Header("Stage 2 : Client")]
    [SerializeField] private Transform stage3Transform;
    [SerializeField] private Button quickJoinButton;
    [SerializeField] private Button refreshListButton;
    [SerializeField] private Input_Button joinCode;
    [SerializeField] private Transform lobbiesListHolder;
    [SerializeField] private LobbySingleUI lobbySingleUiPrefab;
    [SerializeField] private Button backButtonClient;
    [SerializeField] private Transform connectingTransform;
    [SerializeField] private float refreshTimerMax = 10f;

    [Header("Generic")]
    [SerializeField] private Transform failedToConnectTransform;
    [SerializeField] private Button okButtonOnFailedToConnect;

    private string roomname;
    private int roomsize = -1;
    private string playername;
    private float refreshTimer = 0f;

    private Lobby currentLobby;
    private readonly List<LobbySingleUI> availableLobbiesList = new();

    [Serializable]
    public class Input_Button
    {
        public TMP_InputField Input;
        public Button Button;
    }

    private void Awake()
    {
        //------------------STAGE 1 SETTINGS------------------------------------

        AddListner(createGameButton, () =>
        {
            Hide(stage1Transform);
            Show(stage2Transform);
        });

        AddListner(joinGameButton, () =>
        {
            Hide(stage1Transform);
            Show(stage3Transform);
        });

        AddListner(quitGameButton,()=> Application.Quit());

        //---------------------STAGE HOST SETTINGS-----------------------------------

        createPublicRoomButton.onClick.AddListener(async () =>
        {
            await CreateLobby(false);
        });

        createPrivateRoomButton.onClick.AddListener(async () =>
        {
            await CreateLobby(true);
        });

        AddListner(backButtonHost, async () =>
        {
            if (currentLobby != null)
            {
                await LobbyManager.TryDeleteLobbyAsync();
            }

            Hide(stage2Transform);
            Show(stage1Transform);
        });

        //---------------------STAGE Client SETTINGS-----------------------------------

        quickJoinButton.onClick.AddListener(async () =>
        {
            try
            {
                connectingTransform.gameObject.SetActive(true);

                currentLobby = await LobbyManager.QuickJoinLobbyAsync();

                if (currentLobby == null) 
                    throw new Exception("Failed to create lobby");

                Debug.Log("Joined lobby with code : " + currentLobby.LobbyCode + " and name : " + currentLobby.Name);

                if (!await GameMultiPlayer.Instance.StartClientWithRelay(currentLobby.Data[LobbyManager.RELAYJOINCODE].Value, connectionType))
                    throw new Exception("Failed to connect");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);

                ShowFailedToConnect();
                NetworkManager.Singleton.Shutdown();
            }
        });

        joinCode.Button.onClick.AddListener(async () =>
        {
            try
            {
                var joincode = joinCode.Input.text.Trim();

                if (string.IsNullOrEmpty(joincode)) return;

                connectingTransform.gameObject.SetActive(true);

                currentLobby = await LobbyManager.JoinLobbyWithCodeAsync(joincode);

                if (currentLobby == null) 
                    throw new Exception("Failed to join lobby");

                Debug.Log("Joined lobby : " + currentLobby.Name);

                if (!await GameMultiPlayer.Instance.StartClientWithRelay(currentLobby.Data[LobbyManager.RELAYJOINCODE].Value, connectionType))
                    throw new Exception("failed to connect with the server");
            }
            catch(Exception ex)
            {
                Debug.LogException(ex);

                ShowFailedToConnect();
                NetworkManager.Singleton.Shutdown();
            }
        });

        AddListner(refreshListButton, () =>
        {
            refreshTimer = 0f;
            _ = QuerryForLobbiesAsync();
        });

        AddListner(okButtonOnFailedToConnect, () =>
        {
            failedToConnectTransform.gameObject.SetActive(false);
        });

        AddListner(backButtonClient, () =>
        {
            Hide(stage3Transform);
            Show(stage1Transform);
        });

        Hide(lobbySingleUiPrefab.transform);

        Hide(stage2Transform);
        Hide(stage3Transform);
        Show(stage1Transform);
        Hide(creatingLobbyVisual);
        Hide(connectingTransform);
        Hide(failedToConnectTransform);

        refreshTimer = refreshTimerMax;
    }

    private void Update()
    {
        if (!IsClientShowing()) return;

        refreshTimer += Time.deltaTime;
        if(refreshTimer > refreshTimerMax)
        {
            refreshTimer = 0f;
            _ = QuerryForLobbiesAsync();
        }
    }

    private void ShowFailedToConnect()
    {
        connectingTransform.gameObject.SetActive(false);
        failedToConnectTransform.gameObject.SetActive(true);
    }
    private async Task QuerryForLobbiesAsync()
    {
        List<Lobby> lobbies = new();

        try
        {
            lobbies = await LobbyManager.QuerryLobbiesAsync(count: 7);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        
        // first destroy the existing lobbies

        foreach(var lobby in availableLobbiesList)
        {
            Destroy(lobby.gameObject);
        }

        availableLobbiesList.Clear();

        foreach(var lobby in lobbies)
        {
            var lobbySingleUI = Instantiate(lobbySingleUiPrefab, lobbiesListHolder);

            lobbySingleUI.RoomName.text = lobby.Data[LobbyManager.LOBBYNAME].Value;

            lobbySingleUI.LobbyJoinId = lobby.Id;

            lobbySingleUI.HostName.text = lobby.Players.Find(player => player.Id == lobby.HostId).Data[LobbyManager.PLAYERNAME].Value;

            lobbySingleUI.SlotsAvailable.text = lobby.AvailableSlots + " / " + lobby.MaxPlayers;

            lobbySingleUI.RelayJoinCode = lobby.Data[LobbyManager.RELAYJOINCODE].Value;

            lobbySingleUI.OnClick(async () =>
            {
                try
                {
                    connectingTransform.gameObject.SetActive(true);

                    currentLobby = await LobbyManager.JoinLobbyWithIdAsync(lobbySingleUI.LobbyJoinId);

                    if (currentLobby == null) 
                        throw new Exception("Failed to join lobby");

                    if (!await GameMultiPlayer.Instance.StartClientWithRelay(lobbySingleUI.RelayJoinCode, connectionType)) 
                        throw new Exception("Failed to connect with relay");

                    Debug.Log("Successfully connected with relay and lobbycode : " + lobbySingleUI.LobbyJoinId);
                }
                catch(Exception ex)
                {
                    Debug.LogException(ex);

                    ShowFailedToConnect();
                    NetworkManager.Singleton.Shutdown();
                }
            });

            availableLobbiesList.Add(lobbySingleUI);

            lobbySingleUI.gameObject.SetActive(true);
        }
    }

    private async Task CreateLobby(bool privateLobby)
    {
        SetLobbyDetails();

        if (string.IsNullOrEmpty(roomname) || string.IsNullOrEmpty(playername) || roomsize == -1) return;

        Show(creatingLobbyVisual);

        try
        {
            var joinCode = await GameMultiPlayer.Instance.StartHostWithRelay(
                maxConnection: roomsize, connectionType: connectionType
            );

            if (string.IsNullOrEmpty(joinCode)) throw new Exception("host not started");

            var lobby = await LobbyManager.CreateLobbyAsync(
                lobbyName: roomname, maxPlayers: roomsize, joinCode, isPrivate: privateLobby, playerName: playername
            );

            currentLobby = lobby;

            if (currentLobby == null) throw new Exception("Problem with lobby creation");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Hide(creatingLobbyVisual);
            ShowFailedToConnect();
            NetworkManager.Singleton.Shutdown();
        }
    }

    private void SetLobbyDetails()
    {
        if (!string.IsNullOrEmpty(roomName.Input.text)) roomname = roomName.Input.text.Trim();

        if (!string.IsNullOrEmpty(roomSize.Input.text))
        {
            var nameOfRoom = roomSize.Input.text.Trim();
            if (!int.TryParse(nameOfRoom, out int size))
            {
                roomSize.Input.text = "";
                roomSize.Input.placeholder.GetComponent<TextMeshProUGUI>().text = "Integer only!";
                return;
            }

            roomsize = size;
        }

        if (!string.IsNullOrEmpty(playerName.Input.text)) playername = playerName.Input.text.Trim();
    }

    private static void AddListner(Button button, Action action) => button.onClick.AddListener(() => action?.Invoke());
    private static void Hide(Transform t) => t.gameObject.SetActive(false);
    private static void Show(Transform t) => t.gameObject.SetActive(true);
    private bool IsClientShowing() => stage3Transform.gameObject.activeSelf;
}
