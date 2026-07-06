using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralWaterMesh : MonoBehaviour
{
    public Material waterMaterial;
    public float uvScale = 1f;   // масштаб UV для тайлинга текстуры

    public void BuildFromBoundary(Vector3 center, Vector3[] boundaryPoints, float waterLevel)
    {
        Mesh waterMesh = CreateFlatDisk(center, boundaryPoints);
        GetComponent<MeshFilter>().sharedMesh = waterMesh;

        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider == null) collider = gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = waterMesh;
        collider.convex = true;
        collider.isTrigger = true;

        if (waterMaterial != null)
            GetComponent<MeshRenderer>().material = waterMaterial;
    }

    private Mesh CreateFlatDisk(Vector3 center, Vector3[] boundaryPoints)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Water_Surface_Mesh";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Центр (локальный 0)
        vertices.Add(Vector3.zero);

        // Граничные точки в локальных координатах
        List<Vector3> localBoundary = new List<Vector3>();
        for (int i = 0; i < boundaryPoints.Length; i++)
        {
            Vector3 localPos = boundaryPoints[i] - center;
            localPos.y = 0f;
            localBoundary.Add(localPos);
            vertices.Add(localPos);
        }

        // Вычисляем bounding box для UV
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Vector3 p in localBoundary)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.z < minZ) minZ = p.z;
            if (p.z > maxZ) maxZ = p.z;
        }
        float rangeX = maxX - minX;
        float rangeZ = maxZ - minZ;
        if (rangeX < 0.001f) rangeX = 1f;
        if (rangeZ < 0.001f) rangeZ = 1f;

        // UV для центра (потом усредним)
        uvs.Add(Vector2.zero);

        // UV для границ (нормализуем в 0–1)
        for (int i = 0; i < localBoundary.Count; i++)
        {
            float u = (localBoundary[i].x - minX) / rangeX * uvScale;
            float v = (localBoundary[i].z - minZ) / rangeZ * uvScale;
            uvs.Add(new Vector2(u, v));
        }

        // UV центра = среднее всех граничных UV
        Vector2 centerUV = Vector2.zero;
        for (int i = 1; i < uvs.Count; i++)
            centerUV += uvs[i];
        centerUV /= (uvs.Count - 1);
        uvs[0] = centerUV;

        // Треугольники: чтобы лицевая сторона смотрела ВВЕРХ,
        // используем порядок (0, next, i) (вместо 0, i, next)
        int count = localBoundary.Count;
        for (int i = 1; i <= count; i++)
        {
            int next = (i == count) ? 1 : i + 1;
            triangles.Add(0);
            triangles.Add(next);   // инвертировано
            triangles.Add(i);
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();

        // Явные нормали вверх
        Vector3[] normals = new Vector3[vertices.Count];
        for (int i = 0; i < normals.Length; i++)
            normals[i] = Vector3.up;
        mesh.normals = normals;

        mesh.RecalculateBounds();
        return mesh;
    }
}