using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timeRemainingText;
    [SerializeField] private Image[] remainingTimeBars;
    [SerializeField] private KillerSingleUI killSingleUIPrefab;
    [SerializeField] private TextMeshProUGUI headingText;

    private readonly List<KillerSingleUI> killerSingleUIsList = new();
    private float currentClockTimer = 0f;
    private float maxClockTimer = 0f;

    public static ScoreUI Instance {  get; private set; }
    private void Awake()
    {
        Instance = this;
        killSingleUIPrefab.gameObject.SetActive(false);
    }
    private void Start()
    {
        PlayerShootable.OnAnyPlayerDead += PlayerShootable_OnAnyPlayerDead;
        PlayerShootable.OnAnyPlayerSpawned += PlayerShootable_OnAnyPlayerSpawned;

        maxClockTimer = GameProperties.MatchTimeDuration * 60f;
        currentClockTimer = maxClockTimer;

        headingText.text = "Players | Wins at " + GameProperties.WinAtKills;
    }

    private void PlayerShootable_OnAnyPlayerSpawned(object sender, System.EventArgs e)
    {
        var playerId = ((PlayerShootable)sender).OwnerClientId;

        foreach (var player in killerSingleUIsList)
        {
            if (player.PlayerId == playerId)
            {
                // this is the one that just got spawned.
                player.HideDeadVisual();
            }
        }
    }

    private void PlayerShootable_OnAnyPlayerDead(object sender, PlayerShootable.OnAnyPlayerDeadEventArgs e)
    {
        var playerId = ((PlayerShootable)sender).OwnerClientId;
        foreach (var player in killerSingleUIsList)
        {
            if (player.PlayerId == playerId)
            {
                // this is the one that just died.
                player.ShowDeadVisual();
            }
        }
    }

    public void AddPlayer(PlayerGamePlayData playerGamePlayData)
    {
        var killerSingleUI = Instantiate(killSingleUIPrefab, killSingleUIPrefab.transform.parent); // on the same parent.
        killerSingleUI.PlayerId = playerGamePlayData.PlayerId;
        var playerData = GameMultiPlayer.Instance.GetPlayerDataFor(playerGamePlayData.PlayerId);
        killerSingleUI.SetName(playerData.Name.ToString());
        killerSingleUI.SetKIllsCount(playerGamePlayData.KillsCount,showKillsText: true);
        killerSingleUI.gameObject.SetActive(true);
        killerSingleUIsList.Add(killerSingleUI);
    }

    public void RemovePlayer(PlayerGamePlayData playerGamePlayData)
    {
        var killer = killerSingleUIsList.Find(kl => kl.PlayerId == playerGamePlayData.PlayerId);
        if(killer != null)
        {
            killerSingleUIsList.Remove(killer);
            Destroy(killer.gameObject);
        }
    }

    public void UpdateKIllersVisualsData()
    {
        foreach(var playerData in GameManager.Instance.GetPlayerDataList())
        {
            var killerSingleUI = killerSingleUIsList.Find(kl => kl.PlayerId == playerData.PlayerId);
            killerSingleUI.SetKIllsCount(playerData.KillsCount, showKillsText: true);
        }
    }
    public void SetClockTimer(float clockTimer)
    {
        currentClockTimer = clockTimer;
        maxClockTimer = GameProperties.MatchTimeDuration * 60;
    }
    private void Update()
    {
        if (maxClockTimer <= 0f) return;

        currentClockTimer -= Time.deltaTime;
        foreach(var bar in remainingTimeBars)
        {
            bar.fillAmount = currentClockTimer / maxClockTimer;
        }

        int minutes = (int)currentClockTimer / 60;
        int seconds = (int)currentClockTimer % 60;

        timeRemainingText.text = $"{minutes:D2}:{seconds:D2}";
    }

    private void OnDestroy()
    {
        PlayerShootable.OnAnyPlayerDead -= PlayerShootable_OnAnyPlayerDead;
        PlayerShootable.OnAnyPlayerSpawned -= PlayerShootable_OnAnyPlayerSpawned;
    }
}
