using CharacterSelectScene;
using System;
using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameStatusUI : NetworkBehaviour
{
    [SerializeField] private Transform statusForServer;
    [SerializeField] private Transform statusForClients;

    [Header("For the server")]
    [SerializeField] private Button[] gameLevelButtons;
    [SerializeField] private TMP_InputField gameDurationInputField;
    [SerializeField] private Button enterDurationButton;
    [SerializeField] private Button npcsActivateToggleButton;
    [SerializeField] private TMP_InputField winAtKillsInputField;
    [SerializeField] private Button enterWinAtKillsButton;

    [Header("For the clients")]
    [SerializeField] private TextMeshProUGUI gameLevelText;
    [SerializeField] private TextMeshProUGUI durationText;
    [SerializeField] private TextMeshProUGUI npcActiveText;
    [SerializeField] private TextMeshProUGUI winAtKillsText;

    private readonly NetworkVariable<StatusData> statusData = new NetworkVariable<StatusData>(
        value: default, readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

    private struct StatusData : INetworkSerializable
    {
        public int Duration;
        public int Level;
        public bool NpcActive;
        public int WinAt;
        public FixedString128Bytes RoomName;
        public FixedString128Bytes JoinCode;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Duration);
            serializer.SerializeValue(ref Level);
            serializer.SerializeValue(ref NpcActive);
            serializer.SerializeValue(ref WinAt);
            serializer.SerializeValue(ref RoomName);
            serializer.SerializeValue(ref JoinCode);
        }
    }

    private void Awake()
    {
        statusData.OnValueChanged += ValueChanged;
    }

    private void ValueChanged(StatusData previousValue, StatusData statusData)
    {
        gameLevelText.text = ((GameProperties.Level)statusData.Level).ToString();
        durationText.text = statusData.Duration.ToString() + "Mins";
        npcActiveText.text = statusData.NpcActive ? "Active" : "Off";
        winAtKillsText.text = statusData.WinAt.ToString() + " Kills";
        SelectionUI.Instance.RoomInfo(statusData.RoomName.ToString(), statusData.JoinCode.ToString());

        // also update the GameProperties
        GameProperties.WinAtKills = statusData.WinAt;
        GameProperties.MatchTimeDuration = statusData.Duration;
    }

    public override void OnNetworkSpawn()
    {
        if(IsClient && !IsServer)
        {
            // a normal client

            IEnumerator UpdateStatusAtLocalLevel()
            {
                yield return new WaitForSecondsRealtime(1f);
                UpdateStatusLocalLevel(statusData.Value);
            }

            StartCoroutine(UpdateStatusAtLocalLevel());
        }

        base.OnNetworkSpawn();

        if (!IsHost)
        {
            statusForServer.gameObject.SetActive(false);
            statusForClients.gameObject.SetActive(true);
            return;
        }

        // this is the server
        statusForClients.gameObject.SetActive(false);
        statusForServer.gameObject.SetActive(true);

        foreach (var button in gameLevelButtons)
        {
            // Setting the game level options
            button.onClick.AddListener(() =>
            {
                var bt = button;
                GameProperties.GameLevel = (GameProperties.Level)Array.IndexOf(gameLevelButtons, button);
                statusData.Value = GetStatusData();
                foreach(var btn in gameLevelButtons)
                {
                    btn.GetComponent<Outline>().enabled = btn == bt;
                }
            });
        }

        enterDurationButton.onClick.AddListener(() =>
        {
            var text = gameDurationInputField.text;

            if (string.IsNullOrEmpty(text)) return;

            if(int.TryParse(text, out var duration))
            {
                GameProperties.MatchTimeDuration = duration;
                statusData.Value = GetStatusData();
                return;
            }

            if(gameDurationInputField.placeholder is TextMeshProUGUI placeHolder)
            {
                placeHolder.text = "Numbers only!";
            }
        });

        enterWinAtKillsButton.onClick.AddListener(() => 
        {
            var text = winAtKillsInputField.text;

            if (string.IsNullOrEmpty(text)) return;

            if (int.TryParse(text, out var kills))
            {
                GameProperties.WinAtKills = kills;
                statusData.Value = GetStatusData();
                return;
            }

            if (winAtKillsInputField.placeholder is TextMeshProUGUI placeHolder)
            {
                placeHolder.text = "Numbers only!";
            }
        });

        npcsActivateToggleButton.onClick.AddListener(() => 
        {
            GameProperties.AllowNPCsIn1v1 = !GameProperties.AllowNPCsIn1v1;
            npcsActivateToggleButton.GetComponent<Image>().color = (GameProperties.AllowNPCsIn1v1 ? Color.green : Color.red);
            npcsActivateToggleButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = GameProperties.AllowNPCsIn1v1 ? "Enabled" : "Disabled";
            statusData.Value = GetStatusData();
        });

        StartCoroutine(SetupStatusData());
    }

    private IEnumerator SetupStatusData()
    {
        yield return new WaitForSecondsRealtime(1f);

        if(IsServer)
        statusData.Value = GetStatusData();
    }

    private StatusData GetStatusData()
    {
        return new StatusData()
        {
            Duration = GameProperties.MatchTimeDuration,
            NpcActive = GameProperties.AllowNPCsIn1v1,
            WinAt = GameProperties.WinAtKills,
            Level = (int)GameProperties.GameLevel,
            RoomName = LobbyManager.ActiveLobby != null ? LobbyManager.ActiveLobby.Name : "",
            JoinCode = LobbyManager.ActiveLobby != null ? LobbyManager.ActiveLobby.LobbyCode : "",
        };
    }

    private void UpdateStatusLocalLevel(StatusData statusData)
    {
        gameLevelText.text = ((GameProperties.Level) statusData.Level).ToString();
        durationText.text = statusData.Duration.ToString() + "Mins";
        npcActiveText.text = statusData.NpcActive ? "Active" : "Off";
        winAtKillsText.text = statusData.WinAt.ToString() + " Kills";
        SelectionUI.Instance.RoomInfo(statusData.RoomName.ToString(), statusData.JoinCode.ToString());

        // also update the GameProperties
        GameProperties.WinAtKills = statusData.WinAt;
        GameProperties.MatchTimeDuration = statusData.Duration;
    }
}
