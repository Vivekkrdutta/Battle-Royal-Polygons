using StarterAssets;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using FireArmType = FireArm.FireArmType;

/// <summary>
/// Manages equipping various guns
/// </summary>
public class GunMan : NetworkBehaviour
{
    [SerializeField] private Transform ikTargetTransform;
    [SerializeField] private GunsListSO gunsListSO;
    [SerializeField] private Transform gunsHolder;
    [SerializeField] private AudioClip gunswapSound;
    [SerializeField] private AudioClip gunReloadSound;

    public static event EventHandler<OnGunModificationEventArgs> OnGunsModifiedLocalLevel;
    public class OnGunModificationEventArgs : EventArgs
    {
        public FireArm FireArm;
        public FireArmType FireArmType;
        public bool IsThisASwap;
    }

    private FireArm activeFireArm = null;
    private FireArm primaryFireArm = null;
    private FireArm secondaryFireArm = null;
    private FireArm currentReloadingGun = null;
    private ThirdPersonShooter shooter = null;
    private InteractableObject nearbyInteractableInRange = null;
    private StarterAssetsInputs inputs = null;
    public static GunMan LocalGunMan { get; private set; }


    // The following variables are used to prevent race around conditions due to being called every frame.
    private bool _didAlreadyInteract = false;
    private bool _allowedToReload = true;
    private bool _didSwapRightNow = false;

    private void Awake()
    {
        inputs = GetComponent<StarterAssetsInputs>();
        shooter = GetComponent<ThirdPersonShooter>();
    }
    private void Update()
    {
        if (!IsOwner) return;

        HandleInteractions();
        HandleReloadLogic();
        HandleGunSwaps();
    }

    public void InitializeFireArm()
    {
        int index = -1;
        foreach(var playerData in GameMultiPlayer.Instance.GetPlayerDataList())
        {
            if(OwnerClientId == playerData.PlayerID)
            {
                index = playerData.FireArmIndex;
            }
        }
        SetFireArmForIndexAndType(index, FireArmType.Primary, false);
    }
    private void HandleInteractions()
    {
        if (inputs.interact && !_didAlreadyInteract && nearbyInteractableInRange)
        {
            _didAlreadyInteract = true;
            TryInteractWithNearby();
        }

        if (!inputs.interact && _didAlreadyInteract)
        {
            _didAlreadyInteract = false;
        }
    }

    private void HandleReloadLogic()
    {
        if(inputs.reload && _allowedToReload && activeFireArm)
        {
            _allowedToReload = false;

            GetComponent<AudioSource>().PlayOneShot(gunReloadSound,1f * GameProperties.VolumeScale);
            // enable to reload again after some time

            StartCoroutine(ReEnableToReload(activeFireArm.GetGunSO().ReloadTime));
            currentReloadingGun = activeFireArm;
            FireArmStatusUI.Instance.SetReloading(currentReloadingGun, true);
        }
    }
    private IEnumerator ReEnableToReload(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);

        activeFireArm.TryReloadBullets();
        // also update the visuals
        FireArmStatusUI.Instance.UpdateAllVisuals(activeFireArm);

