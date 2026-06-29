using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralWaterMesh : MonoBehaviour
{
    public int raysCount = 64;
    public float waterSurfaceOffset = 0.02f;

    private Mesh mesh;

    void Awake()
    {
        mesh = new Mesh();
        mesh.name = "WaterPit";
        GetComponent<MeshFilter>().mesh = mesh;
    }

    public void BuildFromBoundary(Vector3 center, Vector3[] boundaryPoints)
    {
        if (boundaryPoints.Length < 3) return;

        float minEdgeHeight = Mathf.Infinity;
        foreach (Vector3 p in boundaryPoints)
            if (p.y < minEdgeHeight) minEdgeHeight = p.y;
        float waterLevel = Mathf.Max(center.y, minEdgeHeight - waterSurfaceOffset);

        // Перемещаем объект в центр
        transform.position = new Vector3(center.x, waterLevel, center.z);
        transform.rotation = Quaternion.identity;

        // Локальные координаты
        Vector3 localCenter = Vector3.zero; // после перемещения объекта центр в нуле
        List<Vector3> localBoundary = new List<Vector3>();
        foreach (Vector3 p in boundaryPoints)
        {
            Vector3 worldPt = new Vector3(p.x, waterLevel, p.z);
            localBoundary.Add(transform.InverseTransformPoint(worldPt));
        }

        // Вершины
        Vector3[] vertices = new Vector3[1 + localBoundary.Count];
        vertices[0] = localCenter;
        for (int i = 0; i < localBoundary.Count; i++)
            vertices[1 + i] = localBoundary[i];

        // Треугольники веером
        int[] tris = new int[localBoundary.Count * 3];
        for (int i = 0; i < localBoundary.Count; i++)
        {
            int next = (i + 1) % localBoundary.Count;
            tris[i * 3] = 0;
            tris[i * 3 + 1] = 1 + i;
            tris[i * 3 + 2] = 1 + next;
        }

        // UV и вертексные цвета для мягких краёв
        Bounds bounds = new Bounds(vertices[0], Vector3.zero);
        for (int i = 1; i < vertices.Length; i++)
            bounds.Encapsulate(vertices[i]);
        float maxExt = Mathf.Max(bounds.size.x, bounds.size.z);
        float scale = maxExt > 0 ? 1f / maxExt : 1f;

        Vector2[] uv = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        uv[0] = new Vector2(0.5f, 0.5f);
        colors[0] = Color.white;

        for (int i = 1; i < vertices.Length; i++)
        {
            Vector3 local = vertices[i] - bounds.center;
            uv[i] = new Vector2(0.5f + local.x * scale, 0.5f + local.z * scale);
            float dist = Vector3.Distance(vertices[i], Vector3.zero);
            float alpha = Mathf.InverseLerp(0, maxExt * 0.7f, dist);
            colors[i] = new Color(1, 1, 1, alpha);
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.uv = uv;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}