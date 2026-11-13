using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    // Presentation only. The Button component owns click and keyboard submission.
    public sealed class MenuButtonFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private Image[] pieces;
        private Sprite[] regular;
        private Sprite[] pressed;
        private Button button;
        private bool highlighted;
        private bool selected;
        private bool down;
        private GameObject selectionAccent;

        public void Configure(Button owner, Image[] images, Sprite[] normal, Sprite[] active)
        {
            button = owner; pieces = images; regular = normal; pressed = active;
            RectTransform accent = RtsUiFactory.Rect("Selected Accent", transform);
            accent.anchorMin = new Vector2(0.2f, 0f);
            accent.anchorMax = new Vector2(0.8f, 0f);
            accent.pivot = new Vector2(0.5f, 0f);
            accent.anchoredPosition = new Vector2(0f, 15f);
            accent.sizeDelta = new Vector2(0f, 5f);
            Image line = accent.gameObject.AddComponent<Image>();
            line.color = new Color(1f, 0.83f, 0.38f);
            line.raycastTarget = false;
            selectionAccent = accent.gameObject;
            Refresh();
        }

        public void SetSelected(bool value) { selected = value; Refresh(); }
        public void OnPointerEnter(PointerEventData data) { highlighted = true; Refresh(); }
        public void OnPointerExit(PointerEventData data) { highlighted = false; down = false; Refresh(); }
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) { down = true; Refresh(); } }
        public void OnPointerUp(PointerEventData data) { down = false; Refresh(); }
        public void OnSelect(BaseEventData data) { highlighted = true; Refresh(); }
        public void OnDeselect(BaseEventData data) { highlighted = false; down = false; Refresh(); }
        private void OnDisable() { highlighted = down = false; Refresh(); }

        private void Refresh()
        {
            if (pieces == null) return;
            if (selectionAccent != null) selectionAccent.SetActive(selected);
            bool active = down || selected;
            Sprite[] sprites = active ? pressed : regular;
            Color tint = button != null && !button.interactable
                ? new Color(0.55f, 0.55f, 0.55f)
                : highlighted ? new Color(1f, 0.94f, 0.76f) : Color.white;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;
                if (sprites != null && i < sprites.Length) pieces[i].sprite = sprites[i];
                pieces[i].color = tint;
            }
        }
    }
}
