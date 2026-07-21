using System;
using UnityEngine;

internal sealed class EntityPresentationFactory : IDisposable
{
    private readonly PresentationPrefabCatalog catalog;
    private readonly int fallbackTextureSize;
    private Texture2D fallbackCircleTexture;
    private Sprite fallbackCircleSprite;
    private Material fallbackGridLineMaterial;

    public bool UsesPrefabCatalog => catalog != null;

    public EntityPresentationFactory(int circleTextureSize = 128)
    {
        fallbackTextureSize = Mathf.Max(8, circleTextureSize);
        catalog = Resources.Load<PresentationPrefabCatalog>("PresentationPrefabCatalog");
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
        GameObject circleObject = catalog != null && catalog.CircleOverlayPrefab != null
            ? UnityEngine.Object.Instantiate(catalog.CircleOverlayPrefab, parent)
            : new GameObject(objectName);

        if (circleObject.transform.parent != parent)
        {
            circleObject.transform.SetParent(parent);
        }

        circleObject.name = objectName;
        circleObject.transform.position = new Vector3(position.x, position.y, 0f);
        circleObject.transform.localScale = Vector3.one * radius * 2f;

        SpriteRenderer spriteRenderer = circleObject.GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer = circleObject.AddComponent<SpriteRenderer>();
        }

        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = GetFallbackCircleSprite();
        }

        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;
        return circleObject;
    }

    public GameObject CreateLabeledCircle(
        PresentationEntityKind entityKind,
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
        GameObject template = GetEntityPrefab(entityKind);
        GameObject circleObject = template != null
            ? UnityEngine.Object.Instantiate(template, parent)
            : CreateCircle(objectName, position, radius, color, sortingOrder, parent);

        circleObject.name = objectName;
        circleObject.transform.position = new Vector3(position.x, position.y, 0f);
        circleObject.transform.localScale = Vector3.one * radius * 2f;
        SpriteRenderer spriteRenderer = circleObject.GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer = circleObject.AddComponent<SpriteRenderer>();
        }

        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = GetFallbackCircleSprite();
        }

        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;

        TextMesh textMesh = circleObject.GetComponentInChildren<TextMesh>();

        if (textMesh == null)
        {
            textMesh = CreateWorldLabel(circleObject.transform);
        }

        textMesh.text = label;
        textMesh.color = labelColor;
        return circleObject;
    }

    public void CreateGridLine(Vector3 start, Vector3 end, Transform parent)
    {
        GameObject lineObject = catalog != null && catalog.GridLinePrefab != null
            ? UnityEngine.Object.Instantiate(catalog.GridLinePrefab, parent)
            : new GameObject("GridLine");

        if (lineObject.transform.parent != parent)
        {
            lineObject.transform.SetParent(parent);
        }

        lineObject.name = "GridLine";
        LineRenderer lineRenderer = lineObject.GetComponent<LineRenderer>();

        if (lineRenderer == null)
        {
            lineRenderer = lineObject.AddComponent<LineRenderer>();
        }

        if (lineRenderer.sharedMaterial == null)
        {
            lineRenderer.sharedMaterial = GetFallbackGridLineMaterial();
        }
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
        Release(fallbackCircleSprite);
        Release(fallbackCircleTexture);
        Release(fallbackGridLineMaterial);
    }

    private GameObject GetEntityPrefab(PresentationEntityKind entityKind)
    {
        if (catalog == null)
        {
            return null;
        }

        switch (entityKind)
        {
            case PresentationEntityKind.PlayerBase:
                return catalog.PlayerBasePrefab;
            case PresentationEntityKind.EnemyBase:
                return catalog.EnemyBasePrefab;
            case PresentationEntityKind.Factory:
                return catalog.FactoryPrefab;
            case PresentationEntityKind.PlayerInfantry:
                return catalog.PlayerInfantryPrefab;
            case PresentationEntityKind.EnemyInfantry:
                return catalog.EnemyInfantryPrefab;
            default:
                return null;
        }
    }

    private static TextMesh CreateWorldLabel(Transform parent)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent);
        labelObject.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        labelObject.transform.localScale = Vector3.one * 0.08f;

        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 48;

        MeshRenderer meshRenderer = labelObject.GetComponent<MeshRenderer>();
        meshRenderer.sortingOrder = 40;
        return textMesh;
    }

    private Sprite GetFallbackCircleSprite()
    {
        if (fallbackCircleSprite != null)
        {
            return fallbackCircleSprite;
        }

        fallbackCircleTexture = CreateCircleTexture(fallbackTextureSize);
        fallbackCircleSprite = Sprite.Create(
            fallbackCircleTexture,
            new Rect(0, 0, fallbackCircleTexture.width, fallbackCircleTexture.height),
            new Vector2(0.5f, 0.5f),
            fallbackCircleTexture.width
        );
        return fallbackCircleSprite;
    }

    private Material GetFallbackGridLineMaterial()
    {
        if (fallbackGridLineMaterial == null)
        {
            fallbackGridLineMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        return fallbackGridLineMaterial;
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
