using UnityEngine;

public sealed class RtsCameraController : MonoBehaviour
{
    private Camera controlledCamera;
    private float mapHalfSize;
    private float moveSpeed;
    private float zoomSpeed;
    private float minSize;
    private float maxSize;
    private bool strategicView;
    private Vector3 savedPosition;
    private float savedSize;

    public bool IsStrategicView => strategicView;

    public void Configure(
        Camera targetCamera,
        float mapWorldSize,
        float cameraMoveSpeed,
        float cameraZoomSpeed,
        float minimumSize,
        float initialSize,
        float maximumSize
    )
    {
        controlledCamera = targetCamera;
        mapHalfSize = mapWorldSize / 2f;
        moveSpeed = cameraMoveSpeed;
        zoomSpeed = cameraZoomSpeed;
        minSize = minimumSize;
        maxSize = maximumSize;

        if (controlledCamera == null)
        {
            Debug.LogError("No Main Camera found.");
            return;
        }

        controlledCamera.orthographic = true;
        controlledCamera.transform.position = new Vector3(0f, 0f, -10f);
        controlledCamera.orthographicSize = Mathf.Clamp(initialSize, minSize, maxSize);
        controlledCamera.backgroundColor = new Color(0.08f, 0.08f, 0.09f);
        savedPosition = controlledCamera.transform.position;
        savedSize = controlledCamera.orthographicSize;
    }

    public void Tick(float deltaTime)
    {
        if (controlledCamera == null)
        {
            return;
        }

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 position = controlledCamera.transform.position;
        position += new Vector3(input.x, input.y, 0f).normalized * moveSpeed * deltaTime;

        controlledCamera.orthographicSize = Mathf.Clamp(
            controlledCamera.orthographicSize - Input.mouseScrollDelta.y * zoomSpeed,
            minSize,
            maxSize
        );

        controlledCamera.transform.position = position;
        ClampToMapBounds();
    }

    public void ToggleStrategicView()
    {
        if (controlledCamera == null)
        {
            return;
        }

        strategicView = !strategicView;

        if (strategicView)
        {
            savedPosition = controlledCamera.transform.position;
            savedSize = controlledCamera.orthographicSize;
            float aspect = Mathf.Max(0.01f, controlledCamera.aspect);
            float overviewSize = Mathf.Max(mapHalfSize, mapHalfSize / aspect) * 1.03f;
            controlledCamera.orthographicSize = Mathf.Clamp(overviewSize, minSize, maxSize);
            controlledCamera.transform.position = new Vector3(0f, 0f, -10f);
        }
        else
        {
            controlledCamera.orthographicSize = Mathf.Clamp(savedSize, minSize, maxSize);
            controlledCamera.transform.position = savedPosition;
            ClampToMapBounds();
        }
    }

    public void CenterOnWorld(Vector2 worldPosition)
    {
        if (controlledCamera == null)
        {
            return;
        }

        Vector3 position = controlledCamera.transform.position;
        position.x = worldPosition.x;
        position.y = worldPosition.y;
        position.z = -10f;
        controlledCamera.transform.position = position;
        ClampToMapBounds();
    }

    private void ClampToMapBounds()
    {
        Vector3 position = controlledCamera.transform.position;
        float verticalExtent = controlledCamera.orthographicSize;
        float horizontalExtent = verticalExtent * controlledCamera.aspect;
        float maxX = Mathf.Max(0f, mapHalfSize - horizontalExtent);
        float maxY = Mathf.Max(0f, mapHalfSize - verticalExtent);
        position.x = Mathf.Clamp(position.x, -maxX, maxX);
        position.y = Mathf.Clamp(position.y, -maxY, maxY);
        position.z = -10f;
        controlledCamera.transform.position = position;
    }
}
