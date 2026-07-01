using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class GameManager : MonoBehaviour
{
    // ========== Water ==========
    public ProceduralWaterMesh waterPrefab;
    public float waterMaxRadius = 5f;
    public float waterDepthThreshold = 0.05f;
    public int waterBoundaryRays = 64;
    public float waterCleanupRadius = 6f;

    // ========== Tools ==========
    public UIStyleConfig uiStyle;
    public float maxDistance = 80f;
    public enum ToolMode { Dig, Raise, Paint, Water }
    public ToolMode currentMode = ToolMode.Dig;
    public float strength = 0.8f;
    public float brushSize = 2.5f;
    public int paintLayer = 0;
    public float paintStrength = 0.5f;
    public float actionInterval = 0.05f;

    // ========== Эталонный террейн (перетащите сюда исходный TerrainData) ==========
    [Header("Terrain Original")]
    public TerrainData originalTerrainData;

    // ========== Луч ==========
    [Header("Ray Settings")]
    [Range(0f, 1f)]
    public float rayForwardWeight = 0.95f;
    [Range(0f, 1f)]
    public float rayDownWeight = 0.05f;

    [Header("Laser Visual")]
    public Color laserColor = Color.red;
    public float laserWidth = 0.02f;

    // ========== Activation (Grip) ==========
    public InputActionProperty activateAction;

    // ========== UI Toggle ==========
    public InputActionProperty toggleUIAction;

    // ========== UI ==========
    private GameObject uiRoot;
    private bool uiActive = false;
    private TMP_Text modeText;
    private TMP_Text strengthText;
    private TMP_Text brushSizeText;
    private TMP_Text paintLayerText;
    private TMP_Text paintStrengthText;
    private Button digButton, raiseButton, paintButton, waterButton;
    private Slider strengthSlider, brushSizeSlider, paintStrengthSlider;
    private Button prevLayerButton, nextLayerButton;

    private float lastActionTime = 0f;

    public float uiDistance = 1.2f;
    public Vector3 uiScale = new Vector3(0.003f, 0.003f, 0.003f);

    private float lastUIDistance;
    private Vector3 lastUIScale;

    // Террейн
    private Terrain currentTerrain;
    private TerrainData currentTerrainData;

    // Лазер
    private LineRenderer laserLine;
    private Transform rightController;

    void Awake()
    {
        currentTerrain = Terrain.activeTerrain;
        if (currentTerrain != null)
            currentTerrainData = currentTerrain.terrainData;

        CreateUI();
        if (uiRoot != null)
            uiRoot.SetActive(false);

        lastUIDistance = uiDistance;
        lastUIScale = uiScale;

        CreateLaser();
    }

    void Start()
    {
        ResetTerrainToOriginal();
    }

    void ResetTerrainToOriginal()
    {
        if (currentTerrainData == null || originalTerrainData == null)
        {
            Debug.LogWarning("Нет эталонного TerrainData или текущего террейна. Сброс не выполнен.");
            return;
        }

        int res = currentTerrainData.heightmapResolution;
        if (originalTerrainData.heightmapResolution != res)
        {
            Debug.LogError("Разрешение эталонного террейна не совпадает с текущим! Сброс невозможен.");
            return;
        }

        float[,] heights = originalTerrainData.GetHeights(0, 0, res, res);
        currentTerrainData.SetHeights(0, 0, heights);
        currentTerrainData.SyncHeightmap();
    }

    void CreateLaser()
    {
        GameObject laserObj = new GameObject("Laser");
        laserObj.transform.SetParent(transform);
        laserLine = laserObj.AddComponent<LineRenderer>();
        laserLine.startWidth = laserWidth;
        laserLine.endWidth = laserWidth;
        laserLine.material = new Material(Shader.Find("Sprites/Default"));
        laserLine.startColor = laserColor;
        laserLine.endColor = laserColor;
        laserLine.positionCount = 2;
        laserLine.enabled = false;
    }

    void Update()
    {
        if (toggleUIAction != null && toggleUIAction.action != null && toggleUIAction.action.WasPressedThisFrame())
            ToggleUI();

        bool gripPressed = activateAction != null && activateAction.action != null && activateAction.action.IsPressed();
        UpdateLaser(gripPressed);

        if (!uiActive && gripPressed)
        {
            if (Time.time - lastActionTime >= actionInterval)
            {
                Transform controller = GetRightController();
                if (controller != null)
                {
                    ExecuteAction(controller);
                    lastActionTime = Time.time;
                }
            }
        }

        if (uiActive && uiRoot != null)
        {
            if (uiDistance != lastUIDistance || uiScale != lastUIScale)
            {
                uiRoot.transform.localPosition = new Vector3(0, 0, uiDistance);
                uiRoot.transform.localScale = uiScale;
                lastUIDistance = uiDistance;
                lastUIScale = uiScale;
            }
        }

        UpdateUI();
    }

    void UpdateLaser(bool gripPressed)
    {
        if (laserLine == null) return;

        Transform controller = GetRightController();
        if (controller == null)
        {
            laserLine.enabled = false;
            return;
        }

        Vector3 direction = (controller.forward * rayForwardWeight - controller.up * rayDownWeight).normalized;
        Ray ray = new Ray(controller.position, direction);
        int layerMask = 1 << LayerMask.NameToLayer("Terrain");
        if (layerMask == 0) layerMask = -1;

        if (gripPressed && Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
        {
            laserLine.enabled = true;
            laserLine.SetPosition(0, controller.position);
            laserLine.SetPosition(1, hit.point);
        }
        else
        {
            laserLine.enabled = false;
        }
    }

    Transform GetRightController()
    {
        if (rightController == null || !rightController.gameObject.activeInHierarchy)
        {
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in all)
            {
                if (t.name == "RightHand Controller" || t.name == "Right Controller" || t.name.Contains("Right"))
                {
                    rightController = t;
                    break;
                }
            }
        }
        return rightController;
    }

    void ToggleUI()
    {
        if (uiRoot == null) return;
        uiActive = !uiActive;
        if (uiActive)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                uiRoot.transform.SetParent(cam.transform, false);
                uiRoot.transform.localPosition = new Vector3(0, 0, uiDistance);
                uiRoot.transform.localRotation = Quaternion.identity;
                uiRoot.transform.localScale = uiScale;
            }
            else
            {
                uiRoot.transform.SetParent(null);
                uiRoot.transform.position = new Vector3(0, 2.0f, 1.5f);
                uiRoot.transform.LookAt(Vector3.zero);
            }
            uiRoot.SetActive(true);
        }
        else
        {
            uiRoot.SetActive(false);
            uiRoot.transform.SetParent(null);
        }
    }

    void ExecuteAction(Transform controllerTransform)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        Vector3 direction = (controllerTransform.forward * rayForwardWeight - controllerTransform.up * rayDownWeight).normalized;
        int layerMask = 1 << LayerMask.NameToLayer("Terrain");
        if (layerMask == 0) layerMask = -1;

        Ray ray = new Ray(controllerTransform.position, direction);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
        {
            if (hit.collider.GetComponent<Terrain>() != null)
                PerformAction(hit.point);
        }
    }

    void PerformAction(Vector3 worldPos)
    {
        switch (currentMode)
        {
            case ToolMode.Dig:
                TerrainDeformer.Deform(Terrain.activeTerrain, worldPos, -strength * Time.deltaTime, brushSize);
                break;
            case ToolMode.Raise:
                TerrainDeformer.Deform(Terrain.activeTerrain, worldPos, strength * Time.deltaTime, brushSize);
                break;
            case ToolMode.Paint:
                TerrainDeformer.PaintTexture(Terrain.activeTerrain, worldPos, brushSize, paintStrength, paintLayer);
                break;
            case ToolMode.Water:
                PourWater(worldPos);
                break;
        }
    }

    // ================= ВОДА (автоматическое заполнение до краёв) =================
    void PourWater(Vector3 center)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        // 1. Находим минимальную высоту в радиусе (дно ямы)
        float minHeight = GetMinHeightInRadius(center, waterMaxRadius, terrain);

        // 2. Находим среднюю высоту на границе радиуса (уровень земли вокруг ямы)
        float edgeHeight = GetAverageHeightAtRadius(center, waterMaxRadius, terrain);

        // Если яма слишком мелкая, не заливаем
        if (edgeHeight - minHeight < waterDepthThreshold)
            return;

        // Уровень воды = edgeHeight (чуть ниже, чтобы вода не выливалась, если местность неровная)
        float waterLevel = edgeHeight;

        // 3. Строим границу воды на уровне waterLevel
        List<Vector3> boundary = new List<Vector3>();
        for (int i = 0; i < waterBoundaryRays; i++)
        {
            float angle = i * Mathf.PI * 2f / waterBoundaryRays;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            float dist = FindIntersectionWithLevel(center, dir, waterLevel, terrain);
            Vector3 edgePoint = center + dir * dist;
            boundary.Add(new Vector3(edgePoint.x, waterLevel, edgePoint.z));
        }

        if (boundary.Count < 3) return;

        // Удаляем старую воду в радиусе
        RemoveOldWater(center, waterCleanupRadius);

        // Создаём новую воду
        ProceduralWaterMesh water = Instantiate(waterPrefab, center, Quaternion.identity);
        water.BuildFromBoundary(center, boundary.ToArray(), waterLevel);
    }

    // Поиск точки пересечения луча с уровнем waterLevel (бинарный поиск)
    float FindIntersectionWithLevel(Vector3 center, Vector3 dir, float waterLevel, Terrain terrain)
    {
        float low = 0f;
        float high = waterMaxRadius;
        const float precision = 0.01f;

        // Если на максимальном расстоянии высота всё ещё ниже уровня, вода выходит за радиус
        if (terrain.SampleHeight(center + dir * high) <= waterLevel)
            return high;

        while (high - low > precision)
        {
            float mid = (low + high) * 0.5f;
            float h = terrain.SampleHeight(center + dir * mid);
            if (h > waterLevel)
                high = mid;
            else
                low = mid;
        }
        return (low + high) * 0.5f;
    }

    float GetMinHeightInRadius(Vector3 center, float radius, Terrain terrain)
    {
        float min = Mathf.Infinity;
        int steps = 10;
        int pointsPerCircle = 16;
        for (int i = 0; i <= steps; i++)
        {
            float r = radius * i / steps;
            for (int j = 0; j < pointsPerCircle; j++)
            {
                float angle = j * Mathf.PI * 2f / pointsPerCircle;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * r;
                float h = terrain.SampleHeight(center + offset);
                if (h < min) min = h;
            }
        }
        return (min == Mathf.Infinity) ? 0f : min;
    }

    float GetAverageHeightAtRadius(Vector3 center, float radius, Terrain terrain)
    {
        float sum = 0f;
        int count = 0;
        int pointsPerCircle = 32;
        for (int j = 0; j < pointsPerCircle; j++)
        {
            float angle = j * Mathf.PI * 2f / pointsPerCircle;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
            float h = terrain.SampleHeight(center + offset);
            sum += h;
            count++;
        }
        return count > 0 ? sum / count : 0f;
    }

    void RemoveOldWater(Vector3 point, float radius)
    {
        ProceduralWaterMesh[] allWaters = FindObjectsByType<ProceduralWaterMesh>();
        foreach (var w in allWaters)
        {
            if (Vector3.Distance(w.transform.position, point) <= radius)
                Destroy(w.gameObject);
        }
    }

    // ===== UI =====
    void CreateUI()
    {
        uiRoot = new GameObject("FixedUI", typeof(RectTransform), typeof(Canvas));
        uiRoot.transform.SetParent(null);

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        uiRoot.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = uiRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(480, 300);

        uiRoot.layer = LayerMask.NameToLayer("UI");
        AssignLayerRecursively(uiRoot, LayerMask.NameToLayer("UI"));

        // Фон
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(uiRoot.transform, false);
        panel.GetComponent<Image>().color = uiStyle != null ? uiStyle.panelBgColor : new Color(0, 0, 0, 0.5f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // Заголовок
        modeText = CreateText("ModeText", uiRoot.transform, "Mode: Dig", 24, TextAlignmentOptions.Center);
        modeText.rectTransform.anchorMin = new Vector2(0, 1);
        modeText.rectTransform.anchorMax = new Vector2(1, 1);
        modeText.rectTransform.pivot = new Vector2(0.5f, 1);
        modeText.rectTransform.anchoredPosition = new Vector2(0, -10);
        modeText.rectTransform.sizeDelta = new Vector2(-20, 35);

        // Кнопки
        digButton = CreateButton("BtnDig", uiRoot.transform, "Dig", new Vector2(10, -55), new Vector2(90, 40));
        raiseButton = CreateButton("BtnRaise", uiRoot.transform, "Raise", new Vector2(10, -105), new Vector2(90, 40));
        paintButton = CreateButton("BtnPaint", uiRoot.transform, "Paint", new Vector2(10, -155), new Vector2(90, 40));
        waterButton = CreateButton("BtnWater", uiRoot.transform, "Water", new Vector2(10, -205), new Vector2(90, 40));

        digButton.onClick.AddListener(() => currentMode = ToolMode.Dig);
        raiseButton.onClick.AddListener(() => currentMode = ToolMode.Raise);
        paintButton.onClick.AddListener(() => currentMode = ToolMode.Paint);
        waterButton.onClick.AddListener(() => currentMode = ToolMode.Water);

        // Strength
        strengthText = CreateText("StrengthLabel", uiRoot.transform, "Strength: 0.8", 16, TextAlignmentOptions.Left);
        strengthText.rectTransform.anchorMin = new Vector2(0.3f, 1);
        strengthText.rectTransform.anchorMax = new Vector2(0.7f, 1);
        strengthText.rectTransform.anchoredPosition = new Vector2(0, -55);
        strengthText.rectTransform.sizeDelta = new Vector2(0, 25);

        strengthSlider = CreateSlider("StrengthSlider", uiRoot.transform, new Vector2(0, -95), new Vector2(210, 20), 0.01f, 5f, 0.8f);
        strengthSlider.onValueChanged.AddListener(val => strength = val);

        // Brush Size
        brushSizeText = CreateText("BrushLabel", uiRoot.transform, "Brush: 2.5", 16, TextAlignmentOptions.Left);
        brushSizeText.rectTransform.anchorMin = new Vector2(0.3f, 1);
        brushSizeText.rectTransform.anchorMax = new Vector2(0.7f, 1);
        brushSizeText.rectTransform.anchoredPosition = new Vector2(0, -135);
        brushSizeText.rectTransform.sizeDelta = new Vector2(0, 25);

        brushSizeSlider = CreateSlider("BrushSlider", uiRoot.transform, new Vector2(0, -175), new Vector2(210, 20), 0.1f, 20f, 2.5f);
        brushSizeSlider.onValueChanged.AddListener(val => brushSize = val);

        // Paint Strength
        paintStrengthText = CreateText("PaintLabel", uiRoot.transform, "Paint: 0.5", 16, TextAlignmentOptions.Left);
        paintStrengthText.rectTransform.anchorMin = new Vector2(0.3f, 1);
        paintStrengthText.rectTransform.anchorMax = new Vector2(0.7f, 1);
        paintStrengthText.rectTransform.anchoredPosition = new Vector2(0, -215);
        paintStrengthText.rectTransform.sizeDelta = new Vector2(0, 25);

        paintStrengthSlider = CreateSlider("PaintSlider", uiRoot.transform, new Vector2(0, -255), new Vector2(210, 20), 0f, 2f, 0.5f);
        paintStrengthSlider.onValueChanged.AddListener(val => paintStrength = val);

        // Paint Layer
        prevLayerButton = CreateButton("PrevLayer", uiRoot.transform, "<", new Vector2(10, -270), new Vector2(45, 45));
        nextLayerButton = CreateButton("NextLayer", uiRoot.transform, ">", new Vector2(65, -270), new Vector2(45, 45));
        paintLayerText = CreateText("LayerText", uiRoot.transform, "Layer: 0", 16, TextAlignmentOptions.Center);
        paintLayerText.rectTransform.anchorMin = new Vector2(0, 1);
        paintLayerText.rectTransform.anchorMax = new Vector2(1, 1);
        paintLayerText.rectTransform.anchoredPosition = new Vector2(0, -285);
        paintLayerText.rectTransform.sizeDelta = new Vector2(0, 30);

        prevLayerButton.onClick.AddListener(PrevPaintLayer);
        nextLayerButton.onClick.AddListener(NextPaintLayer);
    }

    void AssignLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            AssignLayerRecursively(child.gameObject, layer);
    }

    TMP_Text CreateText(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = uiStyle != null ? uiStyle.labelFontSize : fontSize;
        tmp.alignment = alignment;
        tmp.color = uiStyle != null ? uiStyle.textColor : Color.white;
        tmp.rectTransform.localScale = Vector3.one;
        return tmp;
    }

    Button CreateButton(string name, Transform parent, string text, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        go.GetComponent<Image>().color = uiStyle != null ? uiStyle.buttonColor : new Color(0.3f, 0.3f, 0.3f, 0.8f);
        Button button = go.GetComponent<Button>();

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(go.transform, false);
        TMP_Text tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = uiStyle != null ? uiStyle.buttonFontSize : 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = uiStyle != null ? uiStyle.buttonTextColor : Color.white;
        tmp.rectTransform.anchorMin = Vector2.zero;
        tmp.rectTransform.anchorMax = Vector2.one;
        tmp.rectTransform.sizeDelta = Vector2.zero;

        return button;
    }

    Slider CreateSlider(string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta, float minVal, float maxVal, float defaultVal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.3f, 1);
        rect.anchorMax = new Vector2(0.7f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        Slider slider = go.GetComponent<Slider>();
        slider.minValue = minVal;
        slider.maxValue = maxVal;
        slider.value = defaultVal;

        // Background
        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        bg.GetComponent<Image>().color = uiStyle != null ? uiStyle.sliderBgColor : new Color(0.2f, 0.2f, 0.2f);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = new Vector2(-20, 0);
        fillAreaRect.anchoredPosition = new Vector2(10, 0);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = uiStyle != null ? uiStyle.sliderFillColor : new Color(0.4f, 0.6f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        // Handle Slide Area
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = Vector2.zero;

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<Image>().color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.sizeDelta = new Vector2(10, 20);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.transition = Selectable.Transition.None;

        return slider;
    }

    void UpdateUI()
    {
        if (modeText) modeText.text = $"Mode: {currentMode}";
        if (strengthText) strengthText.text = $"Strength: {strength:F2}";
        if (brushSizeText) brushSizeText.text = $"Brush: {brushSize:F1}";
        if (paintLayerText) paintLayerText.text = $"Layer: {paintLayer}";
        if (paintStrengthText) paintStrengthText.text = $"Paint str: {paintStrength:F2}";

        bool paintMode = (currentMode == ToolMode.Paint);
        if (paintLayerText) paintLayerText.gameObject.SetActive(paintMode);
        if (paintStrengthText) paintStrengthText.gameObject.SetActive(paintMode);
        if (paintStrengthSlider) paintStrengthSlider.gameObject.SetActive(paintMode);
        if (prevLayerButton) prevLayerButton.gameObject.SetActive(paintMode);
        if (nextLayerButton) nextLayerButton.gameObject.SetActive(paintMode);
    }

    void PrevPaintLayer()
    {
        int layerCount = Terrain.activeTerrain?.terrainData.alphamapLayers ?? 1;
        paintLayer = (paintLayer - 1 + layerCount) % layerCount;
    }

    void NextPaintLayer()
    {
        int layerCount = Terrain.activeTerrain?.terrainData.alphamapLayers ?? 1;
        paintLayer = (paintLayer + 1) % layerCount;
    }
}