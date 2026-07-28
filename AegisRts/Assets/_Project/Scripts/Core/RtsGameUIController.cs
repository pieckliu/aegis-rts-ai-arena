using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

internal sealed class RtsGameUIController
{
    private sealed class HealthView
    {
        public GameObject Root;
        public RectTransform RootRect;
        public RectTransform Fill;
        public Image FillImage;
    }

    private sealed class MinimapMarker
    {
        public GameObject Root;
        public RectTransform Rect;
        public Image Image;
    }

    private readonly Font font;
    private readonly Texture2D minimapDotTexture;
    private readonly Sprite minimapDotSprite;
    private readonly GameObject canvasObject;
    private readonly Canvas canvas;
    private readonly GameObject menuPanel;
    private readonly GameObject hudPanel;
    private readonly GameObject overlayPanel;
    private readonly Text resourceText;
    private readonly Text infoText;
    private readonly Text overlayTitle;
    private readonly Button cancelBuildButton;
    private readonly Button trainButton;
    private readonly Text trainButtonText;
    private readonly Button artilleryButton;
    private readonly Text artilleryButtonText;
    private readonly Button deploymentButton;
    private readonly Text deploymentButtonText;
    private readonly GameObject productionProgress;
    private readonly RectTransform productionFill;
    private readonly Text productionText;
    private readonly GameObject notificationPanel;
    private readonly Text notificationText;
    private readonly RectTransform selectionRect;
    private readonly RectTransform minimapContent;
    private readonly RawImage minimapFog;
    private readonly RectTransform minimapViewport;
    private readonly Dictionary<object, HealthView> healthViews = new Dictionary<object, HealthView>();
    private readonly Dictionary<object, MinimapMarker> minimapMarkers = new Dictionary<object, MinimapMarker>();
    private float notificationTimer;

    public RtsGameUIController(
        Action startGame,
        Action selectFactory,
        Action cancelBuild,
        Action trainInfantry,
        Action trainArtillery,
        Action toggleArtilleryDeployment,
        Action resume,
        Action restart,
        Action returnToMenu,
        Action<Vector2> navigateMinimap
    )
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        minimapDotTexture = CreateCircleTexture(32);
        minimapDotSprite = Sprite.Create(
            minimapDotTexture,
            new Rect(0f, 0f, minimapDotTexture.width, minimapDotTexture.height),
            new Vector2(0.5f, 0.5f),
            minimapDotTexture.width
        );
        minimapDotSprite.name = "MinimapDot";
        EnsureEventSystem();

        canvasObject = new GameObject("RtsGameUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        menuPanel = CreatePanel("MainMenu", canvasObject.transform, Vector2.zero, Vector2.one, new Color(0.025f, 0.04f, 0.07f, 0.98f));
        CreateText("Title", menuPanel.transform, "Aegis RTS AI Arena", 54, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.58f), new Vector2(0.8f, 0.72f));
        CreateButton("Start", menuPanel.transform, "开始游戏", new Vector2(0.4f, 0.42f), new Vector2(0.6f, 0.50f), startGame);

