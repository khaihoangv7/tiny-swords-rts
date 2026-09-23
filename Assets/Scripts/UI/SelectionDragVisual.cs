using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class SelectionDragVisual : MonoBehaviour
    {
        [SerializeField] private Color fillColor = new Color(0.3f, 1f, 0.45f, 0.12f);
        [SerializeField] private Color borderColor = new Color(0.55f, 1f, 0.62f, 0.9f);
        [SerializeField, Min(1f)] private float borderWidth = 2f;

        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform rectangle;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvasRect = (RectTransform)transform;
            var rectangleObject = new GameObject("SelectionDragRectangle", typeof(RectTransform), typeof(Image));
            rectangle = (RectTransform)rectangleObject.transform;
            rectangle.SetParent(transform, false);
            rectangle.anchorMin = rectangle.anchorMax = new Vector2(0.5f, 0.5f);
            rectangle.pivot = Vector2.zero;
            Image fill = rectangleObject.GetComponent<Image>();
            fill.color = fillColor;
            fill.raycastTarget = false;

            AddBorder("Left", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(borderWidth, 0f));
            AddBorder("Right", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(1f, 0f), new Vector2(borderWidth, 0f));
            AddBorder("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, borderWidth));
            AddBorder("Top", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, borderWidth));
            Hide();
        }

        public void Show(Vector2 startScreen, Vector2 currentScreen)
        {
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, startScreen, uiCamera, out Vector2 start)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, currentScreen, uiCamera, out Vector2 current))
            {
                Hide();
                return;
            }

            rectangle.anchoredPosition = Vector2.Min(start, current);
            rectangle.sizeDelta = new Vector2(Mathf.Abs(current.x - start.x),
                Mathf.Abs(current.y - start.y));
            if (!rectangle.gameObject.activeSelf)
            {
                rectangle.SetAsLastSibling();
                rectangle.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (rectangle != null)
                rectangle.gameObject.SetActive(false);
        }

        private void AddBorder(string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 size)
        {
            var borderObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform border = (RectTransform)borderObject.transform;
            border.SetParent(rectangle, false);
            border.anchorMin = anchorMin;
            border.anchorMax = anchorMax;
            border.pivot = pivot;
            border.anchoredPosition = Vector2.zero;
            border.sizeDelta = size;
            Image image = borderObject.GetComponent<Image>();
            image.color = borderColor;
            image.raycastTarget = false;
        }
    }
}
