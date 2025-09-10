
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public abstract class InteractableObject : NetworkBehaviour
{
    [SerializeField] private float rotateSpeed = 5f;
    [SerializeField] protected Image iconImage;
    [SerializeField] private LookAt lookAt;
    [SerializeField] protected Transform uiElementsCanvas;
    [SerializeField] protected bool selfRotateOnPlayerEnter;
    [SerializeField] private float distructionWaitTime;

    public static event EventHandler OnDestroyed;
    public enum LookAt
    {
        None,
        CameraYAxisOnly,
        CameraNoRestriction,
    }
    protected bool isUsable = true;
    public abstract void Interact(GunMan interactor);
    public bool IsUsable => isUsable;
    protected bool autoSetShowHideIcon = true;
    private float prevYDistance = 0f;
    protected bool startRotate = false;
    protected virtual void Awake()
    {
        prevYDistance = transform.position.y;
    }
    protected virtual void Start()
    {
        if (autoSetShowHideIcon)
        {
            HideIconsImage();
        }
    }
    protected void HideIconsImage()
    {
        iconImage.gameObject.SetActive(false);
    }
    protected void ShowIconsImage()
    {
        iconImage.gameObject.SetActive(true);
    }
    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!isUsable) return;

        if(other.TryGetComponent(out GunMan gunMan) && gunMan.IsLocalPlayer)
        {
            gunMan.SetNearByInteractable(this);
            if(autoSetShowHideIcon) ShowIconsImage();
            startRotate = true;
        }
    }
    protected virtual void OnTriggerExit(Collider other)
    {
        if (!isUsable) return;

        if(other.TryGetComponent(out GunMan gunMan) && gunMan.IsLocalPlayer)
        {
            gunMan.SetNearByInteractable(null);
            if(autoSetShowHideIcon) HideIconsImage();
            startRotate=false;
            transform.position = new Vector3(transform.position.x, prevYDistance, transform.position.z);
        }
    }

    protected virtual void Update()
    {
        if (selfRotateOnPlayerEnter && startRotate)
        {
            transform.position = new Vector3(transform.position.x, prevYDistance + 0.3f, transform.position.z);
            var eulerAngles = transform.eulerAngles;

            // slow rotation
            transform.rotation = Quaternion.Euler(0f, eulerAngles.y + rotateSpeed * Time.deltaTime, 0f);
        }

        switch (lookAt)
        {
            case LookAt.CameraYAxisOnly:

                var lookAtPos = GetCameraOpposite();
                // keep the y level same
                lookAtPos.y = uiElementsCanvas.transform.position.y;

                uiElementsCanvas.transform.LookAt(lookAtPos);
                break;

            case LookAt.CameraNoRestriction:

                uiElementsCanvas.transform.LookAt(GetCameraOpposite());
                break;

            default:
                break;
        }
    }
    private Vector3 GetCameraOpposite()
    {
        var distance = 10f;
        var dirFromCam = (uiElementsCanvas.transform.position - Camera.main.transform.position).normalized;
        var lookAtPos = uiElementsCanvas.transform.position + dirFromCam * distance;
        return lookAtPos;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        OnDestroyed?.Invoke(this, null);
    }

    [ServerRpc(RequireOwnership = false)]
    protected void DestroyServerRpc()
    {
        StartCoroutine(StartDestruction());
    }
    private IEnumerator StartDestruction()
    {
        yield return new WaitForSecondsRealtime(distructionWaitTime);
        GetComponent<NetworkObject>().Despawn(destroy: true);
    }
}