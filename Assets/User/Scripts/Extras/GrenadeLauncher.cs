
using UnityEngine;

public class GrenadeLauncher : MonoBehaviour
{
    [SerializeField] private Sprite normalGrenadeBullet;
    [SerializeField] private Sprite smokeGrenadeBullet;
    [SerializeField] private Sprite poisonGrenadeBullet;

    public GameObject grenadePrefab;
    public Transform nozzle;
    public Grenade.Type GrenadeType;

    private void Awake()
    {
        GrenadeType = Grenade.Type.Normal;
    }
    public void Fire(Vector3 targetPoint)
    {
        // 2. Tell server
        GameMultiPlayer.Instance.LaunchGrenade(targetPoint,nozzle.position,(int) GrenadeType);
    }
    public Sprite GetAmmoSprite()
    {
        switch (GrenadeType)
        {
            case Grenade.Type.Normal:
                return normalGrenadeBullet;

            case Grenade.Type.Smoke:
                return smokeGrenadeBullet;

            case Grenade.Type.Poison:
                return poisonGrenadeBullet;

            default:
                break;
        }

        return null;
    }
    public Sprite GetAmmoSpriteOfType(Grenade.Type GrenadeType)
    {
        switch (GrenadeType)
        {
            case Grenade.Type.Normal:
                return normalGrenadeBullet;

            case Grenade.Type.Smoke:
                return smokeGrenadeBullet;

            case Grenade.Type.Poison:
                return poisonGrenadeBullet;

            default:
                break;
        }

        return null;
    }
}
