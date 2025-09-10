
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class AmmoBox : InteractableObject
{
    private GunSO gunSO;

    private Grenade.Type grenadeTypeIfAny;
    public Grenade.Type GetGrenadeType() => grenadeTypeIfAny;
    public int GetBulletsAmount()
    {
        var guntype = gunSO.GunType;

        if(guntype == GunSO.Type.GrenadeLauncher)
        {
            switch (grenadeTypeIfAny)
            {
                case Grenade.Type.Normal: // 40 - 85 %
                    return (int)(Random.Range(0.4f, 0.85f) * gunSO.AmmoCapacity);

                case Grenade.Type.Smoke: // 100 %
                    return gunSO.AmmoCapacity;

                case Grenade.Type.Poison: // 50 - 80 %
                    return (int) (Random.Range(0.7f, 1f) * gunSO.AmmoCapacity) ;
            }
        }

        return (int)(gunSO.AmmoCapacity * Random.Range(0.5f, 1f));
    }
    public void SetGunSO(FireArm fireArm)
    {
        gunSO = fireArm.GetGunSO();
        iconImage.sprite = fireArm.GetAmmoVisual();

        if(fireArm.GetGunSO().GunType == GunSO.Type.GrenadeLauncher)
        {
            // special treatment for grenade launcher : Randomly generate a grenade.
            int rand = Random.Range(0, 3);
            grenadeTypeIfAny = (Grenade.Type)rand;
            iconImage.sprite = fireArm.GetComponent<GrenadeLauncher>().GetAmmoSpriteOfType(grenadeTypeIfAny);
        }
    }
    public GunSO GetGunSO() => gunSO;
    public override void Interact(GunMan interactor)
    {
        var netobj = interactor.GetComponent<NetworkObject>();

        // send server : 
        NetworkInteractQueryServerRpc(netobj);
    }

    [ServerRpc(RequireOwnership = false)]
    private void NetworkInteractQueryServerRpc(NetworkObjectReference interactor,ServerRpcParams serverRpcParams = default)
    {
        // everyone gets to take ammo atleast once.
        // send back the interaction logic to the asker
        NetworkInteractResponseClientRpc(interactor, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new List<ulong>() { serverRpcParams.Receive.SenderClientId }
            }
        });
    }

    [ClientRpc(RequireOwnership = false)]
    // only sent to one person
    private void NetworkInteractResponseClientRpc(NetworkObjectReference networkObjectReference,ClientRpcParams clientRpcParams)
    {
        // as we have recieved the response, therefore, it will not be usable anymore ( because usable is manually synced, locally )
        isUsable = false;
        if(!networkObjectReference.TryGet(out NetworkObject networkObject) || !networkObject.TryGetComponent(out GunMan gunMan))
        {
            Debug.LogError("Gunman or networkobject not found");
            return;
        }

        gunMan.CollectBulletsFromAmmoBox(this);

        // call to destroy the box
        DestroyServerRpc();
    }

    protected override void OnTriggerEnter(Collider other)
    {
        if (!isUsable) return;

        if(other.TryGetComponent(out GunMan gunMan) && gunMan.IsLocalPlayer)
        {
            SetGunSO(gunMan.GetActiveFireArm());
        }

        base.OnTriggerEnter(other);
    }
}
