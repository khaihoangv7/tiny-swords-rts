using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class MapCameraController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float movementSpeed = 12f;
    [SerializeField, Min(0.1f)] private float zoomSpeed = 2f;
    [SerializeField, Min(0.1f)] private float minimumZoom = 5f;
    [SerializeField, Min(0.1f)] private float maximumZoom = 14f;
    [SerializeField] private Rect playableBounds = new Rect(0f, 0f, 40f, 28f);

    private Camera mapCamera;

    private void Awake()
    {
        mapCamera = GetComponent<Camera>();
        mapCamera.orthographic = true;
        ClampCamera();
    }

    private void LateUpdate()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            Vector2 direction = Vector2.zero;
            if (keyboard.aKey.isPressed) direction.x -= 1f;
            if (keyboard.dKey.isPressed) direction.x += 1f;
            if (keyboard.sKey.isPressed) direction.y -= 1f;
            if (keyboard.wKey.isPressed) direction.y += 1f;

            transform.position += (Vector3)(direction.normalized * movementSpeed * Time.unscaledDeltaTime);
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(wheel, 0f))
            {
                mapCamera.orthographicSize = Mathf.Clamp(
                    mapCamera.orthographicSize - wheel / 120f * zoomSpeed,
                    minimumZoom,
                    maximumZoom);
            }
        }

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
    }
}
