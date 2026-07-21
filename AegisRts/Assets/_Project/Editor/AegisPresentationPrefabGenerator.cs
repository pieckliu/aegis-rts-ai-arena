using System.IO;
using UnityEditor;
using UnityEngine;

public static class AegisPresentationPrefabGenerator
{
    private const string ArtDirectory = "Assets/_Project/Art/Generated";
    private const string PrefabDirectory = "Assets/_Project/Prefabs/Presentation";
    private const string ResourceDirectory = "Assets/_Project/Resources";
    private const string MaterialDirectory = "Assets/_Project/Materials";
    private const string CircleAssetPath = ArtDirectory + "/Circle.png";
    private const string CatalogAssetPath = ResourceDirectory + "/PresentationPrefabCatalog.asset";
    private const string GridMaterialPath = MaterialDirectory + "/GridLine.mat";

    [MenuItem("Aegis RTS/Generate Presentation Prefabs")]
    public static void Generate()
    {
        EnsureDirectory(ArtDirectory);
        EnsureDirectory(PrefabDirectory);
        EnsureDirectory(ResourceDirectory);
        EnsureDirectory(MaterialDirectory);

        Sprite circleSprite = GenerateCircleSprite();
        Material gridMaterial = GetOrCreateGridMaterial();
        PresentationPrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<PresentationPrefabCatalog>(
            CatalogAssetPath
        );

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<PresentationPrefabCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        catalog.PlayerBasePrefab = CreateLabeledCirclePrefab(
            "PlayerBaseView",
            circleSprite,
            new Color(0.25f, 0.55f, 1f, 1f),
            20,
            "基地",
            Color.white
        );
        catalog.EnemyBasePrefab = CreateLabeledCirclePrefab(
            "EnemyBaseView",
            circleSprite,
            new Color(1f, 0.25f, 0.25f, 1f),
            20,
            "AI",
            Color.white
        );
        catalog.FactoryPrefab = CreateLabeledCirclePrefab(
            "FactoryView",
            circleSprite,
            new Color(0.35f, 0.9f, 0.45f, 1f),
            20,
            "兵厂",
            Color.black
        );
        catalog.PlayerInfantryPrefab = CreateLabeledCirclePrefab(
            "PlayerInfantryView",
            circleSprite,
            new Color(0.95f, 0.95f, 0.25f, 1f),
            25,
            "兵",
            Color.black
        );
        catalog.EnemyInfantryPrefab = CreateLabeledCirclePrefab(
            "EnemyInfantryView",
            circleSprite,
            new Color(1f, 0.45f, 0.15f, 1f),
            25,
            "敌",
            Color.black
        );
        catalog.CircleOverlayPrefab = CreateCirclePrefab("CircleOverlayView", circleSprite);
        catalog.GridLinePrefab = CreateGridLinePrefab(gridMaterial);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Aegis RTS presentation prefabs generated successfully.");
    }

    private static Sprite GenerateCircleSprite()
    {
        const int size = 128;
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
                float alpha = Vector2.Distance(new Vector2(x, y), center) <= radius ? 1f : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        string absolutePath = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            CircleAssetPath
        );
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(CircleAssetPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(CircleAssetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = size;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(CircleAssetPath);
    }

    private static Material GetOrCreateGridMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(GridMaterialPath);

        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find("Sprites/Default"));
        AssetDatabase.CreateAsset(material, GridMaterialPath);
        return material;
    }

    private static GameObject CreateLabeledCirclePrefab(
        string prefabName,
        Sprite sprite,
        Color color,
        int sortingOrder,
        string label,
        Color labelColor
    )
    {
        GameObject root = CreateCircleTemplate(prefabName, sprite, color, sortingOrder);
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(root.transform);
        labelObject.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        labelObject.transform.localScale = Vector3.one * 0.08f;

        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = label;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 48;
        textMesh.color = labelColor;
        labelObject.GetComponent<MeshRenderer>().sortingOrder = 40;

        return SavePrefab(root, prefabName);
    }

    private static GameObject CreateCirclePrefab(string prefabName, Sprite sprite)
    {
        GameObject root = CreateCircleTemplate(prefabName, sprite, Color.white, 0);
        return SavePrefab(root, prefabName);
    }

    private static GameObject CreateCircleTemplate(
        string objectName,
        Sprite sprite,
        Color color,
        int sortingOrder
    )
    {
        GameObject root = new GameObject(objectName);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return root;
    }

    private static GameObject CreateGridLinePrefab(Material material)
    {
        GameObject root = new GameObject("GridLineView");
        LineRenderer renderer = root.AddComponent<LineRenderer>();
        renderer.sharedMaterial = material;
        renderer.positionCount = 2;
        renderer.startWidth = 0.025f;
        renderer.endWidth = 0.025f;
        renderer.startColor = new Color(1f, 1f, 1f, 0.15f);
        renderer.endColor = new Color(1f, 1f, 1f, 0.15f);
        renderer.sortingOrder = -10;
        return SavePrefab(root, "GridLineView");
    }

    private static GameObject SavePrefab(GameObject root, string prefabName)
    {
        string path = PrefabDirectory + "/" + prefabName + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void EnsureDirectory(string assetPath)
    {
        string[] parts = assetPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
