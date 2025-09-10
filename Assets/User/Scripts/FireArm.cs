
using UnityEngine;

public class FireArm : MonoBehaviour
{
    [SerializeField] private GunSO gunSO;
    // It will be shown over the network
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private BulletsPrefabsSO BulletsPrefabsSO;
    [SerializeField] private BulletTrail trailPrefab;

    [SerializeField] private ParticleSystem hitEffect;
    [Tooltip("For shotgun")]
    [SerializeField] private ParticleSystem blast;
    [SerializeField] private ParticleSystem bulletShells;
    [SerializeField] private Transform nozzle;
    [SerializeField] private bool showTrails = true;
    [SerializeField] private bool shotTrailsOverNetwork = true;

    private int totalBulletsRemaining = 0;
    private int remainingBulletsInCurrentMagazine = 0;
    private int prevFireAudioIndex = -1;
    private FireArmType fireArmType = FireArmType.NotEquipped;

    public FireArmType ArmType { get=> fireArmType; set => fireArmType = value; }
    public enum FireArmType
    {
        Primary,
        Secondary,
        NotEquipped,
    }
    private void Awake()
    {
        remainingBulletsInCurrentMagazine = gunSO.AmmoCapacity;
        totalBulletsRemaining = 0; // no extra mag initially.
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }
    public void Show()
    {
        gameObject.SetActive(true);
    }
    public float GetDamagePowewr() => gunSO.DamageAmount;
    public float GetRateOfFire() => gunSO.RateOfFire;
    public float GetRate() => gunSO.Range;
    public float GetExplosivePower() => gunSO.ExposivePower;
    public GunSO GetGunSO() => gunSO;
    public Sprite GetAmmoVisual()
    {
        if(gunSO.GunType == GunSO.Type.GrenadeLauncher)
        {
            return GetComponent<GrenadeLauncher>().GetAmmoSprite();
        }

        return gunSO.AmmoVisualSprite;
    }
    public AudioClip GetFireSound()
    {
        var clips = gunSO.FireSounds;
        prevFireAudioIndex = (prevFireAudioIndex + 1) % clips.Length;
        return clips[prevFireAudioIndex];
    }
    public ParticleSystem GetMuzzleFlash() => muzzleFlash;
    public Transform GetNozzle() => nozzle;
    public void SetupOrientation()
    {
        transform.localPosition = gunSO.Self.LocalPosition;
        transform.localEulerAngles = gunSO.Self.LocalRotation;
        transform.localScale = gunSO.Self.LocalScale;
    }

    /// <summary>
    /// ShotGun shots are shown over the network. Assult rifle shots are kept at local Level.
    /// </summary>
    /// <param name="hitInfo"></param>
    public void FireABullet(RaycastHit hitInfo)
    {
        if (showTrails)
        {
            GameMultiPlayer.Instance.ShootABulletTrail(
                nozzle.position, hitInfo.point,BulletsPrefabsSO.BulletTrailPrefabsList.IndexOf(trailPrefab) ,shotTrailsOverNetwork);
        }

        if (gunSO.GunType == GunSO.Type.AssultRifle || gunSO.GunType == GunSO.Type.MachineGun)
        {
            if (hitEffect)
            {
                hitEffect.transform.position = hitInfo.point;
                hitEffect.transform.forward = hitInfo.normal;
                hitEffect.Emit(1);
            }

            TryRemoveBulletShells();
        }

        else if(gunSO.GunType == GunSO.Type.ShotGun)
        {
            GameMultiPlayer.Instance.InstantiateShotgunBlast(hitInfo.point, blast);
        }

        else if(gunSO.GunType == GunSO.Type.GrenadeLauncher)
        {
            GetComponent<GrenadeLauncher>().Fire(hitInfo.point);
        }
    }

    public void DecreaseBulletsCount()
    {
        remainingBulletsInCurrentMagazine--;
    }

    /// <summary>
    /// Only in case of a machine gun
    /// </summary>
    public void TryRemoveBulletShells()
    {
        if (bulletShells)
        {
            bulletShells.Play();
        }
    }
    public int GetTotalRemainingBulletsCount() => totalBulletsRemaining;
    public void AddBullets(int val) => totalBulletsRemaining += val;
    public int GetCurrentMagazineBulletCount() { return remainingBulletsInCurrentMagazine; }

    /// <summary>
    /// Reloads using data from remaining bullets and remaining bullets of the FireArm
    /// </summary>
    public void TryReloadBullets()
    {
        // how many bullets we need to fill the magazine
        int bulletsNeeded = gunSO.AmmoCapacity - remainingBulletsInCurrentMagazine;

        // how many bullets we can actually reload (limited by total reserve)
        int bulletsToReload = Mathf.Min(bulletsNeeded, totalBulletsRemaining);

        // apply reload
        remainingBulletsInCurrentMagazine += bulletsToReload;
        totalBulletsRemaining -= bulletsToReload;
    }

}
