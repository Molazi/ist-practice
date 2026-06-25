using UnityEngine;

public static class TerrainDeformer
{
    /// <summary>
    /// Ћокально измен€ет высоты террейна в указанной точке
    /// </summary>
    /// <param name="terrain">“еррейн</param>
    /// <param name="worldPos">ћирова€ позици€ центра кисти</param>
    /// <param name="strength">—ила изменени€ (положительна€ Ч подн€ть, отрицательна€ Ч опустить)</param>
    /// <param name="brushSize">–азмер кисти в мировых единицах (метрах)</param>
    public static void ModifyHeight(Terrain terrain, Vector3 worldPos, float strength, float brushSize = 2f)
    {
        TerrainData data = terrain.terrainData;

        // ѕереводим мировые координаты в локальные координаты террейна (от 0 до 1)
        Vector3 terrainLocalPos = worldPos - terrain.transform.position;
        float normX = terrainLocalPos.x / data.size.x;
        float normZ = terrainLocalPos.z / data.size.z;

        // ѕереводим нормализованные координаты в индексы на карте высот
        int heightmapWidth = data.heightmapResolution;
        int heightmapHeight = data.heightmapResolution;
        int centerX = Mathf.RoundToInt(normX * (heightmapWidth - 1));
        int centerY = Mathf.RoundToInt(normZ * (heightmapHeight - 1));

        // –азмер кисти в пиксел€х карты высот (грубо)
        int brushPixelRadius = Mathf.CeilToInt(brushSize / data.size.x * heightmapWidth);
        int radius = Mathf.Clamp(brushPixelRadius, 1, 50); // ограничим разумным размером

        // ќпредел€ем область дл€ изменени€ (с учЄтом границ)
        int xStart = Mathf.Max(0, centerX - radius);
        int xEnd = Mathf.Min(heightmapWidth - 1, centerX + radius);
        int yStart = Mathf.Max(0, centerY - radius);
        int yEnd = Mathf.Min(heightmapHeight - 1, centerY + radius);

        int width = xEnd - xStart + 1;
        int height = yEnd - yStart + 1;
        if (width <= 0 || height <= 0) return;

        // ѕолучаем текущие высоты в этой области
        float[,] heights = data.GetHeights(xStart, yStart, width, height);

        // »змен€ем высоты с гауссовым сглаживанием (проста€ верси€)
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int worldX = xStart + x;
                int worldY = yStart + y;
                float dist = Vector2.Distance(new Vector2(worldX, worldY), new Vector2(centerX, centerY));
                float influence = Mathf.Clamp01(1f - dist / radius); // линейное затухание
                                                                     // ƒл€ гауссова варианта можно заменить на Mathf.Exp(-(dist*dist)/(2*sigma*sigma))

                heights[y, x] += strength * influence * Time.deltaTime * 10f; // плавно, зависит от времени кадра
            }
        }

        // ѕримен€ем обратно
        data.SetHeights(xStart, yStart, heights);
    }
}