using System;
using System.Collections.Generic;
using System.Linq;
using Resources;
using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class SceneRigidBody : Polygon
{
    [Header("Simulation Object Settings")]
    [Range(0.1f, 10.0f)] public float defaultGridDensity = 0.5f;
    public RBInput RBInput;
    [NonSerialized] public Vector2[] Points;
    public Vector2[] GeneratePoints(float gridDensity, Vector2 offset, bool editorView = false)
    {
        if (gridDensity == 0) gridDensity = defaultGridDensity;

        SetPolygonData();

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
                if (++iterationCount > MaxGizmosIterations && editorView) return generatedPoints.ToArray();

                Vector2 point = new Vector2(x, y) + offset;

                if (IsPointInsidePolygon(point))
                {
                    generatedPoints.Add(point);
                }
            }
        }

        return generatedPoints.ToArray();
    }

    public (float, float) ComputeInertiaAndBalanceRB(ref Vector2[] vectors, ref Vector2 rigidBodyPosition, Vector2 offset, float? gridDensityInput = null)
    {
        float gridDensity = gridDensityInput ?? 0.2f;
        
        Vector2[] points = GeneratePoints(gridDensity, offset);
        int numPoints = points.Length;
        float pointMass = RBInput.mass / numPoints;

        // Centroid
        Vector2 centroid = Vector2.zero;
        foreach (Vector2 point in points) centroid += point;
        centroid /= numPoints;

        // Shift vectors to align centroid with rigid body position
        Vector2 shift = rigidBodyPosition - centroid;
        for (int i = 0; i < vectors.Length; i++) vectors[i] += shift;
        rigidBodyPosition = centroid;

        // Inertia
        float inertia = 0.0f;
        float maxRadiusSqr = 0.0f;
        foreach (Vector2 point in points)
        {
            float dstSqr = (point - rigidBodyPosition).sqrMagnitude;
            inertia += pointMass * dstSqr;
        }

        // MaxRadiusSqr
        foreach (Vector2 vector in vectors) maxRadiusSqr = Mathf.Max(maxRadiusSqr, vector.sqrMagnitude);

        return (inertia, maxRadiusSqr);
    }
}