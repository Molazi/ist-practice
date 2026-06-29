using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class TerrainDeformer : MonoBehaviour
{
    [Header("Collision Mesh Settings")]
    [Tooltip("Разрешение сетки коллизии. 64 — хороший баланс точности и производительности.")]
    public int collisionGridResolution = 64;

    private Terrain terrain;
    private TerrainData terrainData;
    private int heightmapWidth, heightmapHeight;
    private TerrainData originalData;

    private MeshCollider meshCollider;
    private Mesh collisionMesh;

    void Awake()
    {
        terrain = GetComponent<Terrain>();
        originalData = terrain.terrainData;

        // Клонируем для игры
        terrain.terrainData = Instantiate(originalData);
        terrainData = terrain.terrainData;
        heightmapWidth = terrainData.heightmapResolution;
        heightmapHeight = terrainData.heightmapResolution;

        // Отключаем встроенный коллайдер
        TerrainCollider tc = GetComponent<TerrainCollider>();
        if (tc != null) tc.enabled = false;

        // Создаём MeshCollider
        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();

        collisionMesh = new Mesh();
        collisionMesh.name = "TerrainCollision";
        meshCollider.sharedMesh = collisionMesh;

        BuildCollisionMesh();
    }

    void OnDestroy()
    {
        // Возвращаем оригинальный ассет, чтобы редактор не падал
        if (terrain != null && originalData != null)
            terrain.terrainData = originalData;

        // Включаем обратно TerrainCollider и удаляем наш меш
        TerrainCollider tc = terrain?.GetComponent<TerrainCollider>();
        if (tc != null) tc.enabled = true;

        if (meshCollider != null)
            Destroy(meshCollider);

        if (collisionMesh != null)
            Destroy(collisionMesh);
    }

    public void Deform(Vector3 point, float strength, float radius)
    {
        // -------- Изменение высот (без изменений) --------
        Vector3 terrainPos = terrain.transform.position;
        float normX = (point.x - terrainPos.x) / terrainData.size.x;
        float normZ = (point.z - terrainPos.z) / terrainData.size.z;
        if (normX < 0 || normX > 1 || normZ < 0 || normZ > 1) return;

        int centerX = Mathf.RoundToInt(normX * (heightmapWidth - 1));
        int centerZ = Mathf.RoundToInt(normZ * (heightmapHeight - 1));
        int radiusPixels = Mathf.RoundToInt(radius / terrainData.size.x * heightmapWidth);

        int xMin = Mathf.Max(centerX - radiusPixels, 0);
        int xMax = Mathf.Min(centerX + radiusPixels, heightmapWidth - 1);
        int zMin = Mathf.Max(centerZ - radiusPixels, 0);
        int zMax = Mathf.Min(centerZ + radiusPixels, heightmapHeight - 1);

        int width = xMax - xMin + 1;
        int depth = zMax - zMin + 1;

        float[,] heights = terrainData.GetHeights(xMin, zMin, width, depth);
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int mapX = xMin + x;
                int mapZ = zMin + z;
                float dist = Vector2.Distance(new Vector2(mapX, mapZ), new Vector2(centerX, centerZ)) / radiusPixels;
                if (dist <= 1f)
                {
                    float falloff = Mathf.SmoothStep(1f, 0f, dist);
                    heights[z, x] += strength * falloff;
                    heights[z, x] = Mathf.Clamp01(heights[z, x]);
                }
            }
        }
        terrainData.SetHeights(xMin, zMin, heights);

        // Перестраиваем коллизию
        BuildCollisionMesh();
        Physics.SyncTransforms();
    }

    private void BuildCollisionMesh()
    {
        Vector3 size = terrainData.size;
        int gridRes = collisionGridResolution;

        // Шаг в мировых единицах между вершинами сетки
        float stepX = size.x / (gridRes - 1);
        float stepZ = size.z / (gridRes - 1);

        Vector3[] vertices = new Vector3[gridRes * gridRes];
        Vector2[] uv = new Vector2[vertices.Length]; // не обязательны, но нужны для меша

        // Позиция террайна в мире (для перевода в локальные координаты)
        Vector3 terrainWorldPos = terrain.transform.position;

        for (int z = 0; z < gridRes; z++)
        {
            for (int x = 0; x < gridRes; x++)
            {
                // Нормализованные координаты (0..1) внутри террайна
                float normX = (float)x / (gridRes - 1);
                float normZ = (float)z / (gridRes - 1);

                // Мировые координаты точки (без высоты)
                float worldX = terrainWorldPos.x + normX * size.x;
                float worldZ = terrainWorldPos.z + normZ * size.z;

                // Получаем высоту из TerrainData (возвращает высоту в мире)
                float worldY = terrain.SampleHeight(new Vector3(worldX, 0, worldZ));

                // Переводим в локальное пространство террайна (для MeshCollider это обязательно)
                Vector3 localPos = new Vector3(worldX, worldY, worldZ) - terrainWorldPos;
                vertices[z * gridRes + x] = localPos;
                uv[z * gridRes + x] = new Vector2(normX, normZ);
            }
        }

        // Треугольники
        int[] triangles = new int[(gridRes - 1) * (gridRes - 1) * 6];
        int triIndex = 0;
        for (int z = 0; z < gridRes - 1; z++)
        {
            for (int x = 0; x < gridRes - 1; x++)
            {
                int i = z * gridRes + x;
                int iRight = i + 1;
                int iDown = i + gridRes;
                int iDownRight = iDown + 1;

                triangles[triIndex++] = i;
                triangles[triIndex++] = iDown;
                triangles[triIndex++] = iRight;

                triangles[triIndex++] = iRight;
                triangles[triIndex++] = iDown;
                triangles[triIndex++] = iDownRight;
            }
        }

        collisionMesh.Clear();
        collisionMesh.vertices = vertices;
        collisionMesh.triangles = triangles;
        collisionMesh.uv = uv;
        collisionMesh.RecalculateNormals();
        collisionMesh.RecalculateBounds();

        meshCollider.sharedMesh = collisionMesh;
    }
}