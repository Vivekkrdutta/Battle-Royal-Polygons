using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CharacterSelectScene
{
    public class GunsSelectSingleUI : MonoBehaviour
    {
        [SerializeField] private Image BackImage;
        [SerializeField] private Image VisualImage;
        [SerializeField] private Button Button;
        [SerializeField] private TextMeshProUGUI Text;

        public Button GetButton() => Button;
        public void SetName(string name) => Text.text = name;
        public void SetVisual(Sprite sprite) => VisualImage.sprite = sprite;
        public void ActivateBackImage(bool val) => BackImage.gameObject.SetActive(val);
    }
}
