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

    [Header("Camera Settings")]
    [SerializeField] private float cameraMoveSpeed = 14f;
    [SerializeField] private float cameraZoomSpeed = 3f;
    [SerializeField] private float minCameraSize = 6f;
    [SerializeField] private float maxCameraSize = 18f;

    [SerializeField] private float dragSelectThreshold = 10f;

    private RtsEconomyProductionSystem economy;
    private GridMapService gridMap;
    private BuildingPlacementSystem placement;
    private UnitMovementSystem movement;
    private EnemyAISystem enemyAI;
    private EntityPresentationFactory presentation;
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

    private BuildingData playerBaseData = null;
    private BuildingData enemyBaseData = null;

    private readonly List<BuildingData> buildings = new List<BuildingData>();
    private BuildingData selectedBuildingData = null;

    private readonly List<UnitData> units = new List<UnitData>();
    private UnitData selectedUnitData = null;

    private readonly List<UnitData> selectedUnits = new List<UnitData>();
    private readonly Dictionary<UnitData, GameObject> unitSelectionRings = new Dictionary<UnitData, GameObject>();

    private void Awake()
    {
        gameConfig = gameConfig != null
            ? gameConfig
            : Resources.Load<RtsGameConfig>("RtsGameConfig");

        ApplyGameConfig();
        economy = new RtsEconomyProductionSystem(gameConfig);
        gridMap = new GridMapService(mapSize, cellSize);
        placement = new BuildingPlacementSystem(gameConfig, economy, gridMap);
        movement = new UnitMovementSystem(gameConfig, gridMap, units);
        presentation = new EntityPresentationFactory();
        enemyAI = new EnemyAISystem(gameConfig, gridMap, buildings, CreateEnemyInfantry);
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
            gridMap.OccupiedCells,
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
        enemyInfantryAttackDamage = gameConfig.EnemyInfantryAttackDamage;
        enemyInfantryAttackRange = gameConfig.EnemyInfantryAttackRange;
        enemyInfantryAttackCooldown = gameConfig.EnemyInfantryAttackCooldown;
        startingResources = gameConfig.StartingResources;
        factoryCost = gameConfig.FactoryCost;
        infantryCost = gameConfig.InfantryCost;
        passiveResourceIncome = gameConfig.PassiveResourceIncome;
        passiveResourceInterval = gameConfig.PassiveResourceInterval;
        infantryRadius = gameConfig.InfantryRadius;
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
        enemyAI.Tick(Time.deltaTime, playerBaseData, enemyBaseData);
        combat.Tick(Time.deltaTime);
        movement.Tick(Time.deltaTime);
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

        enemyAI.Reset();

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
        gridMap.Clear();
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
        float half = gridMap.HalfSize;

        for (int i = 0; i <= mapSize; i++)
        {
            float position = -half + i * cellSize;

            presentation.CreateGridLine(
                new Vector3(position, -half, 0),
                new Vector3(position, half, 0),
                gridRoot
            );

            presentation.CreateGridLine(
                new Vector3(-half, position, 0),
                new Vector3(half, position, 0),
                gridRoot
            );
        }

        Debug.Log($"Grid created: {mapSize} x {mapSize}");
    }

    private void CreateBase()
    {
        float half = gridMap.HalfSize;

        basePosition = new Vector2(
            half - cellSize * 2.5f,
            half - cellSize * 2.5f
        );

        Vector2Int baseCell = gridMap.WorldToCell(basePosition);
        gridMap.TryOccupy(baseCell);

        baseObject = presentation.CreateLabeledCircle(
            PresentationEntityKind.PlayerBase,
            "Base",
            basePosition,
            baseRadius,
            new Color(0.25f, 0.55f, 1f, 1f),
            20,
            buildingRoot,
            "基地",
            Color.white
        );

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
        float half = gridMap.HalfSize;

        Vector2 enemyBasePosition = new Vector2(
            -half + cellSize * 2.5f,
            -half + cellSize * 2.5f
        );

        Vector2Int enemyBaseCell = gridMap.WorldToCell(enemyBasePosition);
        gridMap.TryOccupy(enemyBaseCell);

        GameObject enemyBaseObject = presentation.CreateLabeledCircle(
            PresentationEntityKind.EnemyBase,
            "EnemyBase",
            enemyBasePosition,
            baseRadius,
            new Color(1f, 0.25f, 0.25f, 1f),
            20,
            buildingRoot,
            "AI",
            Color.white
        );

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
        buildRangeObject = presentation.CreateCircle(
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
        placementPreviewObject = presentation.CreateCircle(
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
        selectionRingObject = presentation.CreateCircle(
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
        if (!placement.CanAfford(BuildingType.Factory))
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
        GameObject ringObject = presentation.CreateCircle(
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

        if (!gridMap.IsWorldInside(mouseWorldPosition))
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

        Vector2Int targetCell = gridMap.WorldToCell(mouseWorldPosition);

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
        int moveCount = movement.CommandGroupMove(selectedUnits, centerCell);

        if (moveCount == 0)
        {
            Debug.LogWarning("Cannot move units: no valid target cells.");
            return;
        }

        Debug.Log($"Move command: {moveCount} units -> around cell {centerCell}");
    }

    private UnitData CreateEnemyInfantry(Vector2Int spawnCell)
    {
        Vector2 spawnPosition = gridMap.CellToWorld(spawnCell);

        GameObject enemyInfantryObject = presentation.CreateLabeledCircle(
            PresentationEntityKind.EnemyInfantry,
            "EnemyInfantry",
            spawnPosition,
            infantryRadius,
            new Color(1f, 0.45f, 0.15f, 1f),
            25,
            buildingRoot,
            "敌",
            Color.black
        );

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

        gridMap.TryOccupy(spawnCell);
        enemyInfantry.Id = nextEntityId++;
        units.Add(enemyInfantry);
        return enemyInfantry;
    }
    
    private void MoveUnitTowards(UnitData unit, Vector2 targetPosition)
    {
        movement.MoveTowards(unit, targetPosition, Time.deltaTime);
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
        presentation?.Dispose();
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
    private void HandlePlacementPreview()
    {
        if (selectedBuilding == BuildingType.None)
        {
            return;
        }

        Vector2 mouseWorldPosition = GetMouseWorldPosition();

        if (!gridMap.IsWorldInside(mouseWorldPosition))
        {
            hasPreviewCell = false;
            SetPlacementPreviewVisible(false);
            return;
        }

        currentPreviewCell = gridMap.WorldToCell(mouseWorldPosition);
        currentPreviewPosition = gridMap.CellToWorld(currentPreviewCell);
        hasPreviewCell = true;

        SetPlacementPreviewVisible(true);

        placementPreviewObject.transform.position = new Vector3(
            currentPreviewPosition.x,
            currentPreviewPosition.y,
            -0.2f
        );

        bool canBuild = placement.CanPlace(
            selectedBuilding,
            basePosition,
            currentPreviewPosition,
            currentPreviewCell
        );
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

        bool canBuild = placement.CanPlace(
            selectedBuilding,
            basePosition,
            currentPreviewPosition,
            currentPreviewCell
        );

        if (!canBuild)
        {
            if (!placement.CanAfford(selectedBuilding))
            {
                Debug.LogWarning($"Cannot build: not enough resources. Need {placement.GetCost(selectedBuilding)}, have {economy.Resources}.");
            }
            else
            {
                Debug.LogWarning("Cannot build here: out of range or cell is occupied.");
            }

            return;
        }

        BuildFactory(currentPreviewPosition, currentPreviewCell);
    }

    private bool BuildFactory(Vector2 position, Vector2Int cell)
    {
        if (!placement.TryReserve(BuildingType.Factory, basePosition, position, cell))
        {
            return false;
        }

        GameObject factoryObject = presentation.CreateLabeledCircle(
            PresentationEntityKind.Factory,
            "Factory",
            position,
            buildingRadius,
            new Color(0.35f, 0.9f, 0.45f, 1f),
            20,
            buildingRoot,
            "兵厂",
            Color.black
        );

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
        
        Debug.Log($"Factory built at cell {cell}. Remaining resources: {economy.Resources}");
        return true;
    }

    private bool TryTrainInfantry(BuildingData factory)
    {
        return economy.TryQueueInfantry(factory);
    }

    private bool TrySpawnPlayerInfantry(BuildingData factory)
    {
        if (!gridMap.TryFindOpenCellNear(factory.Cell, out Vector2Int spawnCell))
        {
            return false;
        }

        SpawnPlayerInfantry(spawnCell);
        return true;
    }

    private void SpawnPlayerInfantry(Vector2Int spawnCell)
    {
        Vector2 spawnPosition = gridMap.CellToWorld(spawnCell);

        GameObject infantryObject = presentation.CreateLabeledCircle(
            PresentationEntityKind.PlayerInfantry,
            "Infantry",
            spawnPosition,
            infantryRadius,
            new Color(0.95f, 0.95f, 0.25f, 1f),
            25,
            buildingRoot,
            "兵",
            Color.black
        );

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

        gridMap.TryOccupy(spawnCell);
        infantry.Id = nextEntityId++;
        units.Add(infantry);

        Debug.Log($"Infantry trained at cell {spawnCell}.");
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(mousePosition);
        return new Vector2(worldPosition.x, worldPosition.y);
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
        presentation.SetCircleColor(
            placementPreviewObject,
            canBuild
            ? new Color(0.2f, 1f, 0.35f, 0.45f)
            : new Color(1f, 0.2f, 0.2f, 0.45f)
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
        return BuildFactory(gridMap.CellToWorld(cell), cell);
    }

}
