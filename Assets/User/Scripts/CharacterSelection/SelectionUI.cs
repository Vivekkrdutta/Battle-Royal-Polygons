using System;
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CharacterSelectScene
{
    public class SelectionUI : MonoBehaviour
    {
        [Header("General Settings")]
        [SerializeField] private TextMeshProUGUI roomInfoText;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private SelectionManager selectionManager;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private GunsListSO GunsListSO;
        [SerializeField] private GunsSelectSingleUI GunsSelectSingleUI;
        [SerializeField] private int numberOfPrefabs= 0;
        [SerializeField] private Transform loadingGameSceneVisual;

        [Header("Gun Status")]
        [SerializeField] private Image gunDetailsBannerImage;
        [SerializeField] private TextMeshProUGUI gunNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI damageText;
        [SerializeField] private TextMeshProUGUI rateOfFireText;
        [SerializeField] private TextMeshProUGUI magazineSizeText;
        [SerializeField] private Image bulletImage;
        [SerializeField] private TextMeshProUGUI rangeText;
        [SerializeField] private TextMeshProUGUI sensitivityText;
        [SerializeField] private TextMeshProUGUI recoilText;
        [SerializeField] private TextMeshProUGUI gunTypeText;
        [SerializeField] private TextMeshProUGUI explosivePowerText;
        [SerializeField] private Transform explosivePowersHolder;
        [SerializeField] private Transform grenadePowerTransform;

        [Header("Banner colors")]
        [SerializeField] private Color[] gunDetailsBannerImageColors;

        [Header("Player's Name Entry")]
        [SerializeField] private TMP_InputField playerNameInputField;
        [SerializeField] private Button saveNameButton;

        [SerializeField] private Transform disconnectedTransform;
        [SerializeField] private Button okOnDisconnectedButton;

        [HideInInspector]
        public int PrefabIndex
        {
            get
            {
                return _prefabIndex;
            }
            set
            {
                _prefabIndex = value;
            }
        }
        [HideInInspector]
        public int FireArmIndex
        {
            get
            {
                return _fireArmIndex;
            }
            set
            {
                _fireArmIndex = value;
                EnableSelectedGunVisual();
            }
        }
        public static SelectionUI Instance { get; private set; }
        private int _fireArmIndex;
        private int _prefabIndex;
        private bool _ready = false;

        private void Awake()
        {
            Instance = this;
            nextButton.onClick.AddListener(() =>
            {
                PrefabIndex = (PrefabIndex + 1) % numberOfPrefabs;
                selectionManager.ChangePlayer(PrefabIndex);
            });
            prevButton.onClick.AddListener(() => 
            {
                PrefabIndex = (PrefabIndex + numberOfPrefabs - 1) % numberOfPrefabs;
                selectionManager.ChangePlayer(PrefabIndex);
            });
            saveNameButton.onClick.AddListener(() =>
            {
                var name = playerNameInputField.text;
                name = name.Trim();
                if(!string.IsNullOrEmpty(name))
                {
                    // change the name
                    selectionManager.ChangeName(name);
                }
                playerNameInputField.name = string.Empty;
            });
            readyButton.onClick.AddListener(() =>
            {
                // Set the ready / unready
                GameMultiPlayer.Instance.SetPlayerReady(GameMultiPlayer.Instance.GetSelfPlayerData(), _ready = !_ready);
                readyButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = _ready ? "Not Ready" : "Ready";
            });
            okOnDisconnectedButton.onClick.AddListener(() =>
            {
                Loader.LoadScene(Loader.Scene.LobbyScene);
            });

            cancelButton.onClick.AddListener(() =>
            {
                try
                {
                    NetworkManager.Singleton.Shutdown();
                }
                catch(Exception ex)
                {
                    Debug.LogException(ex);
                }
                Loader.LoadScene(Loader.Scene.LobbyScene);
            });

            loadingGameSceneVisual.gameObject.SetActive(false);
            disconnectedTransform.gameObject.SetActive(false);

            GameMultiPlayer.Instance.OnLoadingScene += GameMultiplayer_OnLoadingScene;
            GameMultiPlayer.Instance.OnSelfDisconnected += GameMultiplayer_OnSelfDisconnected;
            IntitalizeGunVisuals();
        }

        private void GameMultiplayer_OnSelfDisconnected(object sender, string e)
        {
            disconnectedTransform.gameObject.SetActive(true);
        }

        private void GameMultiplayer_OnLoadingScene(object sender, Loader.Scene e)
        {
            if(e == Loader.Scene.GameScene)
            {
                loadingGameSceneVisual.gameObject.SetActive(true);
            }
        }

        private void IntitalizeGunVisuals()
        {
            // ensure the prefab visual first
            GunsSelectSingleUI.gameObject.SetActive(false);

            foreach (var fireArm in GunsListSO.FireArmsList)
            {
                // instantiate a button
                var gunSO = fireArm.GetGunSO();
                var gunSelectSingleUI = Instantiate(GunsSelectSingleUI, GunsSelectSingleUI.transform.parent); // Under the same parent

                // set the visual and the name
                gunSelectSingleUI.SetVisual(gunSO.GunVisualSprite);
                gunSelectSingleUI.SetName(gunSO.Name);

                // Add the listener
                gunSelectSingleUI.GetButton().onClick.AddListener(() =>
                {
                    var index = GunsListSO.FireArmsList.IndexOf(fireArm);
                    selectionManager.ChangeFireArm(index);
                    FireArmIndex = index;
                    EnableSelectedGunVisual();
                    SetupFireArmDetails(gunSO);
                });

                // activate it
                gunSelectSingleUI.gameObject.SetActive(true);
            }

            StartCoroutine(IntializeGun());
        }

        private IEnumerator IntializeGun()
        {
            yield return new WaitForSecondsRealtime(0.5f);

            var fireArmIndex = GameMultiPlayer.Instance.GetSelfPlayerData().FireArmIndex;
            SetupFireArmDetails(GunsListSO.FireArmsList[fireArmIndex].GetGunSO());
        }


        private void SetupFireArmDetails(GunSO gunSO)
        {
            gunDetailsBannerImage.color = gunDetailsBannerImageColors[(int)gunSO.GunType];

            gunNameText.text = gunSO.FullName;
            descriptionText.text = gunSO.DescriptionOneLine;
            damageText.text = gunSO.DamageAmount.ToString();
            rateOfFireText.text = gunSO.RateOfFire.ToString() + " / sec";
            magazineSizeText.text = gunSO.AmmoCapacity.ToString();
            rangeText.text = gunSO.Range.ToString() + "m";
            sensitivityText.text = gunSO.Sensitivity.ToString() + "%";
            recoilText.text = (gunSO.Recoil + gunSO.Spread).ToString() + "%";
            gunTypeText.text = gunSO.GunType.ToString();

            bulletImage.sprite = gunSO.AmmoVisualSprite;

            grenadePowerTransform.gameObject.SetActive(gunSO.GunType == GunSO.Type.GrenadeLauncher);
            if (gunSO.ExposivePower > 0)
            {
                explosivePowersHolder.gameObject.SetActive(true);
                explosivePowerText.text = gunSO.ExposivePower.ToString();
                return;
            }
            explosivePowersHolder.gameObject.SetActive(false);
        }

        private void EnableSelectedGunVisual()
        {
            foreach(Transform t in GunsSelectSingleUI.transform.parent)
            {
                t.GetComponent<GunsSelectSingleUI>().ActivateBackImage(false);
            }

            // bias of 1 due to prefab being there
            int bias = 1; 

            // Enable the selected one
            GunsSelectSingleUI.transform.parent.GetChild(FireArmIndex + bias)
                .GetComponent<GunsSelectSingleUI>().ActivateBackImage(true);
        }
        public void SetPlayerName(string name)
        {
            playerNameText.text = name;
        }

        public void RoomInfo(string roomName, string joinCode)
        {
            roomInfoText.text = roomName + " : " + joinCode;
        }

        private void OnDestroy()
        {
            GameMultiPlayer.Instance.OnLoadingScene -= GameMultiplayer_OnLoadingScene;
            GameMultiPlayer.Instance.OnSelfDisconnected -= GameMultiplayer_OnSelfDisconnected;
        }
    }
}
