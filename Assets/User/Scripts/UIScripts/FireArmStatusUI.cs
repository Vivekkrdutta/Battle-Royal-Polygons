
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using FireArmType = FireArm.FireArmType;

public class FireArmStatusUI : MonoBehaviour
{
    [SerializeField] private Color[] gunTypeColors;
    [SerializeField] private FireArmVisual primaryFireArmVisual;
    [SerializeField] private FireArmVisual secondaryFireArmVisual;
    [SerializeField] private Image interactAlertImage;

    [SerializeField] private Color inactiveColor;
    public static FireArmStatusUI Instance { get; private set; }

    [System.Serializable]
    public class FireArmVisual
    {
        [SerializeField] private Image gunImage;
        [SerializeField] private Image bulletsImage;
        [SerializeField] private Image backImage;
        [SerializeField] private TextMeshProUGUI bulletsCountText;
        [SerializeField] private FireArmType FireArmType;
        [SerializeField] private RectTransform reloadVisual;
        public Sprite GunSprite 
        { 
            set 
            { 
                gunImage.sprite = value; 
                gunImage.transform.parent.gameObject.SetActive(true);
            } 
        }
        public Sprite BulletsSprite { set { bulletsImage.sprite = value; } }

        public void SetBulletsForFireArm(FireArm fireArm)
        {
            bulletsCountText.text = 
                fireArm.GetCurrentMagazineBulletCount().ToString() + 
                "/" + 
                fireArm.GetTotalRemainingBulletsCount().ToString();
        }
        public Color BackColor { set { backImage.color = value; } }
        public bool ReloadVisualActive { set => reloadVisual.gameObject.SetActive(value); }
        public void InactiveSelf()
        {
            gunImage.transform.parent.gameObject.SetActive(false);
        }
        public override string ToString()
        {
            return FireArmType.ToString();
        }
    }
    public void SetInteractable(bool val)
    {
        interactAlertImage.gameObject.SetActive(val);
    }

    private void Awake()
    {
        Instance = this;
        GunMan.OnGunsModifiedLocalLevel += GunMan_OnGunsModified;
        interactAlertImage.gameObject.SetActive(false);

        primaryFireArmVisual.InactiveSelf();
        secondaryFireArmVisual.InactiveSelf();
        PlayerShootable.OnAnyPlayerDead += PlayerShootable_OnAnyPlayerDead;
    }

    private void PlayerShootable_OnAnyPlayerDead(object sender, PlayerShootable.OnAnyPlayerDeadEventArgs e)
    {
        if(e.Player.GetComponent<ThirdPersonShooter>().IsLocalPlayer)
        {
            // the local player just died
            primaryFireArmVisual.InactiveSelf();
            secondaryFireArmVisual.InactiveSelf();
        }
    }

    private void GunMan_OnGunsModified(object sender, GunMan.OnGunModificationEventArgs e)
    {
        SetGunActive(e.FireArmType,e.FireArm.GetGunSO().GunType);

        if (e.IsThisASwap) return;

        UpdateAllVisuals(e.FireArm);
    }
    
    public void UpdateAllVisuals(FireArm fireArm)
    {
        var selectedGun = (fireArm.ArmType == FireArmType.Primary ? primaryFireArmVisual : secondaryFireArmVisual);

        var gunSO = fireArm.GetGunSO();

        selectedGun.GunSprite = gunSO.GunVisualSpriteStraight;
        selectedGun.BulletsSprite = fireArm.GetAmmoVisual();
        selectedGun.SetBulletsForFireArm(fireArm);
    }

    public void SetReloading(FireArm fireArm, bool val)
    {
        var selecteVisual = fireArm.ArmType == FireArmType.Primary ? primaryFireArmVisual : secondaryFireArmVisual;
        selecteVisual.ReloadVisualActive = val;
    }

    public void SetGunActive(FireArmType fireArmType,GunSO.Type gunType)
    {
        switch(fireArmType)
        {
            case FireArmType.Primary:

                primaryFireArmVisual.BackColor = gunTypeColors[(int) gunType];
                secondaryFireArmVisual.BackColor = inactiveColor;
                break;

            case FireArmType.Secondary:

                secondaryFireArmVisual.BackColor = gunTypeColors[(int)gunType];
                primaryFireArmVisual.BackColor = inactiveColor;
                break;
        }
    }

    private void OnDestroy()
    {
        GunMan.OnGunsModifiedLocalLevel -= GunMan_OnGunsModified;
        PlayerShootable.OnAnyPlayerDead -= PlayerShootable_OnAnyPlayerDead;
    }
}
