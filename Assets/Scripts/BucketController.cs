using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;

public class BucketController : MonoBehaviour
{
    public Transform pourPoint;
    public ProceduralWaterMesh waterPatchPrefab;

    public float maxRadius = 5f;
    public float step = 0.2f;
    public float depthThreshold = 0.03f;
    public int boundaryRays = 64;
    public float cleanupRadius = 6f;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    private bool isHeld = false;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnGrab);
            interactable.selectExited.AddListener(OnRelease);
            interactable.activated.AddListener(OnActivate);
        }
        if (pourPoint == null) Debug.LogError("PourPoint missing");
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

        Vector3[] boundary = GetPitBoundary(center, terrain);
        if (boundary.Length < 3) return;

        // Очистка старых луж вокруг центра
        RemoveOldWaterPatches(center, cleanupRadius);

        ProceduralWaterMesh water = Instantiate(waterPatchPrefab, center, Quaternion.identity);
        water.BuildFromBoundary(center, boundary);
    }

    private Vector3[] GetPitBoundary(Vector3 center, Terrain terrain)
    {
        float centerHeight = terrain.SampleHeight(center);
        List<Vector3> boundary = new List<Vector3>();

        for (int i = 0; i < boundaryRays; i++)
        {
            float angle = i * Mathf.PI * 2f / boundaryRays;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            float dist = FindEdgeDistance(center, dir, centerHeight, terrain);
            Vector3 edgePt = center + dir * dist;
            boundary.Add(new Vector3(edgePt.x, terrain.SampleHeight(edgePt), edgePt.z));
        }
        return boundary.ToArray();
    }

    private float FindEdgeDistance(Vector3 center, Vector3 dir, float centerHeight, Terrain terrain)
    {
        float low = 0f;
        float high = maxRadius;
        const float precision = 0.01f;

        // Проверяем, есть ли граница в пределах maxRadius
        float highHeight = terrain.SampleHeight(center + dir * high);
        if (highHeight <= centerHeight + depthThreshold)
            return high; // яма больше maxRadius

        while (high - low > precision)
        {
            float mid = (low + high) * 0.5f;
            float h = terrain.SampleHeight(center + dir * mid);
            if (h > centerHeight + depthThreshold)
                high = mid;
            else
                low = mid;
        }
        float result = (low + high) * 0.5f;
        return Mathf.Max(0, result - 0.05f); // отступ внутрь
    }

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