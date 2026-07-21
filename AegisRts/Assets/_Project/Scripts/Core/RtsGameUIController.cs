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

    private readonly Font font;
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
    private readonly RectTransform selectionRect;
    private readonly Dictionary<object, HealthView> healthViews = new Dictionary<object, HealthView>();

    public RtsGameUIController(
        Action startGame,
        Action selectFactory,
        Action cancelBuild,
        Action trainInfantry,
        Action resume,
        Action restart,
        Action returnToMenu
    )
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        GameObject commandPanel = CreatePanel("CommandPanel", hudPanel.transform, new Vector2(0.79f, 0.52f), new Vector2(0.985f, 0.97f), new Color(0.04f, 0.055f, 0.075f, 0.94f));
        CreateText("PanelTitle", commandPanel.transform, "指挥面板", 26, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.98f));
        CreateButton("BuildFactory", commandPanel.transform, "建造兵厂", new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.83f), selectFactory);
        cancelBuildButton = CreateButton("CancelBuild", commandPanel.transform, "取消建造", new Vector2(0.08f, 0.59f), new Vector2(0.92f, 0.70f), cancelBuild);
        trainButton = CreateButton("Train", commandPanel.transform, "生产步兵", new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.54f), trainInfantry);
        trainButtonText = trainButton.GetComponentInChildren<Text>();
        infoText = CreateText("Info", commandPanel.transform, "未选中对象", 19, TextAnchor.UpperLeft, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.38f));

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

    public void Refresh(
        GameState state,
        bool paused,
        bool won,
        bool lost,
        int resources,
        int factoryCost,
        int infantryCost,
        int maxQueue,
        BuildingType buildMode,
        BuildingData selectedBuilding,
        IList<UnitData> selectedUnits,
        RtsSelectionInputController selectionInput,
        IList<BuildingData> buildings,
        IList<UnitData> units,
        Camera camera
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

        resourceText.text = $"资源：{resources}    兵厂：{factoryCost}    步兵：{infantryCost}    WASD 移动 / 滚轮缩放 / Esc 暂停";
        cancelBuildButton.gameObject.SetActive(buildMode != BuildingType.None);
        bool factorySelected = selectedBuilding != null && selectedBuilding.Type == BuildingType.Factory;
        trainButton.interactable = factorySelected;
        trainButtonText.text = factorySelected
            ? $"生产步兵 ({selectedBuilding.InfantryQueue}/{maxQueue})"
            : "选择兵厂后生产";

        if (selectedBuilding != null)
        {
            infoText.text = $"{selectedBuilding.DisplayName}\n生命：{selectedBuilding.HitPoints}/{selectedBuilding.MaxHitPoints}\n{selectedBuilding.Description}";
        }
        else if (selectedUnits.Count == 1)
        {
            UnitData unit = selectedUnits[0];
            infoText.text = $"{unit.DisplayName}\n生命：{unit.HitPoints}/{unit.MaxHitPoints}\n{unit.Description}";
        }
        else if (selectedUnits.Count > 1)
        {
            infoText.text = $"已选择 {selectedUnits.Count} 个单位\n右键移动或攻击敌军";
        }
        else
        {
            infoText.text = buildMode == BuildingType.Factory ? "右键在有效格建造兵厂" : "未选中对象";
        }

        overlayPanel.SetActive(paused || won || lost);
        overlayTitle.text = won ? "胜利" : lost ? "失败" : "游戏已暂停";
        UpdateSelectionRectangle(selectionInput);
        UpdateHealthViews(buildings, units, selectedBuilding, camera);
    }

    public void Destroy()
    {
        ClearHealthViews();
        UnityEngine.Object.Destroy(canvasObject);
    }

    private void UpdateSelectionRectangle(RtsSelectionInputController input)
    {
        if (input == null || !input.IsDragging)
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
        Camera camera
    )
    {
        HashSet<object> visible = new HashSet<object>();

        foreach (BuildingData building in buildings)
        {
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
            UpdateHealthView(unit, unit.Position, unit.Radius, unit.HitPoints, unit.MaxHitPoints, camera, visible);
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
}
