using UnityEngine;

public static class TerrainDeformer
{
    public static void Deform(Terrain terrain, Vector3 worldPos, float strength, float brushSize)
    {
        if (terrain == null) return;
        TerrainData data = terrain.terrainData;

        Vector3 terrainLocalPos = worldPos - terrain.transform.position;
        float normX = terrainLocalPos.x / data.size.x;
        float normZ = terrainLocalPos.z / data.size.z;

        int heightmapRes = data.heightmapResolution;
        int centerX = Mathf.RoundToInt(normX * (heightmapRes - 1));
        int centerY = Mathf.RoundToInt(normZ * (heightmapRes - 1));

        int brushPixelRadius = Mathf.CeilToInt(brushSize / data.size.x * heightmapRes);
        int radius = Mathf.Clamp(brushPixelRadius, 1, 50);

        int xStart = Mathf.Max(0, centerX - radius);
        int xEnd = Mathf.Min(heightmapRes - 1, centerX + radius);
        int yStart = Mathf.Max(0, centerY - radius);
        int yEnd = Mathf.Min(heightmapRes - 1, centerY + radius);

        int width = xEnd - xStart + 1;
        int height = yEnd - yStart + 1;
        if (width <= 0 || height <= 0) return;

        float[,] heights = data.GetHeights(xStart, yStart, width, height);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int worldX = xStart + x;
                int worldY = yStart + y;
                float dist = Vector2.Distance(new Vector2(worldX, worldY), new Vector2(centerX, centerY));
                float influence = Mathf.Clamp01(1f - dist / radius);
                heights[y, x] += strength * influence;
            }
        }

        data.SetHeights(xStart, yStart, heights);
        data.SyncHeightmap(); // Обновление коллизии
    }

    public static void PaintTexture(Terrain terrain, Vector3 worldPos, float brushSize, float strength, int layerIndex)
    {
        // Заглушка, можешь реализовать позже
        Debug.LogWarning("PaintTexture is not implemented yet.");
    }
}