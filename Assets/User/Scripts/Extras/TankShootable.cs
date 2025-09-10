using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class TankShootable : CarShootable
{
    [SerializeField] private GameObject damagedTank;
    protected override void DeclareCarIsDead()
    {
        GameMultiPlayer.Instance.InstantiateGameObject(damagedTank,transform.position,transform.rotation);

        // Despawn the tank
        GetComponent<NetworkObject>().Despawn(destroy: true);
    }
}
