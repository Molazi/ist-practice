using UnityEngine;

public static class TerrainDeformer
{
    public static void Deform(Terrain terrain, Vector3 worldPos, float strength, float brushSize)
    {
        TerrainData tData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        int res = tData.heightmapResolution;

        int mapX = Mathf.RoundToInt(((worldPos.x - terrainPos.x) / tData.size.x) * res);
        int mapZ = Mathf.RoundToInt(((worldPos.z - terrainPos.z) / tData.size.z) * res);
        int radius = Mathf.RoundToInt((brushSize / tData.size.x) * res);

        int startX = Mathf.Max(0, mapX - radius);
        int startZ = Mathf.Max(0, mapZ - radius);
        int width = Mathf.Min(res - startX, radius * 2);
        int height = Mathf.Min(res - startZ, radius * 2);

        if (width <= 0 || height <= 0) return;

        float[,] heights = tData.GetHeights(startX, startZ, width, height);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float dist = Vector2.Distance(new Vector2(mapX, mapZ), new Vector2(startX + x, startZ + z));
                if (dist <= radius)
                {
                    float t = Mathf.Clamp01(1f - (dist / radius));
                    float smooth = t * t * (3f - 2f * t);
                    heights[z, x] += strength * smooth * 0.1f;
                }
            }
        }
        tData.SetHeights(startX, startZ, heights);
    }

    public static void PaintTexture(Terrain terrain, Vector3 worldPos, float brushSize, float strength, int layerIndex)
    {
        TerrainData tData = terrain.terrainData;
        int alphaW = tData.alphamapWidth;
        int alphaH = tData.alphamapHeight;
        int layerCount = tData.alphamapLayers;

        Vector3 terrainPos = terrain.transform.position;
        int mapX = Mathf.RoundToInt(((worldPos.x - terrainPos.x) / tData.size.x) * alphaW);
        int mapZ = Mathf.RoundToInt(((worldPos.z - terrainPos.z) / tData.size.z) * alphaH);
        int brushRadius = Mathf.RoundToInt((brushSize / tData.size.x) * alphaW);

        int startX = Mathf.Max(0, mapX - brushRadius);
        int startZ = Mathf.Max(0, mapZ - brushRadius);
        int width = Mathf.Min(alphaW - startX, brushRadius * 2);
        int height = Mathf.Min(alphaH - startZ, brushRadius * 2);

        if (width <= 0 || height <= 0) return;

        float[,,] map = tData.GetAlphamaps(startX, startZ, width, height);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float dist = Vector2.Distance(new Vector2(mapX, mapZ), new Vector2(startX + x, startZ + z));
                if (dist <= brushRadius)
                {
                    // Вычисляем плавную силу затухания кисти к краям
                    float alpha = Mathf.Clamp01(1f - (dist / brushRadius)) * strength;

                    if (alpha > 0f)
                    {
                        float currentTargetWeight = map[z, x, layerIndex];
                        float newTargetWeight = Mathf.Clamp01(currentTargetWeight + alpha);
                        float delta = newTargetWeight - currentTargetWeight;

                        // Считаем сумму всех ОСТАЛЬНЫХ слоев в этой точке
                        float sumOthers = 0f;
                        for (int i = 0; i < layerCount; i++)
                        {
                            if (i != layerIndex) sumOthers += map[z, x, i];
                        }

                        // Пропорционально уменьшаем остальные слои, чтобы убрать серый налет
                        if (sumOthers > 0f)
                        {
                            for (int i = 0; i < layerCount; i++)
                            {
                                if (i != layerIndex)
                                {
                                    map[z, x, i] -= delta * (map[z, x, i] / sumOthers);
                                    map[z, x, i] = Mathf.Clamp01(map[z, x, i]);
                                }
                            }
                        }
                        else
                        {
                            // Если красим по пустому месту, просто зануляем другие слои
                            for (int i = 0; i < layerCount; i++)
                            {
                                if (i != layerIndex) map[z, x, i] = 0f;
                            }
                        }

                        // Применяем новый вес для целевой текстуры
                        map[z, x, layerIndex] = newTargetWeight;
                    }
                }
            }
        }
        tData.SetAlphamaps(startX, startZ, map);
    }
}