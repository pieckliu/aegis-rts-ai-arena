using System.Collections.Generic;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    private enum GameState
    {
        MainMenu,
        Playing
    }

    private enum BuildingType
    {
        None,
        Factory
    }

    private enum UnitType
    {
        Infantry
    }

    [Header("Map Settings")]
    [SerializeField] private int mapSize = 32;
    [SerializeField] private float cellSize = 1f;

    [Header("Base Settings")]
    [SerializeField] private float baseRadius = 0.45f;
    [SerializeField] private float buildRadius = 7f;

    [Header("Building Settings")]
    [SerializeField] private float buildingRadius = 0.42f;

    [Header("Resource Settings")]
    [SerializeField] private int startingResources = 500;
    [SerializeField] private int factoryCost = 150;
    [SerializeField] private int infantryCost = 50;

    [Header("Unit Settings")]
    [SerializeField] private float infantryRadius = 0.42f;

    private int playerResources;

    private class BuildingData
    {
        public string DisplayName;
        public BuildingType Type;
        public GameObject GameObject;
        public Vector2 Position;
        public Vector2Int Cell;
        public float Radius;
        public string Description;

        public BuildingData(
            string displayName,
            BuildingType type,
            GameObject gameObject,
            Vector2 position,
            Vector2Int cell,
            float radius,
            string description
        )
        {
            DisplayName = displayName;
            Type = type;
            GameObject = gameObject;
            Position = position;
            Cell = cell;
            Radius = radius;
            Description = description;
        }
    }

    private class UnitData
    {
        public string DisplayName;
        public UnitType Type;
        public GameObject GameObject;
        public Vector2 Position;
        public Vector2Int Cell;
        public float Radius;

        public UnitData(
            string displayName,
            UnitType type,
            GameObject gameObject,
            Vector2 position,
            Vector2Int cell,
            float radius
        )
        {
            DisplayName = displayName;
            Type = type;
            GameObject = gameObject;
            Position = position;
            Cell = cell;
            Radius = radius;
        }
    }

    private GameState gameState = GameState.MainMenu;
    private BuildingType selectedBuilding = BuildingType.None;

    private Camera mainCamera;

    private Transform gridRoot;
    private Transform buildingRoot;

    private Sprite circleSprite;
    private Texture2D menuBackgroundTexture;

    private GameObject baseObject;
    private GameObject buildRangeObject;
    private GameObject placementPreviewObject;

    private GameObject selectionRingObject;

    private Vector2 basePosition;
    private Vector2 currentPreviewPosition;
    private Vector2Int currentPreviewCell;

    private bool isBuildMenuOpen = false;
    private bool hasPreviewCell = false;
    private bool gameWorldCreated = false;

    private readonly List<BuildingData> buildings = new List<BuildingData>();
    private BuildingData selectedBuildingData = null;

    private readonly List<UnitData> units = new List<UnitData>();

    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

    private void Awake()
    {
        mainCamera = Camera.main;

        circleSprite = CreateCircleSprite(128);
        menuBackgroundTexture = CreateMenuBackgroundTexture();

        SetupCamera();
    }

    private void Update()
    {
        if (gameState != GameState.Playing)
        {
            return;
        }

        HandleSelectionInput();
        HandlePlacementPreview();
        HandlePlacementConfirm();
    }

    private void OnGUI()
    {
        if (gameState == GameState.MainMenu)
        {
            DrawMainMenu();
        }
        else
        {
            DrawGameUI();
        }
    }

    private void DrawMainMenu()
    {
        GUI.DrawTexture(
            new Rect(0, 0, Screen.width, Screen.height),
            menuBackgroundTexture,
            ScaleMode.StretchToFill
        );

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 42,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;

        GUI.Label(
            new Rect(0, Screen.height * 0.25f, Screen.width, 80),
            "Aegis RTS AI Arena",
            titleStyle
        );

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 28
        };

        Rect startButtonRect = new Rect(
            (Screen.width - 220) / 2f,
            Screen.height * 0.5f,
            220,
            70
        );

        if (GUI.Button(startButtonRect, "开始", buttonStyle))
        {
            StartGame();
        }
    }

    private void DrawGameUI()
    {
        float panelWidth = 220f;
        float panelHeight = 450f;
        float panelX = Screen.width - panelWidth - 20f;
        float panelY = 20f;

        GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), "建造菜单");

        Rect dropdownRect = new Rect(panelX + 15f, panelY + 40f, panelWidth - 30f, 35f);

        if (GUI.Button(dropdownRect, isBuildMenuOpen ? "建筑菜单 ▲" : "建筑菜单 ▼"))
        {
            isBuildMenuOpen = !isBuildMenuOpen;
        }

        if (isBuildMenuOpen)
        {
            Rect factoryRect = new Rect(panelX + 15f, panelY + 80f, panelWidth - 30f, 35f);

            if (GUI.Button(factoryRect, $"兵厂 ({factoryCost})"))
            {
                SelectFactory();
                isBuildMenuOpen = false;
            }
        }

        string selectedText = selectedBuilding == BuildingType.Factory
            ? "当前：兵厂"
            : "当前：无";

        GUI.Label(new Rect(panelX + 15f, panelY + 130f, panelWidth - 30f, 25f), selectedText);

        if (selectedBuilding != BuildingType.None)
        {
            Rect cancelRect = new Rect(panelX + 15f, panelY + 165f, panelWidth - 30f, 35f);

            if (GUI.Button(cancelRect, "取消建造"))
            {
                CancelBuildMode();
            }
        }

        GUI.Box(new Rect(panelX + 15f, panelY + 215f, panelWidth - 30f, 115f), "建筑信息");

        if (selectedBuildingData != null)
        {
            GUI.Label(
                new Rect(panelX + 25f, panelY + 245f, panelWidth - 50f, 25f),
                "选中：" + selectedBuildingData.DisplayName
            );

            GUI.Label(
                new Rect(panelX + 25f, panelY + 270f, panelWidth - 50f, 55f),
                selectedBuildingData.Description
            );
        }
        else
        {
            GUI.Label(
                new Rect(panelX + 25f, panelY + 250f, panelWidth - 50f, 25f),
                "未选中建筑"
            );
        }

        GUI.Box(new Rect(panelX + 15f, panelY + 340f, panelWidth - 30f, 80f), "生产单位");

        if (selectedBuildingData != null && selectedBuildingData.Type == BuildingType.Factory)
        {
            if (GUI.Button(
                    new Rect(panelX + 25f, panelY + 370f, panelWidth - 50f, 35f),
                    $"生产步兵 ({infantryCost})"
                ))
            {
                TryTrainInfantry(selectedBuildingData);
            }
        }
        else
        {
            GUI.Label(
                new Rect(panelX + 25f, panelY + 370f, panelWidth - 50f, 35f),
                "请选择兵厂"
            );
        }
        
        GUI.Label(
            new Rect(20, 20, 650, 30),
            "操作提示：点击右侧“建筑菜单” → 选择“兵厂” → 移动鼠标预览 → 鼠标右键确认建造。"
        );

        GUI.Label(
        new Rect(20, 50, 650, 30),
        $"资源：{playerResources}    兵厂成本：{factoryCost}    步兵成本：{infantryCost}"
);
    }

    private void StartGame()
    {
        gameState = GameState.Playing;

        if (!gameWorldCreated)
        {
            CreateGameWorld();
            gameWorldCreated = true;
        }

        Debug.Log("Game started.");
    }

    private void CreateGameWorld()
    {
        playerResources = startingResources;

        gridRoot = new GameObject("GridRoot").transform;
        buildingRoot = new GameObject("BuildingRoot").transform;

        CreateGrid();
        CreateBase();
        CreateBuildRangeObject();
        CreatePlacementPreviewObject();
        CreateSelectionRingObject();

        Debug.Log($"Starting resources: {playerResources}");
    }

    private void SetupCamera()
    {
        if (mainCamera == null)
        {
            Debug.LogError("No Main Camera found.");
            return;
        }

        mainCamera.orthographic = true;
        mainCamera.transform.position = new Vector3(0, 0, -10);
        mainCamera.orthographicSize = 18f;
        mainCamera.backgroundColor = new Color(0.08f, 0.08f, 0.09f);
    }

    private void CreateGrid()
    {
        float half = GetMapHalfSize();

        Material lineMaterial = new Material(Shader.Find("Sprites/Default"));

        for (int i = 0; i <= mapSize; i++)
        {
            float position = -half + i * cellSize;

            CreateLine(
                new Vector3(position, -half, 0),
                new Vector3(position, half, 0),
                lineMaterial
            );

            CreateLine(
                new Vector3(-half, position, 0),
                new Vector3(half, position, 0),
                lineMaterial
            );
        }

        Debug.Log($"Grid created: {mapSize} x {mapSize}");
    }

    private void CreateLine(Vector3 start, Vector3 end, Material material)
    {
        GameObject lineObject = new GameObject("GridLine");
        lineObject.transform.SetParent(gridRoot);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.material = material;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        lineRenderer.startWidth = 0.025f;
        lineRenderer.endWidth = 0.025f;
        lineRenderer.startColor = new Color(1f, 1f, 1f, 0.15f);
        lineRenderer.endColor = new Color(1f, 1f, 1f, 0.15f);
        lineRenderer.sortingOrder = -10;
    }

    private void CreateBase()
    {
        float half = GetMapHalfSize();

        basePosition = new Vector2(
            half - cellSize * 2.5f,
            half - cellSize * 2.5f
        );

        Vector2Int baseCell = WorldToCell(basePosition);
        occupiedCells.Add(baseCell);

        baseObject = CreateCircleObject(
            "Base",
            basePosition,
            baseRadius,
            new Color(0.25f, 0.55f, 1f, 1f),
            20,
            buildingRoot
        );

        CreateWorldLabel(baseObject.transform, "基地", Color.white);

        buildings.Add(new BuildingData(
            "基地",
            BuildingType.None,
            baseObject,
            basePosition,
            baseCell,
            baseRadius,
            "主基地：后续用于建造建筑和管理资源。"
        ));

        Debug.Log($"Base created at cell {baseCell}");
    }

    private void CreateBuildRangeObject()
    {
        buildRangeObject = CreateCircleObject(
            "BuildRange",
            basePosition,
            buildRadius,
            new Color(0.2f, 1f, 0.35f, 0.16f),
            0,
            buildingRoot
        );

        buildRangeObject.SetActive(false);
    }

    private void CreatePlacementPreviewObject()
    {
        placementPreviewObject = CreateCircleObject(
            "PlacementPreview",
            Vector2.zero,
            buildingRadius,
            new Color(0.2f, 1f, 0.35f, 0.45f),
            30,
            buildingRoot
        );

        placementPreviewObject.SetActive(false);
    }

    private void CreateSelectionRingObject()
    {
        selectionRingObject = CreateCircleObject(
            "SelectionRing",
            Vector2.zero,
            0.7f,
            new Color(1f, 0.85f, 0.1f, 0.28f),
            15,
            buildingRoot
        );

        selectionRingObject.SetActive(false);
    }

    private void SelectFactory()
    {
        if (!CanAffordBuilding(BuildingType.Factory))
        {
            Debug.LogWarning($"Cannot select Factory: not enough resources. Need {factoryCost}, have {playerResources}.");
            return;
        }

        selectedBuilding = BuildingType.Factory;
        hasPreviewCell = false;

        if (buildRangeObject != null)
        {
            buildRangeObject.SetActive(true);
        }

        if (placementPreviewObject != null)
        {
            placementPreviewObject.SetActive(true);
        }

        Debug.Log("Selected building: Factory / 兵厂");
    }

    private void CancelBuildMode()
    {
        selectedBuilding = BuildingType.None;
        hasPreviewCell = false;

        if (buildRangeObject != null)
        {
            buildRangeObject.SetActive(false);
        }

        if (placementPreviewObject != null)
        {
            placementPreviewObject.SetActive(false);
        }

        Debug.Log("Build mode cancelled.");
    }

    private void HandleSelectionInput()
    {
        if (selectedBuilding != BuildingType.None)
        {
            return;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (IsMouseOverRightPanel())
        {
            return;
        }

        Vector2 mouseWorldPosition = GetMouseWorldPosition();
        BuildingData clickedBuilding = FindBuildingAt(mouseWorldPosition);

        if (clickedBuilding != null)
        {
            SelectBuilding(clickedBuilding);
        }
        else
        {
            ClearSelectedBuilding();
        }
    }

    private bool IsMouseOverRightPanel()
    {
        float panelWidth = 220f;
        float panelHeight = 450f;
        float panelX = Screen.width - panelWidth - 20f;
        float panelY = 20f;

        Vector2 mousePositionInGui = new Vector2(
            Input.mousePosition.x,
            Screen.height - Input.mousePosition.y
        );

        Rect rightPanelRect = new Rect(panelX, panelY, panelWidth, panelHeight);

        return rightPanelRect.Contains(mousePositionInGui);
    }

    private BuildingData FindBuildingAt(Vector2 worldPosition)
    {
        for (int i = buildings.Count - 1; i >= 0; i--)
        {
            BuildingData building = buildings[i];
            float distance = Vector2.Distance(worldPosition, building.Position);

            if (distance <= building.Radius + 0.2f)
            {
                return building;
            }
        }

        return null;
    }

    private void SelectBuilding(BuildingData building)
    {
        selectedBuildingData = building;

        if (selectionRingObject != null)
        {
            selectionRingObject.SetActive(true);
            selectionRingObject.transform.position = new Vector3(
                building.Position.x,
                building.Position.y,
                -0.15f
            );

            selectionRingObject.transform.localScale = Vector3.one * building.Radius * 2.8f;
        }

        Debug.Log($"Selected building: {building.DisplayName}");
    }

    private void ClearSelectedBuilding()
    {
        selectedBuildingData = null;

        if (selectionRingObject != null)
        {
            selectionRingObject.SetActive(false);
        }

        Debug.Log("Selected building cleared.");
    }

    private void HandlePlacementPreview()
    {
        if (selectedBuilding == BuildingType.None)
        {
            return;
        }

        Vector2 mouseWorldPosition = GetMouseWorldPosition();

        if (!IsInsideMap(mouseWorldPosition))
        {
            hasPreviewCell = false;
            SetPlacementPreviewVisible(false);
            return;
        }

        currentPreviewCell = WorldToCell(mouseWorldPosition);
        currentPreviewPosition = CellToWorld(currentPreviewCell);
        hasPreviewCell = true;

        SetPlacementPreviewVisible(true);

        placementPreviewObject.transform.position = new Vector3(
            currentPreviewPosition.x,
            currentPreviewPosition.y,
            -0.2f
        );

        bool canBuild = CanBuildAt(currentPreviewPosition, currentPreviewCell);
        SetPlacementPreviewColor(canBuild);
    }

    private void HandlePlacementConfirm()
    {
        if (selectedBuilding == BuildingType.None)
        {
            return;
        }

        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        if (!hasPreviewCell)
        {
            Debug.LogWarning("Cannot build: mouse is outside the map.");
            return;
        }

        bool canBuild = CanBuildAt(currentPreviewPosition, currentPreviewCell);

        if (!canBuild)
        {
            if (!CanAffordBuilding(selectedBuilding))
            {
                Debug.LogWarning($"Cannot build: not enough resources. Need {GetBuildingCost(selectedBuilding)}, have {playerResources}.");
            }
            else
            {
                Debug.LogWarning("Cannot build here: out of range or cell is occupied.");
            }

            return;
        }

        BuildFactory(currentPreviewPosition, currentPreviewCell);
    }

    private void BuildFactory(Vector2 position, Vector2Int cell)
    {
        playerResources -= factoryCost;

        GameObject factoryObject = CreateCircleObject(
            "Factory",
            position,
            buildingRadius,
            new Color(0.35f, 0.9f, 0.45f, 1f),
            20,
            buildingRoot
        );

        CreateWorldLabel(factoryObject.transform, "兵厂", Color.black);

        buildings.Add(new BuildingData(
            "兵厂",
            BuildingType.Factory,
            factoryObject,
            position,
            cell,
            buildingRadius,
            "兵厂：后续用于生产步兵、载具等单位。"
        ));
        
        occupiedCells.Add(cell);

        Debug.Log($"Factory built at cell {cell}. Remaining resources: {playerResources}");
    }

    private void TryTrainInfantry(BuildingData factory)
    {
        if (factory == null || factory.Type != BuildingType.Factory)
        {
            Debug.LogWarning("Cannot train Infantry: selected building is not a Factory.");
            return;
        }

        if (playerResources < infantryCost)
        {
            Debug.LogWarning($"Cannot train Infantry: not enough resources. Need {infantryCost}, have {playerResources}.");
            return;
        }

        if (!TryGetSpawnCellNear(factory.Cell, out Vector2Int spawnCell))
        {
            Debug.LogWarning("Cannot train Infantry: no valid spawn cell near Factory.");
            return;
        }

        playerResources -= infantryCost;

        Vector2 spawnPosition = CellToWorld(spawnCell);

        GameObject infantryObject = CreateCircleObject(
            "Infantry",
            spawnPosition,
            infantryRadius,
            new Color(0.95f, 0.95f, 0.25f, 1f),
            25,
            buildingRoot
        );

        CreateWorldLabel(infantryObject.transform, "兵", Color.black);

        UnitData infantry = new UnitData(
            "步兵",
            UnitType.Infantry,
            infantryObject,
            spawnPosition,
            spawnCell,
            infantryRadius
        );

        occupiedCells.Add(spawnCell);
        units.Add(infantry);

        Debug.Log($"Infantry trained at cell {spawnCell}. Remaining resources: {playerResources}");
    }

    private bool TryGetSpawnCellNear(Vector2Int originCell, out Vector2Int spawnCell)
    {
        Vector2Int[] offsets =
        {
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(-1, -1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(1, 1),
            new Vector2Int(0, -2),
            new Vector2Int(-2, 0),
            new Vector2Int(2, 0),
            new Vector2Int(0, 2)
        };

        foreach (Vector2Int offset in offsets)
        {
            Vector2Int candidateCell = originCell + offset;

            if (!IsCellInsideMap(candidateCell))
            {
                continue;
            }

            if (occupiedCells.Contains(candidateCell))
            {
                continue;
            }

            spawnCell = candidateCell;
            return true;
        }

        spawnCell = originCell;
        return false;
    }

    private bool IsCellInsideMap(Vector2Int cell)
    {
        return cell.x >= 0 &&
            cell.x < mapSize &&
            cell.y >= 0 &&
            cell.y < mapSize;
    }

    private int GetBuildingCost(BuildingType buildingType)
    {
        if (buildingType == BuildingType.Factory)
        {
            return factoryCost;
        }

        return 0;
    }

    private bool CanAffordBuilding(BuildingType buildingType)
    {
        return playerResources >= GetBuildingCost(buildingType);
    }

    private bool CanBuildAt(Vector2 worldPosition, Vector2Int cell)
    {
        if (!IsInsideMap(worldPosition))
        {
            return false;
        }

        if (!CanAffordBuilding(selectedBuilding))
        {
            return false;
        }

        float distanceToBase = Vector2.Distance(basePosition, worldPosition);

        if (distanceToBase > buildRadius)
        {
            return false;
        }

        if (occupiedCells.Contains(cell))
        {
            return false;
        }

        return true;
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(mousePosition);
        return new Vector2(worldPosition.x, worldPosition.y);
    }

    private bool IsInsideMap(Vector2 worldPosition)
    {
        float half = GetMapHalfSize();

        return worldPosition.x >= -half &&
               worldPosition.x <= half &&
               worldPosition.y >= -half &&
               worldPosition.y <= half;
    }

    private Vector2Int WorldToCell(Vector2 worldPosition)
    {
        float half = GetMapHalfSize();

        int x = Mathf.FloorToInt((worldPosition.x + half) / cellSize);
        int y = Mathf.FloorToInt((worldPosition.y + half) / cellSize);

        x = Mathf.Clamp(x, 0, mapSize - 1);
        y = Mathf.Clamp(y, 0, mapSize - 1);

        return new Vector2Int(x, y);
    }

    private Vector2 CellToWorld(Vector2Int cell)
    {
        float half = GetMapHalfSize();

        float x = -half + cell.x * cellSize + cellSize / 2f;
        float y = -half + cell.y * cellSize + cellSize / 2f;

        return new Vector2(x, y);
    }

    private float GetMapHalfSize()
    {
        return mapSize * cellSize / 2f;
    }

    private GameObject CreateCircleObject(
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

    private void CreateWorldLabel(Transform parent, string text, Color color)
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

    private void SetPlacementPreviewVisible(bool visible)
    {
        if (placementPreviewObject != null && placementPreviewObject.activeSelf != visible)
        {
            placementPreviewObject.SetActive(visible);
        }
    }

    private void SetPlacementPreviewColor(bool canBuild)
    {
        SpriteRenderer spriteRenderer = placementPreviewObject.GetComponent<SpriteRenderer>();

        spriteRenderer.color = canBuild
            ? new Color(0.2f, 1f, 0.35f, 0.45f)
            : new Color(1f, 0.2f, 0.2f, 0.45f);
    }

    private Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

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

        return Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
    }

    private Texture2D CreateMenuBackgroundTexture()
    {
        Texture2D texture = new Texture2D(2, 2);

        texture.SetPixel(0, 0, new Color(0.03f, 0.04f, 0.06f));
        texture.SetPixel(1, 0, new Color(0.05f, 0.08f, 0.12f));
        texture.SetPixel(0, 1, new Color(0.08f, 0.12f, 0.18f));
        texture.SetPixel(1, 1, new Color(0.04f, 0.05f, 0.07f));

        texture.Apply();

        return texture;
    }
}