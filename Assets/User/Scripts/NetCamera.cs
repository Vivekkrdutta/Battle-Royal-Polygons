
using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Used for setting up the cameras to the target. SetTarget().
/// </summary>
public class NetCamera : MonoBehaviour
{
    [SerializeField] private Type type;
    [SerializeField] private Volume globalVolume;
    [SerializeField] private bool showBlur = true;

    private CinemachineBasicMultiChannelPerlin noise;
    public static NetCamera NormalCamera {  get; private set; }
    public static NetCamera AimCamera { get; private set; }
    private float shakeTime = 0f;

    private static Vignette vignette;

    private static float baseIntensity;
    private static float fadeSpeed;
    private static bool isFlashing;
    public enum Type
    {
        Normal,
        Aim
    }

    private void OnEnable()
    {
        if (this != AimCamera || !showBlur) return;

        if(globalVolume.profile.TryGet(out DepthOfField depthOfField))
        {
            depthOfField.active = true;
        }
    }

    private void OnDisable()
    {
        if (this != AimCamera || !showBlur) return;

        if(globalVolume.profile.TryGet(out DepthOfField depthOfField))
        {
            depthOfField.active = false;
        }
    }

    private void Awake()
    {
        var vcam = GetComponent<CinemachineVirtualCamera>();
        noise = vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        SetupCamera();

        if(globalVolume.profile.TryGet(out Vignette v))
        {
            vignette = v;
            baseIntensity = vignette.intensity.value;
        }
    }

    private void Shake(float intensitiy, float time)
    {
        shakeTime = time;
        noise.m_AmplitudeGain = intensitiy;
    }

    private void Update()
    {
        if(shakeTime > 0f)
        {
            shakeTime -= Time.deltaTime;
            if(shakeTime < 0f)
            {
                noise.m_AmplitudeGain = 0f;
            }
        }

        if (isFlashing)
        {
            vignette.intensity.value = Mathf.MoveTowards(
                vignette.intensity.value,
                baseIntensity,
                fadeSpeed * Time.deltaTime
            );

            if (Mathf.Approximately(vignette.intensity.value, baseIntensity))
            {
                isFlashing = false;
            }
        }
    }
    private void SetupCamera()
    {
        switch (type)
        {
            case Type.Normal:

                NormalCamera = this;
                gameObject.SetActive(true);
                break;

            case Type.Aim:

                AimCamera = this;
                gameObject.SetActive(false);
                break;

            default:
                break;
        }
    }

    public void SetTarget(Transform target)
    {
        var cinemachine = GetComponent<CinemachineVirtualCamera>();
        cinemachine.Follow = target;
    }

    public static void ShakeEffect(float intensitiy = 0.8f, float time = 0.1f)
    {
        NormalCamera.Shake(intensitiy, time);
        AimCamera.Shake(intensitiy, time);
    }

    public static void PlayerDamageEffect(float intensitiy = 0.5f,float duration = 0.2f)
    {
        fadeSpeed = 1f / duration;
        isFlashing = true;

        vignette.intensity.value = intensitiy;
    }
}