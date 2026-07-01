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
    public float maxDistance = 80f; // большой запас для дальнего луча
    public enum ToolMode { Dig, Raise, Paint, Water }
    public ToolMode currentMode = ToolMode.Dig;
    public float strength = 0.8f;
    public float brushSize = 2.5f;
    public int paintLayer = 0;
    public float paintStrength = 0.5f;
    public float actionInterval = 0.05f;

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

    // Для сохранения/восстановления высот террейна
    private float[,] originalHeights;
    private TerrainData terrainData;

    void Awake()
    {
        CreateUI();
        if (uiRoot != null)
            uiRoot.SetActive(false);

        // Сохраняем исходный рельеф, чтобы восстановить при выходе
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            terrainData = terrain.terrainData;
            originalHeights = terrainData.GetHeights(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);
        }
    }

    void Update()
    {
        // Переключение UI
        if (toggleUIAction != null && toggleUIAction.action != null && toggleUIAction.action.WasPressedThisFrame())
        {
            ToggleUI();
        }

        // Инструменты работают только когда UI выключен
        if (!uiActive && activateAction != null && activateAction.action != null && activateAction.action.IsPressed())
        {
            if (Time.time - lastActionTime >= actionInterval)
            {
                Transform activeController = GetActiveControllerTransform();
                if (activeController != null)
                {
                    ExecuteAction(activeController);
                    lastActionTime = Time.time;
                }
            }
        }
        UpdateUI();
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
                uiRoot.transform.localPosition = new Vector3(0, 0, 1.6f);
                uiRoot.transform.localRotation = Quaternion.identity;
                uiRoot.transform.localScale = new Vector3(0.004f, 0.004f, 0.004f);
            }
            else
            {
                uiRoot.transform.SetParent(null);
                uiRoot.transform.position = new Vector3(0, 1.2f, 1.5f);
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

    Transform FindControllerTransform(string name)
    {
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all) if (t.name == name) return t;
        return null;
    }
    Transform GetLeftHandTransform() => FindControllerTransform("LeftHand Controller");
    Transform GetRightHandTransform() => FindControllerTransform("RightHand Controller");
    Transform GetActiveControllerTransform()
    {
        Transform right = GetRightHandTransform();
        if (right != null && right.gameObject.activeInHierarchy) return right;
        Transform left = GetLeftHandTransform();
        if (left != null && left.gameObject.activeInHierarchy) return left;
        return null;
    }

    void ExecuteAction(Transform controllerTransform)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        // Наклонный луч для удобного копания на расстоянии
        Vector3 direction = (controllerTransform.forward * 0.5f - controllerTransform.up).normalized;
        Ray ray = new Ray(controllerTransform.position, direction);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            if (hit.collider.GetComponent<Terrain>() != null)
            {
                PerformAction(hit.point);
            }
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

    // ----- Вода (полная реализация) -----
    void PourWater(Vector3 center)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        float minHeight = GetMinHeightInRadius(center, waterMaxRadius, terrain);
        float centerHeight = terrain.SampleHeight(center);
        if (centerHeight - minHeight < 0.05f) return;

        float waterLevel = minHeight + 0.02f;

        List<Vector3> boundary = new List<Vector3>();
        for (int i = 0; i < waterBoundaryRays; i++)
        {
            float angle = i * Mathf.PI * 2f / waterBoundaryRays;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            float dist = FindEdgeDistance(center, dir, minHeight, terrain);
            Vector3 edgePoint = center + dir * dist;
            boundary.Add(new Vector3(edgePoint.x, waterLevel, edgePoint.z));
        }

        if (boundary.Count < 3) return;

        RemoveOldWater(center, waterCleanupRadius);

        ProceduralWaterMesh water = Instantiate(waterPrefab, center, Quaternion.identity);
        water.BuildFromBoundary(center, boundary.ToArray(), waterLevel);
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

    float FindEdgeDistance(Vector3 center, Vector3 dir, float minHeight, Terrain terrain)
    {
        float low = 0f;
        float high = waterMaxRadius;
        float threshold = minHeight + waterDepthThreshold;
        const float precision = 0.01f;

        if (terrain.SampleHeight(center + dir * high) <= threshold)
            return high;

        while (high - low > precision)
        {
            float mid = (low + high) * 0.5f;
            float h = terrain.SampleHeight(center + dir * mid);
            if (h > threshold) high = mid;
            else low = mid;
        }
        return (low + high) * 0.5f;
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

    // ----- UI -----
    void CreateUI()
    {
        uiRoot = new GameObject("FixedUI", typeof(RectTransform), typeof(Canvas));
        uiRoot.transform.SetParent(null);

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        uiRoot.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = uiRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(640, 400); // ещё больше

        uiRoot.layer = LayerMask.NameToLayer("UI");
        AssignLayerRecursively(uiRoot, LayerMask.NameToLayer("UI"));

        // Фон
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(uiRoot.transform, false);
        panel.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // Заголовок (крупнее)
        modeText = CreateText("ModeText", uiRoot.transform, "Mode: Dig", 24, TextAlignmentOptions.Center);
        modeText.rectTransform.anchorMin = new Vector2(0, 1);
        modeText.rectTransform.anchorMax = new Vector2(1, 1);
        modeText.rectTransform.pivot = new Vector2(0.5f, 1);
        modeText.rectTransform.anchoredPosition = new Vector2(0, -10);
        modeText.rectTransform.sizeDelta = new Vector2(-20, 35);

        // Кнопки режимов (шире и выше)
        digButton = CreateButton("BtnDig", uiRoot.transform, "Dig", new Vector2(10, -55), new Vector2(90, 40));
        raiseButton = CreateButton("BtnRaise", uiRoot.transform, "Raise", new Vector2(10, -105), new Vector2(90, 40));
        paintButton = CreateButton("BtnPaint", uiRoot.transform, "Paint", new Vector2(10, -155), new Vector2(90, 40));
        waterButton = CreateButton("BtnWater", uiRoot.transform, "Water", new Vector2(10, -205), new Vector2(90, 40));

        digButton.onClick.AddListener(() => currentMode = ToolMode.Dig);
        raiseButton.onClick.AddListener(() => currentMode = ToolMode.Raise);
        paintButton.onClick.AddListener(() => currentMode = ToolMode.Paint);
        waterButton.onClick.AddListener(() => currentMode = ToolMode.Water);

        // Strength Slider (min 0.01, max 5)
        strengthText = CreateText("StrengthLabel", uiRoot.transform, "Strength: 0.8", 16, TextAlignmentOptions.Left);
        strengthText.rectTransform.anchorMin = new Vector2(0.3f, 1);
        strengthText.rectTransform.anchorMax = new Vector2(0.7f, 1);
        strengthText.rectTransform.anchoredPosition = new Vector2(0, -55);
        strengthText.rectTransform.sizeDelta = new Vector2(0, 25);

        strengthSlider = CreateSlider("StrengthSlider", uiRoot.transform, new Vector2(0, -95), new Vector2(210, 20), 0.01f, 5f, 0.8f);
        strengthSlider.onValueChanged.AddListener(val => strength = val);

        // Brush Size Slider (min 0.1, max 20)
        brushSizeText = CreateText("BrushLabel", uiRoot.transform, "Brush: 2.5", 16, TextAlignmentOptions.Left);
        brushSizeText.rectTransform.anchorMin = new Vector2(0.3f, 1);
        brushSizeText.rectTransform.anchorMax = new Vector2(0.7f, 1);
        brushSizeText.rectTransform.anchoredPosition = new Vector2(0, -135);
        brushSizeText.rectTransform.sizeDelta = new Vector2(0, 25);

        brushSizeSlider = CreateSlider("BrushSlider", uiRoot.transform, new Vector2(0, -175), new Vector2(210, 20), 0.1f, 20f, 2.5f);
        brushSizeSlider.onValueChanged.AddListener(val => brushSize = val);

        // Paint Strength Slider (min 0, max 2)
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
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
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

        go.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        Button button = go.GetComponent<Button>();

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(go.transform, false);
        TMP_Text tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
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
        bg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);
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
        fill.GetComponent<Image>().color = new Color(0.4f, 0.6f, 1f);
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

    void OnDestroy()
    {
        // Восстанавливаем исходный рельеф при остановке игры
        if (terrainData != null && originalHeights != null)
        {
            terrainData.SetHeights(0, 0, originalHeights);
            terrainData.SyncHeightmap();
        }
    }
}