
using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Handles the shooting logics for the player
/// </summary>
public class ThirdPersonShooter : NetworkBehaviour,ITargetable,IRigsSetter
{
    [SerializeField] private GunsListSO GunsListSO;

    [SerializeField] private float normalSensitivity = 1.0f;
    [SerializeField] private float aimSensitivity = 0.5f;
    [SerializeField] private float aimObjectLerpSpeed = 10f;
    [SerializeField] private float rotationSlerpSpeed = 10f;

    [SerializeField] private Transform aimObjectPrefab;
    [SerializeField] private LayerMask aimLayerMask;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform hoistedWeaponsHolderTransform;
    [SerializeField] private Transform gunsHolder;
    [SerializeField] private Transform grenadeThrowHandTransform;

    [SerializeField] private MultiAimConstraint spineConstraint;
    [SerializeField] private MultiAimConstraint rightHandConstraint;
    [SerializeField] private TwoBoneIKConstraint twoBoneIKConstraint;
    [SerializeField] private Rig rig;
    [SerializeField] private Transform breastTransform;

    private ParticleSystem muzzleFlash;
    private ThirdPersonController controller;
    private StarterAssetsInputs inputs;
    private Transform aimObject;
    private Transform gunNozzle;
    private GunMan gunMan;
    private AudioSource gunAudioSource;

    private bool isAiming = false;
    private bool isAlive = true;
    private float targetAimLayerWeight;
    private float targetAimRigWeight;
    private float fireTimer;
    private float fireDelay;
    private float lastYAngle;

    private void Awake()
    {
        controller = GetComponent<ThirdPersonController>();
        gunAudioSource = GetComponent<AudioSource>();
        inputs = GetComponent<StarterAssetsInputs>();
        animator = GetComponent<Animator>();
        gunMan = GetComponent<GunMan>();

        GetComponent<RigBuilder>().enabled = false;
        GetComponent<RigBuilder>().enabled = true;

        // This shall be ensured that only the server objects listens to this.
        //GameMultiPlayer.OnPlayerJoined += GameMultiPlayer_OnPlayerJoined;

        // stop control and animations right away. This is invoked at local level
        GetComponent<PlayerShootable>().OnPlayerOwnerDead += ThirdPersonShooter_OnPlayerOwnerDead;
        GameManager.OnMatchEnded += GameManager_OnMatchEnded;
    }

    private void GameManager_OnMatchEnded(object sender, GameManager.OnMatchEndedEventArgs e)
    {
        GameManager.OnMatchEnded -= GameManager_OnMatchEnded;

        isAlive = false;
    }

    public bool GetAlive() => isAlive;

    private void ThirdPersonShooter_OnPlayerOwnerDead(object sender, System.EventArgs e)
    {
        isAlive = false;
        isAiming = false;
        SetAimChanges(false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        gunMan.InitializeFireArm();

        if (IsServer) StartCoroutine(SetUpAimConstraint());

        if (!IsOwner) return;

        FireArmStatusUI.Instance.UpdateAllVisuals(gunMan.GetActiveFireArm());

        Status.Instance.SetHealth(1f);

        aimSensitivity = gunMan.GetActiveFireArm().GetGunSO().Sensitivity;
    }

    private IEnumerator SetUpAimConstraint()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        InstantiateAimObjectServerRpc(OwnerClientId);
    }