        _allowedToReload = true;
        FireArmStatusUI.Instance.SetReloading(currentReloadingGun, false);
        currentReloadingGun = null;
    }

    private void HandleGunSwaps()
    {
        if(inputs.swapgun  && !_didSwapRightNow && primaryFireArm && secondaryFireArm) // have all the guns
        {
            _didSwapRightNow = true;
            SwapGun();
        }

        if(!inputs.swapgun && _didSwapRightNow)
        {
            _didSwapRightNow = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {

            return;
        }

        LocalGunMan = this;        
    }
    public FireArm GetActiveFireArm() => activeFireArm;
    private void TryInteractWithNearby()
    {
        if (!IsOwner) return;

        // Interaction UI work done.
        FireArmStatusUI.Instance.SetInteractable(false);

        nearbyInteractableInRange.Interact(this);
    }
    public void SetNearByInteractable(InteractableObject nearbyInteractableInRange)
    {
        this.nearbyInteractableInRange = nearbyInteractableInRange;
        FireArmStatusUI.Instance.SetInteractable(nearbyInteractableInRange);
    }

    public void SetFireArmFromGunSO(GunSO gunSO,bool alsoNetworkUpdate = true)
    {
        var index = gunsListSO.FireArmsList.FindIndex(arm => arm.GetGunSO() == gunSO);

        var currentType = activeFireArm.ArmType;
        // check if having a slot empty.
        if(!primaryFireArm || !secondaryFireArm)
        {
            currentType = primaryFireArm ?  FireArmType.Secondary : FireArmType.Primary;
        }

        // swap with the currently holding gun
        SetFireArmForIndexAndType(index, currentType,alsoNetworkUpdate);
    }

    private void SetFireArmForIndexAndType(int index,FireArmType fireArmType,bool alsoNetworkUpdate = true)
    {
        // instantiate the firarm
        // change your gun yourself at local level, and inform the server to let others know, if needed
        EquipGunAtLocalLevel(index, fireArmType);

        if(!IsOwner) return;

        if(alsoNetworkUpdate) SetFireArmServerRpc(index,fireArmType);

        // Fire the event at local levle for UI updates at FireArmStatusUI
        OnGunsModifiedLocalLevel?.Invoke(this, new OnGunModificationEventArgs
        {
            FireArm = activeFireArm,
            FireArmType = fireArmType
        });
    }

    [ServerRpc(RequireOwnership = true)]
    private void SetFireArmServerRpc(int gunSOIndex,FireArmType fireArmType, ServerRpcParams serverRpcParams = default)
    {
        // Tell all the clients to show this gun
        var sender = serverRpcParams.Receive.SenderClientId;

        var clientIds = new List<ulong>(NetworkManager.ConnectedClientsIds);
        
        // Not he sender
        clientIds.Remove(sender);

        SetFireArmClientRpc(gunSOIndex,fireArmType, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = clientIds
            }
        });
    }

    [ClientRpc(RequireOwnership = false)]
    private void SetFireArmClientRpc(int gunSOIndex,FireArmType fireArmType, ClientRpcParams clientRpcParams = default)
    {
        EquipGunAtLocalLevel(gunSOIndex, fireArmType);
    }

    private void EquipGunAtLocalLevel(int index,FireArmType fireArmType)
    {
        switch(fireArmType)
        {
            case FireArmType.Primary:

                if(primaryFireArm) Destroy(primaryFireArm.gameObject);
                primaryFireArm = InstantiateFireArmAtLocalLevel(index,fireArmType);
                break;

            case FireArmType.Secondary:

                if(secondaryFireArm) Destroy(secondaryFireArm.gameObject);
                secondaryFireArm = InstantiateFireArmAtLocalLevel(index, fireArmType);
                break;
        }
    }

    private FireArm InstantiateFireArmAtLocalLevel(int index,FireArmType type)
    {
        // Instantiate a new Gun
        FireArm newGun = Instantiate(gunsListSO.FireArmsList[index], gunsHolder);
        newGun.ArmType = type;

        if (activeFireArm) activeFireArm.Hide();

        // the newly instantiated gun will always be the active gun
        activeFireArm = newGun;

        // Set up the visuals and update the current gun stata
        if (shooter != null) shooter.SetupActiveGun();

        Debug.Log("Equipped gun : " + type + " by client : " + OwnerClientId);
        GetComponent<AudioSource>().PlayOneShot(gunswapSound,1f * GameProperties.VolumeScale);

        // also set the ik transform
        ikTargetTransform.localPosition = newGun.GetGunSO().LeftHandPlace.LocalPosition;
        ikTargetTransform.localEulerAngles = newGun.GetGunSO().LeftHandPlace.LocalRotation;

        return newGun;
    }

    public void CollectBulletsFromAmmoBox(AmmoBox ammoBox)
    {
        var forgun = activeFireArm;

        // check if this is a GL,
        if(forgun.TryGetComponent(out GrenadeLauncher grenadeLauncher))
        {
            grenadeLauncher.GrenadeType = ammoBox.GetGrenadeType();
        }
        
        forgun.AddBullets(ammoBox.GetBulletsAmount());

        // for owner
        if (!IsOwner) return;

        FireArmStatusUI.Instance.UpdateAllVisuals(forgun);
    }
    
    public void SwapGun()
    {
        if(!IsOwner || activeFireArm == null || primaryFireArm == null || secondaryFireArm == null) return;

        var currentType = activeFireArm.ArmType;
        var toSet = currentType == FireArmType.Primary ? FireArmType.Secondary : FireArmType.Primary;

        SwapGunVisualUpdate(toSet);
        SwapGunServerRpc(toSet);

        // Trigger the event at local level
        OnGunsModifiedLocalLevel?.Invoke(this, new OnGunModificationEventArgs
        {
            IsThisASwap = true,
            FireArmType = toSet,
            FireArm = activeFireArm
        });
    }

    [ServerRpc(RequireOwnership = true)]
    private void SwapGunServerRpc(FireArmType activeGunSetToType,ServerRpcParams serverRpcParams = default)
    {
        var sender = serverRpcParams.Receive.SenderClientId;
        var clients = GameMultiPlayer.Instance.GetClientIdsListExcept(sender);
        SwapGunClientRpc(activeGunSetToType, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = clients,
            }
        });
    }

    [ClientRpc(RequireOwnership = false)]
    private void SwapGunClientRpc(FireArmType activeGunSetToType,ClientRpcParams clientRpcParams)
    {
        SwapGunVisualUpdate(activeGunSetToType);
    }
    private void SwapGunVisualUpdate(FireArmType activeGunSetToType)
    {
        activeFireArm = activeGunSetToType == FireArmType.Primary ? primaryFireArm : secondaryFireArm;
        var inactiveOne = activeGunSetToType == FireArmType.Primary ? secondaryFireArm : primaryFireArm;

        // setups
        if (shooter != null) shooter.SetupActiveGun();

        // also set the ik transform
        ikTargetTransform.localPosition = activeFireArm.GetGunSO().LeftHandPlace.LocalPosition;
        ikTargetTransform.localEulerAngles = activeFireArm.GetGunSO().LeftHandPlace.LocalRotation;

        // play the audio.
        GetComponent<AudioSource>().PlayOneShot(gunswapSound,volumeScale:1f * GameProperties.VolumeScale);

        // avoid glitching
        activeFireArm.Show();
        inactiveOne.Hide();
    }
    public (FireArm Primary, FireArm Secondary) GetFireArms() => (primaryFireArm,secondaryFireArm);
}

