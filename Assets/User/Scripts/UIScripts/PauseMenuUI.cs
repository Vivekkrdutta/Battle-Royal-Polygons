using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI volumeText;
    [SerializeField] private TextMeshProUGUI musicVolumeText;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button audioPlusButton;
    [SerializeField] private Button audioMinusButton;
    [SerializeField] private Button musicPlusButton;
    [SerializeField] private Button musicMinusButton;

    [SerializeField] private InputActionAsset uiInputActionAsset;

    private void Awake()
    {
        quitButton.onClick.AddListener(() => 
        {
            // will include quit logic
            NetworkManager.Singleton.Shutdown();
            Loader.LoadScene(Loader.Scene.LobbyScene);
        });

        audioPlusButton.onClick.AddListener(() => 
        {
            GameProperties.VolumeScale = Mathf.Clamp01(GameProperties.VolumeScale + 0.1f);
            volumeText.text = ((int)(GameProperties.VolumeScale * 10f)).ToString();
        });

        audioMinusButton.onClick.AddListener(() => 
        {
            GameProperties.VolumeScale = Mathf.Clamp01(GameProperties.VolumeScale - 0.1f);
            volumeText.text = ((int)(GameProperties.VolumeScale * 10f)).ToString();
        });

        musicPlusButton.onClick.AddListener(() =>
        {
            var currentVolume = GameManager.Instance.GetComponent<AudioSource>().volume;
            GameManager.Instance.GetComponent<AudioSource>().volume = Mathf.Clamp01(currentVolume + 0.1f);
            musicVolumeText.text = ((int)(GameManager.Instance.GetComponent<AudioSource>().volume * 10f)).ToString();
        });

        musicMinusButton.onClick.AddListener(() =>
        {
            var currentVolume = GameManager.Instance.GetComponent<AudioSource>().volume;
            GameManager.Instance.GetComponent<AudioSource>().volume = Mathf.Clamp01(currentVolume - 0.1f);
            musicVolumeText.text = ((int)(GameManager.Instance.GetComponent<AudioSource>().volume * 10f)).ToString();
        });

        volumeText.text = ((int)(GameProperties.VolumeScale * 10f)).ToString();
        musicVolumeText.text = ((int)(GameManager.Instance.GetComponent<AudioSource>().volume * 10f)).ToString();
    }

    private void Start()
    {
        var pausAction = uiInputActionAsset.FindAction("UI/Pause");
        if (pausAction != null)
        {
            Debug.Log("Pause action found");
            uiInputActionAsset.FindActionMap("UI").Enable();
            pausAction.Enable();
            pausAction.performed += PausAction_performed;
        }
        Hide();
    }

    private void PausAction_performed(InputAction.CallbackContext obj)
    {
        Debug.Log("Pause action performed");
        if (gameObject.activeSelf)
        {
            Hide();
            return;
        }

        Show();
    }

    private void Hide()
    {
        gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    private void Show()
    {
        volumeText.text = ((int)(GameProperties.VolumeScale * 10f)).ToString();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        gameObject.SetActive(true);
    }
}
