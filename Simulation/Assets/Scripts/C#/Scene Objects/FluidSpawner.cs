using System.Collections.Generic;
using System.Linq;
using Resources2;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class FluidSpawner : Polygon
{
    // Serialized fields
    public bool DoDrawBody = true;
    [Range(50.0f, 2000.0f)] public float msSpawnInterval;
    [SerializeField] private int pTypeIndex;
    [SerializeField] private Vector2 velocity;
    [SerializeField] private float tempCelcius;
    [SerializeField] private float spawnDensity;
    [SerializeField] private int maxSpawnedParticlesPerUpdate;

    // References
    private SceneManager sceneManager;
    private PTypeInput pTypeInput;
    private Main main;

    // Other
    private List<Vector2> generatedPoints;

    private void OnValidate() => generatedPoints = null;

    #if UNITY_EDITOR
        public override void OnEditorUpdate() { } // Not used
    #endif

    public PData[] GenerateParticles()
    {
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        if (pTypeInput == null) pTypeInput = GameObject.FindGameObjectWithTag("PTypeInput").GetComponent<PTypeInput>();

        if (generatedPoints == null)
        {
            SetPolygonData();
            generatedPoints = GeneratePoints();
        }

        // Check if pTypeIndex is within range of all pTypes
        if (pTypeIndex >= pTypeInput.particleTypeStates.Length * 3) Debug.LogError("pTypeIndex outside valid range. FluidSpawner: " + this.name);

        PData[] pDatas = new PData[generatedPoints.Count];
        for (int i = 0; i < pDatas.Length; i++)
        {
            pDatas[i] = InitPData(generatedPoints[i]);
        }

        return pDatas;
    }

    private List<Vector2> GeneratePoints()
    {
        if (sceneManager == null) sceneManager = GameObject.Find("SceneManager").GetComponent<SceneManager>();

        List<Vector2> generatedPoints = new();

        // Find the bounding box of the polygon
        Vector2 min = Func.MinVector2(Edges.Select(edge => Func.MinVector2(edge.start, edge.end)).ToArray());
        Vector2 max = Func.MaxVector2(Edges.Select(edge => Func.MaxVector2(edge.start, edge.end)).ToArray());

        // Generate grid points within the bounding box
        int iterationCount = 0;
        for (float x = min.x; x <= max.x; x += spawnDensity)
        {
            for (float y = min.y; y <= max.y; y += spawnDensity)
            {
                Vector2 point = new(x, y);

                if (IsPointInsidePolygon(point) && sceneManager.IsPointInsideBounds(point))
                {
                    if (++iterationCount > maxSpawnedParticlesPerUpdate) return generatedPoints;

                    generatedPoints.Add(point);
                }
            }
        }

        return generatedPoints;
    }

    private PData InitPData(Vector2 pos)
    {
        return new PData
        {
            predPos = new float2(0.0f, 0.0f),
            pos = pos,
            vel = velocity,
            lastVel = new float2(0.0f, 0.0f),
            density = 0.0f,
            nearDensity = 0.0f,
            temperature = Utils.CelsiusToKelvin(tempCelcius),
            temperatureExchangeBuffer = 0.0f,
            lastChunkKey_PType_POrder = pTypeIndex * main.ChunksNumAll // flattened equivelant to PType = 1
        };
    }
}