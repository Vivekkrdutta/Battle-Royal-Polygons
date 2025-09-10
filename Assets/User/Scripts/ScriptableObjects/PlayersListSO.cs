using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayersListSO", menuName = "Scriptable Objects/PlayersListSO")]
public class PlayersListSO : ScriptableObject
{
    public List<Player> PlayersList;
}