    private void Update()
    {
        // the animator must go on
        HandleAimAnimator();
        HandleRigWeight();

        if(!isAlive) return;

        // Them will be handled if alive only
        HandleAimMechanics();

        // firing logics
        HandleFires();
    }
    private void HandleAimMechanics()
    {
        if (!IsOwner) return;

        if (inputs.aim)
        {
            var aimPosition = GetCrossHairWorldPosition();

            if (!isAiming)
            {
                isAiming = true;
                SetAimChanges(true);
            }

            // move the aimobject to that of the center ( the crossheir)
            MoveAimObject(aimPosition);
            HandlePlayerRotation(aimPosition);
        }
        else if (isAiming)
        {
            isAiming = false;
            // check if still aiming
            SetAimChanges(false);
            aimObject.gameObject.SetActive(false);
            controller.ChangeRotationPermission(true);
        }

        if (!inputs.aim)
        {
            lastYAngle = transform.rotation.eulerAngles.y;
            CrossHair.Instance.SetNormalColor();
        }
    }
    private void SetAimChanges(bool isAiming)
    {
        SetAimCamera(isAiming);
        ShowWeapon(isAiming);
        SetAimLayerServerRpc(isAiming);

        targetAimLayerWeight = isAiming ? 1f : 0f;
        targetAimRigWeight = isAiming ? 1f : 0f;
    }
    [ServerRpc(RequireOwnership = false)]
    private void SetAimLayerServerRpc(bool val,ServerRpcParams serverRpcParams = default)
    {
        List<ulong> idsd = new (NetworkManager.ConnectedClientsIds);

        // remove the player that requested
        idsd.Remove(serverRpcParams.Receive.SenderClientId);

        SetAimLayerClientRpc( val,new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                // these clients will be asked to run the client rpc
                TargetClientIds = idsd
            }
        });
    }
    [ClientRpc(RequireOwnership = false)]
    private void SetAimLayerClientRpc(bool val, ClientRpcParams clientRpcParams)
    {
        targetAimLayerWeight = val ? 1 : 0;
        targetAimRigWeight = val ? 1 : 0;
        ShowWeapon(val);
    }
    [ServerRpc(RequireOwnership = false)]
    private void InstantiateAimObjectServerRpc(ulong ownerClientId)
    {
        if (aimObjectPrefab.TryGetComponent(out NetworkObject _))
        {
            var aimObjectPrefab = Instantiate(this.aimObjectPrefab);
            var netAimObject = aimObjectPrefab.GetComponent<NetworkObject>();
            netAimObject.SpawnAsPlayerObject(ownerClientId);

            SetAimConstraintsClientRpc(netAimObject);

            return;
        }
        Debug.Log("No networkObject was found ont the aimPrefab");
    }
    [ClientRpc(RequireOwnership = false)]
    private void SetAimConstraintsClientRpc(NetworkObjectReference aimObjectReference)
    {
        SetUpAimConstraints(aimObjectReference);
    }
    public void SetUpAimConstraints(NetworkObjectReference networkObjectReference)
    {
        if (networkObjectReference.TryGet(out NetworkObject aimObject))
        {
            WeightedTransformArray sources = new() { new WeightedTransform(aimObject.transform, 1f) };
            spineConstraint.data.sourceObjects = sources;
            rightHandConstraint.data.sourceObjects = sources;

            GetComponent<RigBuilder>().enabled = false;
            GetComponent<RigBuilder>().enabled = true;

            this.aimObject = aimObject.transform;


            return;
        }
        Debug.Log("NetworkObject was not found to set the aim constraint");
    }
    private void HandleFires()
    {
        if (!IsOwner) return;

        // resets always
        if(fireTimer < fireDelay) fireTimer += Time.deltaTime;

        if (inputs.aim && inputs.fire)
        {
            // check if he has left any bullets.

            int currentBullets = gunMan.GetActiveFireArm().GetCurrentMagazineBulletCount();
            if(currentBullets <= 0)
            {
                // No bullets.
                return;
            }

            if (fireTimer > fireDelay)
            {
                fireTimer = 0f;
                bool hasRecoil = gunMan.GetActiveFireArm().GetGunSO().Recoil != 0;
                FireABullet(addRecoil:hasRecoil);
            }
        }
    }

    private void ShowWeapon( bool show)
    {
        FireArm fireArm = gunMan.GetActiveFireArm();
        if (show)
        {
            fireArm.transform.SetParent(gunsHolder);
            fireArm.SetupOrientation();
            return;
        }

        fireArm.transform.SetParent(hoistedWeaponsHolderTransform, false);
        fireArm.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        fireArm.transform.localScale = Vector3.one;
    }

    private void FireABullet(bool addRecoil = false)
    {
        // check if there is anything as a target
        var displacement = aimObject.position - gunNozzle.position;
        var targetDir = displacement.normalized;
        Ray ray = new(gunNozzle.position, targetDir);
        Debug.DrawRay(gunNozzle.position, targetDir);

        float maxDistance = gunMan.GetActiveFireArm().GetGunSO().Range;
        
        NetCamera.ShakeEffect(0.1f);
        if (addRecoil)
        {
            AddRecoil();
        }

        PlayFireVisuals();
        gunMan.GetActiveFireArm().DecreaseBulletsCount();
        FireABulletVisualsServerRpc();
        FireArmStatusUI.Instance.UpdateAllVisuals(gunMan.GetActiveFireArm());

        if (!Physics.Raycast(ray, out RaycastHit hitInfo, maxDistance, aimLayerMask))
        {
            return;
        }
        gunMan.GetActiveFireArm().FireABullet(hitInfo);


        if (!hitInfo.collider.TryGetComponent(out IShootable shootable))
        {
            return;
        }

        ShootWithGun(shootable, hitInfo);
    }

    private void AddRecoil()
    {
        if (!IsOwner) return;

        float recoilAmount = gunMan.GetActiveFireArm().GetGunSO().Recoil;
        float spreadAmount = gunMan.GetActiveFireArm().GetGunSO().Spread;

        CrossHair.Instance.Recoil(recoilAmount,spreadAmount);
    }

    private void ShootWithGun(IShootable shootable,RaycastHit hitInfo)
    {
        float radius = 1f;

        FireArm currentGun = gunMan.GetActiveFireArm();

        switch (gunMan.GetActiveFireArm().GetGunSO().GunType)
        {
            case GunSO.Type.AssultRifle:

                // Here we implicate normal damage.
                // also instantiate the visuals
                shootable.AddNormalDamage(currentGun.GetDamagePowewr(),shooter: transform);
                break;

            case GunSO.Type.MachineGun:

                // Here we implicate normal damage.
                shootable.AddNormalDamage(currentGun.GetDamagePowewr(),shooter: transform);
                break;

            case GunSO.Type.ShotGun:

                // Implicate ShotgunDamage
                // Also get the decay amount as we move. At distance = range, the effective power equals zero
                float distance = Vector3.Distance(gunNozzle.position, hitInfo.point);
                float decay = 1 - distance / gunMan.GetActiveFireArm().GetGunSO().Range;

                shootable.AddExplosionForce(currentGun.GetExplosivePower() * decay, hitInfo.point, radius, currentGun.GetDamagePowewr() * decay,shooter: transform);
                break;

            case GunSO.Type.GrenadeLauncher:

                // we dont care if it is a grenade launcher, we just need to launch the grenade.
                break;
        }
    }
    [ServerRpc(RequireOwnership = true)]
    private void FireABulletVisualsServerRpc(ServerRpcParams serverRpcParams = default)
    {
        var sender = serverRpcParams.Receive.SenderClientId;
        var targetClients = new List<ulong>(NetworkManager.ConnectedClientsIds);
        targetClients.Remove(sender);

        FireABulletVisualsClientRpc(new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = targetClients,
            },
        });
    }

    [ClientRpc(RequireOwnership = false)]
    private void FireABulletVisualsClientRpc(ClientRpcParams clientRpcParams = default)
    {
        PlayFireVisuals();
    }

    private void PlayFireVisuals()
    {
        muzzleFlash.Play();
        // also play sound if there is any

        var clip = gunMan.GetActiveFireArm().GetFireSound();
        gunAudioSource.PlayOneShot(clip, 1f * GameProperties.VolumeScale);
    }

    private void HandleRigWeight()
    {
        // target needs to be controlled externally
        float rigLerpSpeed = 5f;
        rig.weight = Mathf.Lerp(rig.weight, targetAimRigWeight, Time.deltaTime * rigLerpSpeed);
    }

    private void HandleAimAnimator()
    {
        int aimLayerIndex = 1;
        float lerpSpeed = 5f;
        float currentWeight = animator.GetLayerWeight(aimLayerIndex);
        float weight = Mathf.Lerp(currentWeight, targetAimLayerWeight, Time.deltaTime * lerpSpeed);

        // set the weight
        animator.SetLayerWeight(aimLayerIndex, weight);
    }

    private void SetAimCamera(bool aim)
    {
        NetCamera.AimCamera.gameObject.SetActive(aim);
        controller.ChangeSensitivity(aim ? aimSensitivity : normalSensitivity);
    }

    private void MoveAimObject(Vector3 position)
    {
        if (aimObject == null) return;
        // the ray cast has hit something valuable
        if(position != default)
        {
            aimObject.position = Vector3.Lerp(aimObject.position,position,Time.deltaTime * aimObjectLerpSpeed);
            aimObject.gameObject.SetActive(true);
            return;
        }
    }
    private Vector3 GetCrossHairWorldPosition()
    {
        CrossHair cs = CrossHair.Instance;
        Vector2 crossHainScreenPos = new (cs.ScreenPosition.x, cs.ScreenPosition.y);
        Ray ray = Camera.main.ScreenPointToRay(crossHainScreenPos);

        // The max distance upto which it checks
        float maxDistance = 500f;
        float range = gunMan.GetActiveFireArm().GetGunSO().Range;

        if(Physics.Raycast(ray,out RaycastHit hitInfo, maxDistance - 1.0f, aimLayerMask))
        {
            // we are hitting something at the center, ensuer its not itself
            bool isSelfTarget = hitInfo.collider.gameObject == gameObject;
            if (isSelfTarget)
            {
                return default;
            }
            HandleCrossHair(hitInfo, range);
            return hitInfo.point;
        }
        return default;
    }

    private void HandleCrossHair(RaycastHit hitInfo,float range)
    {
        if (hitInfo.distance > range - 1.0f)
        {
            CrossHair.Instance.SetOutOfRange();
        }
        else if (hitInfo.collider.gameObject.TryGetComponent(out IShootable _))
        {
            CrossHair.Instance.SetTargetLocked();
        }
        else
        {
            CrossHair.Instance.SetNormalColor();
        }
    }
    // Manage rotation as the player aims
    private void HandlePlayerRotation(Vector3 aimPosition)
    {
        if (aimPosition == default) return;

        // first turn off the controller's ability to trotate
        controller.ChangeRotationPermission(false);
        Vector3 aimDirection = (aimPosition - transform.position).normalized;

        //X-Z plane only
        aimDirection.y = 0;
        if(aimDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(aimDirection, Vector3.up);

            // rotate only if atleast a threshold of 20 degrees is obtained.

            var threshold = Mathf.Abs(targetRotation.eulerAngles.y - lastYAngle);
            if(threshold >= 20f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSlerpSpeed);
                lastYAngle = transform.rotation.eulerAngles.y;
            }
        }
    }

    public void SetupActiveGun()
    {
        FireArm activeGun = gunMan.GetActiveFireArm();
        gunNozzle = activeGun.GetNozzle();
        muzzleFlash = activeGun.GetMuzzleFlash();

        ShowWeapon(isAiming);

        // Also set the rate of fire urf fireDelay
        fireDelay = 1f / activeGun.GetRateOfFire();
        fireDelay *= 1000f;
        fireDelay = Mathf.Ceil(fireDelay);
        fireDelay /= 1000f;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        //GameMultiPlayer.OnPlayerJoined -= GameMultiPlayer_OnPlayerJoined;
        GameManager.OnMatchEnded -= GameManager_OnMatchEnded;
    }
    public Transform GetTargetTransform()
    {
        return breastTransform;
    }
}
