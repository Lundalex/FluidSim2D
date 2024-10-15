using UnityEngine;
using System.Collections.Generic;
using System;
using Resources;
using System.Linq;
using UnityEditor;
using Unity.Mathematics;

[RequireComponent(typeof(PolygonCollider2D))]
public class SceneFluid : Polygon
{
    public Color BodyColor;
    [Range(0.05f, 2.0f)] public float editorPointRadius = 0.05f;
    [Header("Simulation Object Settings")]
    [Range(0.1f, 10.0f)] public float defaultGridDensity = 2.0f;
    public int pTypeIndex;
    [Header("Preview Values")]
    [NonSerialized] public Vector2[] Points;
    private SceneManager sceneManager;
    private Main main;

    public PData[] GenerateParticles(Vector2 pointOffset, float gridDensity = 0)
    {
        if (main == null) main = GameObject.Find("Main Camera").GetComponent<Main>();

        Vector2[] generatedPoints = GeneratePoints(gridDensity);

        PData[] pDatas = new PData[generatedPoints.Length];
        for (int i = 0; i < pDatas.Length; i++)
        {
            pDatas[i] = InitPData(generatedPoints[i] + pointOffset, new(0, 0), 20.0f);
        }

        return pDatas;
    }

    public Vector2[] GeneratePoints(float gridDensity = 0)
    {
        if (sceneManager == null) sceneManager = GameObject.Find("SceneManager").GetComponent<SceneManager>();

        bool editorView = gridDensity == -1;
        if (gridDensity == 0 || gridDensity == -1) gridDensity = defaultGridDensity;

        List<Vector2> generatedPoints = new();

        // Find the bounding box of the polygon
        Vector2 min = Func.MinVector2(Edges.Select(edge => Func.MinVector2(edge.start, edge.end)).ToArray());
        Vector2 max = Func.MaxVector2(Edges.Select(edge => Func.MaxVector2(edge.start, edge.end)).ToArray());

        // Generate grid points within the bounding box
        int iterationCount = 0;
        for (float x = min.x; x <= max.x; x += gridDensity)
        {
            for (float y = min.y; y <= max.y; y += gridDensity)
            {
                Vector2 point = new(x, y);

                if (IsPointInsidePolygon(point) && sceneManager.IsPointInsideBounds(point))
                {
                    if (++iterationCount > MaxGizmosIterations && editorView) return generatedPoints.ToArray();

                    generatedPoints.Add(point);
                }
            }
        }

        return generatedPoints.ToArray();
    }

    PData InitPData(Vector2 pos, Vector2 vel, float tempCelsius)
    {
        return new PData
        {
            predPos = new float2(0.0f, 0.0f),
            pos = pos,
            vel = vel,
            lastVel = new float2(0.0f, 0.0f),
            density = 0.0f,
            nearDensity = 0.0f,
            temperature = Utils.CelsiusToKelvin(tempCelsius),
            temperatureExchangeBuffer = 0.0f,
            lastChunkKey_PType_POrder = pTypeIndex * main.ChunksNumAll // flattened equivelant to PType = 1
        };
    }
}
