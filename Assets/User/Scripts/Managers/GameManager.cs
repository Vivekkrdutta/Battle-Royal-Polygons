using AI;
using System;
using UnityEngine;
using System.Linq;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Only server will handle the logics and also Score managing works
/// </summary>
public class GameManager : NetworkBehaviour
{
    [SerializeField] private List<NetworkSpawnable<InteractableObject>> collectables;
    [SerializeField] private List<NetworkSpawnable<SmartAgentBehaviour>> agents;
    [SerializeField] private int[] maxTotalAgentsLevelWise;
    [SerializeField] private float agentDestroyWaitTime = 3f;
    [SerializeField] private float playerDestroyWaitTime = 4f;

    [SerializeField] private bool spawnCollectibles = true;
    [SerializeField] private bool spawnAgents = true;
    [SerializeField] private LayerMask playersLayerMask;
    [SerializeField] public Volume globalVolume;

    public static event EventHandler<OnMatchEndedEventArgs> OnMatchEnded;
    public class OnMatchEndedEventArgs : EventArgs
    {
        public ulong WinnerID;
    }
    private float durationLeft;
    private readonly float updateAllClientsOfDurationLeftPeriod = 10f;
    private float updateClientTimer = 0f;
    private int agentsTotalCount = 0;
    public static GameManager Instance { get; private set; }

