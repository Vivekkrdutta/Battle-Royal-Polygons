using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base class for all bars : ChangeValue(). Lookat settings.
/// </summary>
public class BarUI : MonoBehaviour
{
    /// <summary>
    /// Must be child of a background image
    /// </summary>
    [Tooltip("Must be child of a background image")]
    [SerializeField] private Image barImage;
    [SerializeField] private LookAt lookAt;
    [Tooltip("How much time after value is changed does it hide automatically")]
    [SerializeField] private float hideTimerMax;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool isStatic = false;
    [Tooltip("Optional colors")]
    [SerializeField] private Color[] customColors;

    private Image backGroundImage; // usually the parent of barImage

    /// <summary>
    /// 0th color is the best, last is the worst.
    /// </summary>
    private Color[] fineIndicatorColors = new Color[] { Color.green, Color.yellow, Color.red};

    private Camera mainCam;
    private float hideTimer = 0f;
    public enum LookAt
    {
        None,
        CameraYAxisOnly,
        CameraNoRestriction,
    }
    protected virtual void Awake()
    {
        mainCam = Camera.main;

        // The parent of the barImage is a background image by design
        backGroundImage = barImage.transform.parent.GetComponent<Image>();

        hideTimer = hideTimerMax;

        if(customColors.Length > 0) fineIndicatorColors = customColors;

        if(!isStatic) Hide();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
    public void Show()
    {
        // reset on show
        hideTimer = hideTimerMax;
        canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
    }
    protected virtual void Update()
    {
        if (isStatic) return;

        switch (lookAt)
        {
            case LookAt.CameraYAxisOnly:

                var lookAtPos = GetCameraOpposite();
                // keep the y level same
                lookAtPos.y = transform.position.y;

                transform.LookAt(lookAtPos);
                break;

            case LookAt.CameraNoRestriction:

                transform.LookAt(GetCameraOpposite());
                break;

            default:
                break;

        }

        HandleAutoHide();
    }

    private void HandleAutoHide()
    {
        if(hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;
            float fadeStartTimeStamp = 1f;

            if(hideTimer < fadeStartTimeStamp)
            {
                // start fading both of the images
                canvasGroup.alpha -= Time.deltaTime;
            }

            if(hideTimer < 0f)
            {
                hideTimer = 0f;
                Hide();
            }
        }
    }

    private Vector3 GetCameraOpposite()
    {
        var distance = 10f;
        var dirFromCam = (transform.position - mainCam.transform.position).normalized;
        var lookAtPos = transform.position + dirFromCam * distance;
        return lookAtPos;
    }

    public virtual void ChangeValue(float value)
    {
        if (value < 0f || value > 1f) return;
        barImage.fillAmount = value;

        // Show the Bar
        Show();

        if(barImage.fillAmount <= 0f)
        {
            Hide();
            return;
        }

        // considering 3 color combos
        if(value < 0.333f)
        {
            // worst
            barImage.color = fineIndicatorColors[2];
        }
        else if( value < 0.666f)
        {
            barImage.color = fineIndicatorColors[1];
        }
        else
        {
            barImage.color = fineIndicatorColors[0];
        }
    }
}
