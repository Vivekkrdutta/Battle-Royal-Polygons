
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Class is made to hold and store the informations about the players game play informations
/// </summary>
public class KillerSingleUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI killsCount;
    [SerializeField] private TextMeshProUGUI deathsCount;
    [SerializeField] private Transform deadVisual;
    [HideInInspector] public ulong PlayerId;
    [SerializeField] private Image sliderImage;

    [SerializeField] private Transform winnerVisual;

    public void SetName(string name)
    {
        nameText.text = name;
    }
    public void SetKIllsCount(int killsCount,bool showKillsText = false)
    {
        this.killsCount.text = (showKillsText ? "Kills " : "") + killsCount.ToString();
        if (sliderImage) sliderImage.fillAmount = (float)killsCount / GameProperties.WinAtKills;
    }
    public void SetDeathsCount(int deathsCount)
    {
       if(this.deathsCount) this.deathsCount.text = deathsCount.ToString();
    }
    public void SetBackGround(Color backGround)
    {
        GetComponent<Image>().color = backGround;
    }
    private void Awake()
    {
        HideDeadVisual();
    }
    public void ShowDeadVisual()
    {
        if(deadVisual != null)
        deadVisual.gameObject.SetActive(true);
    }
    public void HideDeadVisual()
    {
        if(deadVisual != null)
        deadVisual.gameObject.SetActive(false);
    }

    public void ShowWinner()
    {
        winnerVisual.gameObject.SetActive(true);
    }
}