    private readonly NetworkList<PlayerGamePlayData> playerDataList = new(
        new List<PlayerGamePlayData>(),readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
    public enum State
    {
        WaitingToStart,
        Playing,
        Ended,
    }

    /// <summary>
    /// Custom class to handle all types of collectibel objects' routinely instantiation
    /// </summary>
    [Serializable]
    public class NetworkSpawnable<T> where T : NetworkBehaviour
    {
        public string Name;
        public T SpawnPrefab;
        public float FixedWaitTime = 30f;

        [Header("For Collectibles")]
        public List<Transform> SpawnTransforms;

        [Range(0f, 60f)] 
        public float WaitTimeVariance = 5f;

        [Range(0f,20f)]
        public float InitialWaitTime = 5f;

        [HideInInspector]
        public Dictionary<Transform, T> Transform_Spawns_Dectionary = new();
    }
    private void Awake()
    {
        playerDataList.OnListChanged += PlayerDataList_OnListChanged;
        OnMatchEnded += GameManager_OnMatchEnded;
        Instance = this;
    }

    private void GameManager_OnMatchEnded(object sender, OnMatchEndedEventArgs e)
    {
        OnMatchEnded -= GameManager_OnMatchEnded;

        spawnAgents = false;
        spawnCollectibles = false;
    }

    private void PlayerDataList_OnListChanged(NetworkListEvent<PlayerGamePlayData> changeEvent)
    {
        switch (changeEvent.Type)
        {
            case NetworkListEvent<PlayerGamePlayData>.EventType.Add:

                ScoreUI.Instance.AddPlayer(changeEvent.Value);
                break;

            case NetworkListEvent<PlayerGamePlayData>.EventType.Value:

                // for all the clients
                ScoreUI.Instance.UpdateKIllersVisualsData();

                // for the server
                if (!IsServer) return;

                IEnumerator DeclarePlayerHasWon(PlayerGamePlayData data)
                {
                    yield return new WaitForSecondsRealtime(0.2f);
                    // declare the winner and end the match
                    DeclarePlayerHasWonClientRpc(data.PlayerId);
                }

                foreach(var data in playerDataList)
                {
                    if(data.KillsCount >= GameProperties.WinAtKills)
                    {
                        StartCoroutine(DeclarePlayerHasWon(data));
                    }
                }
                break;

            case NetworkListEvent<PlayerGamePlayData>.EventType.RemoveAt:

                ScoreUI.Instance.RemovePlayer(changeEvent.Value);
                break;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        spawnAgents = GameProperties.AllowNPCsIn1v1;

        foreach(var collectible in collectables) StartCoroutine(RoutinelySpawnSpawnable(collectible));

        // Instantiate only in case of 1v1 for now
        if(GameProperties.GameMode == GameProperties.Mode.OneVSOne)
        {
            foreach(var agent in agents)
            {
                StartCoroutine(RoutinelySpawnAgents(agent, true));
            }
        }

        PlayerShootable.OnAnyPlayerDead += PlayerShootable_OnAnyPlayerDead;
        SmartAgentShootable.OnAnyAgentDead += SmartAgentShootable_OnAnyAgentDead;
        GameMultiPlayer.Instance.OnPlayerDisconnected += GameMultiPlayer_OnPlayerDisconnected;

        durationLeft = GameProperties.MatchTimeDuration * 60f;

        StartCoroutine(AddPlayersGameData());
    }

    private void GameMultiPlayer_OnPlayerDisconnected(object sender, ulong e)
    {
        if(!IsServer) return;

        // remove the player data
        int i = 0;
        foreach(var playrData in playerDataList)
        {
            if(playrData.PlayerId == e)
            {
                break;
            }
            i++;
        }

        // will trigger the event onvaluechanged : Remove
        playerDataList.RemoveAt(i);
    }

    [ClientRpc(RequireOwnership = true)]
    private void SetMaxKillsCountToWinClientRpc(int maxKills)
    {
        GameProperties.WinAtKills = maxKills;
    }

    private IEnumerator AddPlayersGameData()
    {
        float waitTime = 1.5f;
        yield return new WaitForSeconds(waitTime);
        SetMaxKillsCountToWinClientRpc(GameProperties.WinAtKills);

        // insert into the playergameplaydatalist
        foreach (var playerData in GameMultiPlayer.Instance.GetPlayerDataList())
        {
            playerDataList.Add(new PlayerGamePlayData
            {
                PlayerId = playerData.PlayerID,
                DeathsCount = 0,
                KillsCount = 0,
            });
        }
    }

    public void RegisterDeath(ulong killer, ulong dead)
    {
        if (!IsServer) return;

        Debug.LogWarning("Registered death for " + (dead != ulong.MaxValue ? dead : "[NPC]") + (killer != ulong.MaxValue ? "by " + killer : "[Unknown]"));
        
        int killeriNdex = -1;
        int deadIndex = -1;

        for(int i = 0; i < playerDataList.Count; i++)
        {
            if (playerDataList[i].PlayerId == killer)
            {
                killeriNdex = i;
            }
            if(playerDataList[i].PlayerId == dead)
            {
                deadIndex = i;
            }
        }

        var validKiller = false;
        var validDeath = false;

        if(killeriNdex != -1)
        {
            validKiller = true;
            // gotten
            var data = playerDataList[killeriNdex];
            data.KillsCount++;
            playerDataList[killeriNdex] = data;
        }

        if(deadIndex != -1)
        {
            validDeath = true;
            var data = playerDataList[deadIndex];
            data.DeathsCount++;
            playerDataList[deadIndex] = data;
        }

        if(validDeath || validKiller) UIUpdateOnDeathClientRpc(killer, dead);
    }

    [ClientRpc(RequireOwnership = true)]
    private void UIUpdateOnDeathClientRpc(ulong killerIndex, ulong deadIndex)
    {
        if (NetworkManager.LocalClientId == killerIndex) Status.Instance.ShowKillStreak();
        if(NetworkManager.LocalClientId == deadIndex) Status.Instance.ShowDeadStreak();
    }

    [ClientRpc(RequireOwnership = true)]
    private void DeclarePlayerHasWonClientRpc(ulong winnerId)
    {
        OnMatchEnded?.Invoke(this, new OnMatchEndedEventArgs
        {
            WinnerID = winnerId,
        });
    }

    [ClientRpc(RequireOwnership = true)]
    private void SetDurationLeftClientRpc(float duration)
    {
        ScoreUI.Instance.SetClockTimer(duration);
    }

    private void UpdateDurationTimer()
    {
        durationLeft -= Time.deltaTime;
        if(durationLeft < 0)
        {
            if (IsServer)
            {
                // Determine the winner player, based on who killed the most

                int maxKills = -1;
                ulong winnerId = ulong.MinValue;
                foreach(var data in playerDataList)
                {
                    if(data.KillsCount >= maxKills)
                    {
                        // this is the winner
                        maxKills = data.KillsCount;
                        winnerId = data.PlayerId;
                    }
                }

                if(winnerId != ulong.MinValue)
                {
                    DeclarePlayerHasWonClientRpc(winnerId);
                }
            }
            
        }
    }

    private void Update()
    {
        UpdateDurationTimer();
        updateClientTimer += Time.deltaTime;
        if(updateClientTimer > updateAllClientsOfDurationLeftPeriod)
        {
            updateClientTimer = 0f;
            SetDurationLeftClientRpc(durationLeft);
        }
    }

    private void SmartAgentShootable_OnAnyAgentDead(object sender, SmartAgentShootable.OnAnyAgentDeadEventArgs e)
    {
        if (!IsServer) return;

        var agnet = e.AgentShootable;
        var netobj = agnet.GetComponent<NetworkObject>();

        IEnumerator DestroyAgent()
        {
            float waitTime = agentDestroyWaitTime;
            yield return new WaitForSeconds(waitTime);
            netobj.Despawn(destroy:  true);
        }

        // despawn the netobj
        StartCoroutine(DestroyAgent());

        agentsTotalCount--;
    }

    private void PlayerShootable_OnAnyPlayerDead(object sender, PlayerShootable.OnAnyPlayerDeadEventArgs e)
    {
        if (!IsServer) return;

        // handle anything necessary, on the player's death
        var player = e.Player;
        var netobj = player.GetComponent<NetworkObject>();

        IEnumerator DestroyPlayer()
        {
            float waitTime = playerDestroyWaitTime;
            yield return new WaitForSecondsRealtime(waitTime);

            Debug.Log("Destroying player");
            netobj.Despawn(destroy: true);
        }

        // despawn the player
        StartCoroutine(DestroyPlayer());

        IEnumerator InstantiatePlayerAfter(PlayerData playerData, float delayAmount)
        {
            yield return new WaitForSecondsRealtime(delayAmount);
            GameMultiPlayer.Instance.InstantiatePlayer(playerData);
        }

        // Also get ready to instantiate the player very much soon after
        StartCoroutine(InstantiatePlayerAfter(
            GameMultiPlayer.Instance.GetPlayerDataFor(player.GetComponent<ThirdPersonShooter>().OwnerClientId), 
            playerDestroyWaitTime + 1f));
    }

    private IEnumerator RoutinelySpawnSpawnable<T>(NetworkSpawnable<T> spawnable,bool alsoCheckAnyNearbyPlayer = false) where T : NetworkBehaviour
    {
        while (true)
        {
            float effectiveWaitTime = spawnable.FixedWaitTime + Random.Range(-spawnable.WaitTimeVariance, spawnable.WaitTimeVariance);

            yield return new WaitForSecondsRealtime(effectiveWaitTime);

            if (!spawnAgents) continue;

            // fetch the available transforms considering all conditions
            var availableTransforms = spawnable.SpawnTransforms.Where(t =>(!spawnable.Transform_Spawns_Dectionary.ContainsKey(t) ||  
             spawnable.Transform_Spawns_Dectionary[t] == null ) &&
                !(
                    alsoCheckAnyNearbyPlayer && Physics.CheckSphere(t.position, 5f)
                )
            ).ToList();

            // check validity
            if (availableTransforms.Count == 0 || !spawnCollectibles) 
            {
                Debug.Log("No available spots to instantiate " + spawnable.Name);
                continue;
            }

            int randIndex = Random.Range(0, availableTransforms.Count);

            // instantiate at local level
            var netobj = Instantiate(spawnable.SpawnPrefab, availableTransforms[randIndex].position, availableTransforms[randIndex].rotation);
            
            // spawn
            netobj.GetComponent<NetworkObject>().Spawn(true);

            if (!spawnable.Transform_Spawns_Dectionary.ContainsKey(availableTransforms[randIndex]))
            {
                spawnable.Transform_Spawns_Dectionary.Add(availableTransforms[randIndex], netobj);
                continue;
            }

            spawnable.Transform_Spawns_Dectionary[availableTransforms[randIndex]] = netobj;
        }
    }

    private IEnumerator RoutinelySpawnAgents(NetworkSpawnable<SmartAgentBehaviour> agent,bool count)
    {
        yield return new WaitForSecondsRealtime(agent.InitialWaitTime);
        while (true)
        {
            var waittime = agent.FixedWaitTime + Random.Range(-1 * agent.WaitTimeVariance, agent.WaitTimeVariance);

            yield return new WaitForSecondsRealtime(waittime);
            
            List<Transform> availableTransforms = new ();

            foreach(var house in House.GetHousesList())
            {
                var transforms = house.GetInstantiaionsTransforms();
                foreach (var t in transforms)
                {
                    if (!house.CheckSphere(t, playersLayerMask)) availableTransforms.Add(t);
                }
            }

            if (!spawnAgents || availableTransforms.Count == 0 || (count && 
                 agentsTotalCount >= maxTotalAgentsLevelWise[(int) GameProperties.GameLevel]))
            {
                continue;
            }

            var randind = Random.Range(0,availableTransforms.Count);

            var newAgent = Instantiate(agent.SpawnPrefab, availableTransforms[randind].position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

            Debug.LogWarning("Spawned agent at : " + availableTransforms[randind].parent.parent.name + "'s transform " + availableTransforms[randind].name);

            newAgent.GetComponent<NetworkObject>().Spawn(true);

            agentsTotalCount++;
        }
    }
    public override void OnDestroy()
    {
        base.OnDestroy();
        PlayerShootable.OnAnyPlayerDead -= PlayerShootable_OnAnyPlayerDead;
        SmartAgentShootable.OnAnyAgentDead -= SmartAgentShootable_OnAnyAgentDead;
        GameMultiPlayer.Instance.OnPlayerDisconnected -= GameMultiPlayer_OnPlayerDisconnected;
        OnMatchEnded -= GameManager_OnMatchEnded;
    }
    public NetworkList<PlayerGamePlayData> GetPlayerDataList() => playerDataList;
}
