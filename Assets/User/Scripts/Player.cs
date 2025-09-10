
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static List<Player> PlayersList = new();
    private void Awake()
    {
        PlayersList.Add(this);
    }

    private void OnDestroy()
    {
        PlayersList.Remove(this);
    }

    public static Player GetPlayerForPlayerData(PlayerData playerData)
    {
        return PlayersList.FirstOrDefault(pl => pl.GetComponent<ThirdPersonShooter>().OwnerClientId == playerData.PlayerID);
    }
}
