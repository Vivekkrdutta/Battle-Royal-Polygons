
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class CharacterSingle : MonoBehaviour
{
    [SerializeField] private string playerName;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Transform readyTransform;
    [SerializeField] private Transform youTransform;
    [SerializeField] private Transform gunsHolder;
    [SerializeField] private Transform handPlaceIKTarget;

    private FireArm fireArm;
    public void SetName(string name)
    {
        nameText.text = name;
        var parent = nameText.transform.parent;
        var dir = (Camera.main.transform.position - parent.position).normalized;
        parent.rotation = Quaternion.LookRotation(-dir);
    }
    public void Show(bool islocalplayer)
    {
        gameObject.SetActive(true);
        youTransform.gameObject.SetActive(islocalplayer);
    }
    private void Awake()
    {
        youTransform.gameObject.SetActive(false);
        readyTransform.gameObject.SetActive(false);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }
    public Transform GetGunsHolder()
    {
        return gunsHolder;
    }
    public void SetFireArm(FireArm fireArmPrefab)
    {
        if(fireArm) Destroy(fireArm.gameObject);

        fireArm = Instantiate(fireArmPrefab, gunsHolder, false);
        fireArm.SetupOrientation();
        fireArm.Show();

        var handPlace = fireArm.GetGunSO().LeftHandPlace;
        handPlaceIKTarget.SetLocalPositionAndRotation(handPlace.LocalPosition, Quaternion.Euler(handPlace.LocalRotation));
    }
    public void SetReady(bool ready)
    {
        readyTransform.gameObject.SetActive(ready);
    }
    public string GetPlayerName()
    {
        return playerName;
    }
}
