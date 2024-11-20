using UnityEngine;
using System.Collections.Generic;
using System;

[ExecuteAlways]
public class Polygon : MonoBehaviour
{
    [Header("Editor Settings")]
    public bool snapPointToGrid = true;
    public float gridSpacing = 1.0f;
    public Color LineColor = Color.black;
    public Color BodyColor = Color.white;
    [NonSerialized] public List<Edge> Edges = new();
    [NonSerialized] public List<Vector2> MeshPoints = new();
    [NonSerialized] public PolygonCollider2D polygonCollider;

    public void SetPolygonData(Vector2? offsetInput = null)
    {
        if (polygonCollider == null) polygonCollider = GetComponent<PolygonCollider2D>();
        Vector2 offset = offsetInput ?? Vector2.zero;

        Edges = new List<Edge>();
        MeshPoints = new List<Vector2>();
        Vector2[] points = polygonCollider.points;
        
        for (int i = 0; i < points.Length; i++) MeshPoints.Add(transform.TransformPoint(points[i]));

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 startPoint = MeshPoints[i];
            Vector2 endPoint = MeshPoints[(i + 1) % points.Length];

            Edge edge = new(startPoint + offset, endPoint + offset);
            Edges.Add(edge);
        }
    }

    public bool IsPointInsidePolygon(Vector2 point)
    {
        if (Edges.Count == 0) SetPolygonData();

        int intersectionCount = 0;
        foreach (Edge edge in Edges)
        {
            Vector2 p1 = edge.start;
            Vector2 p2 = edge.end;

            // Skip horizontal edges
            if (p1.y == p2.y) continue;

            // Check ray-line intersection
            if ((point.y > Mathf.Min(p1.y, p2.y)) && (point.y <= Mathf.Max(p1.y, p2.y)))
            {
                float xIntersection = (p2.x - p1.x) * (point.y - p1.y) / (p2.y - p1.y) + p1.x;

                if (xIntersection > point.x) intersectionCount++;
            }
        }

        // Point is inside the polygon if intersection count is odd
        return (intersectionCount % 2) == 1;
    }
}
