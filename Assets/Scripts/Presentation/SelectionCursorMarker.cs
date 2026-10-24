using TinyKingdom.UI;
using TinyKingdom.Vision;
using UnityEngine;

namespace TinyKingdom.Presentation
{
    // Cursor_04 is four corner sprites, not a mouse cursor. This visual has
    // no collider or gameplay role and stays on the logical root at ground level.
    [DisallowMultipleComponent]
    public sealed class SelectionCursorMarker : MonoBehaviour
    {
        private readonly SpriteRenderer[] corners = new SpriteRenderer[4];
        private bool selected;
        private float localY;

        public bool IsConfigured => corners[0] != null;
        public bool IsSelected => selected;
        public float LocalY => localY;

        public bool Configure(float width, float height, float localY,
            SpriteRenderer reference, float cornerScale = 0.9f,
            bool surroundBuilding = false, float localX = 0f)
        {
            Sprite[] sprites = RtsHudAssetCatalog.Load()?.SelectionCorners;
            if (sprites == null || sprites.Length != 4) return false;
            this.localY = localY;
            for (int i = 0; i < corners.Length; i++)
            {
                if (sprites[i] == null) return false;
                if (corners[i] == null)
                {
                    var child = new GameObject("SelectionCursorCorner" + i);
                    child.transform.SetParent(transform, false);
                    corners[i] = child.AddComponent<SpriteRenderer>();
                }
                SpriteRenderer corner = corners[i];
                corner.sprite = sprites[i];
                corner.sortingLayerID = reference != null
                    ? reference.sortingLayerID : 0;
                // The rear pair sits behind the building sprite; the front
                // pair stays in front at its ground contact.
                corner.sortingOrder = reference != null
                    ? reference.sortingOrder + (surroundBuilding && i < 2 ? -1 : 1)
                    : 9;
                corner.transform.localScale = Vector3.one * cornerScale;
                float spriteWidth = sprites[i].rect.width / sprites[i].pixelsPerUnit
                    * cornerScale;
                float spriteHeight = sprites[i].rect.height / sprites[i].pixelsPerUnit
                    * cornerScale;
                float pivotX = sprites[i].pivot.x / sprites[i].pixelsPerUnit
                    * cornerScale;
                float pivotY = sprites[i].pivot.y / sprites[i].pixelsPerUnit
                    * cornerScale;
                bool right = (i & 1) != 0;
                bool top = i < 2;
                corner.transform.localPosition = new Vector3(
                    localX + (right ? width * 0.5f - spriteWidth + pivotX
                        : -width * 0.5f + pivotX),
                    localY + (top ? height * 0.5f - spriteHeight + pivotY
                        : -height * 0.5f + pivotY), 0f);
                corner.enabled = selected;
            }
            GetComponent<FogPresentationGate>()?.RefreshRenderers();
            return true;
        }

        public void SetSelected(bool value)
        {
            selected = value;
            for (int i = 0; i < corners.Length; i++)
                if (corners[i] != null) corners[i].enabled = value;
        }

        private void OnDisable() => SetSelected(false);
    }
}
