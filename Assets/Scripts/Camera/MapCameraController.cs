using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class MapCameraController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float movementSpeed = 12f;
    [SerializeField, Min(0.1f)] private float zoomSpeed = 2f;
    [SerializeField, Min(0.1f)] private float minimumZoom = 5f;
    [SerializeField, Min(0.1f)] private float maximumZoom = TinyKingdom.Terrain.BattlefieldLayout.Height * .5f;
    [SerializeField] private Rect playableBounds = new Rect(0f, 0f, TinyKingdom.Terrain.BattlefieldLayout.Width, TinyKingdom.Terrain.BattlefieldLayout.Height);
    [SerializeField] private bool edgeScrollEnabled = true;
    [SerializeField, Min(1f)] private float edgeScrollThicknessPixels = 16f;

    private Camera mapCamera;

    public float MovementSpeed => movementSpeed;
    public bool EdgeScrollEnabled => edgeScrollEnabled;
    public float EdgeScrollThicknessPixels => edgeScrollThicknessPixels;

    private void Awake()
    {
        mapCamera = GetComponent<Camera>();
        mapCamera.orthographic = true;
        ClampCamera();
    }

    private void LateUpdate()
    {
        Vector2 direction = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed) direction.x -= 1f;
            if (keyboard.rightArrowKey.isPressed) direction.x += 1f;
            if (keyboard.downArrowKey.isPressed) direction.y -= 1f;
            if (keyboard.upArrowKey.isPressed) direction.y += 1f;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (edgeScrollEnabled)
            {
                Vector2 pointer = mouse.position.ReadValue();
                bool insideWindow = pointer.x >= 0f && pointer.x <= Screen.width
                    && pointer.y >= 0f && pointer.y <= Screen.height;
                if (insideWindow)
                {
                    if (pointer.x <= edgeScrollThicknessPixels) direction.x -= 1f;
                    if (pointer.x >= Screen.width - edgeScrollThicknessPixels) direction.x += 1f;
                    if (pointer.y <= edgeScrollThicknessPixels) direction.y -= 1f;
                    if (pointer.y >= Screen.height - edgeScrollThicknessPixels) direction.y += 1f;
                }
            }

            float wheel = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(wheel, 0f))
            {
                mapCamera.orthographicSize = Mathf.Clamp(
                    mapCamera.orthographicSize - wheel / 120f * zoomSpeed,
                    minimumZoom,
                    maximumZoom);
            }
        }

        if (direction.sqrMagnitude > 0f)
            transform.position += (Vector3)(direction.normalized
                * movementSpeed * Time.unscaledDeltaTime);

        ClampCamera();
    }

    private void ClampCamera()
    {
        float halfHeight = mapCamera.orthographicSize;
        float halfWidth = halfHeight * mapCamera.aspect;
        Vector3 position = transform.position;
        position.x = ClampAxis(position.x, playableBounds.xMin, playableBounds.xMax, halfWidth);
        position.y = ClampAxis(position.y, playableBounds.yMin, playableBounds.yMax, halfHeight);
        transform.position = position;
    }

    private static float ClampAxis(float value, float minimum, float maximum, float halfExtent)
    {
        if (maximum - minimum <= 2f * halfExtent)
            return (minimum + maximum) * 0.5f;

        return Mathf.Clamp(value, minimum + halfExtent, maximum - halfExtent);
    }

    private void OnValidate()
    {
        maximumZoom = Mathf.Max(minimumZoom, maximumZoom);
        edgeScrollThicknessPixels = Mathf.Max(1f, edgeScrollThicknessPixels);
    }
}
