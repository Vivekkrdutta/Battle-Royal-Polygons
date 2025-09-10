
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GunsBox : InteractableObject
{
    [SerializeField] private GunSO[] gunSOs;
    private GunSO gunsO; // the gunSO that is has currently.

    public GunSO GetGunSO() => gunsO;
    protected override void Awake()
    {
        base.Awake();
        autoSetShowHideIcon = false;
    }

    public override void OnNetworkSpawn()
    {
        // randomly select a gun.
        if (IsServer)
        {
            int rand = UnityEngine.Random.Range(0, gunSOs.Length);
            SetGunSOClientRpc(rand);
        }
    }
    public override void Interact(GunMan interactor)
    {
        NetworkInteractQueryServerRpc(interactor.GetComponent<NetworkObject>());
    }

    [ClientRpc(RequireOwnership = true)]
    private void SetGunSOClientRpc(int index)
    {
        var gunSO = gunSOs[index];
        
        gunsO = gunSO;
        iconImage.sprite = gunSO.GunVisualSpriteStraight;
    }

    [ServerRpc(RequireOwnership = false)]
    private void NetworkInteractQueryServerRpc(NetworkObjectReference interactor, ServerRpcParams serverRpcParams = default)
    {
        // send back the interaction logic to the asker
        NetworkInteractResultClientRpc(interactor, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new List<ulong>() { serverRpcParams.Receive.SenderClientId },
            }
        });
    }

    [ClientRpc(RequireOwnership = false)]
    // will be sent to only one, and then destroyed.
    private void NetworkInteractResultClientRpc(NetworkObjectReference networkObjectReference,ClientRpcParams clientRpcParams)
    {
        // not available to use anymore for this player, because he has recieved the response
        isUsable = false;
        if (!networkObjectReference.TryGet(out NetworkObject networkObject) || !networkObject.TryGetComponent(out GunMan gunMan))
        {
            Debug.LogError("Gunman or networkobject not found");
            return;
        }

        // give the swaping reward to the gunman
        gunMan.SetFireArmFromGunSO(gunsO);

        // destroy the object
        DestroyServerRpc();
    }
}
