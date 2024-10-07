using System;
using System.Collections.Generic;
using System.Linq;
using Resources;
using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class SceneRigidBody : Polygon
{
    [Header("Simulation Object Settings")]
    public RBInput RBInput;
    [NonSerialized] public Vector2[] Points;
    public Vector2[] GeneratePoints(float offset)
    {
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
                if (iterationCount++ > MaxGizmosIterations) return generatedPoints.ToArray();

                Vector2 point = new(x, y);

                if (IsPointInsidePolygon(point))
                {
                    generatedPoints.Add(point);
                }
            }
        }

        return generatedPoints.ToArray();
    }
}
