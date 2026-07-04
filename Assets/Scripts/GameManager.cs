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
    public float strength = 0.02f;
    public float brushSize = 2.5f;
    public int paintLayer = 0;
    public float paintStrength = 0.5f;
    public float actionInterval = 0.05f;

    [Header("Paint Material Displacement Settings")]
    public List<LayerDisplacementProfile> layerProfiles = new List<LayerDisplacementProfile>()
    {
        new LayerDisplacementProfile { heightOffset = -0.1f, roughness = 0.0f },
        new LayerDisplacementProfile { heightOffset = 0.3f, roughness = 0.5f },
        new LayerDisplacementProfile { heightOffset = 0.0f, roughness = 0.1f }
    };

    [Header("Terrain Original")]
    public TerrainData originalTerrainData;

    [Header("Ray Settings")]
    [Range(0f, 1f)] public float rayForwardWeight = 0.95f;
    [Range(0f, 1f)] public float rayDownWeight = 0.05f;

    [Header("Laser Visual")]
    public Color laserColor = Color.red;
    public float laserWidth = 0.02f;

    [Header("XR Input Actions")]
    [Tooltip("Сюда кидаешь XRI RightHand/Activate (в эмуляторе это ЛКМ)")]
    public InputActionProperty activateAction;
    [Tooltip("Сюда кидаешь XRI RightHand/Grip (кнопка Сжимания для Меню)")]
    public InputActionProperty toggleUIAction;

    [Header("Dynamic UI Layout Settings")]
    public float uiVerticalSpacing = 65f;
    public float uiStartYOffset = -60f;

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

    private GameObject strengthGroup;
    private GameObject brushGroup;
    private GameObject paintStrengthGroup;
    private GameObject paintLayerGroup;

    private float lastActionTime = 0f;

    [Header("UI Transform Settings")]
    public float uiDistance = 1.2f;
    public Vector3 uiScale = new Vector3(0.003f, 0.003f, 0.003f);

    private float lastUIDistance;
    private Vector3 lastUIScale;

    private Terrain currentTerrain;
    private TerrainData currentTerrainData;
    private LineRenderer laserLine;
    private Transform rightController;

    [System.Serializable]
    public class LayerDisplacementProfile
    {
        public float heightOffset = 0.2f;
        public float roughness = 0.3f;
    }

    private void OnEnable()
    {
        if (activateAction.action != null) activateAction.action.Enable();
        if (toggleUIAction.action != null) toggleUIAction.action.Enable();
    }

    private void OnDisable()
    {
        if (activateAction.action != null) activateAction.action.Disable();
        if (toggleUIAction.action != null) toggleUIAction.action.Disable();
    }

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
        if (currentTerrainData == null || originalTerrainData == null) return;
        int res = currentTerrainData.heightmapResolution;
        if (originalTerrainData.heightmapResolution != res) return;

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
        // 1. Проверяем нажатие ГРИПА для вызова меню
        if (toggleUIAction != null && toggleUIAction.action != null && toggleUIAction.action.WasPressedThisFrame())
        {
            ToggleUI();
        }

        // 2. Считываем ЛКМ (Activate Action) на эмуляторе
        bool isLMBPressed = activateAction != null && activateAction.action != null && activateAction.action.IsPressed();
        bool isLMBJustPressed = activateAction != null && activateAction.action != null && activateAction.action.WasPressedThisFrame();

        // Обновляем состояние лазера на основе нажатия ЛКМ
        UpdateLaser(isLMBPressed);

        // 3. Воздействие на ландшафт работает, только если меню закрыто
        if (!uiActive)
        {
            if (currentMode == ToolMode.Water)
            {
                if (isLMBJustPressed)
                {
                    Transform controller = GetRightController();
                    if (controller != null) ExecuteAction(controller);
                }
            }
            else if (isLMBPressed)
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

        // Обновление положения UI, если оно открыто
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

    void UpdateLaser(bool isLMBPressed)
    {
        if (laserLine == null) return;

        // Если ЛКМ не зажат — тушим лазер сразу
        if (!isLMBPressed)
        {
            laserLine.enabled = false;
            return;
        }

        Transform controller = GetRightController();
        if (controller == null) return;

        Vector3 forwardDir = controller.forward;
        if (forwardDir.sqrMagnitude < 0.01f) forwardDir = Vector3.forward;

        Vector3 direction = (forwardDir * rayForwardWeight - controller.up * rayDownWeight).normalized;

        Camera mainCam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (mainCam != null)
        {
            if (Vector3.Dot(direction, mainCam.transform.forward) < -0.5f)
            {
                direction = mainCam.transform.forward;
            }
        }

        Ray ray = new Ray(controller.position, direction);

        // Защита: если слой Terrain не настроен в проекте, проверяем все слои, кроме воды
        int terrainLayer = LayerMask.NameToLayer("Terrain");
        int layerMask = (terrainLayer != -1) ? (1 << terrainLayer) : ~LayerMask.GetMask("Water");

        laserLine.enabled = true;
        laserLine.SetPosition(0, controller.position);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
        {
            // Если попали в террейн — ведем луч до точки удара
            laserLine.SetPosition(1, hit.point);
        }
        else
        {
            // ФИКС ДЛЯ ЭМУЛЯТОРА: Если промазал мимо земли, рисуем линию просто вперед, чтобы видеть лазер
            laserLine.SetPosition(1, controller.position + direction * 15f);
        }
    }

    Transform GetRightController()
    {
        if (rightController == null || !rightController.gameObject.activeInHierarchy)
        {
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (Transform t in all)
            {
                if (t.GetComponent<RectTransform>() != null) continue;

                string nameLower = t.name.ToLower();
                if (nameLower.Contains("right") && (nameLower.Contains("controller") || nameLower.Contains("hand") || nameLower.Contains("sim")))
                {
                    rightController = t;
                    break;
                }
            }

            // АВАРЯЙНАЯ ЗАГЛУШКА: Если контроллер на эмуляторе не найден, стреляем из камеры
            if (rightController == null)
            {
                Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                if (cam != null) return cam.transform;
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
            Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                uiRoot.transform.SetParent(cam.transform, false);
                uiRoot.transform.localPosition = new Vector3(0, 0, uiDistance);
                uiRoot.transform.localRotation = Quaternion.identity;
                uiRoot.transform.localScale = uiScale;
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
        Vector3 direction = (controllerTransform.forward * rayForwardWeight - controllerTransform.up * rayDownWeight).normalized;
        int layerMask = ~LayerMask.GetMask("Water");

        Ray ray = new Ray(controllerTransform.position, direction);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask))
        {
            if (hit.collider.GetComponent<Terrain>() != null)
            {
                if (currentMode == ToolMode.Water)
                {
                    PourWater(hit.point);
                }
                else
                {
                    PerformAction(hit.point);
                }
            }
        }
    }

    void PerformAction(Vector3 worldPos)
    {
        Terrain terrain = Terrain.activeTerrain;
        float effectiveStrength = strength * Time.deltaTime * 2.0f;

        if (terrain == null) return;

        switch (currentMode)
        {
            case ToolMode.Dig:
                TerrainDeformer.Deform(terrain, worldPos, -effectiveStrength, brushSize);
                break;
            case ToolMode.Raise:
                TerrainDeformer.Deform(terrain, worldPos, effectiveStrength, brushSize);
                break;
            case ToolMode.Paint:
                TerrainDeformer.PaintTexture(terrain, worldPos, brushSize, paintStrength, paintLayer);
                ApplyMaterialDisplacement(terrain, worldPos);
                break;
            case ToolMode.Water:
                PourWater(worldPos);
                break;
        }
    }

    void ApplyMaterialDisplacement(Terrain terrain, Vector3 worldPos)
    {
        if (paintLayer < 0 || paintLayer >= layerProfiles.Count) return;
        LayerDisplacementProfile profile = layerProfiles[paintLayer];

        if (Mathf.Approximately(profile.heightOffset, 0f) && Mathf.Approximately(profile.roughness, 0f)) return;

        TerrainData tData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        int mapX = Mathf.RoundToInt(((worldPos.x - terrainPos.x) / tData.size.x) * tData.heightmapResolution);
        int mapZ = Mathf.RoundToInt(((worldPos.z - terrainPos.z) / tData.size.z) * tData.heightmapResolution);
        int radiusInNodes = Mathf.RoundToInt((brushSize / tData.size.x) * tData.heightmapResolution);

        int startX = Mathf.Max(0, mapX - radiusInNodes);
        int startZ = Mathf.Max(0, mapZ - radiusInNodes);
        int endX = Mathf.Min(tData.heightmapResolution, mapX + radiusInNodes);
        int endZ = Mathf.Min(tData.heightmapResolution, mapZ + radiusInNodes);

        int width = endX - startX;
        int height = endZ - startZ;

        if (width <= 0 || height <= 0) return;

        float[,] heights = tData.GetHeights(startX, startZ, width, height);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int currentNodesX = startX + x;
                int currentNodesZ = startZ + z;

                float dist = Vector2.Distance(new Vector2(mapX, mapZ), new Vector2(currentNodesX, currentNodesZ));
                if (dist <= radiusInNodes)
                {
                    float falloff = 1f - (dist / radiusInNodes);
                    float targetOffset = profile.heightOffset * paintStrength * falloff * Time.deltaTime;

                    if (profile.roughness > 0f)
                    {
                        float noise = Mathf.PerlinNoise(currentNodesX * 0.4f, currentNodesZ * 0.4f) * 2f - 1f;
                        targetOffset += noise * profile.roughness * paintStrength * falloff * Time.deltaTime * 0.5f;
                    }

                    heights[z, x] += targetOffset / tData.size.y;
                }
            }
        }
        tData.SetHeights(startX, startZ, heights);
    }

    void PourWater(Vector3 hitPoint)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        Vector3 trueCenter = hitPoint;
        float lowestHeight = terrain.SampleHeight(hitPoint);
        int scanSteps = 12;

        for (int i = 0; i < scanSteps; i++)
        {
            float angle = i * Mathf.PI * 2f / scanSteps;
            for (float r = brushSize * 0.3f; r <= brushSize * 1.2f; r += brushSize * 0.3f)
            {
                Vector3 testPos = hitPoint + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * r;
                float h = terrain.SampleHeight(testPos);
                if (h < lowestHeight)
                {
                    lowestHeight = h;
                    trueCenter = new Vector3(testPos.x, h, testPos.z);
                }
            }
        }

        float defaultDepth = brushSize * 0.35f;
        float waterLevel = Mathf.Max(lowestHeight + defaultDepth, hitPoint.y);

        RemoveOldWater(trueCenter, brushSize * 3f);

        ProceduralWaterMesh water = Instantiate(waterPrefab, trueCenter, Quaternion.identity);
        water.transform.localScale = Vector3.one;

        int waterLayer = LayerMask.NameToLayer("Water");
        if (waterLayer != -1) water.gameObject.layer = waterLayer;

        int rayCount = 32;
        List<Vector3> boundaryPoints = new List<Vector3>();
        float maxSearchRadius = brushSize * 2.5f;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = i * Mathf.PI * 2f / rayCount;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

            float currentRadius = 0.1f;
            while (currentRadius < maxSearchRadius)
            {
                Vector3 testPoint = trueCenter + dir * currentRadius;
                float groundHeight = terrain.SampleHeight(testPoint);

                if (groundHeight >= waterLevel)
                {
                    currentRadius += 0.25f;
                    break;
                }
                currentRadius += 0.1f;
            }

            currentRadius = Mathf.Min(currentRadius, maxSearchRadius);
            boundaryPoints.Add(new Vector3(trueCenter.x + dir.x * currentRadius, waterLevel, trueCenter.z + dir.z * currentRadius));
        }

        water.BuildFromBoundary(trueCenter, boundaryPoints.ToArray(), waterLevel);
    }

    void RemoveOldWater(Vector3 position, float radius)
    {
        ProceduralWaterMesh[] allWater = FindObjectsByType<ProceduralWaterMesh>();
        foreach (ProceduralWaterMesh water in allWater)
        {
            if (Vector3.Distance(position, water.transform.position) <= radius)
                Destroy(water.gameObject);
        }
    }

    void CreateUI()
    {
        uiRoot = new GameObject("DynamicVRUI", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        uiRoot.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = uiRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(500, 350);

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer != -1)
        {
            uiRoot.layer = uiLayer;
            AssignLayerRecursively(uiRoot, uiLayer);
        }

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(uiRoot.transform, false);
        panel.GetComponent<Image>().color = uiStyle != null ? uiStyle.panelBgColor : new Color(0, 0, 0, 0.75f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        modeText = CreateText("ModeText", uiRoot.transform, "Mode: Dig", 24, TextAlignmentOptions.Center);
        modeText.rectTransform.anchorMin = new Vector2(0, 1);
        modeText.rectTransform.anchorMax = new Vector2(1, 1);
        modeText.rectTransform.pivot = new Vector2(0.5f, 1);
        modeText.rectTransform.anchoredPosition = new Vector2(0, -10);
        modeText.rectTransform.sizeDelta = new Vector2(-20, 35);

        digButton = CreateButton("BtnDig", uiRoot.transform, "Dig", new Vector2(20, -60), new Vector2(110, 40));
        raiseButton = CreateButton("BtnRaise", uiRoot.transform, "Raise", new Vector2(20, -115), new Vector2(110, 40));
        paintButton = CreateButton("BtnPaint", uiRoot.transform, "Paint", new Vector2(20, -170), new Vector2(110, 40));
        waterButton = CreateButton("BtnWater", uiRoot.transform, "Water", new Vector2(20, -225), new Vector2(110, 40));

        digButton.onClick.AddListener(() => currentMode = ToolMode.Dig);
        raiseButton.onClick.AddListener(() => currentMode = ToolMode.Raise);
        paintButton.onClick.AddListener(() => currentMode = ToolMode.Paint);
        waterButton.onClick.AddListener(() => currentMode = ToolMode.Water);

        float contentStartX = 150f;
        float contentWidth = 330f;

        strengthGroup = CreateGroupContainer("Group_Strength", contentStartX, contentWidth);
        strengthText = CreateText("StrengthLabel", strengthGroup.transform, "Strength: 0.020", 16, TextAlignmentOptions.Left);
        strengthText.rectTransform.anchorMin = Vector2.up;
        strengthText.rectTransform.anchorMax = Vector2.up;
        strengthText.rectTransform.pivot = Vector2.up;
        strengthText.rectTransform.anchoredPosition = new Vector2(5, 0);
        strengthText.rectTransform.sizeDelta = new Vector2(contentWidth, 20);
        strengthSlider = CreateSlider("StrengthSlider", strengthGroup.transform, new Vector2(0, -25), new Vector2(contentWidth, 18), 0.005f, 0.1f, 0.02f);
        strengthSlider.onValueChanged.AddListener(val => strength = val);

        brushGroup = CreateGroupContainer("Group_Brush", contentStartX, contentWidth);
        brushSizeText = CreateText("BrushLabel", brushGroup.transform, "Brush: 2.5", 16, TextAlignmentOptions.Left);
        brushSizeText.rectTransform.anchorMin = Vector2.up;
        brushSizeText.rectTransform.anchorMax = Vector2.up;
        brushSizeText.rectTransform.pivot = Vector2.up;
        brushSizeText.rectTransform.anchoredPosition = new Vector2(5, 0);
        brushSizeText.rectTransform.sizeDelta = new Vector2(contentWidth, 20);
        brushSizeSlider = CreateSlider("BrushSlider", brushGroup.transform, new Vector2(0, -25), new Vector2(contentWidth, 18), 0.5f, 8.0f, 2.5f);
        brushSizeSlider.onValueChanged.AddListener(val => brushSize = val);

        paintStrengthGroup = CreateGroupContainer("Group_PaintStrength", contentStartX, contentWidth);
        paintStrengthText = CreateText("PaintStrLabel", paintStrengthGroup.transform, "Paint str: 0.50", 16, TextAlignmentOptions.Left);
        paintStrengthText.rectTransform.anchorMin = Vector2.up;
        paintStrengthText.rectTransform.anchorMax = Vector2.up;
        paintStrengthText.rectTransform.pivot = Vector2.up;
        paintStrengthText.rectTransform.anchoredPosition = new Vector2(5, 0);
        paintStrengthText.rectTransform.sizeDelta = new Vector2(contentWidth, 20);
        paintStrengthSlider = CreateSlider("PaintStrSlider", paintStrengthGroup.transform, new Vector2(0, -25), new Vector2(contentWidth, 18), 0.0f, 1.0f, 0.5f);
        paintStrengthSlider.onValueChanged.AddListener(val => paintStrength = val);

        paintLayerGroup = CreateGroupContainer("Group_PaintLayer", contentStartX, contentWidth);
        paintLayerText = CreateText("LayerLabel", paintLayerGroup.transform, "Layer: 0", 16, TextAlignmentOptions.Center);
        paintLayerText.rectTransform.anchorMin = Vector2.up;
        paintLayerText.rectTransform.anchorMax = Vector2.up;
        paintLayerText.rectTransform.pivot = Vector2.up;
        paintLayerText.rectTransform.anchoredPosition = new Vector2(0, 0);
        paintLayerText.rectTransform.sizeDelta = new Vector2(contentWidth, 25);
        prevLayerButton = CreateButton("BtnPrevLayer", paintLayerGroup.transform, "<", new Vector2(10, -5), new Vector2(40, 35));
        nextLayerButton = CreateButton("BtnNextLayer", paintLayerGroup.transform, ">", new Vector2(contentWidth - 50, -5), new Vector2(40, 35));
        prevLayerButton.onClick.AddListener(PrevPaintLayer);
        nextLayerButton.onClick.AddListener(NextPaintLayer);
    }

    GameObject CreateGroupContainer(string name, float startX, float width)
    {
        GameObject group = new GameObject(name, typeof(RectTransform));
        group.transform.SetParent(uiRoot.transform, false);
        RectTransform rect = group.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(startX, 0);
        rect.sizeDelta = new Vector2(width, 50);
        return group;
    }

    void UpdateUI()
    {
        if (!uiActive || uiRoot == null) return;

        if (modeText) modeText.text = $"Mode: {currentMode}";

        bool showTerrain = (currentMode == ToolMode.Dig || currentMode == ToolMode.Raise);
        bool showPaint = (currentMode == ToolMode.Paint);

        if (strengthGroup) strengthGroup.SetActive(showTerrain);
        if (brushGroup) brushGroup.SetActive(showTerrain || showPaint);
        if (paintStrengthGroup) paintStrengthGroup.SetActive(showPaint);
        if (paintLayerGroup) paintLayerGroup.SetActive(showPaint);

        int visibleCount = 0;
        GameObject[] uiGroups = { strengthGroup, brushGroup, paintStrengthGroup, paintLayerGroup };

        foreach (GameObject group in uiGroups)
        {
            if (group != null && group.activeSelf)
            {
                RectTransform rect = group.GetComponent<RectTransform>();
                float currentY = uiStartYOffset - (visibleCount * uiVerticalSpacing);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, currentY);
                visibleCount++;
            }
        }

        if (strengthText && strengthGroup.activeSelf) strengthText.text = $"Strength: {strength:F3}";
        if (brushSizeText && brushGroup.activeSelf) brushSizeText.text = $"Brush: {brushSize:F1}";
        if (paintLayerText && paintLayerGroup.activeSelf) paintLayerText.text = $"Layer: {paintLayer}";
        if (paintStrengthText && paintStrengthGroup.activeSelf) paintStrengthText.text = $"Paint str: {paintStrength:F2}";
    }

    void AssignLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform) AssignLayerRecursively(child.gameObject, layer);
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
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
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

        go.GetComponent<Image>().color = uiStyle != null ? uiStyle.buttonColor : new Color(0.25f, 0.25f, 0.25f, 0.9f);
        Button button = go.GetComponent<Button>();

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(go.transform, false);
        TMP_Text tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = uiStyle != null ? uiStyle.buttonFontSize : 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = uiStyle != null ? uiStyle.buttonTextColor : Color.white;
        tmp.enableWordWrapping = false;

        RectTransform textRect = tmp.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        return button;
    }

    Slider CreateSlider(string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta, float minVal, float maxVal, float defaultVal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        Slider slider = go.GetComponent<Slider>();
        slider.minValue = minVal;
        slider.maxValue = maxVal;
        slider.value = defaultVal;

        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        bg.GetComponent<Image>().color = uiStyle != null ? uiStyle.sliderBgColor : new Color(0.15f, 0.15f, 0.15f, 1f);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = new Vector2(-10, 0);
        fillAreaRect.anchoredPosition = new Vector2(5, 0);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = uiStyle != null ? uiStyle.sliderFillColor : new Color(0.2f, 0.55f, 1f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

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
        handleRect.anchorMin = new Vector2(0, 0.5f);
        handleRect.anchorMax = new Vector2(0, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(14, 24);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.transition = Selectable.Transition.None;

        return slider;
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