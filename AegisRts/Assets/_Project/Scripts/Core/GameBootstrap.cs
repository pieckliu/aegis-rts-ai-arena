using System.Collections.Generic;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private RtsGameConfig gameConfig;

    [Header("Map Settings")]
    [SerializeField] private int mapSize = 32;
    [SerializeField] private float cellSize = 1f;

    [Header("Base Settings")]
    [SerializeField] private float baseRadius = 0.45f;
    [SerializeField] private float buildRadius = 7f;

    [Header("Building Settings")]
    [SerializeField] private float buildingRadius = 0.42f;
    [SerializeField] private float infantryTrainingTime = 3f;
    [SerializeField] private int maxFactoryQueueSize = 5;

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

    [Header("Camera Settings")]
    [SerializeField] private float cameraMoveSpeed = 14f;
    [SerializeField] private float cameraZoomSpeed = 3f;
    [SerializeField] private float minCameraSize = 6f;
    [SerializeField] private float maxCameraSize = 18f;

    [SerializeField] private float dragSelectThreshold = 10f;

    private RtsEconomyProductionSystem economy;
    private ArenaOrchestrator arena;
    private RtsEntityLifecycle lifecycle;
    private RtsCombatSystem combat;
    private RtsSelectionInputController selectionInput;
    private RtsGameUIController ui;
    private bool isPaused;
    private int nextEntityId = 1;
    private float matchTime;

    private GameState gameState = GameState.MainMenu;
    private BuildingType selectedBuilding = BuildingType.None;

    private Camera mainCamera;
    private RtsCameraController cameraController;

    private Transform gridRoot;
    private Transform buildingRoot;

    private Sprite circleSprite;

    private GameObject baseObject;
    private GameObject buildRangeObject;
    private GameObject placementPreviewObject;

    private GameObject selectionRingObject;

    private Vector2 basePosition;
    private Vector2 currentPreviewPosition;
    private Vector2Int currentPreviewCell;

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

    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

    private void Awake()
    {
        gameConfig = gameConfig != null
            ? gameConfig
            : Resources.Load<RtsGameConfig>("RtsGameConfig");

        ApplyGameConfig();
        economy = new RtsEconomyProductionSystem(gameConfig);
        arena = new ArenaOrchestrator(
            gameConfig,
            economy,
            buildings,
            units,
            () => matchTime,
            () => gameState == GameState.Playing && !gameWon && !gameLost && !isPaused,
            () => gameWon,
            () => gameLost,
            CommandMoveUnits,
            CommandAttackUnit,
            CommandAttackBuilding,
            TryTrainInfantry,
            TryBuildFactoryAtCell
        );
        lifecycle = new RtsEntityLifecycle(
            buildings,
            units,
            occupiedCells,
            OnUnitRemoved,
            OnBuildingRemoved
        );
        combat = new RtsCombatSystem(gameConfig, buildings, units, MoveUnitTowards, lifecycle);
        selectionInput = new RtsSelectionInputController(dragSelectThreshold);
        ui = new RtsGameUIController(
            StartGame,
            SelectFactory,
            CancelBuildMode,
            TrainSelectedFactory,
            ResumeGame,
            RestartGame,
            ReturnToMainMenu
        );
        mainCamera = Camera.main;
        cameraController = gameObject.AddComponent<RtsCameraController>();

        circleSprite = CreateCircleSprite(128);

        cameraController.Configure(
            mainCamera,
            mapSize * cellSize,
            cameraMoveSpeed,
            cameraZoomSpeed,
            minCameraSize,
            maxCameraSize
        );
    }

    private void ApplyGameConfig()
    {
        if (gameConfig == null)
        {
            Debug.LogWarning("RtsGameConfig was not found; using scene defaults.");
            return;
        }

        if (!gameConfig.IsValid())
        {
            Debug.LogError("RtsGameConfig contains invalid values.");
            return;
        }

        mapSize = gameConfig.MapSize;
        cellSize = gameConfig.CellSize;
        baseRadius = gameConfig.BaseRadius;
        buildRadius = gameConfig.BuildRadius;
        buildingRadius = gameConfig.BuildingRadius;
        infantryTrainingTime = gameConfig.InfantryTrainingTime;
        maxFactoryQueueSize = gameConfig.MaxFactoryQueueSize;
        playerBaseHitPoints = gameConfig.PlayerBaseHitPoints;
        factoryHitPoints = gameConfig.FactoryHitPoints;
        enemyBaseHitPoints = gameConfig.EnemyBaseHitPoints;
        infantryAttackDamage = gameConfig.InfantryAttackDamage;
        infantryAttackRange = gameConfig.InfantryAttackRange;
        infantryAttackCooldown = gameConfig.InfantryAttackCooldown;
        playerInfantryHitPoints = gameConfig.PlayerInfantryHitPoints;
        enemyInfantryHitPoints = gameConfig.EnemyInfantryHitPoints;
        unitAggroRange = gameConfig.UnitAggroRange;
        enemySpawnInterval = gameConfig.EnemySpawnInterval;
        enemyInfantryAttackDamage = gameConfig.EnemyInfantryAttackDamage;
        enemyInfantryAttackRange = gameConfig.EnemyInfantryAttackRange;
        enemyInfantryAttackCooldown = gameConfig.EnemyInfantryAttackCooldown;
        startingResources = gameConfig.StartingResources;
        factoryCost = gameConfig.FactoryCost;
        infantryCost = gameConfig.InfantryCost;
        passiveResourceIncome = gameConfig.PassiveResourceIncome;
        passiveResourceInterval = gameConfig.PassiveResourceInterval;
        infantryRadius = gameConfig.InfantryRadius;
        unitMoveSpeed = gameConfig.UnitMoveSpeed;
        cameraMoveSpeed = gameConfig.CameraMoveSpeed;
        cameraZoomSpeed = gameConfig.CameraZoomSpeed;
        minCameraSize = gameConfig.MinCameraSize;
        maxCameraSize = gameConfig.MaxCameraSize;
        dragSelectThreshold = gameConfig.DragSelectThreshold;
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
        cameraController.Tick(Time.deltaTime);
        economy.TickIncome(Time.deltaTime);
        economy.TickProduction(Time.deltaTime, buildings, TrySpawnPlayerInfantry);
        selectionInput.TickSelection(
            selectedBuilding == BuildingType.None,
            IsPointerOverUI,
            HandleSingleClickSelection,
            SelectUnitsInDragRect
        );
        HandleUnitMoveCommand();
        HandlePlacementPreview();
        HandlePlacementConfirm();
        UpdateEnemyAI();
        combat.Tick(Time.deltaTime);
        UpdateUnitMovement();
        UpdateSelectionRingPositions();
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
        economy.Reset();
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

        Debug.Log($"Starting resources: {economy.Resources}");
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
        hasPreviewCell = false;
        selectionRingObject = null;
        placementPreviewObject = null;
        buildRangeObject = null;
        baseObject = null;
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
            Debug.LogWarning($"Cannot select Factory: not enough resources. Need {factoryCost}, have {economy.Resources}.");
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
        Rect selectionRect = GetScreenRect(selectionInput.DragStart, selectionInput.DragCurrent);

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

        if (!selectionInput.ConsumeCommandClick(true, IsPointerOverUI))
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
            unit.Waypoints.Clear();
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
            unit.Waypoints.Clear();
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

        HashSet<Vector2Int> pathBlockedCells = new HashSet<Vector2Int>(occupiedCells);

        foreach (Vector2Int selectedCell in selectedCurrentCells)
        {
            pathBlockedCells.Remove(selectedCell);
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
            List<Vector2Int> path = GridPathfinder.FindPath(
                unit.Cell,
                targetCell,
                mapSize,
                mapSize,
                pathBlockedCells
            );

            if (path.Count == 0)
            {
                occupiedCells.Add(unit.Cell);
                unit.IsMoving = false;
                continue;
            }

            occupiedCells.Add(targetCell);

            unit.AttackTarget = null;
            unit.AttackUnitTarget = null;
            unit.Cell = targetCell;
            unit.TargetCell = targetCell;
            unit.TargetPosition = CellToWorld(targetCell);
            unit.Waypoints.Clear();

            for (int pathIndex = 1; pathIndex < path.Count; pathIndex++)
            {
                unit.Waypoints.Add(CellToWorld(path[pathIndex]));
            }

            unit.IsMoving = true;
            pathBlockedCells.Add(targetCell);
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

    private void LateUpdate()
    {
        ui.Refresh(
            gameState,
            isPaused,
            gameWon,
            gameLost,
            economy.Resources,
            factoryCost,
            infantryCost,
            maxFactoryQueueSize,
            selectedBuilding,
            selectedBuildingData,
            selectedUnits,
            selectionInput,
            buildings,
            units,
            mainCamera
        );
    }

    private void OnDestroy()
    {
        ui?.Destroy();
    }

    private bool IsPointerOverUI()
    {
        return ui != null && ui.IsPointerOverUI();
    }

    private void TrainSelectedFactory()
    {
        TryTrainInfantry(selectedBuildingData);
    }

    private void ResumeGame()
    {
        isPaused = false;
    }

    private void OnUnitRemoved(UnitData unit)
    {
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

    }

    private void OnBuildingRemoved(BuildingData building)
    {
        if (selectedBuildingData == building)
        {
            selectedBuildingData = null;
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
            Vector2 movementTarget = unit.Waypoints.Count > 0
                ? unit.Waypoints[0]
                : unit.TargetPosition;
            Vector2 nextPosition = Vector2.MoveTowards(
                currentPosition,
                movementTarget,
                unitMoveSpeed * Time.deltaTime
            );

            unit.GameObject.transform.position = new Vector3(
                nextPosition.x,
                nextPosition.y,
                0f
            );

            unit.Position = nextPosition;

            if (Vector2.Distance(nextPosition, movementTarget) < 0.01f)
            {
                unit.GameObject.transform.position = new Vector3(
                    movementTarget.x,
                    movementTarget.y,
                    0f
                );

                unit.Position = movementTarget;

                if (unit.Waypoints.Count > 0)
                {
                    unit.Waypoints.RemoveAt(0);
                }

                unit.IsMoving = unit.Waypoints.Count > 0;

                if (!unit.IsMoving)
                {
                    Debug.Log($"{unit.DisplayName} arrived at cell {unit.Cell}");
                }
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
                Debug.LogWarning($"Cannot build: not enough resources. Need {GetBuildingCost(selectedBuilding)}, have {economy.Resources}.");
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
        economy.TrySpend(factoryCost);

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

        Debug.Log($"Factory built at cell {cell}. Remaining resources: {economy.Resources}");
    }

    private bool TryTrainInfantry(BuildingData factory)
    {
        return economy.TryQueueInfantry(factory);
    }

    private bool TrySpawnPlayerInfantry(BuildingData factory)
    {
        if (!TryGetSpawnCellNear(factory.Cell, out Vector2Int spawnCell))
        {
            return false;
        }

        SpawnPlayerInfantry(spawnCell);
        return true;
    }

    private void SpawnPlayerInfantry(Vector2Int spawnCell)
    {
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

        Debug.Log($"Infantry trained at cell {spawnCell}.");
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
        return economy.CanAfford(GetBuildingCost(buildingType));
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

    public ArenaObservation GetArenaObservation()
    {
        return arena.GetObservation();
    }

    public string GetArenaObservationJson()
    {
        return arena.GetObservationJson();
    }

    public ArenaActionResult ExecuteArenaAction(ArenaAction action)
    {
        return arena.Execute(action);
    }

    private void CommandMoveUnits(List<UnitData> actors, Vector2Int cell)
    {
        SelectMultipleUnits(actors);
        TryMoveSelectedUnitsToCell(cell);
    }

    private void CommandAttackUnit(List<UnitData> actors, UnitData target)
    {
        SelectMultipleUnits(actors);
        TryAttackSelectedUnits(target);
    }

    private void CommandAttackBuilding(List<UnitData> actors, BuildingData target)
    {
        SelectMultipleUnits(actors);
        TryAttackSelectedUnits(target);
    }

    private bool TryBuildFactoryAtCell(Vector2Int cell)
    {
        Vector2 position = CellToWorld(cell);
        BuildingType previousSelection = selectedBuilding;
        selectedBuilding = BuildingType.Factory;
        bool canBuild = IsCellInsideMap(cell) && CanBuildAt(position, cell);
        selectedBuilding = previousSelection;

        if (!canBuild)
        {
            return false;
        }

        BuildFactory(position, cell);
        return true;
    }

}
