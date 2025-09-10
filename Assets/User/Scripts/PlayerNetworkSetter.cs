using StarterAssets;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Must contain ThirdPersonController,ThirdPersonshooter,StarterAssetInputs,PlayerInput
/// </summary>
public class PlayerNetworkSetter : NetworkBehaviour
{
    [SerializeField] private Transform followTransform;

    private StarterAssetsInputs starterAssetsInputs;
    private PlayerInput playerInput;

    private void Awake()
    {
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();
        playerInput = GetComponent<PlayerInput>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // If this is not the Local player, then turn off these components
        if (!IsOwner)
        {
            playerInput.enabled = false;
            starterAssetsInputs.enabled = false;
            Debug.Log("Not the owner");
            return;
        }

        Debug.Log("Setting cameras");
        SetupCameras();
    }

    private void SetupCameras()
    {
        NetCamera.NormalCamera.SetTarget(followTransform);
        NetCamera.AimCamera.SetTarget(followTransform);

        MinimapCameraFollow.Instance.SetTarget(transform);
    }
}
