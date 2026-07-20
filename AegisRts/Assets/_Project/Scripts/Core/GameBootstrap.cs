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
        Base,
        Factory
    }

    private enum UnitType
    {
        Infantry
    }

    private enum Team
    {
        Player,
        Enemy
    }

    [Header("Map Settings")]
    [SerializeField] private int mapSize = 32;
    [SerializeField] private float cellSize = 1f;

    [Header("Base Settings")]
    [SerializeField] private float baseRadius = 0.45f;
    [SerializeField] private float buildRadius = 7f;

    [Header("Building Settings")]
    [SerializeField] private float buildingRadius = 0.42f;

    [Header("Health Settings")]
    [SerializeField] private int playerBaseHitPoints = 500;
    [SerializeField] private int factoryHitPoints = 300;
    [SerializeField] private int enemyBaseHitPoints = 400;

    [Header("Combat Settings")]
    [SerializeField] private int infantryAttackDamage = 20;
    [SerializeField] private float infantryAttackRange = 1.2f;
    [SerializeField] private float infantryAttackCooldown = 1f;
    [SerializeField] private int playerInfantryHitPoints = 100;
    [SerializeField] private int enemyInfantryHitPoints = 80;
    [SerializeField] private float unitAggroRange = 4f;

    [Header("Enemy AI Settings")]
    [SerializeField] private float enemySpawnInterval = 15f;
    [SerializeField] private int enemyInfantryAttackDamage = 10;
    [SerializeField] private float enemyInfantryAttackRange = 1.2f;
    [SerializeField] private float enemyInfantryAttackCooldown = 1.2f;

    [Header("Resource Settings")]
    [SerializeField] private int startingResources = 500;
    [SerializeField] private int factoryCost = 150;
    [SerializeField] private int infantryCost = 50;
    [SerializeField] private int passiveResourceIncome = 10;
    [SerializeField] private float passiveResourceInterval = 5f;

    [Header("Unit Settings")]
    [SerializeField] private float infantryRadius = 0.42f;
    [SerializeField] private float unitMoveSpeed = 5f;

    [SerializeField] private float dragSelectThreshold = 10f;

    private int playerResources;
    private float resourceIncomeTimer;
    private bool isPaused;
    private int nextEntityId = 1;
    private float matchTime;

    private class BuildingData
    {
        public int Id;
        public string DisplayName;
        public BuildingType Type;
        public GameObject GameObject;
        public Vector2 Position;
        public Vector2Int Cell;
        public float Radius;
        public string Description;
        public Team Team;
        public int MaxHitPoints;
        public int HitPoints;

        public BuildingData(
            string displayName,
            BuildingType type,
            GameObject gameObject,
            Vector2 position,
            Vector2Int cell,
            float radius,
            string description,
            Team team,
            int maxHitPoints
        )
        {
            DisplayName = displayName;
            Type = type;
            GameObject = gameObject;
            Position = position;
            Cell = cell;
            Radius = radius;
            Description = description;
            Team = team;
            MaxHitPoints = maxHitPoints;
            HitPoints = maxHitPoints;
        }
    }

    private class UnitData
    {
        public int Id;
        public string DisplayName;
        public UnitType Type;
        public GameObject GameObject;
        public Vector2 Position;
        public Vector2Int Cell;
        public float Radius;
        public string Description;
        public Team Team;

        public int MaxHitPoints;
        public int HitPoints;

        public bool IsMoving;
        public Vector2 TargetPosition;
        public Vector2Int TargetCell;

        public int AttackDamage;
        public float AttackRange;
        public float AttackCooldown;
        public float AttackTimer;

        public BuildingData AttackTarget;
        public UnitData AttackUnitTarget;

        public UnitData(
            string displayName,
            UnitType type,
            GameObject gameObject,
            Vector2 position,
            Vector2Int cell,
            float radius,
            string description,
            Team team,
            int maxHitPoints,
            int attackDamage,
            float attackRange,
            float attackCooldown
        )
        {
            DisplayName = displayName;
            Type = type;
            GameObject = gameObject;
            Position = position;
            Cell = cell;
            Radius = radius;
            Description = description;
            Team = team;

            MaxHitPoints = maxHitPoints;
            HitPoints = maxHitPoints;

            IsMoving = false;
            TargetPosition = position;
            TargetCell = cell;

            AttackDamage = attackDamage;
            AttackRange = attackRange;
            AttackCooldown = attackCooldown;
            AttackTimer = 0f;

            AttackTarget = null;
            AttackUnitTarget = null;
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

    private bool gameWon = false;

    private bool gameLost = false;
    private float enemySpawnTimer = 0f;

    private BuildingData playerBaseData = null;
    private BuildingData enemyBaseData = null;

    private readonly List<BuildingData> buildings = new List<BuildingData>();
    private BuildingData selectedBuildingData = null;

    private readonly List<UnitData> units = new List<UnitData>();
    private UnitData selectedUnitData = null;

    private readonly List<UnitData> selectedUnits = new List<UnitData>();
    private readonly Dictionary<UnitData, GameObject> unitSelectionRings = new Dictionary<UnitData, GameObject>();

    private bool isDraggingSelection = false;
    private Vector2 dragStartScreenPosition;
    private Vector2 dragCurrentScreenPosition;

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

        if (gameWon || gameLost)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isPaused = !isPaused;
        }

        if (isPaused)
        {
            return;
        }

        matchTime += Time.deltaTime;
        UpdateResources();
        HandleSelectionInput();
        HandleUnitMoveCommand();
        HandlePlacementPreview();
        HandlePlacementConfirm();
        UpdateEnemyAI();
        UpdateUnitCombat();
        UpdateUnitMovement();
        UpdateSelectionRingPositions();
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

            GUI.Label(
                new Rect(panelX + 25f, panelY + 315f, panelWidth - 50f, 25f),
                $"生命值：{selectedBuildingData.HitPoints}/{selectedBuildingData.MaxHitPoints}"
            );
        }
        else if (selectedUnits.Count > 0)
        {
            if (selectedUnits.Count == 1)
            {
                UnitData selectedUnit = selectedUnits[0];

                GUI.Label(
                    new Rect(panelX + 25f, panelY + 245f, panelWidth - 50f, 25f),
                    "选中单位：" + selectedUnit.DisplayName
                );

                GUI.Label(
                    new Rect(panelX + 25f, panelY + 270f, panelWidth - 50f, 55f),
                    selectedUnit.Description
                );

                GUI.Label(
                    new Rect(panelX + 25f, panelY + 315f, panelWidth - 50f, 25f),
                    $"生命值：{selectedUnit.HitPoints}/{selectedUnit.MaxHitPoints}"
                );
            }
            else
            {
                GUI.Label(
                    new Rect(panelX + 25f, panelY + 245f, panelWidth - 50f, 25f),
                    $"已选中单位：{selectedUnits.Count}"
                );

                GUI.Label(
                    new Rect(panelX + 25f, panelY + 270f, panelWidth - 50f, 55f),
                    "右键点击地图可群体移动；右键点击 AI 基地可群体攻击。"
                );
            }
        }
        else
        {
            GUI.Label(
                new Rect(panelX + 25f, panelY + 250f, panelWidth - 50f, 25f),
                "未选中对象"
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

        if (gameWon)
        {
            GUIStyle victoryStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 38,
                alignment = TextAnchor.MiddleCenter
            };
            victoryStyle.normal.textColor = Color.yellow;

            GUI.Label(
                new Rect(0, Screen.height * 0.38f, Screen.width, 80),
                "胜利：AI 基地已被摧毁",
                victoryStyle
            );

            DrawEndGameButtons(Screen.height * 0.52f);
        }

        if (gameLost)
        {
            GUIStyle defeatStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 38,
                alignment = TextAnchor.MiddleCenter
            };
            defeatStyle.normal.textColor = Color.red;

            GUI.Label(
                new Rect(0, Screen.height * 0.48f, Screen.width, 80),
                "失败：玩家基地已被摧毁",
                defeatStyle
            );

            DrawEndGameButtons(Screen.height * 0.60f);
        }

        if (isPaused && !gameWon && !gameLost)
        {
            GUI.Box(
                new Rect((Screen.width - 300f) / 2f, Screen.height * 0.35f, 300f, 190f),
                "游戏已暂停"
            );

            if (GUI.Button(new Rect((Screen.width - 220f) / 2f, Screen.height * 0.35f + 45f, 220f, 35f), "继续"))
            {
                isPaused = false;
            }

            if (GUI.Button(new Rect((Screen.width - 220f) / 2f, Screen.height * 0.35f + 90f, 220f, 35f), "重新开始"))
            {
                RestartGame();
            }

            if (GUI.Button(new Rect((Screen.width - 220f) / 2f, Screen.height * 0.35f + 135f, 220f, 35f), "返回主菜单"))
            {
                ReturnToMainMenu();
            }
        }
        DrawSelectionRectangle();
    }

    private void DrawEndGameButtons(float y)
    {
        if (GUI.Button(new Rect((Screen.width - 220f) / 2f, y, 220f, 40f), "重新开始"))
        {
            RestartGame();
        }

        if (GUI.Button(new Rect((Screen.width - 220f) / 2f, y + 50f, 220f, 40f), "返回主菜单"))
        {
            ReturnToMainMenu();
        }
    }

    private void DrawSelectionRectangle()
    {
        if (!isDraggingSelection)
        {
            return;
        }

        float dragDistance = Vector2.Distance(dragStartScreenPosition, dragCurrentScreenPosition);

        if (dragDistance < dragSelectThreshold)
        {
            return;
        }

        Rect guiRect = GetGuiRect(dragStartScreenPosition, dragCurrentScreenPosition);

        Color previousColor = GUI.color;

        GUI.color = new Color(0.2f, 0.8f, 1f, 0.18f);
        GUI.DrawTexture(guiRect, Texture2D.whiteTexture);

        GUI.color = new Color(0.2f, 0.8f, 1f, 0.85f);
        DrawRectBorder(guiRect, 2f);

        GUI.color = previousColor;
    }

    private void DrawRectBorder(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), Texture2D.whiteTexture);
    }

    private Rect GetScreenRect(Vector2 screenStart, Vector2 screenEnd)
    {
        float xMin = Mathf.Min(screenStart.x, screenEnd.x);
        float xMax = Mathf.Max(screenStart.x, screenEnd.x);
        float yMin = Mathf.Min(screenStart.y, screenEnd.y);
        float yMax = Mathf.Max(screenStart.y, screenEnd.y);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private Rect GetGuiRect(Vector2 screenStart, Vector2 screenEnd)
    {
        Rect screenRect = GetScreenRect(screenStart, screenEnd);

        return new Rect(
            screenRect.xMin,
            Screen.height - screenRect.yMax,
            screenRect.width,
            screenRect.height
        );
    }

    private void StartGame()
    {
        gameState = GameState.Playing;
        isPaused = false;

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
        resourceIncomeTimer = passiveResourceInterval;
        gameWon = false;
        gameLost = false;
        matchTime = 0f;
        nextEntityId = 1;

        gridRoot = new GameObject("GridRoot").transform;
        buildingRoot = new GameObject("BuildingRoot").transform;

        CreateGrid();
        CreateBase();
        CreateEnemyBase();
        CreateBuildRangeObject();
        CreatePlacementPreviewObject();
        CreateSelectionRingObject();

        enemySpawnTimer = enemySpawnInterval;

        Debug.Log($"Starting resources: {playerResources}");
    }

    private void UpdateResources()
    {
        resourceIncomeTimer -= Time.deltaTime;

        if (resourceIncomeTimer > 0f)
        {
            return;
        }

        resourceIncomeTimer = passiveResourceInterval;
        playerResources = ArenaGameRules.ApplyIncome(playerResources, passiveResourceIncome);
    }

    private void RestartGame()
    {
        DestroyGameWorld();
        CreateGameWorld();
        gameWorldCreated = true;
        gameState = GameState.Playing;
        isPaused = false;
    }

    private void ReturnToMainMenu()
    {
        DestroyGameWorld();
        gameWorldCreated = false;
        gameState = GameState.MainMenu;
        isPaused = false;
    }

    private void DestroyGameWorld()
    {
        ClearUnitSelectionRings();

        if (gridRoot != null)
        {
            Destroy(gridRoot.gameObject);
        }

        if (buildingRoot != null)
        {
            Destroy(buildingRoot.gameObject);
        }

        buildings.Clear();
        units.Clear();
        selectedUnits.Clear();
        occupiedCells.Clear();
        selectedBuildingData = null;
        selectedUnitData = null;
        playerBaseData = null;
        enemyBaseData = null;
        selectedBuilding = BuildingType.None;
        isBuildMenuOpen = false;
        hasPreviewCell = false;
        selectionRingObject = null;
        placementPreviewObject = null;
        buildRangeObject = null;
        baseObject = null;
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

        playerBaseData = new BuildingData(
            "基地",
            BuildingType.Base,
            baseObject,
            basePosition,
            baseCell,
            baseRadius,
            "主基地：后续用于建造建筑和管理资源。",
            Team.Player,
            playerBaseHitPoints
        );

        buildings.Add(playerBaseData);
        playerBaseData.Id = nextEntityId++;

        Debug.Log($"Base created at cell {baseCell}");
    }

    private void CreateEnemyBase()
    {
        float half = GetMapHalfSize();

        Vector2 enemyBasePosition = new Vector2(
            -half + cellSize * 2.5f,
            -half + cellSize * 2.5f
        );

        Vector2Int enemyBaseCell = WorldToCell(enemyBasePosition);
        occupiedCells.Add(enemyBaseCell);

        GameObject enemyBaseObject = CreateCircleObject(
            "EnemyBase",
            enemyBasePosition,
            baseRadius,
            new Color(1f, 0.25f, 0.25f, 1f),
            20,
            buildingRoot
        );

        CreateWorldLabel(enemyBaseObject.transform, "AI", Color.white);

        enemyBaseData = new BuildingData(
            "AI基地",
            BuildingType.Base,
            enemyBaseObject,
            enemyBasePosition,
            enemyBaseCell,
            baseRadius,
            "敌方 AI 基地：摧毁它即可获得胜利。",
            Team.Enemy,
            enemyBaseHitPoints
        );

        buildings.Add(enemyBaseData);
        enemyBaseData.Id = nextEntityId++;

        Debug.Log($"Enemy base created at cell {enemyBaseCell}");
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

        if (Input.GetMouseButtonDown(0))
        {
            if (IsMouseOverRightPanel())
            {
                return;
            }

            isDraggingSelection = true;
            dragStartScreenPosition = Input.mousePosition;
            dragCurrentScreenPosition = dragStartScreenPosition;
        }

        if (isDraggingSelection && Input.GetMouseButton(0))
        {
            dragCurrentScreenPosition = Input.mousePosition;
        }

        if (isDraggingSelection && Input.GetMouseButtonUp(0))
        {
            isDraggingSelection = false;
            dragCurrentScreenPosition = Input.mousePosition;

            if (IsMouseOverRightPanel())
            {
                return;
            }

            float dragDistance = Vector2.Distance(dragStartScreenPosition, dragCurrentScreenPosition);

            if (dragDistance >= dragSelectThreshold)
            {
                SelectUnitsInDragRect();
            }
            else
            {
                HandleSingleClickSelection();
            }
        }
    }

    private void HandleSingleClickSelection()
    {
        Vector2 mouseWorldPosition = GetMouseWorldPosition();

        UnitData clickedUnit = FindUnitAt(mouseWorldPosition);

        if (clickedUnit != null && clickedUnit.Team == Team.Player)
        {
            SelectSingleUnit(clickedUnit);
            return;
        }

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

    private void SelectUnitsInDragRect()
    {
        Rect selectionRect = GetScreenRect(dragStartScreenPosition, dragCurrentScreenPosition);

        List<UnitData> unitsInRect = new List<UnitData>();

        foreach (UnitData unit in units)
        {
            if (unit.Team != Team.Player)
            {
                continue;
            }

            Vector3 unitScreenPosition = mainCamera.WorldToScreenPoint(unit.Position);

            if (selectionRect.Contains(unitScreenPosition))
            {
                unitsInRect.Add(unit);
            }
        }

        if (unitsInRect.Count > 0)
        {
            SelectMultipleUnits(unitsInRect);
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

    private UnitData FindUnitAt(Vector2 worldPosition)
    {
        for (int i = units.Count - 1; i >= 0; i--)
        {
            UnitData unit = units[i];
            float distance = Vector2.Distance(worldPosition, unit.Position);

            if (distance <= unit.Radius + 0.2f)
            {
                return unit;
            }
        }

        return null;
    }

    private void SelectBuilding(BuildingData building)
    {
        selectedBuildingData = building;
        selectedUnitData = null;
        selectedUnits.Clear();
        ClearUnitSelectionRings();

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

    private void SelectSingleUnit(UnitData unit)
    {
        List<UnitData> singleUnitList = new List<UnitData>
        {
            unit
        };

        SelectMultipleUnits(singleUnitList);
    }

    private void SelectMultipleUnits(List<UnitData> unitsToSelect)
    {
        selectedBuildingData = null;
        selectedUnitData = null;

        if (selectionRingObject != null)
        {
            selectionRingObject.SetActive(false);
        }

        selectedUnits.Clear();
        ClearUnitSelectionRings();

        foreach (UnitData unit in unitsToSelect)
        {
            if (unit == null || unit.Team != Team.Player)
            {
                continue;
            }

            selectedUnits.Add(unit);
            CreateUnitSelectionRing(unit);
        }

        if (selectedUnits.Count == 1)
        {
            selectedUnitData = selectedUnits[0];
            Debug.Log($"Selected unit: {selectedUnitData.DisplayName}");
        }
        else
        {
            Debug.Log($"Selected {selectedUnits.Count} units.");
        }
    }

    private void CreateUnitSelectionRing(UnitData unit)
    {
        GameObject ringObject = CreateCircleObject(
            "UnitSelectionRing",
            unit.Position,
            unit.Radius * 1.5f,
            new Color(1f, 0.85f, 0.1f, 0.35f),
            18,
            buildingRoot
        );

        unitSelectionRings[unit] = ringObject;
    }

    private void ClearUnitSelectionRings()
    {
        foreach (GameObject ringObject in unitSelectionRings.Values)
        {
            if (ringObject != null)
            {
                Destroy(ringObject);
            }
        }

        unitSelectionRings.Clear();
    }

    private void UpdateSelectionRingPositions()
    {
        foreach (KeyValuePair<UnitData, GameObject> pair in unitSelectionRings)
        {
            UnitData unit = pair.Key;
            GameObject ringObject = pair.Value;

            if (unit == null || ringObject == null)
            {
                continue;
            }

            ringObject.transform.position = new Vector3(
                unit.Position.x,
                unit.Position.y,
                -0.15f
            );
        }

        if (selectedBuildingData != null && selectionRingObject != null)
        {
            selectionRingObject.transform.position = new Vector3(
                selectedBuildingData.Position.x,
                selectedBuildingData.Position.y,
                -0.15f
            );
        }
    }

    private void ClearSelectedBuilding()
    {
        selectedBuildingData = null;
        selectedUnitData = null;
        selectedUnits.Clear();

        ClearUnitSelectionRings();

        if (selectionRingObject != null)
        {
            selectionRingObject.SetActive(false);
        }

        Debug.Log("Selection cleared.");
    }

    private void HandleUnitMoveCommand()
    {
        if (selectedBuilding != BuildingType.None)
        {
            return;
        }

        if (selectedUnits.Count == 0)
        {
            return;
        }

        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        if (IsMouseOverRightPanel())
        {
            return;
        }

        Vector2 mouseWorldPosition = GetMouseWorldPosition();

        if (!IsInsideMap(mouseWorldPosition))
        {
            Debug.LogWarning("Cannot command units: target is outside the map.");
            return;
        }

        UnitData targetUnit = FindUnitAt(mouseWorldPosition);

        if (targetUnit != null && targetUnit.Team == Team.Enemy)
        {
            TryAttackSelectedUnits(targetUnit);
            return;
        }

        BuildingData targetBuilding = FindBuildingAt(mouseWorldPosition);

        if (targetBuilding != null && targetBuilding.Team == Team.Enemy)
        {
            TryAttackSelectedUnits(targetBuilding);
            return;
        }

        Vector2Int targetCell = WorldToCell(mouseWorldPosition);

        TryMoveSelectedUnitsToCell(targetCell);
    }

    private void TryAttackSelectedUnit(BuildingData targetBuilding)
    {
        if (selectedUnitData == null)
        {
            return;
        }

        if (targetBuilding == null || targetBuilding.Team != Team.Enemy)
        {
            Debug.LogWarning("Cannot attack: invalid target.");
            return;
        }

        selectedUnitData.AttackTarget = targetBuilding;
        selectedUnitData.IsMoving = false;

        Debug.Log($"Attack command: {selectedUnitData.DisplayName} -> {targetBuilding.DisplayName}");
    }

    private void TryAttackSelectedUnits(BuildingData targetBuilding)
    {
        if (targetBuilding == null || targetBuilding.Team != Team.Enemy)
        {
            Debug.LogWarning("Cannot attack: invalid building target.");
            return;
        }

        int commandCount = 0;

        foreach (UnitData unit in selectedUnits)
        {
            if (unit == null || unit.Team != Team.Player)
            {
                continue;
            }

            unit.AttackTarget = targetBuilding;
            unit.AttackUnitTarget = null;
            unit.IsMoving = false;
            commandCount++;
        }

        Debug.Log($"Attack command: {commandCount} units -> {targetBuilding.DisplayName}");
    }

    private void TryAttackSelectedUnits(UnitData targetUnit)
    {
        if (targetUnit == null || targetUnit.Team != Team.Enemy)
        {
            Debug.LogWarning("Cannot attack: invalid unit target.");
            return;
        }

        int commandCount = 0;

        foreach (UnitData unit in selectedUnits)
        {
            if (unit == null || unit.Team != Team.Player)
            {
                continue;
            }

            unit.AttackUnitTarget = targetUnit;
            unit.AttackTarget = null;
            unit.IsMoving = false;
            commandCount++;
        }

        Debug.Log($"Attack command: {commandCount} units -> {targetUnit.DisplayName}");
    }

    private void TryMoveSelectedUnitsToCell(Vector2Int centerCell)
    {
        List<UnitData> movableUnits = new List<UnitData>();
        HashSet<Vector2Int> selectedCurrentCells = new HashSet<Vector2Int>();

        foreach (UnitData unit in selectedUnits)
        {
            if (unit == null || unit.Team != Team.Player)
            {
                continue;
            }

            movableUnits.Add(unit);
            selectedCurrentCells.Add(unit.Cell);
        }

        if (movableUnits.Count == 0)
        {
            return;
        }

        List<Vector2Int> targetCells = FindGroupTargetCells(
            centerCell,
            movableUnits.Count,
            selectedCurrentCells
        );

        if (targetCells.Count == 0)
        {
            Debug.LogWarning("Cannot move units: no valid target cells.");
            return;
        }

        foreach (UnitData unit in movableUnits)
        {
            occupiedCells.Remove(unit.Cell);
        }

        int moveCount = Mathf.Min(movableUnits.Count, targetCells.Count);

        for (int i = 0; i < moveCount; i++)
        {
            UnitData unit = movableUnits[i];
            Vector2Int targetCell = targetCells[i];

            occupiedCells.Add(targetCell);

            unit.AttackTarget = null;
            unit.AttackUnitTarget = null;
            unit.Cell = targetCell;
            unit.TargetCell = targetCell;
            unit.TargetPosition = CellToWorld(targetCell);
            unit.IsMoving = true;
        }

        Debug.Log($"Move command: {moveCount} units -> around cell {centerCell}");
    }

    private List<Vector2Int> FindGroupTargetCells(
        Vector2Int centerCell,
        int requiredCount,
        HashSet<Vector2Int> selectedCurrentCells
    )
    {
        List<Vector2Int> result = new List<Vector2Int>();
        HashSet<Vector2Int> reservedCells = new HashSet<Vector2Int>();

        int maxSearchRadius = 6;

        for (int radius = 0; radius <= maxSearchRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    Vector2Int candidateCell = new Vector2Int(
                        centerCell.x + dx,
                        centerCell.y + dy
                    );

                    if (!IsCellInsideMap(candidateCell))
                    {
                        continue;
                    }

                    if (reservedCells.Contains(candidateCell))
                    {
                        continue;
                    }

                    bool occupiedByNonSelectedUnitOrBuilding =
                        occupiedCells.Contains(candidateCell) &&
                        !selectedCurrentCells.Contains(candidateCell);

                    if (occupiedByNonSelectedUnitOrBuilding)
                    {
                        continue;
                    }

                    result.Add(candidateCell);
                    reservedCells.Add(candidateCell);

                    if (result.Count >= requiredCount)
                    {
                        return result;
                    }
                }
            }
        }

        return result;
    }
    private void TryMoveSelectedUnitToCell(Vector2Int targetCell)
    {
        if (selectedUnitData == null)
        {
            return;
        }

        if (!IsCellInsideMap(targetCell))
        {
            Debug.LogWarning("Cannot move unit: target cell is outside the map.");
            return;
        }

        if (targetCell == selectedUnitData.Cell)
        {
            Debug.Log("Unit is already at the target cell.");
            return;
        }

        if (occupiedCells.Contains(targetCell))
        {
            Debug.LogWarning($"Cannot move unit: target cell {targetCell} is occupied.");
            return;
        }

        selectedUnitData.AttackTarget = null;
        selectedUnitData.AttackUnitTarget = null;
        occupiedCells.Remove(selectedUnitData.Cell);
        occupiedCells.Add(targetCell);

        selectedUnitData.Cell = targetCell;
        selectedUnitData.TargetCell = targetCell;
        selectedUnitData.TargetPosition = CellToWorld(targetCell);
        selectedUnitData.IsMoving = true;

        Debug.Log($"Move command: {selectedUnitData.DisplayName} -> cell {targetCell}");
    }

    private void UpdateEnemyAI()
    {
        if (enemyBaseData == null || !buildings.Contains(enemyBaseData))
        {
            return;
        }

        if (playerBaseData == null || !buildings.Contains(playerBaseData))
        {
            return;
        }

        enemySpawnTimer -= Time.deltaTime;

        if (enemySpawnTimer > 0f)
        {
            return;
        }

        enemySpawnTimer = enemySpawnInterval;

        SpawnEnemyInfantry();
    }

    private void SpawnEnemyInfantry()
    {
        if (enemyBaseData == null)
        {
            return;
        }

        if (!TryGetSpawnCellNear(enemyBaseData.Cell, out Vector2Int spawnCell))
        {
            Debug.LogWarning("Enemy AI cannot spawn infantry: no valid spawn cell.");
            return;
        }

        Vector2 spawnPosition = CellToWorld(spawnCell);

        GameObject enemyInfantryObject = CreateCircleObject(
            "EnemyInfantry",
            spawnPosition,
            infantryRadius,
            new Color(1f, 0.45f, 0.15f, 1f),
            25,
            buildingRoot
        );

        CreateWorldLabel(enemyInfantryObject.transform, "敌", Color.black);

        UnitData enemyInfantry = new UnitData(
            "敌方步兵",
            UnitType.Infantry,
            enemyInfantryObject,
            spawnPosition,
            spawnCell,
            infantryRadius,
            "敌方步兵：由 AI 基地自动生产，会优先攻击附近玩家步兵，否则攻击玩家基地。",
            Team.Enemy,
            enemyInfantryHitPoints,
            enemyInfantryAttackDamage,
            enemyInfantryAttackRange,
            enemyInfantryAttackCooldown
        );

        enemyInfantry.AttackTarget = playerBaseData;

        occupiedCells.Add(spawnCell);
        enemyInfantry.Id = nextEntityId++;
        units.Add(enemyInfantry);

        Debug.Log($"Enemy infantry spawned at cell {spawnCell} and is attacking player base.");
    }
    
    private void UpdateUnitCombat()
    {
        UnitData[] unitsSnapshot = units.ToArray();

        foreach (UnitData unit in unitsSnapshot)
        {
            if (unit == null || !units.Contains(unit))
            {
                continue;
            }

            TryAutoAcquireUnitTarget(unit);

            if (unit.AttackUnitTarget != null)
            {
                UpdateUnitAttackUnit(unit);
                continue;
            }

            if (unit.AttackTarget != null)
            {
                UpdateUnitAttackBuilding(unit);
                continue;
            }
        }
    }

    private void TryAutoAcquireUnitTarget(UnitData unit)
    {
        if (unit == null)
        {
            return;
        }

        UnitData nearestEnemyUnit = FindNearestEnemyUnitInRange(unit, unitAggroRange);

        if (nearestEnemyUnit == null)
        {
            return;
        }

        unit.AttackUnitTarget = nearestEnemyUnit;
        unit.AttackTarget = null;
        unit.IsMoving = false;
    }

    private UnitData FindNearestEnemyUnitInRange(UnitData sourceUnit, float range)
    {
        UnitData nearestUnit = null;
        float nearestDistance = float.MaxValue;

        foreach (UnitData candidate in units)
        {
            if (candidate == null)
            {
                continue;
            }

            if (candidate.Team == sourceUnit.Team)
            {
                continue;
            }

            float distance = Vector2.Distance(sourceUnit.Position, candidate.Position);

            if (distance > range)
            {
                continue;
            }

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestUnit = candidate;
            }
        }

        return nearestUnit;
    }

    private void UpdateUnitAttackUnit(UnitData attacker)
    {
        UnitData target = attacker.AttackUnitTarget;

        if (target == null || !units.Contains(target))
        {
            attacker.AttackUnitTarget = null;
            return;
        }

        float distance = Vector2.Distance(attacker.Position, target.Position);

        if (distance > attacker.AttackRange)
        {
            MoveUnitTowards(attacker, target.Position);
            return;
        }

        attacker.AttackTimer -= Time.deltaTime;

        if (attacker.AttackTimer > 0f)
        {
            return;
        }

        attacker.AttackTimer = attacker.AttackCooldown;
        target.HitPoints = ArenaGameRules.ApplyDamage(target.HitPoints, attacker.AttackDamage);

        Debug.Log($"{attacker.DisplayName} attacked {target.DisplayName}. HP: {target.HitPoints}/{target.MaxHitPoints}");

        if (target.HitPoints <= 0)
        {
            DestroyUnit(target);
            attacker.AttackUnitTarget = null;
        }
    }

    private void UpdateUnitAttackBuilding(UnitData attacker)
    {
        BuildingData target = attacker.AttackTarget;

        if (target == null || !buildings.Contains(target))
        {
            attacker.AttackTarget = null;
            return;
        }

        float distance = Vector2.Distance(attacker.Position, target.Position);

        if (distance > attacker.AttackRange)
        {
            MoveUnitTowards(attacker, target.Position);
            return;
        }

        attacker.AttackTimer -= Time.deltaTime;

        if (attacker.AttackTimer > 0f)
        {
            return;
        }

        attacker.AttackTimer = attacker.AttackCooldown;
        target.HitPoints = ArenaGameRules.ApplyDamage(target.HitPoints, attacker.AttackDamage);

        Debug.Log($"{attacker.DisplayName} attacked {target.DisplayName}. HP: {target.HitPoints}/{target.MaxHitPoints}");

        if (target.HitPoints <= 0)
        {
            DestroyBuilding(target);
            attacker.AttackTarget = null;
        }
    }

    private void MoveUnitTowards(UnitData unit, Vector2 targetPosition)
    {
        Vector2 nextPosition = Vector2.MoveTowards(
            unit.Position,
            targetPosition,
            unitMoveSpeed * Time.deltaTime
        );

        unit.GameObject.transform.position = new Vector3(
            nextPosition.x,
            nextPosition.y,
            0f
        );

        unit.Position = nextPosition;
        SyncCombatMovementCell(unit);
    }

    private void SyncCombatMovementCell(UnitData unit)
    {
        Vector2Int currentCell = WorldToCell(unit.Position);

        if (currentCell == unit.Cell)
        {
            return;
        }

        occupiedCells.Remove(unit.Cell);
        unit.Cell = currentCell;
        unit.TargetCell = currentCell;
        occupiedCells.Add(currentCell);
    }

    private void DestroyUnit(UnitData unit)
    {
        if (unit == null)
        {
            return;
        }

        Debug.Log($"{unit.DisplayName} destroyed.");

        units.Remove(unit);
        occupiedCells.Remove(unit.Cell);

        if (selectedUnitData == unit)
        {
            selectedUnitData = null;
        }

        selectedUnits.Remove(unit);

        if (unitSelectionRings.TryGetValue(unit, out GameObject ringObject))
        {
            if (ringObject != null)
            {
                Destroy(ringObject);
            }

            unitSelectionRings.Remove(unit);
        }

        foreach (UnitData otherUnit in units)
        {
            if (otherUnit.AttackUnitTarget == unit)
            {
                otherUnit.AttackUnitTarget = null;
            }
        }

        if (unit.GameObject != null)
        {
            Destroy(unit.GameObject);
        }
    }

    private void DestroyBuilding(BuildingData building)
    {
        if (building == null)
        {
            return;
        }

        Debug.Log($"{building.DisplayName} destroyed.");

        buildings.Remove(building);
        occupiedCells.Remove(building.Cell);

        if (selectedBuildingData == building)
        {
            selectedBuildingData = null;
        }

        if (building.GameObject != null)
        {
            Destroy(building.GameObject);
        }

        if (building == enemyBaseData)
        {
            gameWon = true;
            Debug.Log("Victory! Enemy AI base destroyed.");
        }

        if (building == playerBaseData)
        {
            gameLost = true;
            Debug.Log("Defeat! Player base destroyed.");
        }
    }
    private void UpdateUnitMovement()
    {
        foreach (UnitData unit in units)
        {
            if (unit.AttackTarget != null || unit.AttackUnitTarget != null)
            {
                continue;
            }

            if (!unit.IsMoving)
            {
                continue;
            }

            Vector2 currentPosition = unit.GameObject.transform.position;
            Vector2 nextPosition = Vector2.MoveTowards(
                currentPosition,
                unit.TargetPosition,
                unitMoveSpeed * Time.deltaTime
            );

            unit.GameObject.transform.position = new Vector3(
                nextPosition.x,
                nextPosition.y,
                0f
            );

            unit.Position = nextPosition;

            if (Vector2.Distance(nextPosition, unit.TargetPosition) < 0.01f)
            {
                unit.GameObject.transform.position = new Vector3(
                    unit.TargetPosition.x,
                    unit.TargetPosition.y,
                    0f
                );

                unit.Position = unit.TargetPosition;
                unit.IsMoving = false;

                Debug.Log($"{unit.DisplayName} arrived at cell {unit.Cell}");
            }

            if (selectedUnitData == unit && selectionRingObject != null)
            {
                selectionRingObject.transform.position = new Vector3(
                    unit.Position.x,
                    unit.Position.y,
                    -0.15f
                );
            }
        }
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
        playerResources = ArenaGameRules.Spend(playerResources, factoryCost);

        GameObject factoryObject = CreateCircleObject(
            "Factory",
            position,
            buildingRadius,
            new Color(0.35f, 0.9f, 0.45f, 1f),
            20,
            buildingRoot
        );

        CreateWorldLabel(factoryObject.transform, "兵厂", Color.black);

        BuildingData factory = new BuildingData(
            "兵厂",
            BuildingType.Factory,
            factoryObject,
            position,
            cell,
            buildingRadius,
            "兵厂：后续用于生产步兵、载具等单位。",
            Team.Player,
            factoryHitPoints
        );
        factory.Id = nextEntityId++;
        buildings.Add(factory);
        
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

        playerResources = ArenaGameRules.Spend(playerResources, infantryCost);

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
            infantryRadius,
            "步兵：基础作战单位。当前版本支持左键选中、右键移动、攻击敌方建筑和敌方单位。",
            Team.Player,
            playerInfantryHitPoints,
            infantryAttackDamage,
            infantryAttackRange,
            infantryAttackCooldown
        );

        occupiedCells.Add(spawnCell);
        infantry.Id = nextEntityId++;
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
        return ArenaGameRules.CanAfford(playerResources, GetBuildingCost(buildingType));
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

    public ArenaObservation GetArenaObservation()
    {
        List<ArenaEntityObservation> buildingObservations = new List<ArenaEntityObservation>();
        List<ArenaEntityObservation> unitObservations = new List<ArenaEntityObservation>();

        foreach (BuildingData building in buildings)
        {
            buildingObservations.Add(ToObservation(
                building.Id,
                building.Type.ToString(),
                building.Team,
                building.Position,
                building.Cell,
                building.HitPoints,
                building.MaxHitPoints
            ));
        }

        foreach (UnitData unit in units)
        {
            unitObservations.Add(ToObservation(
                unit.Id,
                unit.Type.ToString(),
                unit.Team,
                unit.Position,
                unit.Cell,
                unit.HitPoints,
                unit.MaxHitPoints
            ));
        }

        return new ArenaObservation
        {
            MatchTime = matchTime,
            PlayerResources = playerResources,
            IsTerminal = gameWon || gameLost,
            Result = gameWon ? "PlayerWon" : gameLost ? "PlayerLost" : "Running",
            Buildings = buildingObservations.ToArray(),
            Units = unitObservations.ToArray()
        };
    }

    public string GetArenaObservationJson()
    {
        return JsonUtility.ToJson(GetArenaObservation());
    }

    public ArenaActionResult ExecuteArenaAction(ArenaAction action)
    {
        if (gameState != GameState.Playing || gameWon || gameLost || isPaused)
        {
            return ArenaActionResult.Reject("The match is not accepting actions.");
        }

        if (action == null || string.IsNullOrEmpty(action.Type))
        {
            return ArenaActionResult.Reject("Action type is required.");
        }

        if (action.Type == "Move")
        {
            List<UnitData> actors = FindPlayerUnits(action.UnitIds);

            if (actors.Count == 0)
            {
                return ArenaActionResult.Reject("No valid player units.");
            }

            SelectMultipleUnits(actors);
            TryMoveSelectedUnitsToCell(new Vector2Int(action.CellX, action.CellY));
            return ArenaActionResult.Success("Move command accepted.");
        }

        if (action.Type == "Attack")
        {
            List<UnitData> actors = FindPlayerUnits(action.UnitIds);

            if (actors.Count == 0)
            {
                return ArenaActionResult.Reject("No valid player units.");
            }

            SelectMultipleUnits(actors);

            foreach (UnitData targetUnit in units)
            {
                if (targetUnit.Id == action.TargetId && targetUnit.Team == Team.Enemy)
                {
                    TryAttackSelectedUnits(targetUnit);
                    return ArenaActionResult.Success("Unit attack command accepted.");
                }
            }

            foreach (BuildingData targetBuilding in buildings)
            {
                if (targetBuilding.Id == action.TargetId && targetBuilding.Team == Team.Enemy)
                {
                    TryAttackSelectedUnits(targetBuilding);
                    return ArenaActionResult.Success("Building attack command accepted.");
                }
            }

            return ArenaActionResult.Reject("Enemy target was not found.");
        }

        if (action.Type == "TrainInfantry")
        {
            foreach (BuildingData building in buildings)
            {
                if (building.Team == Team.Player && building.Type == BuildingType.Factory)
                {
                    if (!ArenaGameRules.CanAfford(playerResources, infantryCost))
                    {
                        return ArenaActionResult.Reject("Not enough resources.");
                    }

                    TryTrainInfantry(building);
                    return ArenaActionResult.Success("Infantry training accepted.");
                }
            }

            return ArenaActionResult.Reject("No player factory exists.");
        }

        if (action.Type == "BuildFactory")
        {
            Vector2Int cell = new Vector2Int(action.CellX, action.CellY);
            Vector2 position = CellToWorld(cell);
            BuildingType previousSelection = selectedBuilding;
            selectedBuilding = BuildingType.Factory;
            bool canBuild = IsCellInsideMap(cell) && CanBuildAt(position, cell);
            selectedBuilding = previousSelection;

            if (!canBuild)
            {
                return ArenaActionResult.Reject("Factory cannot be built at that cell.");
            }

            BuildFactory(position, cell);
            return ArenaActionResult.Success("Factory construction accepted.");
        }

        return ArenaActionResult.Reject("Unknown action type.");
    }

    private List<UnitData> FindPlayerUnits(int[] ids)
    {
        List<UnitData> result = new List<UnitData>();

        if (ids == null)
        {
            return result;
        }

        foreach (int id in ids)
        {
            foreach (UnitData unit in units)
            {
                if (unit.Id == id && unit.Team == Team.Player && !result.Contains(unit))
                {
                    result.Add(unit);
                    break;
                }
            }
        }

        return result;
    }

    private ArenaEntityObservation ToObservation(
        int id,
        string kind,
        Team team,
        Vector2 position,
        Vector2Int cell,
        int hitPoints,
        int maxHitPoints
    )
    {
        return new ArenaEntityObservation
        {
            Id = id,
            Kind = kind,
            Team = team.ToString(),
            X = position.x,
            Y = position.y,
            CellX = cell.x,
            CellY = cell.y,
            HitPoints = hitPoints,
            MaxHitPoints = maxHitPoints
        };
    }
}
