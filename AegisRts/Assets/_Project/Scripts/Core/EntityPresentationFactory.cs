using System;
using UnityEngine;

internal sealed class EntityPresentationFactory : IDisposable
{
    private readonly Texture2D circleTexture;
    private readonly Sprite circleSprite;
    private readonly Material gridLineMaterial;

    public EntityPresentationFactory(int circleTextureSize = 128)
    {
        circleTexture = CreateCircleTexture(Mathf.Max(8, circleTextureSize));
        circleSprite = Sprite.Create(
            circleTexture,
            new Rect(0, 0, circleTexture.width, circleTexture.height),
            new Vector2(0.5f, 0.5f),
            circleTexture.width
        );
        gridLineMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    public GameObject CreateCircle(
        string objectName,
        Vector2 position,
        float radius,
        Color color,
        int sortingOrder,
        Transform parent
    )
    {
        GameObject circleObject = new GameObject(objectName);
        circleObject.transform.SetParent(parent);
        circleObject.transform.position = new Vector3(position.x, position.y, 0f);
        circleObject.transform.localScale = Vector3.one * radius * 2f;

        SpriteRenderer spriteRenderer = circleObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = circleSprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;
        return circleObject;
    }

    public GameObject CreateLabeledCircle(
        string objectName,
        Vector2 position,
        float radius,
        Color color,
        int sortingOrder,
        Transform parent,
        string label,
        Color labelColor
    )
    {
        GameObject circleObject = CreateCircle(
            objectName,
            position,
            radius,
            color,
            sortingOrder,
            parent
        );
        CreateWorldLabel(circleObject.transform, label, labelColor);
        return circleObject;
    }

    public void CreateGridLine(Vector3 start, Vector3 end, Transform parent)
    {
        GameObject lineObject = new GameObject("GridLine");
        lineObject.transform.SetParent(parent);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.sharedMaterial = gridLineMaterial;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        lineRenderer.startWidth = 0.025f;
        lineRenderer.endWidth = 0.025f;
        lineRenderer.startColor = new Color(1f, 1f, 1f, 0.15f);
        lineRenderer.endColor = new Color(1f, 1f, 1f, 0.15f);
        lineRenderer.sortingOrder = -10;
    }

    public void SetCircleColor(GameObject circleObject, Color color)
    {
        if (circleObject != null &&
            circleObject.TryGetComponent(out SpriteRenderer spriteRenderer))
        {
            spriteRenderer.color = color;
        }
    }

    public void Dispose()
    {
        Release(circleSprite);
        Release(circleTexture);
        Release(gridLineMaterial);
    }

    private static void CreateWorldLabel(Transform parent, string text, Color color)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent);
        labelObject.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        labelObject.transform.localScale = Vector3.one * 0.08f;

        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 48;
        textMesh.color = color;

        MeshRenderer meshRenderer = labelObject.GetComponent<MeshRenderer>();
        meshRenderer.sortingOrder = 40;
    }

    private static Texture2D CreateCircleTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear
        };
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = distance <= radius ? 1f : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private static void Release(UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(asset);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
