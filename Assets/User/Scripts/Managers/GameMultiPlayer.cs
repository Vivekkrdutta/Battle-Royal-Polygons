
using CharacterSelectScene;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class GameMultiPlayer : NetworkBehaviour
{
    [Tooltip("The actual player prefabs")]
    [SerializeField] private PlayersListSO PlayersListSO;
    [SerializeField] private NetworkObjectsPrefabSO NetworkObjectsPrefabSO;
    [SerializeField] private ParticleSystem[] shotGunBlastPrefabs;

    [Tooltip("At the 0th index, normal grenade, at 1st index, smoke grenade , at 2nd index, poison gas grenade.")]
    [SerializeField] private Grenade[] grenadePrefabs;
    [SerializeField] private GunsListSO GunsListSO;
    [SerializeField] private BulletsPrefabsSO BulletsListSO;
    [SerializeField] private PlayersListSO PlayerListSO;
    [SerializeField] private float gameStartTimer = 3f;

    private int maxNumberOfPlayers;
    public int MaxPlayers {  get => maxNumberOfPlayers; set => maxNumberOfPlayers = value; }
    
    /// <summary>
    /// Runs on all clients, at the very moment the client is disconnected.
    /// </summary>
    public event EventHandler<ulong> OnPlayerDisconnected;


    public event EventHandler<string> OnSelfDisconnected;

    /// <summary>
    /// Triggerred for all clients just before loading a scene.
    /// </summary>
    public event EventHandler<Loader.Scene> OnLoadingScene;

    private readonly NetworkList<PlayerData> playerDatalist = new (new List<PlayerData>(),
        readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
    public static GameMultiPlayer Instance {  get; private set; }
    private void Awake()
    {
        // Make singleton.
        if( Instance == null )
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private void Start()
    {
        Loader.OnSceneLoaded += Loader_OnSceneLoaded;
    }

    private void Loader_OnSceneLoaded(object sender, Loader.Scene e)
    {
        if(e == Loader.Scene.LobbyScene)
        {
            ResetSubscriptions();
        }
    }

    public async Task<bool> StartClientWithRelay(string relayJoinCode,RelayManager.ConnectionType connectionType)
    {
        try
        {
            if (await RelayManager.StartClientWithRelay(relayJoinCode, connectionType.ToString()))
            {
                Debug.Log("Connected to host!");

                NetworkManager.OnClientDisconnectCallback += NetworkManager_OnSelfDisconnected;
                
                return true;
            }

            throw new Exception("Connection was not established");

        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
        }

        return false;
    }

    public async Task<string> StartHostWithRelay(int maxConnection,RelayManager.ConnectionType connectionType)
    {
        try
        {
            NetworkManager.Singleton.ConnectionApprovalCallback += NetworkManager_ConnectionApproval;

            NetworkManager.OnConnectionEvent += NetworkManager_OnConnectionEvent_Server;

            var joincode = await RelayManager.StartHostWithRelay(maxConnections: maxConnection, connectionType.ToString());

            MaxPlayers = maxConnection;

            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += SceneManager_OnLoadEventCompleted;

            Loader.NetworkLoadScene(Loader.Scene.CharacterSelectScene, LoadSceneMode.Single);

            return joincode;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        return null;
    }

    private void SceneManager_OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log("Loaded scene : " + sceneName);
     
        if(sceneName == Loader.Scene.CharacterSelectScene.ToString())
        {
            if(!GetComponent<AudioSource>().isPlaying) GetComponent<AudioSource>().Play();

            // reset the ready queue.
            for(int i = 0; i< playerDatalist.Count; i++)
            {
                var data = playerDatalist[i];
                data.Ready = false;
                playerDatalist[i] = data;
            }

            static IEnumerator SetRoomInfo()
            {
                yield return new WaitForSecondsRealtime(1f);

                if(LobbyManager.ActiveLobby == null)
                {
                    SelectionUI.Instance.RoomInfo("Room closed", "_No more joins");
                    yield break;
                }

                SelectionUI.Instance.RoomInfo(LobbyManager.ActiveLobby.Name, LobbyManager.ActiveLobby.LobbyCode);
            }

            StartCoroutine(SetRoomInfo());
            return;
        }
        
        if(sceneName == Loader.Scene.GameScene.ToString())
        {
            // we are in the game scene, start instantiating the players
            StartCoroutine(InstantiateAllPlayers());

            GameManager.OnMatchEnded += GameManager_OnMatchEnded;

            _= LobbyManager.TryDeleteLobbyAsync();

            GetComponent<AudioSource>().Stop();
        }
    }

    private void GameManager_OnMatchEnded(object sender, GameManager.OnMatchEndedEventArgs e)
    {
        GameManager.OnMatchEnded -= GameManager_OnMatchEnded;

        IEnumerator LoadBackCharactersScene()
        {
            yield return new WaitForSecondsRealtime(5f);

            Loader.NetworkLoadScene(Loader.Scene.CharacterSelectScene, LoadSceneMode.Single);

            // destroy the existing players first

            foreach (var player in Player.PlayersList)
            {
                if (player.TryGetComponent(out NetworkObject netObject))
                {
                    netObject.Despawn(destroy: true);
                }
            }
        }
        StartCoroutine(LoadBackCharactersScene());
    }

    private void NetworkManager_ConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (!IsServer) return; // double safety.

        var activeScene = SceneManager.GetActiveScene().name;
        var count = playerDatalist.Count;

        // -----------APROVED SITUATION--------------------
        if (activeScene == Loader.Scene.CharacterSelectScene.ToString() && count < MaxPlayers)
        {
            response.Approved = true;
            response.CreatePlayerObject = false;
            Debug.Log("Approved : " + request.ClientNetworkId);
            return;
        }

        // -----------NOT APPROVED------------------------
        Debug.Log("Not approved");
        response.Approved = false;
        response.Reason = "The game has either started or is Full!";
    }
    private void NetworkManager_OnConnectionEvent_Server(NetworkManager _, ConnectionEventData connectionData)
    {
        switch (connectionData.EventType)
        {
            case ConnectionEvent.ClientConnected:

                // joining will be done on the character select scene only!
                HandleClientConnected(clientID: connectionData.ClientId);
                break;

            case ConnectionEvent.ClientDisconnected:

                // inform every one of the disconnection event
                playerDatalist.Remove(GetPlayerDataFor(connectionData.ClientId));
                HandleClientDisconnectedClientRpc(disconnectedClient: connectionData.ClientId);
                break;
        }
    }

    public void ResetSubscriptions()
    {
        // Unsubscribing does not throw any errors. Chill.

        // For the server side
        NetworkManager.Singleton.ConnectionApprovalCallback -= NetworkManager_ConnectionApproval;

        NetworkManager.OnConnectionEvent -= NetworkManager_OnConnectionEvent_Server;

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= SceneManager_OnLoadEventCompleted;

        if (IsServer)
        {
            playerDatalist.Clear();
        }

        // For the client side
        NetworkManager.OnClientDisconnectCallback -= NetworkManager_OnSelfDisconnected;
    }

    private void NetworkManager_OnSelfDisconnected(ulong selfId)
    {
        NetworkManager.OnClientDisconnectCallback -= NetworkManager_OnSelfDisconnected;

        OnSelfDisconnected?.Invoke(this, NetworkManager.Singleton.DisconnectReason);
    }

    private void HandleClientConnected(ulong clientID)
    {
        if (!IsServer) return; // only for the server
        var playerName = "Player " + playerDatalist.Count.ToString();

        PlayerData playerData = new()
        {
            Ready = false,
            Name = playerName,
            PlayerID = clientID,
            PrefabIndex = GetNextFreePrefabIndex(),
            PositionIndex = GetNextFreePositionIndex(),
            FireArmIndex = GetRandomGunIndex(),
        };

        playerDatalist.Add(playerData);
    }

    [ClientRpc(RequireOwnership =  true)]
    private void HandleClientDisconnectedClientRpc(ulong disconnectedClient)
    {
        OnPlayerDisconnected?.Invoke(this, disconnectedClient);
    }

    public void ShootABulletTrail(Vector3 instantiatePosition,Vector3 target,int trailPrefabIndex,bool networkInform = false)
    {
        var trail = Instantiate(BulletsListSO.BulletTrailPrefabsList[trailPrefabIndex],instantiatePosition,Quaternion.identity);
        var dir = (target - trail.transform.position).normalized;
        trail.transform.forward = dir;
        trail.SetTarget(target);

        if (networkInform) ShootTrailServerRpc(instantiatePosition,target,trailPrefabIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ShootTrailServerRpc(Vector3 instantiatePosition, Vector3 target,int trailPrefabIndex, ServerRpcParams serverRpcParams = default)
    {
        var clients = GetClientIdsListExcept(serverRpcParams.Receive.SenderClientId);
        ShootTrailClientRpc(instantiatePosition,target,trailPrefabIndex, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = clients
            }
        });
    }

    [ClientRpc(RequireOwnership = false)]
    private void ShootTrailClientRpc(Vector3 instantiatePosition, Vector3 target,int bulletsTrailIndex,ClientRpcParams clientRpcParams)
    {
        var trail = Instantiate(BulletsListSO.BulletTrailPrefabsList[bulletsTrailIndex],instantiatePosition,Quaternion.identity);
        var dir = (target - trail.transform.position).normalized;
        trail.transform.forward = dir;

        trail.SetTarget(target);
    }

    public void InstantiateShotgunBlast(Vector3 position,ParticleSystem blast)
    {
        int index = Array.IndexOf(shotGunBlastPrefabs, blast);
        Instantiate(blast, position, Quaternion.identity);
        SpawnShotgunblastServerRpc(position, index);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnShotgunblastServerRpc(Vector3 position, int index,ServerRpcParams serverRpcParams = default)
    {
        var sender = serverRpcParams.Receive.SenderClientId;
        var clients = new List<ulong>(NetworkManager.ConnectedClientsIds);
        clients.Remove(sender);

        SpawnShotgunblastClientRpc(position, index, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = clients
            }
        });
    }
    [ClientRpc(RequireOwnership = false)]
    private void SpawnShotgunblastClientRpc(Vector3 position, int index, ClientRpcParams clientRpcParams)
    {
        Instantiate(shotGunBlastPrefabs[index], position, Quaternion.identity);
    }

    /// <summary>
    /// Spawns the player over the network.
    /// </summary>
    /// <param name="playerData"></param>
    public void InstantiatePlayer(PlayerData playerData)
    {
        if(!IsServer) return;

        var player = Instantiate(PlayersListSO.PlayersList[playerData.PrefabIndex].gameObject);

        Debug.Log("Instantiating prefab index of type : " + playerData.PrefabIndex + " and name : " + player.name);

        var playerNetwork = player.GetComponent<NetworkObject>();

        Transform selectedTransform;

        var tents = Tent.GetTentsList().Count;

        if(playerData.PositionIndex >= tents)
        {
            selectedTransform = House.GetHousesList()[playerData.PositionIndex].GetInstantiaionsTransforms().First();
        }
        else
        {
            selectedTransform = Tent.GetTentsList()[playerData.PositionIndex].GetInstantiaionsTransforms().First().transform;
        }

        playerNetwork.transform.SetPositionAndRotation(selectedTransform.position, Quaternion.identity);
        
        playerNetwork.SpawnAsPlayerObject(playerData.PlayerID);

        playerNetwork.transform.SetPositionAndRotation(selectedTransform.position, Quaternion.identity);
    }

    /// <summary>
    /// The method will immediately instantiate all the players that are joined.
    /// </summary>
    /// <returns></returns>
    private IEnumerator InstantiateAllPlayers()
    {
        yield return new WaitForSecondsRealtime(gameStartTimer);
        if (IsServer)
        {
            foreach(var playerData in playerDatalist)
            {
                InstantiatePlayer(playerData);
            }
        }
    }
    public void NetworkDestroy(Transform transform)
    {
        if(transform.TryGetComponent(out NetworkObject networkObject))
        {
            DestroyServerRpc(networkObject);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void DestroyServerRpc(NetworkObjectReference networkObjectReference)
    {
        networkObjectReference.TryGet(out NetworkObject networkObject);
        if(networkObject != null)
        {
            networkObject.Despawn(true);
        }
    }

    /// <summary>
    /// The GameObject must have NetworkObject Component and It must be in the NetworkObjectsPrefabSO list.
    /// </summary>
    /// <param name="go"></param>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    public void InstantiateGameObject(GameObject go,Vector3 position, Quaternion rotation)
    {
        if(go.TryGetComponent(out NetworkObject networkObject))
        {
            int index = Array.FindIndex(NetworkObjectsPrefabSO.List,obj => obj == networkObject);
            if(index != -1)
            {
                InstantiateGameObjectServerRpc(index,position,rotation);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void InstantiateGameObjectServerRpc(int index,Vector3 position, Quaternion rotation)
    {
        var netobj = NetworkObjectsPrefabSO.List[index];

        var newNetObj = Instantiate(netobj,position,rotation);

        newNetObj.Spawn();
    }
    public List<ulong> GetClientIdsListExcept(ulong id)
    {
        var clients = new List<ulong>(NetworkManager.ConnectedClientsIds);
        clients.Remove(id);
        return clients;
    }

    /// <summary>
    /// Normal = 0, smoke = 1, poison = 2
    /// </summary>
    /// <param name="targetPoint"></param>
    /// <param name="instantiatePosition"></param>
    /// <param name="grenadeIndex"></param>
    public void LaunchGrenade(Vector3 targetPoint, Vector3 instantiatePosition, int grenadeIndex = 0)
    {
        LaunchGrenadeServerRpc(targetPoint,instantiatePosition,grenadeIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void LaunchGrenadeServerRpc(Vector3 targetPoint,Vector3 instantiatePosition,int grenadeIndex = 0,ServerRpcParams server = default)
    {
        // Spawn authoritative grenade
        var serverGrenade = Instantiate(grenadePrefabs[grenadeIndex], instantiatePosition, Quaternion.identity);
        serverGrenade.LauncherId = server.Receive.SenderClientId;
        var netObj = serverGrenade.GetComponent<NetworkObject>();
        netObj.Spawn(true);


        var rb = serverGrenade.GetComponent<Rigidbody>();
        float launchSpeed = rb.GetComponent<Grenade>().GetLaunchSpeed();
        Vector3 dir = (targetPoint - instantiatePosition).normalized;
        rb.AddForce(launchSpeed * rb.mass * dir, ForceMode.Impulse);
    }
    public NetworkList<PlayerData> GetPlayerDataList() => playerDatalist;
    public override void OnDestroy()
    {
        base.OnDestroy();
        Loader.OnSceneLoaded -= Loader_OnSceneLoaded;
    }
    public void UpdatePlayerData(PlayerData playerData)
    {
        UpdatePlayerDataServerRpc(playerData);
    }
    [ServerRpc(RequireOwnership = false)]
    private void UpdatePlayerDataServerRpc(PlayerData playerData)
    {
        // fetch the data
        for(int i = 0; i < playerDatalist.Count; i++)
        {
            if (playerDatalist[i].PlayerID == playerData.PlayerID)
            {
                playerDatalist[i] = playerData;
                Debug.Log("Updated userdata successfully");
                break;
            }
        }
    }

    public void SetPlayerReady(PlayerData playerData, bool ready)
    {
        SetPlayerReadyServerRpc(playerData.PlayerID, ready);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerReadyServerRpc(ulong playerId, bool ready)
    {
        var index = -1;
        foreach(var data in playerDatalist)
        {
            if(data.PlayerID == playerId)
            {
                index = playerDatalist.IndexOf(data);
                break;
            }
        }

        if (index == -1) return;

        var playerData = playerDatalist[index];
        playerData.Ready = ready;
        playerDatalist[index] = playerData;
        Debug.Log("Changed ready for " + playerData.Name);

        TryLoadGameScene();
    }

    // If all the players are ready, then the gamescene is loaded.
    private void TryLoadGameScene()
    {
        if(!IsServer) return;

        var count = 0;
        foreach (var data in playerDatalist)
        {
            if (data.Ready) count++;
        }

        if (count == playerDatalist.Count)
        {
            // Actually load the scene.
            // make it a coroutine.
            static IEnumerator LoadGame()
            {
                yield return new WaitForSecondsRealtime(1f);
                Loader.NetworkLoadScene(Loader.Scene.GameScene, LoadSceneMode.Single);
            }

            OnLoadingSceneClientRpc(Loader.Scene.GameScene);

            StartCoroutine(LoadGame());
        }
    }
    [ClientRpc(RequireOwnership = true)]
    private void OnLoadingSceneClientRpc(Loader.Scene scene)
    {
        GetComponent<AudioSource>().Stop();
        OnLoadingScene?.Invoke(this, scene);
    }

    public PlayerData GetSelfPlayerData()
    {
        var id = NetworkManager.LocalClientId;
        foreach (var playerData in playerDatalist)
        {
            if (id == playerData.PlayerID) return playerData;
        }

        return default;
    }

    public PlayerData GetPlayerDataFor(ulong playerID)
    {
        foreach(var playerData in playerDatalist)
        {
            if(playerData.PlayerID == playerID) return playerData;
        }
        return default;
    }
    private int GetNextFreePrefabIndex()
    {
        for (int i = 0; i < PlayersListSO.PlayersList.Count; i++)
        {
            var available = true;
            foreach (var data in playerDatalist)
            {
                if (data.PrefabIndex == i) available = false;
            }
            if (available) return i;
        }

        return -1;
    }
    private int GetNextFreePositionIndex()
    {
        return playerDatalist.Count;
    }
    private int GetRandomGunIndex()
    {
        return Random.Range(0, GunsListSO.FireArmsList.Count);
    }
}

