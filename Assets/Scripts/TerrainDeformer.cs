using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class TerrainDeformer : MonoBehaviour
{
    private Terrain terrain;
    private TerrainData terrainData;
    private int heightmapWidth, heightmapHeight;
    private TerrainData originalData;

    void Awake()
    {
        terrain = GetComponent<Terrain>();
        originalData = terrain.terrainData;

        // Клонируем, чтобы изменения не сохранялись после остановки
        terrain.terrainData = Instantiate(originalData);
        terrainData = terrain.terrainData;
        heightmapWidth = terrainData.heightmapResolution;
        heightmapHeight = terrainData.heightmapResolution;
    }

    void OnDestroy()
    {
        if (terrain != null && originalData != null)
            terrain.terrainData = originalData;
    }

    /// <summary>Деформация высот (копание / насыпание).</summary>
    public void Deform(Vector3 point, float strength, float radius)
    {
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
    }

    /// <summary>Рисование текстур (слой с индексом layerIndex).</summary>
    public void PaintTexture(Vector3 worldPos, float radius, float strength, int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= terrainData.alphamapLayers) return;

        Vector3 terrainPos = terrain.transform.position;
        float normX = (worldPos.x - terrainPos.x) / terrainData.size.x;
        float normZ = (worldPos.z - terrainPos.z) / terrainData.size.z;
        if (normX < 0 || normX > 1 || normZ < 0 || normZ > 1) return;

        int alphaWidth = terrainData.alphamapWidth;
        int alphaHeight = terrainData.alphamapHeight;

        int centerX = Mathf.RoundToInt(normX * (alphaWidth - 1));
        int centerZ = Mathf.RoundToInt(normZ * (alphaHeight - 1));
        int radiusPixels = Mathf.RoundToInt(radius / terrainData.size.x * alphaWidth);

        int xMin = Mathf.Max(centerX - radiusPixels, 0);
        int xMax = Mathf.Min(centerX + radiusPixels, alphaWidth - 1);
        int zMin = Mathf.Max(centerZ - radiusPixels, 0);
        int zMax = Mathf.Min(centerZ + radiusPixels, alphaHeight - 1);

        int width = xMax - xMin + 1;
        int depth = zMax - zMin + 1;

        float[,,] alphamaps = terrainData.GetAlphamaps(xMin, zMin, width, depth);

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
                    float apply = strength * falloff * Time.deltaTime;

                    float current = alphamaps[z, x, layerIndex];
                    float newWeight = Mathf.Clamp01(current + apply);
                    float delta = newWeight - current;
                    if (delta <= 0f) continue;

                    alphamaps[z, x, layerIndex] = newWeight;

                    // Уменьшаем остальные слои пропорционально
                    float totalOthers = 0f;
                    for (int l = 0; l < terrainData.alphamapLayers; l++)
                        if (l != layerIndex) totalOthers += alphamaps[z, x, l];

                    if (totalOthers > 0f)
                    {
                        for (int l = 0; l < terrainData.alphamapLayers; l++)
                        {
                            if (l == layerIndex) continue;
                            alphamaps[z, x, l] -= delta * (alphamaps[z, x, l] / totalOthers);
                            alphamaps[z, x, l] = Mathf.Max(0f, alphamaps[z, x, l]);
                        }
                    }

                    // Нормализация
                    float sum = 0f;
                    for (int l = 0; l < terrainData.alphamapLayers; l++) sum += alphamaps[z, x, l];
                    if (sum > 0f)
                        for (int l = 0; l < terrainData.alphamapLayers; l++)
                            alphamaps[z, x, l] /= sum;
                }
            }
        }
        terrainData.SetAlphamaps(xMin, zMin, alphamaps);
    }
}