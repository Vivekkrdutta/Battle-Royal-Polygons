using System;
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class Status : MonoBehaviour
{
    [SerializeField] private BarUI healthBar;
    [SerializeField] private Transform playerDisconnectedTransform; // must contain a text as 0th child
    [SerializeField] private Transform selfDisconnectedTransform;
    [SerializeField] private Button quitButtonOnSelfDisconnected;
    [SerializeField] private CanvasGroup killStreakCanvasGroup;
    [SerializeField] private CanvasGroup deadCanvasGroup;

    [SerializeField] private CanvasGroup statusPanelTransform;
    [SerializeField] private KillerSingleUI killerSingleUIPrefab;
    [SerializeField] private Button hideStatusPanelButton;
    [SerializeField] private float hideTimerMax = 2f;

    [Header("Audio clips")]
    [SerializeField] private AudioClip killAudioClip;
    [SerializeField] private AudioClip deadAudioClip;
    [SerializeField] private AudioClip gameEndedAudioClip;

    [Header("Win visuals")]
    [SerializeField] private Transform gameFinishedUI;
    [SerializeField] private TextMeshProUGUI winnerName;

    public static Status Instance;
    private float hideTimer = 0f;
    private bool isShowingDeadStreak = false;
    private bool isShowingKillStreak = false;


    private void Awake()
    {
        Instance = this;

        killStreakCanvasGroup.alpha = 0f;
        deadCanvasGroup.alpha = 0f;
        
        killerSingleUIPrefab.gameObject.SetActive(false);

        statusPanelTransform.gameObject.SetActive(false);

        selfDisconnectedTransform.gameObject.SetActive(false);

        playerDisconnectedTransform.gameObject.SetActive(false);

        hideStatusPanelButton.onClick.AddListener(() =>
        {
            statusPanelTransform.gameObject.SetActive(false);
        });

        quitButtonOnSelfDisconnected.onClick.AddListener(() => 
        {
            try
            {
                NetworkManager.Singleton.Shutdown();
            }
            catch(Exception ex)
            {
                Debug.LogException(ex);
            }
            Loader.LoadScene(Loader.Scene.LobbyScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        });

        GameManager.OnMatchEnded += GameManager_OnMatchEnded;

        gameFinishedUI.gameObject.SetActive(false);

        PlayerShootable.OnAnyPlayerSpawned += PlayerShootable_OnAnyPlayerSpawned;

        GameMultiPlayer.Instance.OnPlayerDisconnected += GameMultiplayer_OnPlayerDisconnected;

        GameMultiPlayer.Instance.OnSelfDisconnected += GameMultiplayer_OnSelfDisconnected;
    }

    private void GameMultiplayer_OnSelfDisconnected(object sender, string e)
    {
        selfDisconnectedTransform.gameObject.SetActive(true);
    }

    private void GameMultiplayer_OnPlayerDisconnected(object sender, ulong e)
    {
        IEnumerator GlimpseDisconnectedTransform()
        {
            playerDisconnectedTransform.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(3f);
            playerDisconnectedTransform.gameObject.SetActive(false);
        }

        StartCoroutine(GlimpseDisconnectedTransform());
    }

    private void PlayerShootable_OnAnyPlayerSpawned(object sender, System.EventArgs e)
    {
        if (NetworkManager.Singleton.LocalClientId != (sender as PlayerShootable).OwnerClientId) return;

        statusPanelTransform.GetComponent<CanvasGroup>().alpha = 0;
        statusPanelTransform.gameObject.SetActive(false);
    }

    private void GameManager_OnMatchEnded(object sender, GameManager.OnMatchEndedEventArgs e)
    {
        GameManager.OnMatchEnded -= GameManager_OnMatchEnded;

        Debug.LogWarning("The match has finally ended, with winner : " + e.WinnerID);
        AudioSource.PlayClipAtPoint(gameEndedAudioClip, Camera.main.transform.position, 1f * GameProperties.VolumeScale);

        ShowStatusPanel();

        gameFinishedUI.gameObject.SetActive(true);
        
        foreach(var playerData in GameMultiPlayer.Instance.GetPlayerDataList())
        {
            if(playerData.PlayerID == e.WinnerID)
            {
                // this is the winner
                winnerName.text = playerData.Name.ToString();
                break;
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void SetHealth(float val)
    {
        healthBar.ChangeValue(val);
    }

    public void ShowKillStreak()
    {
        isShowingKillStreak = true;
        killStreakCanvasGroup.alpha = 1f;
        
        AudioSource.PlayClipAtPoint(killAudioClip, Camera.main.transform.position,1f * GameProperties.VolumeScale);
    }

    public void ShowDeadStreak()
    {
        isShowingDeadStreak = true;
        deadCanvasGroup.alpha = 1f;

        AudioSource.PlayClipAtPoint(deadAudioClip, Camera.main.transform.position, 1f * GameProperties.VolumeScale);
    }

    private void Update()
    {
        if (isShowingDeadStreak)
        {
            hideTimer += Time.deltaTime;
            if(hideTimer > hideTimerMax / 2f)
            {
                hideTimer = 0f;
                isShowingDeadStreak = false;
                deadCanvasGroup.alpha = 0f;

                ShowStatusPanel();
            }
        }

        if (isShowingKillStreak)
        {
            hideTimer += Time.deltaTime;
            if(hideTimer > hideTimerMax)
            {
                hideTimer = 0f;
                isShowingKillStreak = false;
                killStreakCanvasGroup.alpha = 0f;
            }
        }
    }

    private void ShowStatusPanel()
    {
        // also setup the players.
        foreach (Transform child in killerSingleUIPrefab.transform.parent)
        {
            if (child != killerSingleUIPrefab.transform)
                Destroy(child.gameObject);
        }

        foreach (var player in GameManager.Instance.GetPlayerDataList())
        {
            var killer = Instantiate(killerSingleUIPrefab, killerSingleUIPrefab.transform.parent);
            killer.SetName(GameMultiPlayer.Instance.GetPlayerDataFor(player.PlayerId).Name.ToString());
            killer.SetKIllsCount(player.KillsCount);
            killer.SetDeathsCount(player.DeathsCount);

            killer.gameObject.SetActive(true);
        }

        // show the status panel info
        statusPanelTransform.gameObject.SetActive(true);
        statusPanelTransform.GetComponent<CanvasGroup>().alpha = 1f;
    }

    private void OnDestroy()
    {
        GameManager.OnMatchEnded -= GameManager_OnMatchEnded;
        PlayerShootable.OnAnyPlayerSpawned -= PlayerShootable_OnAnyPlayerSpawned;
        GameMultiPlayer.Instance.OnPlayerDisconnected -= GameMultiplayer_OnPlayerDisconnected;
        GameMultiPlayer.Instance.OnSelfDisconnected -= GameMultiplayer_OnSelfDisconnected;
    }
}
