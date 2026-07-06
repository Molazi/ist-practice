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
    public int waterBoundaryRays = 128;
    public float waterCleanupRadius = 5f;

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

    // ========== Target Terrain ==========
    public Terrain targetTerrain;

    // ========== Луч ==========
    [Header("Ray Settings")]
    public float rayDownAngle = 20f;

    [Header("Laser Visual")]
    public Color laserColor = Color.red;
    public float laserWidth = 0.02f;

    // ========== Activation (Grip) ==========
    public InputActionProperty activateAction;
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

    public float uiDistance = 1.8f;
    public Vector3 uiScale = new Vector3(0.004f, 0.004f, 0.004f);
    private float lastUIDistance;
    private Vector3 lastUIScale;

    private LineRenderer laserLine;
    private Transform rightController;

    // Сохранение ландшафта
    private float[,] originalHeights;
    private float[,,] originalAlphamaps;
    private TerrainData currentTerrainData;

    // Позиции для сдвига UI
    private Vector2 strengthTextOrigPos, strengthSliderOrigPos;
    private Vector2 paintStrengthTextOrigPos, paintStrengthSliderOrigPos;
    private Vector2 prevLayerOrigPos, nextLayerOrigPos, layerTextOrigPos;

    void Awake()
    {
        if (targetTerrain == null)
            targetTerrain = Terrain.activeTerrain;
        if (targetTerrain != null)
            currentTerrainData = targetTerrain.terrainData;

        CreateUI();
        if (uiRoot != null) uiRoot.SetActive(false);
        lastUIDistance = uiDistance;
        lastUIScale = uiScale;
        CreateLaser();
    }

    void Start()
    {
        if (currentTerrainData != null)
        {
            int res = currentTerrainData.heightmapResolution;
            originalHeights = currentTerrainData.GetHeights(0, 0, res, res);
            int alphaRes = currentTerrainData.alphamapResolution;
            int layers = currentTerrainData.alphamapLayers;
            originalAlphamaps = currentTerrainData.GetAlphamaps(0, 0, alphaRes, alphaRes);
        }
        ResetTerrainToOriginal();
    }

    void OnDestroy()
    {
        ResetTerrainToOriginal();
    }

    void ResetTerrainToOriginal()
    {
        if (currentTerrainData == null || originalHeights == null) return;
        currentTerrainData.SetHeights(0, 0, originalHeights);
        if (originalAlphamaps != null)
            currentTerrainData.SetAlphamaps(0, 0, originalAlphamaps);
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
        bool gripJustPressed = activateAction != null && activateAction.action != null && activateAction.action.WasPressedThisFrame();

        UpdateLaser(gripPressed);

        if (!uiActive)
        {
            if (currentMode == ToolMode.Water)
            {
                if (gripJustPressed)
                {
                    Transform controller = GetRightController();
                    if (controller != null) ExecuteAction(controller);
                }
            }
            else if (gripPressed)
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
        if (controller == null) { laserLine.enabled = false; return; }

        Vector3 direction = Quaternion.AngleAxis(rayDownAngle, controller.right) * controller.forward;
        Ray ray = new Ray(controller.position, direction);
        int layerMask = 1 << LayerMask.NameToLayer("Terrain");
        if (layerMask == 0) layerMask = -1;

        if (gripPressed && Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
        {
            laserLine.enabled = true;
            laserLine.SetPosition(0, controller.position);
            laserLine.SetPosition(1, hit.point);
        }
        else laserLine.enabled = false;
    }

    Transform GetRightController()
    {
        if (rightController == null || !rightController.gameObject.activeInHierarchy)
        {
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (Transform t in all)
                if (t.name == "RightHand Controller" || t.name == "Right Controller" || t.name.Contains("Right"))
                { rightController = t; break; }
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
        if (targetTerrain == null) return;
        Vector3 direction = Quaternion.AngleAxis(rayDownAngle, controllerTransform.right) * controllerTransform.forward;
        int layerMask = 1 << LayerMask.NameToLayer("Terrain");
        if (layerMask == 0) layerMask = -1;
        Ray ray = new Ray(controllerTransform.position, direction);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
            if (hit.collider.GetComponent<Terrain>() != null)
                PerformAction(hit.point);
    }

    void PerformAction(Vector3 worldPos)
    {
        switch (currentMode)
        {
            case ToolMode.Dig:
                TerrainDeformer.Deform(targetTerrain, worldPos, -strength * Time.deltaTime, brushSize);
                SpawnPhysicsParticles(worldPos, new Color(0.77f, 0.64f, 0.52f), 12, 0.05f, 1.5f);
                break;
            case ToolMode.Raise:
                TerrainDeformer.Deform(targetTerrain, worldPos, strength * Time.deltaTime, brushSize);
                SpawnPhysicsParticles(worldPos, new Color(0.77f, 0.64f, 0.52f), 12, 0.05f, 1.5f);
                break;
            case ToolMode.Paint:
                TerrainDeformer.PaintTexture(targetTerrain, worldPos, brushSize, paintStrength, paintLayer);
                SpawnPhysicsParticles(worldPos, Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f), 20, 0.04f, 2f);
                break;
            case ToolMode.Water:
                PourWater(worldPos);
                // Мощный всплеск: 50 крупных синих кубиков, сила 3.5, живут 3 секунды
                SpawnPhysicsParticles(worldPos, new Color(0.3f, 0.5f, 1f), 100, 0.1f, 3f, 9.5f);
                break;
        }
    }
    // Спавн физических кубиков
    void SpawnPhysicsParticles(Vector3 position, Color color, int count = 10, float size = 0.05f, float lifetime = 2f, float force = 0.8f)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = position + Random.insideUnitSphere * 0.05f;
            cube.transform.localScale = Vector3.one * size;
            cube.transform.rotation = Random.rotation;

            Rigidbody rb = cube.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.AddForce(Random.onUnitSphere * force, ForceMode.Impulse);

            Renderer rend = cube.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Unlit/Color"));
            rend.material.color = color;

            Destroy(cube, lifetime);
        }
    }

    // ================= ВОДА =================
    void PourWater(Vector3 center)
    {
        if (targetTerrain == null) return;
        float bottomHeight = targetTerrain.SampleHeight(center);
        int rays = Mathf.Max(waterBoundaryRays, 64);
        float maxSearchRadius = 50f;
        float step = 0.05f;
        float[] maxHeights = new float[rays];
        List<Vector3> contourPoints = new List<Vector3>();

        for (int i = 0; i < rays; i++)
        {
            float angle = i * Mathf.PI * 2f / rays;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            float maxH = bottomHeight;
            float dist = 0f;
            while (dist < maxSearchRadius)
            {
                Vector3 testPoint = center + dir * dist;
                float h = targetTerrain.SampleHeight(testPoint);
                if (h > maxH) maxH = h;
                dist += step * 4;
            }
            maxHeights[i] = maxH;
        }

        float waterLevel = Mathf.Min(maxHeights);
        if (waterLevel - bottomHeight < 0.15f) return;

        for (int i = 0; i < rays; i++)
        {
            float angle = i * Mathf.PI * 2f / rays;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            float prevDist = 0f, prevHeight = bottomHeight, dist = 0f;
            bool found = false;
            while (dist < maxSearchRadius)
            {
                Vector3 testPoint = center + dir * dist;
                float h = targetTerrain.SampleHeight(testPoint);
                if (h >= waterLevel)
                {
                    float t = 0f;
                    if (Mathf.Abs(h - prevHeight) > 0.0001f)
                        t = (waterLevel - prevHeight) / (h - prevHeight);
                    float contourDist = prevDist + t * (dist - prevDist);
                    Vector3 contourPoint = center + dir * contourDist;
                    contourPoint.y = waterLevel;
                    contourPoints.Add(contourPoint);
                    found = true;
                    break;
                }
                prevDist = dist; prevHeight = h;
                dist += step;
            }
            if (!found)
            {
                Vector3 farPoint = center + dir * maxSearchRadius;
                farPoint.y = waterLevel;
                contourPoints.Add(farPoint);
            }
        }
        if (contourPoints.Count < 3) return;

        Vector3 waterCenter = new Vector3(center.x, waterLevel, center.z);
        RemoveOldWater(waterCenter, waterCleanupRadius);
        ProceduralWaterMesh water = Instantiate(waterPrefab, waterCenter, Quaternion.identity);
        water.transform.localScale = Vector3.one;
        water.BuildFromBoundary(waterCenter, contourPoints.ToArray(), waterLevel);
    }

    void RemoveOldWater(Vector3 position, float radius)
    {
        ProceduralWaterMesh[] allWater = FindObjectsByType<ProceduralWaterMesh>();
        foreach (ProceduralWaterMesh water in allWater)
            if (Vector3.Distance(position, water.transform.position) <= radius)
                Destroy(water.gameObject);
    }

    // ===== UI (полный код без изменений) =====
    void CreateUI()
    {
        uiRoot = new GameObject("FixedUI", typeof(RectTransform), typeof(Canvas));
        uiRoot.transform.SetParent(null);
        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        uiRoot.AddComponent<TrackedDeviceGraphicRaycaster>();
        RectTransform canvasRect = uiRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(800, 500);
        uiRoot.layer = LayerMask.NameToLayer("UI");
        AssignLayerRecursively(uiRoot, LayerMask.NameToLayer("UI"));

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(uiRoot.transform, false);
        panel.GetComponent<Image>().color = uiStyle != null ? uiStyle.panelBgColor : new Color(0, 0, 0, 0.5f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one; panelRect.sizeDelta = Vector2.zero;

        modeText = CreateText("ModeText", uiRoot.transform, "Режим: Копание", 28, TextAlignmentOptions.Left);
        modeText.rectTransform.anchorMin = new Vector2(0, 1);
        modeText.rectTransform.anchorMax = new Vector2(0, 1);
        modeText.rectTransform.pivot = new Vector2(0, 1);
        modeText.rectTransform.anchoredPosition = new Vector2(30, -15);
        modeText.rectTransform.sizeDelta = new Vector2(350, 40);

        float sliderLeftMargin = 30f;
        float sliderWidth = 740f;
        float labelHeight = 28f, sliderHeight = 24f;
        float yPos = -70f;
        float rowSpacing = 50f;

        brushSizeText = CreateText("BrushLabel", uiRoot.transform, "Кисть: 2.5", 20, TextAlignmentOptions.Left);
        brushSizeText.rectTransform.anchorMin = new Vector2(0, 1); brushSizeText.rectTransform.anchorMax = new Vector2(0, 1);
        brushSizeText.rectTransform.pivot = new Vector2(0, 1);
        brushSizeText.rectTransform.anchoredPosition = new Vector2(sliderLeftMargin, yPos);
        brushSizeText.rectTransform.sizeDelta = new Vector2(250, labelHeight);
        yPos -= labelHeight + 8;
        brushSizeSlider = CreateSlider("BrushSlider", uiRoot.transform, new Vector2(sliderLeftMargin, yPos), new Vector2(sliderWidth, sliderHeight), 0.1f, 10f, 2.5f);
        brushSizeSlider.onValueChanged.AddListener(val => brushSize = val);
        yPos -= sliderHeight + rowSpacing;

        strengthText = CreateText("StrengthLabel", uiRoot.transform, "Сила: 0.800", 20, TextAlignmentOptions.Left);
        strengthText.rectTransform.anchorMin = new Vector2(0, 1); strengthText.rectTransform.anchorMax = new Vector2(0, 1);
        strengthText.rectTransform.pivot = new Vector2(0, 1);
        strengthText.rectTransform.anchoredPosition = new Vector2(sliderLeftMargin, yPos);
        strengthText.rectTransform.sizeDelta = new Vector2(250, labelHeight);
        strengthTextOrigPos = strengthText.rectTransform.anchoredPosition;
        yPos -= labelHeight + 8;
        strengthSlider = CreateSlider("StrengthSlider", uiRoot.transform, new Vector2(sliderLeftMargin, yPos), new Vector2(sliderWidth, sliderHeight), 0.001f, 3f, 0.8f);
        strengthSlider.onValueChanged.AddListener(val => strength = val);
        strengthSliderOrigPos = strengthSlider.GetComponent<RectTransform>().anchoredPosition;
        yPos -= sliderHeight + rowSpacing;

        paintStrengthText = CreateText("PaintLabel", uiRoot.transform, "Краска: 0.50", 20, TextAlignmentOptions.Left);
        paintStrengthText.rectTransform.anchorMin = new Vector2(0, 1); paintStrengthText.rectTransform.anchorMax = new Vector2(0, 1);
        paintStrengthText.rectTransform.pivot = new Vector2(0, 1);
        paintStrengthText.rectTransform.anchoredPosition = new Vector2(sliderLeftMargin, yPos);
        paintStrengthText.rectTransform.sizeDelta = new Vector2(250, labelHeight);
        paintStrengthTextOrigPos = paintStrengthText.rectTransform.anchoredPosition;
        yPos -= labelHeight + 8;
        paintStrengthSlider = CreateSlider("PaintSlider", uiRoot.transform, new Vector2(sliderLeftMargin, yPos), new Vector2(sliderWidth, sliderHeight), 0f, 2f, 0.5f);
        paintStrengthSlider.onValueChanged.AddListener(val => paintStrength = val);
        paintStrengthSliderOrigPos = paintStrengthSlider.GetComponent<RectTransform>().anchoredPosition;
        yPos -= sliderHeight + 45f;

        float arrowButtonWidth = 120f, arrowButtonHeight = 55f;
        prevLayerButton = CreateButton("PrevLayer", uiRoot.transform, "◄", Vector2.zero, Vector2.zero);
        RectTransform prevRect = prevLayerButton.GetComponent<RectTransform>();
        prevRect.anchorMin = prevRect.anchorMax = new Vector2(0, 1);
        prevRect.pivot = new Vector2(0, 1);
        prevRect.anchoredPosition = new Vector2(60, yPos);
        prevRect.sizeDelta = new Vector2(arrowButtonWidth, arrowButtonHeight);
        prevLayerButton.onClick.AddListener(PrevPaintLayer);
        prevLayerOrigPos = prevRect.anchoredPosition;

        nextLayerButton = CreateButton("NextLayer", uiRoot.transform, "►", Vector2.zero, Vector2.zero);
        RectTransform nextRect = nextLayerButton.GetComponent<RectTransform>();
        nextRect.anchorMin = nextRect.anchorMax = new Vector2(1, 1);
        nextRect.pivot = new Vector2(1, 1);
        nextRect.anchoredPosition = new Vector2(-60, yPos);
        nextRect.sizeDelta = new Vector2(arrowButtonWidth, arrowButtonHeight);
        nextLayerButton.onClick.AddListener(NextPaintLayer);
        nextLayerOrigPos = nextRect.anchoredPosition;

        paintLayerText = CreateText("LayerText", uiRoot.transform, "Слой: 0", 22, TextAlignmentOptions.Center);
        paintLayerText.rectTransform.anchorMin = new Vector2(0, 1); paintLayerText.rectTransform.anchorMax = new Vector2(1, 1);
        paintLayerText.rectTransform.pivot = new Vector2(0.5f, 1);
        paintLayerText.rectTransform.anchoredPosition = new Vector2(0, yPos - 8);
        paintLayerText.rectTransform.sizeDelta = new Vector2(-(arrowButtonWidth * 2 + 60), 34);
        layerTextOrigPos = paintLayerText.rectTransform.anchoredPosition;

        yPos -= arrowButtonHeight + 20f;

        float modeButtonWidth = 140f, modeButtonHeight = 55f;
        float bottomMargin = 25f, startX = 60f, spacingX = 30f;

        digButton = CreateModeButton("BtnDig", "Копать", new Vector2(startX, bottomMargin), new Vector2(modeButtonWidth, modeButtonHeight));
        raiseButton = CreateModeButton("BtnRaise", "Насыпать", new Vector2(startX + modeButtonWidth + spacingX, bottomMargin), new Vector2(modeButtonWidth, modeButtonHeight));
        paintButton = CreateModeButton("BtnPaint", "Красить", new Vector2(startX + (modeButtonWidth + spacingX) * 2, bottomMargin), new Vector2(modeButtonWidth, modeButtonHeight));
        waterButton = CreateModeButton("BtnWater", "Вода", new Vector2(startX + (modeButtonWidth + spacingX) * 3, bottomMargin), new Vector2(modeButtonWidth, modeButtonHeight));

        digButton.onClick.AddListener(() => currentMode = ToolMode.Dig);
        raiseButton.onClick.AddListener(() => currentMode = ToolMode.Raise);
        paintButton.onClick.AddListener(() => currentMode = ToolMode.Paint);
        waterButton.onClick.AddListener(() => currentMode = ToolMode.Water);
    }

    Button CreateModeButton(string name, string text, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        Button btn = CreateButton(name, uiRoot.transform, text, anchoredPos, sizeDelta);
        RectTransform rect = btn.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0, 0);
        rect.anchoredPosition = anchoredPos;
        return btn;
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
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        go.GetComponent<Image>().color = uiStyle != null ? uiStyle.buttonColor : new Color(0.3f, 0.3f, 0.3f, 0.8f);
        Button button = go.GetComponent<Button>();

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(go.transform, false);
        TMP_Text tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = uiStyle != null ? uiStyle.buttonFontSize : 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = uiStyle != null ? uiStyle.buttonTextColor : Color.white;
        tmp.rectTransform.anchorMin = Vector2.zero; tmp.rectTransform.anchorMax = Vector2.one; tmp.rectTransform.sizeDelta = Vector2.zero;
        return button;
    }

    Slider CreateSlider(string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta, float minVal, float maxVal, float defaultVal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        Slider slider = go.GetComponent<Slider>();
        slider.minValue = minVal; slider.maxValue = maxVal; slider.value = defaultVal;

        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        bg.GetComponent<Image>().color = uiStyle != null ? uiStyle.sliderBgColor : new Color(0.2f, 0.2f, 0.2f);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.sizeDelta = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero; fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = new Vector2(-30, 0); fillAreaRect.anchoredPosition = new Vector2(15, 0);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = uiStyle != null ? uiStyle.sliderFillColor : new Color(0.4f, 0.6f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.sizeDelta = Vector2.zero;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero; handleAreaRect.anchorMax = Vector2.one; handleAreaRect.sizeDelta = Vector2.zero;

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<Image>().color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one; handleRect.sizeDelta = new Vector2(12, 24);

        slider.fillRect = fillRect; slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.transition = Selectable.Transition.None;
        return slider;
    }

    void UpdateUI()
    {
        string modeStr = currentMode switch
        {
            ToolMode.Dig => "Копание",
            ToolMode.Raise => "Насыпь",
            ToolMode.Paint => "Краска",
            ToolMode.Water => "Вода",
            _ => ""
        };
        if (modeText) modeText.text = $"Режим: {modeStr}";

        bool digOrRaise = (currentMode == ToolMode.Dig || currentMode == ToolMode.Raise);
        bool paintMode = (currentMode == ToolMode.Paint);
        bool waterMode = (currentMode == ToolMode.Water);

        if (brushSizeText) brushSizeText.gameObject.SetActive(!waterMode);
        if (brushSizeSlider) brushSizeSlider.gameObject.SetActive(!waterMode);
        if (strengthText) strengthText.gameObject.SetActive(digOrRaise);
        if (strengthSlider) strengthSlider.gameObject.SetActive(digOrRaise);
        if (paintStrengthText) paintStrengthText.gameObject.SetActive(paintMode);
        if (paintStrengthSlider) paintStrengthSlider.gameObject.SetActive(paintMode);
        if (paintLayerText) paintLayerText.gameObject.SetActive(paintMode);
        if (prevLayerButton) prevLayerButton.gameObject.SetActive(paintMode);
        if (nextLayerButton) nextLayerButton.gameObject.SetActive(paintMode);

        if (paintMode)
        {
            Vector2 shift = strengthTextOrigPos - paintStrengthTextOrigPos;
            paintStrengthText.rectTransform.anchoredPosition = paintStrengthTextOrigPos + shift;
            paintStrengthSlider.GetComponent<RectTransform>().anchoredPosition = paintStrengthSliderOrigPos + shift;
            prevLayerButton.GetComponent<RectTransform>().anchoredPosition = prevLayerOrigPos + shift;
            nextLayerButton.GetComponent<RectTransform>().anchoredPosition = nextLayerOrigPos + shift;
            paintLayerText.rectTransform.anchoredPosition = layerTextOrigPos + shift;
        }
        else
        {
            paintStrengthText.rectTransform.anchoredPosition = paintStrengthTextOrigPos;
            paintStrengthSlider.GetComponent<RectTransform>().anchoredPosition = paintStrengthSliderOrigPos;
            prevLayerButton.GetComponent<RectTransform>().anchoredPosition = prevLayerOrigPos;
            nextLayerButton.GetComponent<RectTransform>().anchoredPosition = nextLayerOrigPos;
            paintLayerText.rectTransform.anchoredPosition = layerTextOrigPos;
        }

        if (brushSizeText && !waterMode) brushSizeText.text = $"Кисть: {brushSize:F1}";
        if (strengthText && digOrRaise) strengthText.text = $"Сила: {strength:F3}";
        if (paintStrengthText && paintMode) paintStrengthText.text = $"Краска: {paintStrength:F2}";
        if (paintLayerText && paintMode) paintLayerText.text = $"Слой: {paintLayer}";
    }

    void PrevPaintLayer()
    {
        int layerCount = targetTerrain?.terrainData.alphamapLayers ?? 1;
        paintLayer = (paintLayer - 1 + layerCount) % layerCount;
    }

    void NextPaintLayer()
    {
        int layerCount = targetTerrain?.terrainData.alphamapLayers ?? 1;
        paintLayer = (paintLayer + 1) % layerCount;
    }
}