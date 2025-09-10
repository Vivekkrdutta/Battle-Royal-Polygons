using System;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Unity.Services.Lobbies.Models;
using Unity.Services.Lobbies;
using Unity.Services.Authentication;

public class LobbyManager : NetworkBehaviour
{
    public const string RELAYJOINCODE = "RELAYJOINCODE";
    public const string LOBBYNAME = "LobbyName";
    public const string PLAYERNAME = "PlayerName";

    private static Lobby currentLobby;
    private readonly float lobbyUpdateTimerMax = 15;
    private float lobbyUpdateTimer = 0f;
    private static LobbyManager instance;

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private void Update()
    {
        if (currentLobby == null || currentLobby.HostId != AuthenticationService.Instance.PlayerId) return;

        lobbyUpdateTimer += Time.deltaTime;

        if (lobbyUpdateTimer < lobbyUpdateTimerMax) return;

        lobbyUpdateTimer = 0f;

        _ = SendSingleHeartBeatAsync();
    }

    private async Task SendSingleHeartBeatAsync()
    {
        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            Debug.Log("Sent heart beat ping to mother lobby");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    public static async Task<Lobby> CreateLobbyAsync(string lobbyName,int maxPlayers,string relayJoinCode,bool isPrivate,string playerName = "")
    {
        try
        {
            var player = new Unity.Services.Lobbies.Models.Player(

                id: AuthenticationService.Instance.PlayerId,
                data: new Dictionary<string, PlayerDataObject>()
                {
                    { PLAYERNAME, new PlayerDataObject(visibility: PlayerDataObject.VisibilityOptions.Public, playerName) }
                }
            );

            var Data = new Dictionary<string, DataObject>()
            {
                { RELAYJOINCODE, new DataObject(visibility: DataObject.VisibilityOptions.Public,value: relayJoinCode) },

                { LOBBYNAME, new DataObject(visibility: DataObject.VisibilityOptions.Public, value:lobbyName) }
            };

            var options = new CreateLobbyOptions()
            {
                // The Accessability modifier
                IsPrivate = isPrivate,

                // This is the Player Info of the creator
                Player = player,

                // Key value pairs for small data exchange
                Data = Data,
            };

            var createdLobby = await LobbyService.Instance.CreateLobbyAsync(

                lobbyName: lobbyName,
                maxPlayers: maxPlayers,
                options: options
            );

            Debug.Log("Lobby code : " + createdLobby.LobbyCode);

            return currentLobby = createdLobby;
        }
        catch(Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }

    public static async Task<Lobby> JoinLobbyWithIdAsync(string lobbyId)
    {
        try
        {
            var joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
            return currentLobby = joinedLobby;
        }
        catch(Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }

    public static async Task<Lobby> JoinLobbyWithCodeAsync(string lobbyCode)
    {
        try
        {
            return currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
        }
        catch(Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }

    public static async Task<Lobby> QuickJoinLobbyAsync()
    {
        try
        {
            var joinedLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
            return currentLobby = joinedLobby;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }

    public static async Task<List<Lobby>> QuerryLobbiesAsync(int count = 10)
    {
        try
        {
            QueryResponse lobbies = await LobbyService.Instance.QueryLobbiesAsync(

                // the options
                options: new QueryLobbiesOptions()
                {
                    // Number of lobbies to fetch ( max )
                    Count = count,

                    // the slots must be more than 0
                    Filters = new List<QueryFilter>()
                    {
                        new (
                            field: QueryFilter.FieldOptions.AvailableSlots,
                            op: QueryFilter.OpOptions.GT,
                            value: "0"
                         ),
                    },

                    // in descending order
                    Order = new List<QueryOrder>()
                    {
                        new (
                            asc: false,
                            field: QueryOrder.FieldOptions.AvailableSlots
                        ),
                    }
                }
            );

            return lobbies.Results;
        }
        catch(Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }

    public static async Task TryDeleteLobbyAsync()
    {
        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
            currentLobby = null;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public static Lobby ActiveLobby { get => currentLobby; }
}
