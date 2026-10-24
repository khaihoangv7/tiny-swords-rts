using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    // The scene contains only a camera, EventSystem and this component.
    public sealed class MapSelectionMenu : MonoBehaviour
    {
        private void Start()
        {
            var canvasObject = new GameObject("Map Selection Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform background = Rect("Background", canvasObject.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            background.gameObject.AddComponent<Image>().color = new Color(0.055f, 0.105f, 0.12f);

            Label("Title", background, "TINY KINGDOM", 54, new Vector2(0f, 228f),
                new Vector2(700f, 85f), new Color(1f, 0.89f, 0.61f));
            Label("Subtitle", background, "CHOOSE BATTLEFIELD", 23,
                new Vector2(0f, 168f), new Vector2(700f, 46f), Color.white);

            Card(background, "TWO KINGDOMS", "The original island battlefield",
                "Map", new Vector2(-410f, -16f), new Color(0.19f, 0.37f, 0.27f));
            Card(background, "STRAIGHT LANE", "Two bases connected by one straight road",
                "LinearLane", new Vector2(0f, -16f), new Color(0.18f, 0.31f, 0.43f));
            Card(background, "GOBLIN ISLAND", "Three bridges and a guarded island mine",
                "GoblinIsland", new Vector2(410f, -16f), new Color(0.24f, 0.32f, 0.30f));
            Label("Hint", background, "Blue versus Red  •  One Castle and three Pawns per side",
                17, new Vector2(0f, -245f), new Vector2(900f, 42f),
                new Color(0.79f, 0.84f, 0.82f));
        }

        private static void Card(Transform parent, string title, string description,
            string sceneName, Vector2 position, Color color)
        {
            RectTransform rect = Rect(title, parent, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), position, new Vector2(380f, 245f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            rect.gameObject.AddComponent<MapSelectionCard>().Initialize(sceneName, image);
            Label("Map Name", rect, title, 26, new Vector2(0f, 48f),
                new Vector2(350f, 54f), Color.white);
            Label("Description", rect, description, 18, new Vector2(0f, -23f),
                new Vector2(350f, 64f), new Color(0.9f, 0.94f, 0.87f));
            Label("Action", rect, "CLICK TO PLAY", 17, new Vector2(0f, -79f),
                new Vector2(300f, 42f), new Color(1f, 0.88f, 0.58f));
        }

        private static Text Label(string name, Transform parent, string value, int size,
            Vector2 position, Vector2 dimensions, Color color)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), position, dimensions);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = value;
            UiLanguage.Bind(text);
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min,
            Vector2 max, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
