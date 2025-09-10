
using StarterAssets;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ChatsUI : NetworkBehaviour
{
    [SerializeField] private InputActionAsset uiInputs;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button sendChatButton;

    [SerializeField] private Transform chatsHolder;
    [SerializeField] private ChatBoxSingleUI chatBoxPrefab;

    private readonly List<ChatBoxSingleUI> chatBoxesList = new();
    private InputAction sendAction;

    private struct MessageData : INetworkSerializable
    {
        public FixedString128Bytes SenderName;
        public FixedString512Bytes Message;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SenderName);
            serializer.SerializeValue(ref Message);
        }
    }

    private void Awake()
    {
        sendChatButton.onClick.AddListener(() =>
        {
            var text = chatInputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            PostMessageServerRpc(new MessageData()
            {
                SenderName = GameMultiPlayer.Instance.GetSelfPlayerData().Name,
                Message = text
            });

            chatInputField.text = "";
        });

        chatInputField.onDeselect.AddListener((val) =>
        {
            var localPlayer = Player.PlayersList.Find(player => player.GetComponent<ThirdPersonShooter>().IsLocalPlayer);
            if (localPlayer != null)
            {
                localPlayer.GetComponent<StarterAssetsInputs>().enabled = true;
                localPlayer.GetComponent<PlayerInput>().enabled = true;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        });
    }

    [ServerRpc(RequireOwnership = false)]
    private void PostMessageServerRpc(MessageData messageData)
    {
        PostMessageClientRpc(messageData);
    }

    [ClientRpc(RequireOwnership = true)]
    private void PostMessageClientRpc(MessageData messageData)
    {
        
        for(int i = 3; i < chatBoxesList.Count; i++)
        {
            var box = chatBoxesList[i];
            Destroy(box.gameObject);
        }

        var newBox = Instantiate(chatBoxPrefab, chatsHolder);

        newBox.messageText.text = messageData.Message.ToString();

        newBox.senderNameText.text = messageData.SenderName.ToString();

        newBox.transform.SetSiblingIndex(0);

        chatBoxesList.RemoveAll(box => chatBoxesList.IndexOf(box) >= 3);

        chatBoxesList.Insert(0, newBox);

        newBox.gameObject.SetActive(true);
    }

    private void Start()
    {
        uiInputs.Enable();
        var chatAction = uiInputs.FindAction("UI/Chat");
        chatAction.Enable();
        chatAction.performed += ChatAction_performed;
        sendAction = uiInputs.FindAction("UI/Enter");
        sendAction.Enable();
        sendAction.performed += SendAction_performed;
        chatBoxPrefab.gameObject.SetActive(false);
    }

    private void SendAction_performed(InputAction.CallbackContext obj)
    {
        var text = chatInputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        PostMessageServerRpc(new MessageData()
        {
            SenderName = GameMultiPlayer.Instance.GetSelfPlayerData().Name,
            Message = text
        });

        chatInputField.text = "";
    }

    private void ChatAction_performed(InputAction.CallbackContext obj)
    {
        chatInputField.Select();
        var localPlayer = Player.PlayersList.Find(player => player.GetComponent<ThirdPersonShooter>().IsLocalPlayer);

        if(localPlayer != null)
        {
            // the turn off the starter assets pack
            localPlayer.GetComponent<StarterAssetsInputs>().enabled = false;
            localPlayer.GetComponent<PlayerInput>().enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDisable()
    {
        var chatAction = uiInputs.FindAction("UI/Chat");
        chatAction.performed -= ChatAction_performed;
        sendAction.performed -= SendAction_performed;
        sendAction.Disable();
        chatAction.Disable();
    }
}
