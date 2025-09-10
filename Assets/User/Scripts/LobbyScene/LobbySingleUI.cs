using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class LobbySingleUI : MonoBehaviour
{
    public TextMeshProUGUI RoomName;
    public TextMeshProUGUI HostName;
    public TextMeshProUGUI SlotsAvailable;

    [HideInInspector]
    public string RelayJoinCode;

    [HideInInspector]
    public string LobbyJoinId;

    public void OnClick(Action action)
    {
        GetComponent<Button>().onClick.AddListener(() => { action(); });
    }
}
