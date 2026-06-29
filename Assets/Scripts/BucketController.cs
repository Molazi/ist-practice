using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;

public class BucketController : MonoBehaviour
{
    [Header("Components")]
    public Transform pourPoint;               // точка у края ведра (откуда льём)
    public ProceduralWaterMesh waterPatchPrefab; // префаб воды (с ProceduralWaterMesh)

    [Header("Detection Settings")]
    public float maxRadius = 5f;              // максимальный радиус поиска ямы
    public float depthThreshold = 0.05f;      // порог разницы высот (край vs уровень воды)
    public int boundaryRays = 64;             // количество лучей для определения контура
    public float cleanupRadius = 6f;          // радиус очистки старых луж

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    private bool isHeld = false;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        if (interactable == null)
        {
            Debug.LogError("BucketController requires an XRBaseInteractable (e.g., XR Grab Interactable).");
            return;
        }

        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
        interactable.activated.AddListener(OnActivate);

        if (pourPoint == null)
            Debug.LogError("Pour Point not assigned! Create an empty child at the bucket rim.");
        if (waterPatchPrefab == null)
            Debug.LogWarning("WaterPatch prefab not assigned.");
    }

    void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
            interactable.activated.RemoveListener(OnActivate);
        }
    }

    private void OnGrab(SelectEnterEventArgs args) => isHeld = true;
    private void OnRelease(SelectExitEventArgs args) => isHeld = false;

    private void OnActivate(ActivateEventArgs args)
    {
        if (isHeld) PourWater();
    }

    private void PourWater()
    {
        if (pourPoint == null || waterPatchPrefab == null) return;

        Vector3 center = pourPoint.position;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        // Получаем границу ямы (контур по уровню воды)
        Vector3[] boundary = GetPitBoundary(center, terrain);
        if (boundary.Length < 3)
        {
            Debug.Log("No suitable depression found – water not poured.");
            return;
        }

        // Все точки границы имеют одинаковую Y-координату (уровень воды)
        float waterLevel = boundary[0].y;

        // Удаляем старые лужи вокруг центра
        RemoveOldWaterPatches(center, cleanupRadius);

        // Создаём новый экземпляр воды
        ProceduralWaterMesh water = Instantiate(waterPatchPrefab, center, Quaternion.identity);
        water.BuildFromBoundary(center, boundary, waterLevel);
        Debug.Log($"Water poured with {boundary.Length} points, level={waterLevel:F2}");
    }

    // Вычисляет минимальную высоту в заданном радиусе от центра
    private float GetMinHeightInRadius(Vector3 center, float radius, Terrain terrain)
    {
        float min = Mathf.Infinity;
        int steps = 10;          // количество окружностей
        int pointsPerCircle = 16; // точек на каждой окружности
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
        return min;
    }

    // Возвращает массив точек границы вокруг центра на уровне waterLevel
    private Vector3[] GetPitBoundary(Vector3 center, Terrain terrain)
    {
        float minHeight = GetMinHeightInRadius(center, maxRadius, terrain);
        float centerHeight = terrain.SampleHeight(center);

        // Если дно не ниже центра хотя бы на 5 см – ямы нет
        if (centerHeight - minHeight < 0.05f)
            return new Vector3[0];

        // Уровень воды – чуть выше дна
        float waterLevel = minHeight + 0.02f;

        // Собираем контур по уровню воды
        List<Vector3> boundary = new List<Vector3>();
        for (int i = 0; i < boundaryRays; i++)
        {
            float angle = i * Mathf.PI * 2f / boundaryRays;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            float dist = FindEdgeDistanceToWaterLevel(center, dir, waterLevel, terrain);
            Vector3 edgePoint = center + dir * dist;
            // Точка на уровне воды (плоская поверхность)
            boundary.Add(new Vector3(edgePoint.x, waterLevel, edgePoint.z));
        }
        return boundary.ToArray();
    }

    // Находит расстояние от центра вдоль dir, где высота превышает waterLevel + порог
    private float FindEdgeDistanceToWaterLevel(Vector3 center, Vector3 dir, float waterLevel, Terrain terrain)
    {
        float low = 0f;
        float high = maxRadius;
        const float precision = 0.01f;

        // Проверяем, есть ли подъём выше waterLevel + порог в пределах maxRadius
        float highHeight = terrain.SampleHeight(center + dir * high);
        if (highHeight <= waterLevel + depthThreshold)
            return high; // нет края – возвращаем максимальный радиус

        // Бинарный поиск границы
        while (high - low > precision)
        {
            float mid = (low + high) * 0.5f;
            float h = terrain.SampleHeight(center + dir * mid);
            if (h > waterLevel + depthThreshold)
                high = mid; // граница левее
            else
                low = mid;  // граница дальше
        }
        return (low + high) * 0.5f;
    }

    // Удаляет все старые лужи в заданном радиусе
    private void RemoveOldWaterPatches(Vector3 point, float radius)
    {
        ProceduralWaterMesh[] allWaters = Object.FindObjectsByType<ProceduralWaterMesh>(FindObjectsSortMode.None);
        foreach (ProceduralWaterMesh w in allWaters)
        {
            if (Vector3.Distance(w.transform.position, point) <= radius)
                Destroy(w.gameObject);
        }
    }
}