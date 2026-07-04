using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralWaterMesh : MonoBehaviour
{
    public void BuildFromBoundary(Vector3 center, Vector3[] boundaryPoints, float waterLevel)
    {
        // 1. Генерируем идеально плоский диск воды по точкам краев ямы
        Mesh waterMesh = CreateFlatDisk(center, boundaryPoints);
        GetComponent<MeshFilter>().sharedMesh = waterMesh;

        // 2. Автоматически добавляем коллайдер-триггер, повторяющий форму воды
        // Это пригодится, когда будешь опускать ведро ( OnTriggerEnter )
        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider == null) collider = gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = waterMesh;
        collider.convex = true;
        collider.isTrigger = true;
    }

    private Mesh CreateFlatDisk(Vector3 center, Vector3[] boundaryPoints)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Water_Surface_Mesh";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Локальный центр объекта (0, 0, 0)
        vertices.Add(Vector3.zero);

        // Заполняем вершины вокруг центра
        for (int i = 0; i < boundaryPoints.Length; i++)
        {
            // Переводим мировые точки границ в локальные координаты относительно центра воды
            Vector3 localPos = boundaryPoints[i] - center;
            localPos.y = 0f; // Гарантируем, что диск абсолютно плоский по вертикали
            vertices.Add(localPos);
        }

        // Правильный обход вершин по часовой стрелке, чтобы нормали смотрели строго ВВЕРХ
        int count = boundaryPoints.Length;
        for (int i = 1; i <= count; i++)
        {
            int next = (i == count) ? 1 : i + 1;
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(next);
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

        // Базовые UV, чтобы стандартные текстуры воды не растягивались
        Vector2[] uvs = new Vector2[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
        }
        mesh.uv = uvs;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}