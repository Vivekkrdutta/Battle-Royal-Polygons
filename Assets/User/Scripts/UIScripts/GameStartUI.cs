using UnityEngine;
using UnityEngine.InputSystem;

public class GameStartUI : MonoBehaviour
{
    [SerializeField] private InputActionAsset uiInputAsset;

    private void Start()
    {
        uiInputAsset.Enable();
        uiInputAsset.FindAction("UI/Interact").Enable();
        uiInputAsset.FindAction("UI/Interact").performed += InteractAction_performed;
    }

    private void InteractAction_performed(InputAction.CallbackContext obj)
    {
        Hide();
    }

    private void Hide()
    {
        uiInputAsset.FindAction("UI/Interact").Disable();
        uiInputAsset.FindAction("UI/Interact").performed -= InteractAction_performed;
        gameObject.SetActive(false);
    }
}
