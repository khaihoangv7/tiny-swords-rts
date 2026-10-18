using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    public sealed class MapSelectionCard : MonoBehaviour, IPointerClickHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        private string sceneName;
        private Image image;
        private Color normalColor;

        public void Initialize(string targetScene, Image targetImage)
        {
            sceneName = targetScene;
            image = targetImage;
            normalColor = image.color;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Select();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (image != null) image.color = Color.Lerp(normalColor, Color.white, 0.18f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (image != null) image.color = normalColor;
        }

        public void Select()
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
