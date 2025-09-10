using UnityEngine;
using UnityEngine.UI;

public class CrossHair : MonoBehaviour
{
    [SerializeField] private RectTransform crossHairImage;
    [SerializeField] private float resetLerpSpeed;

    [SerializeField] private Color outOrRnageColor = Color.gray;
    [SerializeField] private Color targetLockColor = Color.red;
    public static CrossHair Instance;
    private Vector3 targetPos;
    private bool reset = false;
    private float coolDownTimer = 0f;
    private const float coolDownTimerMax = 0.5f;
    private Vector3 screenCenter;
    public void Recoil(float delta, float spread = 0f)
    {
        coolDownTimer = coolDownTimerMax;
        var currentPos = crossHairImage.position;
        var spreadVector = new Vector3(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f);
        var newPos = currentPos + Vector3.up * delta + spreadVector;
        targetPos = newPos;

        reset = false;
    }
    private void Awake()
    {
        Instance = this;
        screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        GunMan.OnGunsModifiedLocalLevel += GunMan_OnGunsModified;
    }

    private void GunMan_OnGunsModified(object sender, GunMan.OnGunModificationEventArgs e)
    {
        crossHairImage.GetComponent<Image>().sprite = e.FireArm.GetGunSO().CursorSprite;
    }

    private void Update()
    {
        if(coolDownTimer >= 0f)
        {
            coolDownTimer -= Time.deltaTime;
            if(coolDownTimer < 0f)
            {
                reset = true;
            }
        }

        crossHairImage.transform.position = Vector3.Lerp(
            crossHairImage.transform.position,reset ? screenCenter : targetPos, resetLerpSpeed * Time.deltaTime);
    }
    public Vector3 ScreenPosition
    {
        get
        {
            return crossHairImage.position;
        }
    }
    public void SetOutOfRange()
    {
        crossHairImage.GetComponent<Image>().color = outOrRnageColor;
    }
    public void SetTargetLocked()
    {
        crossHairImage.GetComponent<Image>().color = targetLockColor;
    }
    public void SetNormalColor()
    {
        crossHairImage.GetComponent<Image>().color = Color.white ;
    }

    private void OnDestroy()
    {
        GunMan.OnGunsModifiedLocalLevel -= GunMan_OnGunsModified;
    }
}
