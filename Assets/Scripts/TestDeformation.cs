using UnityEngine;
using UnityEngine.InputSystem;

public class TestDeformation : MonoBehaviour
{
    public Terrain terrain;
    public float strength = -0.1f;
    public float brushSize = 2f;

    private float[,] originalHeights;
    private TerrainData terrainData;
    private bool isMousePressed;

    void Start()
    {
        if (terrain == null)
            terrain = Terrain.activeTerrain; // если забыли перетащить, возьмём активный
        terrainData = terrain.terrainData;
        // Сохраняем исходные высоты при старте
        originalHeights = terrainData.GetHeights(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);
    }

    void Update()
    {
        isMousePressed = Mouse.current.leftButton.isPressed;

        if (isMousePressed)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0));
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == terrain.gameObject)
                {
                    TerrainDeformer.ModifyHeight(terrain, hit.point, strength, brushSize);
                }
            }
        }
    }

    void OnDestroy()
    {
        // Восстанавливаем рельеф при остановке игры
        if (terrainData != null && originalHeights != null)
            terrainData.SetHeights(0, 0, originalHeights);
    }
}