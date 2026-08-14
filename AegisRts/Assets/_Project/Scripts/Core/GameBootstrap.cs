using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private RtsGameConfig gameConfig;

    [Header("Map Settings")]
    [SerializeField] private int mapSize = 48;
    [SerializeField] private float cellSize = 1f;

    [Header("Base Settings")]
    [SerializeField] private float baseRadius = 1.25f;
    [SerializeField] private int baseFootprintRadius = 1;
    [SerializeField] private float buildRadius = 7f;

    [Header("Building Settings")]
    [SerializeField] private float buildingRadius = 1.05f;
    [SerializeField] private float infantryTrainingTime = 3f;
    [SerializeField] private float artilleryTrainingTime = 6f;
    [SerializeField] private int maxFactoryQueueSize = 5;
    [SerializeField] private int garrisonCapacity = 4;
    [SerializeField] private float garrisonDamageMultiplier = 1.5f;

    [Header("Health Settings")]
    [SerializeField] private int playerBaseHitPoints = 500;
    [SerializeField] private int factoryHitPoints = 300;
    [SerializeField] private int garrisonHitPoints = 350;
    [SerializeField] private int enemyBaseHitPoints = 400;

    [Header("Combat Settings")]
    [SerializeField] private int infantryAttackDamage = 20;
    [SerializeField] private float infantryAttackRange = 1.2f;
    [SerializeField] private float infantryAttackCooldown = 1f;
    [SerializeField] private int playerInfantryHitPoints = 100;
    [SerializeField] private int artilleryAttackDamage = 45;
    [SerializeField] private float artilleryAttackRange = 6f;
    [SerializeField] private float artilleryAttackCooldown = 2.4f;
    [SerializeField] private float artilleryBuildingDamageMultiplier = 1.75f;
    [SerializeField] private int playerArtilleryHitPoints = 70;
    [SerializeField] private int enemyInfantryHitPoints = 80;
    [SerializeField] private float unitAggroRange = 4f;

    [Header("Enemy AI Settings")]
    [SerializeField] private int enemyInfantryAttackDamage = 10;
    [SerializeField] private float enemyInfantryAttackRange = 1.2f;
    [SerializeField] private float enemyInfantryAttackCooldown = 1.2f;

    [Header("Resource Settings")]
    [SerializeField] private int startingResources = 500;
    [SerializeField] private int factoryCost = 150;
    [SerializeField] private int garrisonCost = 120;
    [SerializeField] private int infantryCost = 50;
    [SerializeField] private int artilleryCost = 120;
    [SerializeField] private int passiveResourceIncome = 10;
    [SerializeField] private float passiveResourceInterval = 5f;

    [Header("Unit Settings")]
    [SerializeField] private float infantryRadius = 0.42f;
    [SerializeField] private float artilleryRadius = 0.52f;
    [SerializeField] private float artilleryMoveSpeed = 2.6f;

    [Header("Camera Settings")]
    [SerializeField] private float cameraMoveSpeed = 14f;
    [SerializeField] private float cameraZoomSpeed = 3f;
    [SerializeField] private float minCameraSize = 6f;
    [SerializeField] private float initialCameraSize = 6f;
    [SerializeField] private float maxCameraSize = 26f;
    [SerializeField, Range(0f, 1f)] private float initialCameraInwardBias;

    [SerializeField] private float dragSelectThreshold = 10f;

    [Header("Visibility Settings")]
    [SerializeField] private float buildingSightRange = 6f;
    [SerializeField] private float unitSightRange = 5f;
    [SerializeField] private float enemyLastKnownDuration = 8f;

    private RtsEconomyProductionSystem economy;
    private GridMapService gridMap;
    private BuildingPlacementSystem placement;
    private UnitMovementSystem movement;
    private EnemyAISystem enemyAI;
    private EntityPresentationFactory presentation;
    private ArenaOrchestrator arena;
    private ArenaTelemetrySystem telemetry;
    private RtsEntityLifecycle lifecycle;
    private RtsCombatSystem combat;
    private RtsWorldFeedbackSystem feedback;
    private RtsVisibilitySystem visibility;
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
    private GameObject artilleryRangeObject;

    private Vector2 basePosition;
    private Vector2 currentPreviewPosition;
    private bool inspectorDemoPrepared;
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
        telemetry = new ArenaTelemetrySystem(() => matchTime);
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
            TryTrainArtillery,
            SetArtilleryDeployment,
            TryGarrisonUnits,
            EvacuateGarrison,
            TryBuildFactoryAtCell,
            TryBuildGarrisonAtCell
        );
        lifecycle = new RtsEntityLifecycle(
            buildings,
            units,
            gridMap.OccupiedCells,
            OnUnitRemoved,
            OnBuildingRemoved
        );
        combat = new RtsCombatSystem(
            gameConfig,
            buildings,
            units,
            MoveUnitTowards,
            lifecycle,
            PlayCombatFeedback,
            OnTargetAcquired
        );
        selectionInput = new RtsSelectionInputController(dragSelectThreshold);
        ui = new RtsGameUIController(
            StartGame,
            SelectFactory,
            SelectGarrison,
            CancelBuildMode,
            TrainSelectedFactory,
            TrainSelectedFactoryArtillery,
            ToggleSelectedArtilleryDeployment,
            EvacuateSelectedGarrison,
            ToggleArenaInspector,
            PrepareInspectorDemo,
            ResumeGame,
            RestartGame,
            ReturnToMainMenu,
            NavigateFromMinimap
        );
        mainCamera = Camera.main;
        cameraController = gameObject.AddComponent<RtsCameraController>();

        cameraController.Configure(
            mainCamera,
            mapSize * cellSize,
            cameraMoveSpeed,
            cameraZoomSpeed,
            minCameraSize,
            initialCameraSize,
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
        baseFootprintRadius = gameConfig.BaseFootprintRadius;
        buildRadius = gameConfig.BuildRadius;
        buildingRadius = gameConfig.BuildingRadius;
        infantryTrainingTime = gameConfig.InfantryTrainingTime;
        artilleryTrainingTime = gameConfig.ArtilleryTrainingTime;
        maxFactoryQueueSize = gameConfig.MaxFactoryQueueSize;
        garrisonCapacity = gameConfig.GarrisonCapacity;
        garrisonDamageMultiplier = gameConfig.GarrisonDamageMultiplier;
        playerBaseHitPoints = gameConfig.PlayerBaseHitPoints;
        factoryHitPoints = gameConfig.FactoryHitPoints;
        garrisonHitPoints = gameConfig.GarrisonHitPoints;
        enemyBaseHitPoints = gameConfig.EnemyBaseHitPoints;
        infantryAttackDamage = gameConfig.InfantryAttackDamage;
        infantryAttackRange = gameConfig.InfantryAttackRange;
        infantryAttackCooldown = gameConfig.InfantryAttackCooldown;
        playerInfantryHitPoints = gameConfig.PlayerInfantryHitPoints;
        artilleryAttackDamage = gameConfig.ArtilleryAttackDamage;
        artilleryAttackRange = gameConfig.ArtilleryAttackRange;
        artilleryAttackCooldown = gameConfig.ArtilleryAttackCooldown;
        artilleryBuildingDamageMultiplier = gameConfig.ArtilleryBuildingDamageMultiplier;
        playerArtilleryHitPoints = gameConfig.PlayerArtilleryHitPoints;
        enemyInfantryHitPoints = gameConfig.EnemyInfantryHitPoints;
        unitAggroRange = gameConfig.UnitAggroRange;
        enemyInfantryAttackDamage = gameConfig.EnemyInfantryAttackDamage;
        enemyInfantryAttackRange = gameConfig.EnemyInfantryAttackRange;
        enemyInfantryAttackCooldown = gameConfig.EnemyInfantryAttackCooldown;
        startingResources = gameConfig.StartingResources;
        factoryCost = gameConfig.FactoryCost;
        garrisonCost = gameConfig.GarrisonCost;
        infantryCost = gameConfig.InfantryCost;
        artilleryCost = gameConfig.ArtilleryCost;
        passiveResourceIncome = gameConfig.PassiveResourceIncome;
        passiveResourceInterval = gameConfig.PassiveResourceInterval;
        infantryRadius = gameConfig.InfantryRadius;
        artilleryRadius = gameConfig.ArtilleryRadius;
        artilleryMoveSpeed = gameConfig.ArtilleryMoveSpeed;
        cameraMoveSpeed = gameConfig.CameraMoveSpeed;
        cameraZoomSpeed = gameConfig.CameraZoomSpeed;
        minCameraSize = gameConfig.MinCameraSize;
        initialCameraSize = gameConfig.InitialCameraSize;
        maxCameraSize = gameConfig.MaxCameraSize;
        initialCameraInwardBias = gameConfig.InitialCameraInwardBias;
        dragSelectThreshold = gameConfig.DragSelectThreshold;
        buildingSightRange = gameConfig.BuildingSightRange;
        unitSightRange = gameConfig.UnitSightRange;
        enemyLastKnownDuration = gameConfig.EnemyLastKnownDuration;
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

        if (Input.GetKeyDown(KeyCode.M))
        {
            cameraController.ToggleStrategicView();
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            ToggleArenaInspector();
        }

        if (isPaused)
        {
            return;
        }

        matchTime += Time.deltaTime;
        cameraController.Tick(Time.deltaTime);
        economy.TickIncome(Time.deltaTime);
        economy.TickProduction(Time.deltaTime, buildings, TrySpawnPlayerUnit);
        selectionInput.TickSelection(
            selectedBuilding == BuildingType.None,
            IsPointerOverUI,
            TryBeginDirectUnitMove,
            HandleSingleClickSelection,
            SelectUnitsInDragRect,
            MoveDraggedUnitsToPointer
        );
        HandleUnitMoveCommand();
        HandlePlacementPreview();
        HandlePlacementConfirm();
        if (enemyAI.Tick(Time.deltaTime, playerBaseData, enemyBaseData))
        {
            telemetry.RecordWave(CountUnits(Team.Enemy));
        }
        combat.Tick(Time.deltaTime);
        feedback?.Tick(Time.deltaTime);
        movement.Tick(Time.deltaTime);
        UpdatePendingGarrisons();
        visibility?.Tick(Time.deltaTime);
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
        inspectorDemoPrepared = false;
        telemetry.Reset();
        telemetry.Record(ArenaTelemetryKind.Match, "MATCH INITIALIZED");

        gridRoot = new GameObject("GridRoot").transform;
        buildingRoot = new GameObject("BuildingRoot").transform;
        feedback = new RtsWorldFeedbackSystem(presentation, buildingRoot);
        CreateGrid();
        CreateBase();
        CreateEnemyBase();
        FocusCameraOnPlayerBase();
        CreateBuildRangeObject();
        CreatePlacementPreviewObject();
        CreateSelectionRingObject();
        CreateArtilleryRangeObject();
        visibility = new RtsVisibilitySystem(
            gridMap,
            buildings,
            units,
            buildingSightRange,
            unitSightRange,
            enemyLastKnownDuration,
            buildingRoot
        );
        visibility.Tick(0f);

        enemyAI.Reset();

        Debug.Log($"Starting resources: {economy.Resources}");
    }

    private void FocusCameraOnPlayerBase()
    {
        Vector2 focusPosition = Vector2.Lerp(
            basePosition,
            Vector2.zero,
            initialCameraInwardBias
        );
        cameraController.CenterOnWorld(focusPosition);
    }

    private void RestartGame()
    {
        StopAllCoroutines();
        DestroyGameWorld();
        CreateGameWorld();
        gameWorldCreated = true;
        gameState = GameState.Playing;
        isPaused = false;
    }

    private void ReturnToMainMenu()
    {
        StopAllCoroutines();
        DestroyGameWorld();
        gameWorldCreated = false;
        gameState = GameState.MainMenu;
        isPaused = false;
    }

    private void DestroyGameWorld()
    {
        feedback?.Clear();
        feedback = null;
        visibility?.Destroy();
        visibility = null;
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
        artilleryRangeObject = null;
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
            half - cellSize * 5f,   // Keep the opening camera focused on the player base.
            half - cellSize * 5f
        );

        Vector2Int baseCell = gridMap.WorldToCell(basePosition);
        List<Vector2Int> baseFootprint = gridMap.GetSquareFootprint(
            baseCell,
            baseFootprintRadius
        );
        gridMap.TryOccupy(baseFootprint);

        baseObject = presentation.CreateLabeledCircle(
            PresentationEntityKind.PlayerBase,
            "Base",
            basePosition,
            baseRadius,
            new Color(0.25f, 0.55f, 1f, 1f),
            20,
            buildingRoot,
            "HQ",
            Color.white
        );

        playerBaseData = new BuildingData(
            "Player Base",
            BuildingType.Base,
            baseObject,
            basePosition,
            baseCell,
            baseRadius,
            "Primary headquarters for construction and resource management.",
            Team.Player,
            playerBaseHitPoints,
            baseFootprint
        );

        buildings.Add(playerBaseData);
        playerBaseData.Id = nextEntityId++;
        telemetry.RecordBuilding(playerBaseData);

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
        List<Vector2Int> enemyBaseFootprint = gridMap.GetSquareFootprint(
            enemyBaseCell,
            baseFootprintRadius
        );
        gridMap.TryOccupy(enemyBaseFootprint);

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
            "AI Base",
            BuildingType.Base,
            enemyBaseObject,
            enemyBasePosition,
            enemyBaseCell,
            baseRadius,
            "Enemy AI headquarters. Destroy it to win the match.",
            Team.Enemy,
            enemyBaseHitPoints,
            enemyBaseFootprint
        );

        buildings.Add(enemyBaseData);
        enemyBaseData.Id = nextEntityId++;
        telemetry.RecordBuilding(enemyBaseData);

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

    private void CreateArtilleryRangeObject()
    {
        artilleryRangeObject = presentation.CreateCircle(
            "ArtilleryRange",
            Vector2.zero,
            artilleryAttackRange,
            new Color(0.8f, 0.35f, 1f, 0.09f),
            14,
            buildingRoot
        );

        artilleryRangeObject.SetActive(false);
    }

    private void SelectFactory()
    {
        if (!placement.CanAfford(BuildingType.Factory))
        {
            Debug.LogWarning($"Cannot select Factory: not enough resources. Need {factoryCost}, have {economy.Resources}.");
            ui.ShowNotification($"Not enough resources. A factory costs {factoryCost}.", true);
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

        Debug.Log("Selected building: Factory.");
    }

    private void SelectGarrison()
    {
        if (!placement.CanAfford(BuildingType.Garrison))
        {
            Debug.LogWarning(
                $"Cannot select Garrison: not enough resources. Need {garrisonCost}, have {economy.Resources}."
            );
            ui.ShowNotification($"Not enough resources. A garrison costs {garrisonCost}.", true);
            return;
        }

        selectedBuilding = BuildingType.Garrison;
        hasPreviewCell = false;

        if (buildRangeObject != null)
        {
            buildRangeObject.SetActive(true);
        }

        if (placementPreviewObject != null)
        {
            placementPreviewObject.SetActive(true);
        }

        Debug.Log("Selected building: Garrison.");
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

    private bool TryBeginDirectUnitMove()
    {
        UnitData draggedUnit = FindUnitAt(GetMouseWorldPosition());

        if (draggedUnit == null || draggedUnit.Team != Team.Player)
        {
            return false;
        }

        if (!selectedUnits.Contains(draggedUnit))
        {
            SelectSingleUnit(draggedUnit);
        }

        return true;
    }

    private void MoveDraggedUnitsToPointer()
    {
        if (selectedUnits.Count == 0)
        {
            return;
        }

        Vector2 targetPosition = GetMouseWorldPosition();

        if (!gridMap.IsWorldInside(targetPosition))
        {
            Debug.LogWarning("Cannot drag units: target is outside the map.");
            return;
        }

        TryMoveSelectedUnitsToCell(
            gridMap.WorldToCell(targetPosition),
            targetPosition
        );
    }

    private void SelectUnitsInDragRect()
    {
        Rect selectionRect = GetScreenRect(selectionInput.DragStart, selectionInput.DragCurrent);

        List<UnitData> unitsInRect = new List<UnitData>();

        foreach (UnitData unit in units)
        {
            if (unit.Team != Team.Player || unit.GarrisonBuilding != null)
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

            if (building.Team == Team.Enemy &&
                visibility != null &&
                !visibility.IsVisible(building.Position))
            {
                continue;
            }

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

            if (unit.GarrisonBuilding != null)
            {
                continue;
            }

            if (unit.Team == Team.Enemy &&
                visibility != null &&
                !visibility.IsVisible(unit.Position))
            {
                continue;
            }

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
            if (unit == null ||
                unit.Team != Team.Player ||
                unit.GarrisonBuilding != null)
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

        UpdateArtilleryRangeIndicator();
    }

    private void UpdateArtilleryRangeIndicator()
    {
        UnitData artillery = selectedUnits.Count == 1
            ? selectedUnits[0]
            : null;
        bool showRange = artillery != null &&
            artillery.Type == UnitType.Artillery &&
            artillery.IsDeployed;

        if (artilleryRangeObject == null)
        {
            return;
        }

        artilleryRangeObject.SetActive(showRange);

        if (showRange)
        {
            artilleryRangeObject.transform.position = new Vector3(
                artillery.Position.x,
                artillery.Position.y,
                0f
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

        if (targetBuilding != null &&
            targetBuilding.Team == Team.Player &&
            targetBuilding.Type == BuildingType.Garrison)
        {
            TryGarrisonUnits(selectedUnits, targetBuilding);
            return;
        }

        if (targetBuilding != null && targetBuilding.Team == Team.Enemy)
        {
            TryAttackSelectedUnits(targetBuilding);
            return;
        }

        Vector2Int targetCell = gridMap.WorldToCell(mouseWorldPosition);

        TryMoveSelectedUnitsToCell(targetCell, mouseWorldPosition);
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
            unit.GarrisonTarget = null;
            unit.IsMoving = false;
            unit.Waypoints.Clear();
            commandCount++;
        }

        Debug.Log($"Attack command: {commandCount} units -> {targetBuilding.DisplayName}");
        telemetry.RecordPlayerOrder(
            $"ORDER ATTACK  UNITS {commandCount:00} -> " +
            $"E#{targetBuilding.Id} {targetBuilding.Type}"
        );
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
            unit.GarrisonTarget = null;
            unit.IsMoving = false;
            unit.Waypoints.Clear();
            commandCount++;
        }

        Debug.Log($"Attack command: {commandCount} units -> {targetUnit.DisplayName}");
        telemetry.RecordPlayerOrder(
            $"ORDER ATTACK  UNITS {commandCount:00} -> " +
            $"E#{targetUnit.Id} {targetUnit.Type}"
        );
    }

    private void TryMoveSelectedUnitsToCell(
        Vector2Int centerCell,
        Vector2? centerWorldPosition = null
    )
    {
        int moveCount = centerWorldPosition.HasValue
            ? movement.CommandGroupMove(
                selectedUnits,
                centerCell,
                centerWorldPosition.Value
            )
            : movement.CommandGroupMove(selectedUnits, centerCell);

        if (moveCount == 0)
        {
            Debug.LogWarning("Cannot move units: no valid target cells.");
            return;
        }

        Debug.Log($"Move command: {moveCount} units -> around cell {centerCell}");
        telemetry.RecordPlayerOrder(
            $"ORDER MOVE  UNITS {moveCount:00} -> CELL {centerCell.x:00},{centerCell.y:00}"
        );
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
            string.Empty,
            Color.black
        );

        UnitData enemyInfantry = new UnitData(
            "Enemy Infantry",
            UnitType.Infantry,
            enemyInfantryObject,
            spawnPosition,
            spawnCell,
            infantryRadius,
            "AI infantry attacks nearby player units first, then advances on the player base.",
            Team.Enemy,
            enemyInfantryHitPoints,
            enemyInfantryAttackDamage,
            enemyInfantryAttackRange,
            enemyInfantryAttackCooldown
        );

        gridMap.TryOccupy(spawnCell);
        enemyInfantry.Id = nextEntityId++;
        units.Add(enemyInfantry);
        telemetry.RecordProduced(enemyInfantry);
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
            matchTime,
            economy.Resources,
            factoryCost,
            garrisonCost,
            infantryCost,
            artilleryCost,
            maxFactoryQueueSize,
            infantryTrainingTime,
            artilleryTrainingTime,
            selectedBuilding,
            selectedBuildingData,
            selectedUnits,
            selectionInput,
            buildings,
            units,
            mainCamera,
            gridMap.MapSize,
            gridMap.HalfSize,
            visibility,
            telemetry
        );
        ui.Tick(Time.unscaledDeltaTime);
    }

    private void OnDestroy()
    {
        feedback?.Clear();
        visibility?.Destroy();
        ui?.Destroy();
        presentation?.Dispose();
    }

    private bool IsPointerOverUI()
    {
        return ui != null && ui.IsPointerOverUI();
    }

    public void ToggleArenaInspector()
    {
        if (ui == null || mainCamera == null)
        {
            return;
        }

        bool opening = !ui.IsInspectorVisible;
        ui.ToggleInspector(mainCamera);

        if (opening)
        {
            cameraController.CenterOnWorld(GetInspectorFocusPosition());
        }
    }

    private Vector2 GetInspectorFocusPosition()
    {
        if (selectedUnits.Count > 0)
        {
            Vector2 center = Vector2.zero;

            foreach (UnitData unit in selectedUnits)
            {
                center += unit.Position;
            }

            return center / selectedUnits.Count;
        }

        if (selectedBuildingData != null)
        {
            return selectedBuildingData.Position;
        }

        return playerBaseData != null ? playerBaseData.Position : basePosition;
    }

    public void PrepareInspectorDemo()
    {
        if (gameState != GameState.Playing || gameWon || gameLost)
        {
            ui.ShowNotification("Start a match before running the showcase.", true);
            return;
        }

        if (inspectorDemoPrepared)
        {
            ui.ShowNotification("The showcase scenario is already active.");
            return;
        }

        DestroyGameWorld();
        CreateGameWorld();
        gameWorldCreated = true;
        gameState = GameState.Playing;
        isPaused = false;
        inspectorDemoPrepared = true;
        telemetry.Record(
            ArenaTelemetryKind.Demo,
            "SHOWCASE RESET  deterministic scenario initialized"
        );
        StartCoroutine(RunInspectorDemo());
    }

    private IEnumerator RunInspectorDemo()
    {
        telemetry.SetObjective("Demonstrate scouting, defense and combined arms");
        telemetry.SetDemoPhase("01 INFRASTRUCTURE");
        BuildingData factory = FindPlayerBuilding(BuildingType.Factory);
        BuildingData garrison = FindPlayerBuilding(BuildingType.Garrison);

        if (factory == null)
        {
            Vector2Int factoryCell = playerBaseData.Cell + Vector2Int.left * 4;
            BuildFactory(gridMap.CellToWorld(factoryCell), factoryCell);
            factory = FindPlayerBuilding(BuildingType.Factory);
        }

        if (garrison == null)
        {
            Vector2Int garrisonCell = playerBaseData.Cell + Vector2Int.down * 4;
            BuildGarrison(gridMap.CellToWorld(garrisonCell), garrisonCell);
            garrison = FindPlayerBuilding(BuildingType.Garrison);
        }

        yield return new WaitForSeconds(0.8f);

        telemetry.SetDemoPhase("02 FORCE COMPOSITION");
        Vector2Int playerOrigin = factory != null
            ? factory.Cell
            : playerBaseData.Cell;
        List<UnitData> demoInfantry = new List<UnitData>();
        List<UnitData> demoArtillery = new List<UnitData>();

        for (int index = 0; index < 6; index++)
        {
            if (!gridMap.TryFindOpenCellNear(playerOrigin, out Vector2Int spawnCell))
            {
                break;
            }

            SpawnPlayerInfantry(spawnCell);
            demoInfantry.Add(units[units.Count - 1]);
        }

        for (int index = 0; index < 2; index++)
        {
            if (!gridMap.TryFindOpenCellNear(playerOrigin, out Vector2Int spawnCell))
            {
                break;
            }

            SpawnPlayerArtillery(spawnCell);
            demoArtillery.Add(units[units.Count - 1]);
        }

        yield return new WaitForSeconds(0.8f);

        telemetry.SetDemoPhase("03 DEFENSIVE POSTURE");
        if (garrison != null)
        {
            int garrisonCount = Mathf.Min(2, demoInfantry.Count);

            for (int index = 0; index < garrisonCount; index++)
            {
                EnterGarrison(demoInfantry[index], garrison);
            }
        }

        if (demoArtillery.Count > 0)
        {
            SetArtilleryDeployment(
                new List<UnitData> { demoArtillery[0] },
                true
            );
        }

        yield return new WaitForSeconds(0.8f);

        telemetry.SetDemoPhase("04 RECON MOVEMENT");
        Vector2 combatPosition = Vector2.Lerp(basePosition, Vector2.zero, 0.38f);
        Vector2Int combatCell = gridMap.WorldToCell(combatPosition);
        List<UnitData> movingInfantry = new List<UnitData>();

        for (int index = 0; index < demoInfantry.Count; index++)
        {
            UnitData infantry = demoInfantry[index];

            if (infantry.GarrisonBuilding == null && movingInfantry.Count < 3)
            {
                movingInfantry.Add(infantry);
            }
        }

        movement.CommandGroupMove(movingInfantry, combatCell, combatPosition);
        telemetry.RecordPlayerOrder(
            $"DEMO RECON  UNITS {movingInfantry.Count:00} -> CELL {combatCell.x:00},{combatCell.y:00}"
        );
        SelectMultipleUnits(movingInfantry);

        yield return new WaitForSeconds(1.2f);

        telemetry.SetDemoPhase("05 ENEMY CONTACT");
        for (int index = 0; index < 7; index++)
        {
            if (!gridMap.TryFindOpenCellNear(combatCell, out Vector2Int spawnCell))
            {
                break;
            }

            UnitData enemy = CreateEnemyInfantry(spawnCell);
            enemy.AttackTarget = playerBaseData;
            enemy.IsMoving = false;
        }

        telemetry.RecordWave(CountUnits(Team.Enemy));
        visibility?.Tick(0f);
        cameraController.CenterOnWorld(Vector2.Lerp(basePosition, combatPosition, 0.45f));
        ui.ShowNotification("Showcase ready: movement, garrison, artillery and enemy contact are active.");
        yield return new WaitForSeconds(1.2f);
        telemetry.SetDemoPhase("06 LIVE ENGAGEMENT");
        telemetry.SetObjective("Hold the garrison and eliminate the contact group");
    }

    private BuildingData FindPlayerBuilding(BuildingType type)
    {
        foreach (BuildingData building in buildings)
        {
            if (building != null && building.Team == Team.Player && building.Type == type)
            {
                return building;
            }
        }

        return null;
    }

    private void TrainSelectedFactory()
    {
        TryTrainInfantry(selectedBuildingData);
    }

    private void TrainSelectedFactoryArtillery()
    {
        TryTrainArtillery(selectedBuildingData);
    }

    private void ToggleSelectedArtilleryDeployment()
    {
        List<UnitData> artilleryUnits = new List<UnitData>();
        bool shouldDeploy = false;

        foreach (UnitData unit in selectedUnits)
        {
            if (unit == null || unit.Type != UnitType.Artillery)
            {
                continue;
            }

            artilleryUnits.Add(unit);
            shouldDeploy |= !unit.IsDeployed;
        }

        if (artilleryUnits.Count == 0)
        {
            return;
        }

        SetArtilleryDeployment(artilleryUnits, shouldDeploy);
        ui.ShowNotification(
            shouldDeploy
                ? $"Deployed {artilleryUnits.Count} artillery unit(s)."
                : $"Undeployed {artilleryUnits.Count} artillery unit(s)."
        );
    }

    private void SetArtilleryDeployment(
        List<UnitData> artilleryUnits,
        bool deployed
    )
    {
        int changedCount = 0;

        foreach (UnitData unit in artilleryUnits)
        {
            if (unit == null ||
                unit.Team != Team.Player ||
                unit.Type != UnitType.Artillery)
            {
                continue;
            }

            if (deployed)
            {
                movement.Stop(unit);
            }

            unit.IsDeployed = deployed;
            changedCount++;
            presentation.SetCircleColor(
                unit.GameObject,
                deployed
                    ? new Color(0.55f, 0.2f, 0.95f, 1f)
                    : new Color(0.8f, 0.35f, 1f, 1f)
            );
        }

        if (changedCount > 0)
        {
            telemetry.Record(
                ArenaTelemetryKind.Deployment,
                $"ARTILLERY {(deployed ? "DEPLOY" : "MOBILE")}  COUNT {changedCount:00}",
                Team.Player
            );
        }
    }

    private int TryGarrisonUnits(
        List<UnitData> actors,
        BuildingData targetBuilding
    )
    {
        if (targetBuilding == null ||
            targetBuilding.Team != Team.Player ||
            targetBuilding.Type != BuildingType.Garrison)
        {
            return 0;
        }

        int reservedSlots = targetBuilding.GarrisonedUnits.Count;

        foreach (UnitData unit in units)
        {
            if (unit != null && unit.GarrisonTarget == targetBuilding)
            {
                reservedSlots++;
            }
        }

        int orderedCount = 0;

        foreach (UnitData unit in new List<UnitData>(actors))
        {
            if (reservedSlots >= targetBuilding.GarrisonCapacity ||
                unit == null ||
                unit.Team != Team.Player ||
                unit.Type != UnitType.Infantry ||
                unit.GarrisonTarget != null ||
                unit.GarrisonBuilding != null)
            {
                continue;
            }

            if (!gridMap.TryFindOpenCellNear(
                    targetBuilding.Cell,
                    out Vector2Int approachCell
                ))
            {
                break;
            }

            int commanded = movement.CommandGroupMove(
                new List<UnitData> { unit },
                approachCell
            );

            if (commanded == 0)
            {
                continue;
            }

            unit.GarrisonTarget = targetBuilding;
            reservedSlots++;
            orderedCount++;
        }

        if (orderedCount > 0)
        {
            telemetry.Record(
                ArenaTelemetryKind.Garrison,
                $"ORDER GARRISON  UNITS {orderedCount:00} -> P#{targetBuilding.Id}",
                Team.Player
            );
            ui.ShowNotification(
                $"Ordered {orderedCount} infantry unit(s) into the garrison."
            );
        }
        else
        {
            ui.ShowNotification(
                $"Cannot garrison: infantry only. Capacity {targetBuilding.GarrisonedUnits.Count}/{targetBuilding.GarrisonCapacity}.",
                true
            );
        }

        return orderedCount;
    }

    private void UpdatePendingGarrisons()
    {
        foreach (UnitData unit in new List<UnitData>(units))
        {
            BuildingData targetBuilding = unit?.GarrisonTarget;

            if (targetBuilding == null)
            {
                continue;
            }

            if (!buildings.Contains(targetBuilding) ||
                targetBuilding.Team != Team.Player ||
                targetBuilding.Type != BuildingType.Garrison)
            {
                unit.GarrisonTarget = null;
                continue;
            }

            if (unit.IsMoving)
            {
                continue;
            }

            EnterGarrison(unit, targetBuilding);
        }
    }

    private bool EnterGarrison(UnitData unit, BuildingData targetBuilding)
    {
        if (unit == null ||
            targetBuilding == null ||
            unit.Type != UnitType.Infantry ||
            unit.Team != targetBuilding.Team ||
            unit.GarrisonBuilding != null ||
            targetBuilding.GarrisonedUnits.Count >= targetBuilding.GarrisonCapacity)
        {
            if (unit != null)
            {
                unit.GarrisonTarget = null;
            }

            return false;
        }

        gridMap.Release(unit.Cell);
        unit.GarrisonTarget = null;
        unit.GarrisonBuilding = targetBuilding;
        unit.Position = targetBuilding.Position;
        unit.Cell = targetBuilding.Cell;
        unit.TargetPosition = targetBuilding.Position;
        unit.TargetCell = targetBuilding.Cell;
        unit.IsMoving = false;
        unit.Waypoints.Clear();
        unit.AttackTarget = null;
        unit.AttackUnitTarget = null;
        targetBuilding.GarrisonedUnits.Add(unit);

        if (unit.GameObject != null)
        {
            unit.GameObject.transform.position = new Vector3(
                targetBuilding.Position.x,
                targetBuilding.Position.y,
                0f
            );
            unit.GameObject.SetActive(false);
        }

        RemoveUnitFromSelection(unit);
        Debug.Log(
            $"{unit.DisplayName} entered {targetBuilding.DisplayName}. " +
            $"Garrison: {targetBuilding.GarrisonedUnits.Count}/{targetBuilding.GarrisonCapacity}"
        );
        telemetry.Record(
            ArenaTelemetryKind.Garrison,
            $"P#{unit.Id} entered P#{targetBuilding.Id}  LOAD " +
            $"{targetBuilding.GarrisonedUnits.Count}/{targetBuilding.GarrisonCapacity}",
            Team.Player
        );
        return true;
    }

    private void EvacuateSelectedGarrison()
    {
        if (selectedBuildingData == null ||
            selectedBuildingData.Type != BuildingType.Garrison)
        {
            return;
        }

        EvacuateGarrison(selectedBuildingData);
    }

    private void EvacuateGarrison(BuildingData building)
    {
        int evacuatedCount = EvacuateGarrison(building, true);

        if (evacuatedCount == 0)
        {
            ui.ShowNotification("No infantry can currently evacuate this garrison.", true);
        }
    }

    private int EvacuateGarrison(BuildingData building, bool showNotification)
    {
        if (building == null || building.Type != BuildingType.Garrison)
        {
            return 0;
        }

        int evacuatedCount = 0;

        foreach (UnitData unit in new List<UnitData>(building.GarrisonedUnits))
        {
            if (!gridMap.TryFindOpenCellNear(
                    building.Cell,
                    out Vector2Int exitCell
                ) ||
                !gridMap.TryOccupy(exitCell))
            {
                continue;
            }

            building.GarrisonedUnits.Remove(unit);
            unit.GarrisonBuilding = null;
            unit.GarrisonTarget = null;
            unit.Cell = exitCell;
            unit.TargetCell = exitCell;
            unit.Position = gridMap.CellToWorld(exitCell);
            unit.TargetPosition = unit.Position;
            unit.IsMoving = false;
            unit.Waypoints.Clear();
            unit.AttackTarget = null;
            unit.AttackUnitTarget = null;

            if (unit.GameObject != null)
            {
                unit.GameObject.transform.position = new Vector3(
                    unit.Position.x,
                    unit.Position.y,
                    0f
                );
                unit.GameObject.SetActive(true);
            }

            evacuatedCount++;
        }

        if (showNotification && evacuatedCount > 0)
        {
            telemetry.Record(
                ArenaTelemetryKind.Garrison,
                $"EVACUATE P#{building.Id}  UNITS {evacuatedCount:00}",
                Team.Player
            );
            ui.ShowNotification($"Evacuated {evacuatedCount} infantry unit(s).");
        }

        return evacuatedCount;
    }

    private void RemoveUnitFromSelection(UnitData unit)
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

    private void PlayCombatFeedback(CombatFeedbackEvent combatFeedback)
    {
        telemetry.RecordDamage(combatFeedback);
        feedback?.PlayCombatFeedback(combatFeedback);
    }

    private void OnTargetAcquired(UnitData source, UnitData target)
    {
        if (source == null || target == null)
        {
            return;
        }

        telemetry.Record(
            ArenaTelemetryKind.Targeting,
            $"{ShortTeam(source.Team)}#{source.Id} acquired " +
            $"{ShortTeam(target.Team)}#{target.Id}",
            source.Team
        );
    }

    private int CountUnits(Team team)
    {
        int count = 0;

        foreach (UnitData unit in units)
        {
            if (unit != null && unit.Team == team)
            {
                count++;
            }
        }

        return count;
    }

    private static string ShortTeam(Team team)
    {
        return team == Team.Player ? "P" : "E";
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
        telemetry.Record(
            ArenaTelemetryKind.Lifecycle,
            $"{ShortTeam(unit.Team)}#{unit.Id} destroyed {unit.Type}",
            unit.Team
        );

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
        telemetry.Record(
            ArenaTelemetryKind.Lifecycle,
            $"{ShortTeam(building.Team)}#{building.Id} destroyed {building.Type}",
            building.Team
        );
        EvacuateGarrison(building, false);

        if (selectedBuildingData == building)
        {
            selectedBuildingData = null;

            if (selectionRingObject != null)
            {
                selectionRingObject.SetActive(false);
            }
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
                string buildingName = selectedBuilding == BuildingType.Garrison
                    ? "garrison"
                    : "factory";
                ui.ShowNotification(
                    $"Not enough resources. A {buildingName} costs {placement.GetCost(selectedBuilding)}.",
                    true
                );
            }
            else
            {
                Debug.LogWarning("Cannot build here: out of range or cell is occupied.");
                ui.ShowNotification("Cannot build here: out of range or grid area occupied.", true);
            }

            return;
        }

        if (selectedBuilding == BuildingType.Garrison)
        {
            BuildGarrison(currentPreviewPosition, currentPreviewCell);
        }
        else
        {
            BuildFactory(currentPreviewPosition, currentPreviewCell);
        }
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
            "FAC",
            Color.black
        );

        BuildingData factory = new BuildingData(
            "Factory",
            BuildingType.Factory,
            factoryObject,
            position,
            cell,
            buildingRadius,
            "Production structure occupying a 3x3 grid area. Its shared queue trains infantry and artillery.",
            Team.Player,
            factoryHitPoints,
            placement.GetFootprint(BuildingType.Factory, cell)
        );
        factory.Id = nextEntityId++;
        buildings.Add(factory);
        telemetry.RecordBuilding(factory);
        
        Debug.Log($"Factory built at cell {cell}. Remaining resources: {economy.Resources}");
        ui.ShowNotification("Factory construction complete.");
        return true;
    }

    private bool TryTrainInfantry(BuildingData factory)
    {
        if (factory == null || factory.Type != BuildingType.Factory)
        {
            ui.ShowNotification("Select a factory first.", true);
            return false;
        }

        if (factory.ProductionQueueCount >= maxFactoryQueueSize)
        {
            ui.ShowNotification("The production queue is full.", true);
            return false;
        }

        if (!economy.CanAfford(infantryCost))
        {
            ui.ShowNotification($"Not enough resources. Infantry costs {infantryCost}.", true);
            return false;
        }

        if (!economy.TryQueueInfantry(factory))
        {
            ui.ShowNotification("The infantry production request was rejected.", true);
            return false;
        }

        telemetry.Record(
            ArenaTelemetryKind.Production,
            $"P#{factory.Id} queued Infantry  Q {factory.ProductionQueueCount}/{maxFactoryQueueSize}",
            Team.Player
        );

        ui.ShowNotification($"Infantry queued ({factory.ProductionQueueCount}/{maxFactoryQueueSize}).");
        return true;
    }

    private bool BuildGarrison(Vector2 position, Vector2Int cell)
    {
        if (!placement.TryReserve(
                BuildingType.Garrison,
                basePosition,
                position,
                cell
            ))
        {
            return false;
        }

        GameObject garrisonObject = presentation.CreateLabeledCircle(
            PresentationEntityKind.Garrison,
            "Garrison",
            position,
            buildingRadius,
            new Color(0.15f, 0.8f, 0.85f, 1f),
            20,
            buildingRoot,
            "GAR",
            Color.black
        );

        BuildingData garrison = new BuildingData(
            "Garrison",
            BuildingType.Garrison,
            garrisonObject,
            position,
            cell,
            buildingRadius,
            $"Defensive position for up to {garrisonCapacity} infantry. Right-click with infantry selected to enter; garrisoned damage increases by {Mathf.RoundToInt((garrisonDamageMultiplier - 1f) * 100f)}%.",
            Team.Player,
            garrisonHitPoints,
            placement.GetFootprint(BuildingType.Garrison, cell)
        )
        {
            GarrisonCapacity = garrisonCapacity,
            GarrisonDamageMultiplier = garrisonDamageMultiplier
        };
        garrison.Id = nextEntityId++;
        buildings.Add(garrison);
        telemetry.RecordBuilding(garrison);

        Debug.Log(
            $"Garrison built at cell {cell}. Remaining resources: {economy.Resources}"
        );
        ui.ShowNotification("Garrison construction complete.");
        return true;
    }

    private bool TryTrainArtillery(BuildingData factory)
    {
        if (factory == null || factory.Type != BuildingType.Factory)
        {
            ui.ShowNotification("Select a factory first.", true);
            return false;
        }

        if (factory.ProductionQueueCount >= maxFactoryQueueSize)
        {
            ui.ShowNotification("The production queue is full.", true);
            return false;
        }

        if (!economy.CanAfford(artilleryCost))
        {
            ui.ShowNotification($"Not enough resources. Artillery costs {artilleryCost}.", true);
            return false;
        }

        if (!economy.TryQueueArtillery(factory))
        {
            ui.ShowNotification("The artillery production request was rejected.", true);
            return false;
        }

        telemetry.Record(
            ArenaTelemetryKind.Production,
            $"P#{factory.Id} queued Artillery  Q {factory.ProductionQueueCount}/{maxFactoryQueueSize}",
            Team.Player
        );

        ui.ShowNotification($"Artillery queued ({factory.ProductionQueueCount}/{maxFactoryQueueSize}).");
        return true;
    }

    private bool TrySpawnPlayerUnit(BuildingData factory, UnitType unitType)
    {
        if (!gridMap.TryFindOpenCellNear(factory.Cell, out Vector2Int spawnCell))
        {
            return false;
        }

        if (unitType == UnitType.Artillery)
        {
            SpawnPlayerArtillery(spawnCell);
        }
        else
        {
            SpawnPlayerInfantry(spawnCell);
        }

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
            string.Empty,
            Color.black
        );

        UnitData infantry = new UnitData(
            "Infantry",
            UnitType.Infantry,
            infantryObject,
            spawnPosition,
            spawnCell,
            infantryRadius,
            "Basic combat unit. Left-click to select; right-click to move or attack enemy units and structures.",
            Team.Player,
            playerInfantryHitPoints,
            infantryAttackDamage,
            infantryAttackRange,
            infantryAttackCooldown
        );

        gridMap.TryOccupy(spawnCell);
        infantry.Id = nextEntityId++;
        units.Add(infantry);
        telemetry.RecordProduced(infantry);

        Debug.Log($"Infantry trained at cell {spawnCell}.");
    }

    private void SpawnPlayerArtillery(Vector2Int spawnCell)
    {
        Vector2 spawnPosition = gridMap.CellToWorld(spawnCell);

        GameObject artilleryObject = presentation.CreateLabeledCircle(
            PresentationEntityKind.PlayerArtillery,
            "Artillery",
            spawnPosition,
            artilleryRadius,
            new Color(0.8f, 0.35f, 1f, 1f),
            25,
            buildingRoot,
            string.Empty,
            Color.black
        );

        UnitData artillery = new UnitData(
            "Artillery",
            UnitType.Artillery,
            artilleryObject,
            spawnPosition,
            spawnCell,
            artilleryRadius,
            "Mobile while undeployed but unable to fire. Deploy for long-range attacks and bonus structure damage.",
            Team.Player,
            playerArtilleryHitPoints,
            artilleryAttackDamage,
            artilleryAttackRange,
            artilleryAttackCooldown,
            artilleryMoveSpeed,
            artilleryBuildingDamageMultiplier
        );

        gridMap.TryOccupy(spawnCell);
        artillery.Id = nextEntityId++;
        units.Add(artillery);
        telemetry.RecordProduced(artillery);

        Debug.Log($"Artillery trained at cell {spawnCell}.");
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(mousePosition);
        return new Vector2(worldPosition.x, worldPosition.y);
    }

    private void NavigateFromMinimap(Vector2 normalizedPosition)
    {
        float mapWorldSize = gridMap.HalfSize * 2f;
        Vector2 worldPosition = new Vector2(
            -gridMap.HalfSize + Mathf.Clamp01(normalizedPosition.x) * mapWorldSize,
            -gridMap.HalfSize + Mathf.Clamp01(normalizedPosition.y) * mapWorldSize
        );
        cameraController.CenterOnWorld(worldPosition);
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

    private bool TryBuildGarrisonAtCell(Vector2Int cell)
    {
        return BuildGarrison(gridMap.CellToWorld(cell), cell);
    }

}
