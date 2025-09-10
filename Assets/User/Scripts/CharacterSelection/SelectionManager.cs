using CharacterSelectScene;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The class starts from Character Select Scene.
/// </summary>
public class SelectionManager : NetworkBehaviour
{
    [SerializeField] private List<CharacterSingle> characterList;
    [SerializeField] private Transform[] showTransforms;
    [SerializeField] private GunsListSO GunsListSO;
    public static SelectionManager Instance {  get; private set; }
    private readonly Dictionary<ulong, PlayerVisual> PLayerId_PlayerVisual_Dictionary = new();
    private class PlayerVisual
    {
        public List<CharacterSingle> CharacterSinglesList = new();
        public int SelectedPlayerPrefabIndex;
        public CharacterSingle SelectedCharacter
        {
            get => CharacterSinglesList[SelectedPlayerPrefabIndex];
        }
        public void Refresh(bool showyouTransform)
        {
            foreach(var character in CharacterSinglesList)
            {
                if (character == SelectedCharacter) continue;
                character.Hide();
                character.SetReady(false);
            }
            SelectedCharacter.Show(showyouTransform);
        }
        public void DeleteCharacters()
        {
            foreach (var character in CharacterSinglesList)
            {
                Destroy(character.gameObject);
            }

            CharacterSinglesList = null;
        }
    }

    private void Awake()
    {
        Instance = this;
        foreach(var character in characterList)
        {
            character.Hide();
        }
        GameMultiPlayer.Instance.GetPlayerDataList().OnListChanged += GameMultiplayer_OnPlayerDataListChanged;
    }

    private void GameMultiplayer_OnPlayerDataListChanged(NetworkListEvent<PlayerData> changeEvent)
    {
        switch (changeEvent.Type)
        {
            case NetworkListEvent<PlayerData>.EventType.Add:

                InitializeVisuals(changeEvent.Value);
                break;

            case NetworkListEvent<PlayerData>.EventType.Value:

                SetVisualForPlayerData(changeEvent.Value);
                break;

            case NetworkListEvent<PlayerData>.EventType.Remove:
                
                HandlePlayerRemoved(changeEvent.Value);
                break;

            case NetworkListEvent<PlayerData>.EventType.RemoveAt:

                HandlePlayerRemoved(changeEvent.Value);
                break;
        } 
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // whatever found, just update yourself.
        foreach(var playerData in GameMultiPlayer.Instance.GetPlayerDataList())
        {
            InitializeVisuals(playerData);
        }
    }

    private void InitializeVisuals(PlayerData playerData)
    {
        List<CharacterSingle> characters = new();

        foreach (var character in characterList)
        {
            var newChar = Instantiate(character);
            characters.Add(newChar);
            newChar.transform.SetParent(showTransforms[playerData.PositionIndex], false);
            newChar.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        PLayerId_PlayerVisual_Dictionary.Add(playerData.PlayerID, new PlayerVisual
        {
            CharacterSinglesList = characters,
            SelectedPlayerPrefabIndex = playerData.PrefabIndex,
        });

        IEnumerator SetVisualIntialize()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            SetVisualForPlayerData(playerData);
            Debug.Log("Setup complete for player : " + playerData.Name);
        }
         StartCoroutine(SetVisualIntialize());
    }

    private void HandlePlayerRemoved(PlayerData removedData)
    {
        PLayerId_PlayerVisual_Dictionary[removedData.PlayerID].DeleteCharacters();
        PLayerId_PlayerVisual_Dictionary.Remove(removedData.PlayerID);
    }

    private void SetVisualForPlayerData(PlayerData playerData)
    {
        var visual = PLayerId_PlayerVisual_Dictionary[playerData.PlayerID];
        var gun = GunsListSO.FireArmsList[playerData.FireArmIndex];

        visual.SelectedPlayerPrefabIndex = playerData.PrefabIndex;
        visual.SelectedCharacter.SetName(playerData.Name.ToString());
        visual.SelectedCharacter.SetFireArm(gun);
        visual.SelectedCharacter.SetReady(ready: playerData.Ready);

        visual.Refresh(showyouTransform: playerData.PlayerID == NetworkManager.LocalClientId);
        if(playerData.PlayerID == NetworkManager.LocalClientId)
        {
            // modification of the localplayer
            SelectionUI.Instance.SetPlayerName(visual.SelectedCharacter.GetPlayerName());
        }
    }

    public void ChangePlayer(int prefabIndex)
    {
        Debug.Log("Setting prefabIndex to : " +  prefabIndex);

        var playerData = GameMultiPlayer.Instance.GetSelfPlayerData();
        playerData.PrefabIndex = prefabIndex;
        GameMultiPlayer.Instance.UpdatePlayerData(playerData);
    }
    public void ChangeFireArm(int fireArmIndex)
    {
        var playerData = GameMultiPlayer.Instance.GetSelfPlayerData();
        playerData.FireArmIndex = fireArmIndex;
        GameMultiPlayer.Instance.UpdatePlayerData(playerData);
    }
    public void ChangeName(string name)
    {
        var playerData = GameMultiPlayer.Instance.GetSelfPlayerData();
        playerData.Name = name;
        GameMultiPlayer.Instance.UpdatePlayerData(playerData);
    }
    public override void OnDestroy()
    {
        base.OnDestroy();
        GameMultiPlayer.Instance.GetPlayerDataList().OnListChanged -= GameMultiplayer_OnPlayerDataListChanged;
    }
}
