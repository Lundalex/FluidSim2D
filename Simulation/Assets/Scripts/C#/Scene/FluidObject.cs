using UnityEngine;
using System.Collections.Generic;
using System;
using Resources;
using System.Linq;
using UnityEditor;

[RequireComponent(typeof(PolygonCollider2D))]
public class SceneFluid : Polygon
{
    [Range(0.1f, 10.0f)] public float pointOffset = 2.0f;
    [Range(0.05f, 2.0f)] public float editorPointRadius = 0.05f;
    // public customMaterial;
    [NonSerialized] public Vector2[] Points;
    protected SceneManager sceneManager;

    public Vector2[] GeneratePoints(float offset = 0)
    {
        if (sceneManager == null) sceneManager = GameObject.Find("SceneManager").GetComponent<SceneManager>();

        bool editorView = offset == -1;
        if (offset == 0 || offset == -1) offset = pointOffset;

        List<Vector2> generatedPoints = new();

        // Find the bounding box of the polygon
        Vector2 min = Func.MinVector2(Edges.Select(edge => Func.MinVector2(edge.start, edge.end)).ToArray());
        Vector2 max = Func.MaxVector2(Edges.Select(edge => Func.MaxVector2(edge.start, edge.end)).ToArray());

        // Generate grid points within the bounding box
        int iterationCount = 0;
        for (float x = min.x; x <= max.x; x += offset)
        {
            for (float y = min.y; y <= max.y; y += offset)
            {
                if (iterationCount++ > MaxGizmosIterations && editorView) return generatedPoints.ToArray();

                Vector2 point = new(x, y);

                if (IsPointInsidePolygon(point) && sceneManager.IsPointInsideBounds(point))
                {
                    generatedPoints.Add(point);
                }
            }
        }

        return generatedPoints.ToArray();
    }
}
