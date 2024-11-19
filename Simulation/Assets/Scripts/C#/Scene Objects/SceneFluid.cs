using UnityEngine;
using System.Collections.Generic;
using System;
using Resources2;
using System.Linq;
using UnityEditor;
using Unity.Mathematics;
using PM = ProgramManager;

[RequireComponent(typeof(PolygonCollider2D))]
public class SceneFluid : Polygon
{
    public EditorRenderMethod editorRenderMethod;
    public int MaxGizmosIterations = 20000;
    [Range(0.1f, 10.0f)] public float editorGridSpacing = 0.5f;
    [Range(0.05f, 2.0f)] public float editorPointRadius = 0.05f;

    [Header("Simulation Object Settings")]
    [Range(0.1f, 2.0f)] public float defaultGridSpacing = 2.0f;
    [SerializeField] private float particleTemperatureCelcius = 20.0f;
    [SerializeField] private int pTypeIndex = 0;

    [Header("Preview Values")]
    [NonSerialized] public Vector2[] Points;
    private SceneManager sceneManager;
    private PTypeInput pTypeInput;
    private Main main;

    private void OnValidate() => PM.Instance.doOnSettingsChanged = true;
    
    public PData[] GenerateParticles(Vector2 pointOffset, float gridSpacing = 0)
    {
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        if (pTypeInput == null) pTypeInput = GameObject.FindGameObjectWithTag("PTypeInput").GetComponent<PTypeInput>();

        SetPolygonData();
        List<Vector2> generatedPoints = GeneratePoints(gridSpacing);

        // Check if pTypeIndex is within range of all pTypes
        if (pTypeIndex >= pTypeInput.particleTypeStates.Length * 3) Debug.LogError("pTypeIndex outside valid range. SceneFluid: " + this.name);

        PData[] pDatas = new PData[generatedPoints.Count];
        for (int i = 0; i < pDatas.Length; i++)
        {
            pDatas[i] = InitPData(generatedPoints[i] + pointOffset, particleTemperatureCelcius);
        }

        return pDatas;
    }

    public List<Vector2> GeneratePoints(float gridSpacing = 0)
    {
        if (sceneManager == null) sceneManager = GameObject.Find("SceneManager").GetComponent<SceneManager>();

        bool editorView = gridSpacing == -1;
        if (editorView) gridSpacing = editorGridSpacing;
        else if (gridSpacing == 0) gridSpacing = defaultGridSpacing;

        List<Vector2> generatedPoints = new();

        // Find the bounding box of the polygon
        Vector2 min = Func.MinVector2(Edges.Select(edge => Func.MinVector2(edge.start, edge.end)).ToArray());
        Vector2 max = Func.MaxVector2(Edges.Select(edge => Func.MaxVector2(edge.start, edge.end)).ToArray());

        // Generate grid points within the bounding box
        int iterationCount = 0;
        for (float x = min.x; x <= max.x; x += gridSpacing)
        {
            for (float y = min.y; y <= max.y; y += gridSpacing)
            {
                Vector2 point = new(x, y);

                if (IsPointInsidePolygon(point) && sceneManager.IsPointInsideBounds(point) && sceneManager.IsSpaceEmpty(point, this))
                {
                    if (++iterationCount > MaxGizmosIterations && editorView) return generatedPoints;

                    generatedPoints.Add(point);
                }
            }
        }

        return generatedPoints;
    }

    private PData InitPData(Vector2 pos, float tempCelsius)
    {
        return new PData
        {
            predPos = new float2(0.0f, 0.0f),
            pos = pos,
            vel = new(0, 0),
            lastVel = new float2(0.0f, 0.0f),
            density = 0.0f,
            nearDensity = 0.0f,
            temperature = Utils.CelsiusToKelvin(tempCelsius),
            temperatureExchangeBuffer = 0.0f,
            lastChunkKey_PType_POrder = pTypeIndex * main.ChunksNumAll // flattened equivelant to PType = 1
        };
    }
}