        hudPanel = CreatePanel("Hud", canvasObject.transform, Vector2.zero, Vector2.one, Color.clear);
        resourceText = CreateText("Resources", hudPanel.transform, string.Empty, 24, TextAnchor.MiddleLeft, new Vector2(0.02f, 0.93f), new Vector2(0.65f, 0.985f));
        GameObject commandPanel = CreatePanel("CommandPanel", hudPanel.transform, new Vector2(0.79f, 0.40f), new Vector2(0.985f, 0.97f), new Color(0.04f, 0.055f, 0.075f, 0.94f));
        CreateText("PanelTitle", commandPanel.transform, "指挥面板", 26, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.89f), new Vector2(0.92f, 0.98f));
        CreateButton("BuildFactory", commandPanel.transform, "建造兵厂", new Vector2(0.08f, 0.77f), new Vector2(0.92f, 0.87f), selectFactory);
        cancelBuildButton = CreateButton("CancelBuild", commandPanel.transform, "取消建造", new Vector2(0.08f, 0.65f), new Vector2(0.92f, 0.75f), cancelBuild);
        trainButton = CreateButton("Train", commandPanel.transform, "生产步兵", new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.62f), trainInfantry);
        trainButtonText = trainButton.GetComponentInChildren<Text>();
        artilleryButton = CreateButton("TrainArtillery", commandPanel.transform, "生产火炮", new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.50f), trainArtillery);
        artilleryButtonText = artilleryButton.GetComponentInChildren<Text>();
        deploymentButton = CreateButton("ToggleArtilleryDeployment", commandPanel.transform, "部署火炮", new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.38f), toggleArtilleryDeployment);
        deploymentButtonText = deploymentButton.GetComponentInChildren<Text>();
        deploymentButton.gameObject.SetActive(false);
        productionProgress = CreatePanel(
            "ProductionProgress",
            commandPanel.transform,
            new Vector2(0.08f, 0.24f),
            new Vector2(0.92f, 0.27f),
            new Color(0.02f, 0.08f, 0.12f, 0.95f)
        );
        productionProgress.GetComponent<Image>().raycastTarget = false;
        GameObject progressFill = CreatePanel(
            "ProductionFill",
            productionProgress.transform,
            Vector2.zero,
            Vector2.one,
            new Color(0.15f, 0.75f, 1f, 0.9f)
        );
        progressFill.GetComponent<Image>().raycastTarget = false;
        productionFill = progressFill.GetComponent<RectTransform>();
        productionText = CreateText(
            "ProductionText",
            productionProgress.transform,
            string.Empty,
            14,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            Vector2.one
        );
        productionProgress.SetActive(false);
        infoText = CreateText("Info", commandPanel.transform, "未选中对象", 17, TextAnchor.UpperLeft, new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.22f));

        notificationPanel = CreatePanel(
            "Notification",
            hudPanel.transform,
            new Vector2(0.32f, 0.86f),
            new Vector2(0.68f, 0.92f),
            new Color(0.05f, 0.24f, 0.36f, 0.96f)
        );
        notificationPanel.GetComponent<Image>().raycastTarget = false;
        notificationText = CreateText(
            "NotificationText",
            notificationPanel.transform,
            string.Empty,
            21,
            TextAnchor.MiddleCenter,
            new Vector2(0.03f, 0.05f),
            new Vector2(0.97f, 0.95f)
        );
        notificationText.raycastTarget = false;
        notificationPanel.SetActive(false);

        GameObject minimapPanel = CreatePanel(
            "Minimap",
            hudPanel.transform,
            new Vector2(0.02f, 0.035f),
            new Vector2(0.205f, 0.365f),
            new Color(0.015f, 0.025f, 0.04f, 0.96f)
        );
        CreateText(
            "MinimapTitle",
            minimapPanel.transform,
            "TACTICAL MAP  ·  M OVERVIEW",
            15,
            TextAnchor.MiddleCenter,
            new Vector2(0.03f, 0.91f),
            new Vector2(0.97f, 0.99f)
        ).raycastTarget = false;
        GameObject minimapMap = CreatePanel(
            "MinimapContent",
            minimapPanel.transform,
            new Vector2(0.055f, 0.07f),
            new Vector2(0.945f, 0.88f),
            new Color(0.035f, 0.055f, 0.075f, 1f)
        );
        minimapContent = minimapMap.GetComponent<RectTransform>();
        MinimapPointerHandler pointerHandler = minimapMap.AddComponent<MinimapPointerHandler>();
        pointerHandler.Configure(minimapContent, navigateMinimap);

        GameObject fogObject = new GameObject("MinimapFog", typeof(RectTransform), typeof(RawImage));
        fogObject.transform.SetParent(minimapContent, false);
        RectTransform fogRect = fogObject.GetComponent<RectTransform>();
        fogRect.anchorMin = Vector2.zero;
        fogRect.anchorMax = Vector2.one;
        fogRect.offsetMin = Vector2.zero;
        fogRect.offsetMax = Vector2.zero;
        minimapFog = fogObject.GetComponent<RawImage>();
        minimapFog.raycastTarget = false;

        GameObject viewport = CreatePanel(
            "MinimapViewport",
            minimapContent,
            Vector2.zero,
            Vector2.one,
            new Color(0.3f, 0.85f, 1f, 0.18f)
        );
        viewport.GetComponent<Image>().raycastTarget = false;
        minimapViewport = viewport.GetComponent<RectTransform>();

        GameObject selection = CreatePanel("SelectionRectangle", hudPanel.transform, Vector2.zero, Vector2.zero, new Color(0.15f, 0.7f, 1f, 0.2f));
        selectionRect = selection.GetComponent<RectTransform>();
        selectionRect.anchorMin = Vector2.zero;
        selectionRect.anchorMax = Vector2.zero;
        selectionRect.pivot = Vector2.zero;
        selection.GetComponent<Image>().raycastTarget = false;
        selection.SetActive(false);

        overlayPanel = CreatePanel("Overlay", canvasObject.transform, new Vector2(0.34f, 0.30f), new Vector2(0.66f, 0.70f), new Color(0.025f, 0.035f, 0.055f, 0.97f));
        overlayTitle = CreateText("OverlayTitle", overlayPanel.transform, string.Empty, 38, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.94f));
        CreateButton("Resume", overlayPanel.transform, "继续", new Vector2(0.17f, 0.48f), new Vector2(0.83f, 0.62f), resume);
        CreateButton("Restart", overlayPanel.transform, "重新开始", new Vector2(0.17f, 0.29f), new Vector2(0.83f, 0.43f), restart);
        CreateButton("Menu", overlayPanel.transform, "返回主菜单", new Vector2(0.17f, 0.10f), new Vector2(0.83f, 0.24f), returnToMenu);
        overlayPanel.SetActive(false);
    }

    public bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (notificationTimer <= 0f)
        {
            return;
        }

        notificationTimer -= unscaledDeltaTime;

        if (notificationTimer <= 0f)
        {
            notificationPanel.SetActive(false);
        }
    }

    public void ShowNotification(string message, bool isError = false)
    {
        notificationText.text = message;
        notificationPanel.GetComponent<Image>().color = isError
            ? new Color(0.48f, 0.10f, 0.10f, 0.96f)
            : new Color(0.05f, 0.24f, 0.36f, 0.96f);
        notificationPanel.SetActive(true);
        notificationTimer = 2.4f;
    }

    public void Refresh(
        GameState state,
        bool paused,
        bool won,
        bool lost,
        int resources,
        int factoryCost,
        int infantryCost,
        int artilleryCost,
        int maxQueue,
        float infantryTrainingTime,
        float artilleryTrainingTime,
        BuildingType buildMode,
        BuildingData selectedBuilding,
        IList<UnitData> selectedUnits,
        RtsSelectionInputController selectionInput,
        IList<BuildingData> buildings,
        IList<UnitData> units,
        Camera camera,
        float mapHalfSize,
        RtsVisibilitySystem visibility
    )
    {
        bool playing = state == GameState.Playing;
        menuPanel.SetActive(!playing);
        hudPanel.SetActive(playing);

        if (!playing)
        {
            overlayPanel.SetActive(false);
            ClearHealthViews();
            return;
        }

        resourceText.text = $"资源：{resources}    兵厂：{factoryCost}    步兵：{infantryCost}    火炮：{artilleryCost}    WASD 移动 / 滚轮缩放 / M 战略视角 / Esc 暂停";
        cancelBuildButton.gameObject.SetActive(buildMode != BuildingType.None);
        bool factorySelected = selectedBuilding != null && selectedBuilding.Type == BuildingType.Factory;
        trainButton.interactable = factorySelected;
        artilleryButton.interactable = factorySelected;
        trainButtonText.text = GetProductionButtonText(
            selectedBuilding,
            UnitType.Infantry,
            maxQueue
        );
        artilleryButtonText.text = GetProductionButtonText(
            selectedBuilding,
            UnitType.Artillery,
            maxQueue
        );

        int selectedArtilleryCount = 0;
        bool allSelectedArtilleryDeployed = true;

        foreach (UnitData unit in selectedUnits)
        {
            if (unit == null || unit.Type != UnitType.Artillery)
            {
                continue;
            }

            selectedArtilleryCount++;
            allSelectedArtilleryDeployed &= unit.IsDeployed;
        }

        deploymentButton.gameObject.SetActive(selectedArtilleryCount > 0);
        deploymentButtonText.text = allSelectedArtilleryDeployed
            ? "取消部署"
            : "部署火炮";
        BuildingData producingFactory = factorySelected ? selectedBuilding : null;

        if (producingFactory == null)
        {
            foreach (BuildingData building in buildings)
            {
                if (building.Team == Team.Player &&
                    building.Type == BuildingType.Factory &&
                    building.ProductionQueueCount > 0)
                {
                    producingFactory = building;
                    break;
                }
            }
        }

        bool producing = producingFactory != null &&
            producingFactory.ProductionQueueCount > 0;
        productionProgress.SetActive(producing);

        if (producing)
        {
            UnitType productionType = producingFactory.CurrentProductionType;
            float trainingTime = productionType == UnitType.Artillery
                ? artilleryTrainingTime
                : infantryTrainingTime;
            float progress = trainingTime > 0f
                ? 1f - Mathf.Clamp01(producingFactory.ProductionTimer / trainingTime)
                : 0f;
            productionFill.anchorMin = Vector2.zero;
            productionFill.anchorMax = new Vector2(progress, 1f);
            productionFill.offsetMin = Vector2.zero;
            productionFill.offsetMax = Vector2.zero;
            string productionName = productionType == UnitType.Artillery ? "火炮" : "步兵";
            productionText.text = $"{productionName} {Mathf.RoundToInt(progress * 100f)}% · 队列 {producingFactory.ProductionQueueCount}";
        }

        if (selectedBuilding != null)
        {
            infoText.text = $"{selectedBuilding.DisplayName}\n生命：{selectedBuilding.HitPoints}/{selectedBuilding.MaxHitPoints}\n{selectedBuilding.Description}";
        }
        else if (selectedUnits.Count == 1)
        {
            UnitData unit = selectedUnits[0];
            string deploymentStatus = unit.Type == UnitType.Artillery
                ? $"\n状态：{(unit.IsDeployed ? "已部署（不可移动）" : "未部署（不可开火）")}"
                : string.Empty;
            infoText.text = $"{unit.DisplayName}\n生命：{unit.HitPoints}/{unit.MaxHitPoints}{deploymentStatus}\n{unit.Description}\n拖动该单位移动，右键移动/攻击";
        }
        else if (selectedUnits.Count > 1)
        {
            infoText.text = $"已选择 {selectedUnits.Count} 个单位\n拖动单位移动，或右键移动/攻击敌军";
        }
        else
        {
            infoText.text = buildMode == BuildingType.Factory ? "右键在有效格建造兵厂" : "未选中对象";
        }

        overlayPanel.SetActive(paused || won || lost);
        overlayTitle.text = won ? "胜利" : lost ? "失败" : "游戏已暂停";
        UpdateSelectionRectangle(selectionInput);
        Func<Vector2, bool> isWorldVisible = visibility == null
            ? null
            : new Func<Vector2, bool>(visibility.IsVisible);
        UpdateHealthViews(
            buildings,
            units,
            selectedBuilding,
            selectedUnits,
            camera,
            isWorldVisible
        );
        UpdateMinimap(buildings, units, camera, mapHalfSize, visibility);
    }

    public void Destroy()
    {
        ClearHealthViews();
        ClearMinimapMarkers();
        UnityEngine.Object.Destroy(canvasObject);
        Release(minimapDotSprite);
        Release(minimapDotTexture);
    }

    internal static string GetProductionButtonText(
        BuildingData selectedBuilding,
        UnitType unitType,
        int maxQueue
    )
    {
        if (selectedBuilding == null ||
            selectedBuilding.Type != BuildingType.Factory)
        {
            return "选择兵厂后生产";
        }

        int queuedCount = unitType == UnitType.Artillery
            ? selectedBuilding.ArtilleryQueue
            : selectedBuilding.InfantryQueue;
        string unitName = unitType == UnitType.Artillery ? "火炮" : "步兵";
        return $"生产{unitName} ({queuedCount}/{maxQueue})";
    }

    private void UpdateMinimap(
        IList<BuildingData> buildings,
        IList<UnitData> units,
        Camera camera,
        float mapHalfSize,
        RtsVisibilitySystem visibility
    )
    {
        minimapFog.texture = visibility?.FogTexture;
        HashSet<object> visibleMarkers = new HashSet<object>();

        foreach (BuildingData building in buildings)
        {
            if (building == null)
            {
                continue;
            }

            Vector2 markerPosition = building.Position;
            bool lastKnown = false;

            if (building.Team == Team.Enemy &&
                (visibility == null || !visibility.IsVisible(building.Position)))
            {
                if (visibility == null ||
                    !visibility.TryGetLastKnownContact(
                        building,
                        out markerPosition,
                        out _
                    ))
                {
                    continue;
                }

                lastKnown = true;
            }

            Color color = building.Team == Team.Enemy
                ? new Color(1f, 0.22f, 0.2f, 1f)
                : building.Type == BuildingType.Factory
                    ? new Color(0.2f, 0.95f, 0.4f, 1f)
                    : new Color(0.25f, 0.6f, 1f, 1f);
            color.a = lastKnown ? 0.48f : 1f;
            UpdateMinimapMarker(
                building,
                markerPosition,
                color,
                building.Type == BuildingType.Base ? 11f : 8f,
                mapHalfSize,
                visibleMarkers,
                GetMarkerName(building, lastKnown)
            );
        }

        foreach (UnitData unit in units)
        {
            if (unit == null)
            {
                continue;
            }

            Vector2 markerPosition = unit.Position;
            float freshness = 1f;
            bool lastKnown = false;

            if (unit.Team == Team.Enemy &&
                (visibility == null || !visibility.IsVisible(unit.Position)))
            {
                if (visibility == null ||
                    !visibility.TryGetLastKnownContact(
                        unit,
                        out markerPosition,
                        out freshness
                    ))
                {
                    continue;
                }

                lastKnown = true;
            }

            Color color = unit.Team == Team.Enemy
                ? new Color(1f, 0.55f, 0.12f, 1f)
                : unit.Type == UnitType.Artillery
                    ? new Color(0.8f, 0.35f, 1f, 1f)
                    : new Color(1f, 0.92f, 0.2f, 1f);
            color.a = lastKnown ? Mathf.Lerp(0.16f, 0.62f, freshness) : 1f;
            UpdateMinimapMarker(
                unit,
                markerPosition,
                color,
                6f,
                mapHalfSize,
                visibleMarkers,
                GetMarkerName(unit, lastKnown)
            );
        }

        List<object> stale = new List<object>();

        foreach (object key in minimapMarkers.Keys)
        {
            if (!visibleMarkers.Contains(key))
            {
                stale.Add(key);
            }
        }

        foreach (object key in stale)
        {
            UnityEngine.Object.Destroy(minimapMarkers[key].Root);
            minimapMarkers.Remove(key);
        }

        UpdateMinimapViewport(camera, mapHalfSize);
        minimapViewport.SetAsLastSibling();
    }

    private void UpdateMinimapMarker(
        object key,
        Vector2 worldPosition,
        Color color,
        float size,
        float mapHalfSize,
        ISet<object> visibleMarkers,
        string markerName
    )
    {
        visibleMarkers.Add(key);

        if (!minimapMarkers.TryGetValue(key, out MinimapMarker marker))
        {
            GameObject root = CreatePanel(
                markerName,
                minimapContent,
                Vector2.zero,
                Vector2.zero,
                color
            );
            marker = new MinimapMarker
            {
                Root = root,
                Rect = root.GetComponent<RectTransform>(),
                Image = root.GetComponent<Image>()
            };
            marker.Image.raycastTarget = false;
            marker.Image.sprite = minimapDotSprite;
            marker.Image.preserveAspect = true;
            marker.Rect.pivot = new Vector2(0.5f, 0.5f);
            minimapMarkers[key] = marker;
        }

        marker.Root.name = markerName;
        float mapSize = Mathf.Max(0.001f, mapHalfSize * 2f);
        Vector2 normalized = new Vector2(
            Mathf.Clamp01((worldPosition.x + mapHalfSize) / mapSize),
            Mathf.Clamp01((worldPosition.y + mapHalfSize) / mapSize)
        );
        marker.Rect.anchorMin = normalized;
        marker.Rect.anchorMax = normalized;
        marker.Rect.anchoredPosition = Vector2.zero;
        marker.Rect.sizeDelta = Vector2.one * size;
        marker.Image.color = color;
    }

    private static string GetMarkerName(BuildingData building, bool lastKnown)
    {
        if (building.Team == Team.Player)
        {
            return building.Type == BuildingType.Base
                ? "PlayerBaseMapDot"
                : "PlayerFactoryMapDot";
        }

        return lastKnown ? "LastKnownEnemyBuildingMapDot" : "EnemyBuildingMapDot";
    }

    private static string GetMarkerName(UnitData unit, bool lastKnown)
    {
        if (unit.Team == Team.Player)
        {
            return unit.Type == UnitType.Artillery
                ? "PlayerArtilleryMapDot"
                : "PlayerUnitMapDot";
        }

        return lastKnown ? "LastKnownEnemyUnitMapDot" : "EnemyUnitMapDot";
    }

    private void UpdateMinimapViewport(Camera camera, float mapHalfSize)
    {
        if (camera == null || mapHalfSize <= 0f)
        {
            minimapViewport.gameObject.SetActive(false);
            return;
        }

        minimapViewport.gameObject.SetActive(true);
        float mapSize = mapHalfSize * 2f;
        float vertical = camera.orthographicSize;
        float horizontal = vertical * camera.aspect;
        Vector2 cameraPosition = camera.transform.position;
        minimapViewport.anchorMin = new Vector2(
            Mathf.Clamp01((cameraPosition.x - horizontal + mapHalfSize) / mapSize),
            Mathf.Clamp01((cameraPosition.y - vertical + mapHalfSize) / mapSize)
        );
        minimapViewport.anchorMax = new Vector2(
            Mathf.Clamp01((cameraPosition.x + horizontal + mapHalfSize) / mapSize),
            Mathf.Clamp01((cameraPosition.y + vertical + mapHalfSize) / mapSize)
        );
        minimapViewport.offsetMin = Vector2.zero;
        minimapViewport.offsetMax = Vector2.zero;
    }

    private void ClearMinimapMarkers()
    {
        foreach (MinimapMarker marker in minimapMarkers.Values)
        {
            UnityEngine.Object.Destroy(marker.Root);
        }

        minimapMarkers.Clear();
    }

    private void UpdateSelectionRectangle(RtsSelectionInputController input)
    {
        if (input == null || !input.IsBoxSelecting)
        {
            selectionRect.gameObject.SetActive(false);
            return;
        }

        Vector2 min = ScreenToCanvas(Vector2.Min(input.DragStart, input.DragCurrent));
        Vector2 max = ScreenToCanvas(Vector2.Max(input.DragStart, input.DragCurrent));
        selectionRect.gameObject.SetActive(true);
        selectionRect.anchoredPosition = min;
        selectionRect.sizeDelta = max - min;
    }

    private void UpdateHealthViews(
        IList<BuildingData> buildings,
        IList<UnitData> units,
        BuildingData selectedBuilding,
        IList<UnitData> selectedUnits,
        Camera camera,
        Func<Vector2, bool> isWorldVisible
    )
    {
        HashSet<object> visible = new HashSet<object>();

        foreach (BuildingData building in buildings)
        {
            if (building.Team == Team.Enemy &&
                (isWorldVisible == null || !isWorldVisible(building.Position)))
            {
                continue;
            }

            bool isTargeted = false;

            foreach (UnitData unit in units)
            {
                if (unit.AttackTarget == building)
                {
                    isTargeted = true;
                    break;
                }
            }

            if (building == selectedBuilding || building.HitPoints < building.MaxHitPoints || isTargeted)
            {
                UpdateHealthView(
                    building,
                    building.Position,
                    building.Radius,
                    building.HitPoints,
                    building.MaxHitPoints,
                    camera,
                    visible
                );
            }
        }

        foreach (UnitData unit in units)
        {
            if (unit.Team == Team.Enemy &&
                (isWorldVisible == null || !isWorldVisible(unit.Position)))
            {
                continue;
            }

            bool isTargeted = false;

            foreach (UnitData other in units)
            {
                if (other.AttackUnitTarget == unit)
                {
                    isTargeted = true;
                    break;
                }
            }

            if (selectedUnits.Contains(unit) ||
                unit.HitPoints < unit.MaxHitPoints ||
                isTargeted)
            {
                UpdateHealthView(
                    unit,
                    unit.Position,
                    unit.Radius,
                    unit.HitPoints,
                    unit.MaxHitPoints,
                    camera,
                    visible
                );
            }
        }

        List<object> stale = new List<object>();

        foreach (object key in healthViews.Keys)
        {
            if (!visible.Contains(key))
            {
                stale.Add(key);
            }
        }

        foreach (object key in stale)
        {
            UnityEngine.Object.Destroy(healthViews[key].Root);
            healthViews.Remove(key);
        }
    }

    private void UpdateHealthView(
        object key,
        Vector2 world,
        float worldRadius,
        int hp,
        int maxHp,
        Camera camera,
        ISet<object> visible
    )
    {
        if (maxHp <= 0 || camera == null)
        {
            return;
        }

        visible.Add(key);

        if (!healthViews.TryGetValue(key, out HealthView view))
        {
            GameObject root = CreatePanel("HealthBar", hudPanel.transform, Vector2.zero, Vector2.zero, new Color(0.18f, 0.02f, 0.02f, 0.9f));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(42f, 6f);
            root.GetComponent<Image>().raycastTarget = false;
            GameObject fill = CreatePanel("Fill", root.transform, Vector2.zero, Vector2.one, Color.green);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.zero;
            fillRect.pivot = Vector2.zero;
            fill.GetComponent<Image>().raycastTarget = false;
            view = new HealthView
            {
                Root = root,
                RootRect = rootRect,
                Fill = fillRect,
                FillImage = fill.GetComponent<Image>()
            };
            healthViews[key] = view;
        }

        float ratio = Mathf.Clamp01((float)hp / maxHp);
        Vector3 screen = camera.WorldToScreenPoint(world);
        Vector3 topScreen = camera.WorldToScreenPoint(world + Vector2.up * (worldRadius + 0.16f));
        Vector3 radiusScreen = camera.WorldToScreenPoint(world + Vector2.right * worldRadius);
        float scale = Mathf.Max(canvas.scaleFactor, 0.0001f);
        float width = Mathf.Clamp(Mathf.Abs(radiusScreen.x - screen.x) * 1.8f / scale, 24f, 56f);
        view.Root.SetActive(screen.z >= 0f);
        view.RootRect.anchoredPosition = ScreenToCanvas(topScreen);
        view.RootRect.sizeDelta = new Vector2(width, 6f);
        view.Fill.sizeDelta = new Vector2(width * ratio, 6f);
        view.FillImage.color = ratio > 0.5f ? Color.green : ratio > 0.25f ? Color.yellow : Color.red;
    }

    private Vector2 ScreenToCanvas(Vector2 screenPosition)
    {
        float scale = Mathf.Max(canvas.scaleFactor, 0.0001f);
        return screenPosition / scale;
    }

    private void ClearHealthViews()
    {
        foreach (HealthView view in healthViews.Values)
        {
            UnityEngine.Object.Destroy(view.Root);
        }

        healthViews.Clear();
    }

    private GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = color;
        panel.GetComponent<Image>().raycastTarget = color.a > 0.01f;
        return panel;
    }

    private Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, Vector2 min, Vector2 max)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(Text));
        item.transform.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text text = item.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.text = value;
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 min, Vector2 max, Action callback)
    {
        GameObject item = CreatePanel(name, parent, min, max, new Color(0.12f, 0.32f, 0.52f, 0.95f));
        Button button = item.AddComponent<Button>();
        button.targetGraphic = item.GetComponent<Image>();
        CreateText("Label", item.transform, label, 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        button.onClick.AddListener(() => callback());
        return button;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    private static Texture2D CreateCircleTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "MinimapDotTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius + 1f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false);
        return texture;
    }

    private static void Release(UnityEngine.Object value)
    {
        if (value == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(value);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
