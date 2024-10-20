using UnityEngine;
using System.Collections.Generic;
using System;

public class Polygon : MonoBehaviour
{
    [Header("Editor Settings")]
    public Color LineColor = Color.black;
    public Color BodyColor = Color.white;
    [NonSerialized] public List<Edge> Edges = new();
    [NonSerialized] public List<Vector2> MeshPoints = new();

    protected virtual void Awake() => SetPolygonData();

    public void SetPolygonData(Vector2? offsetInput = null)
    {
        Vector2 offset = offsetInput ?? Vector2.zero;

        Edges = new List<Edge>();
        MeshPoints = new List<Vector2>();
        Vector2[] points = GetComponent<PolygonCollider2D>().points;
        
        for (int i = 0; i < points.Length; i++) MeshPoints.Add(transform.TransformPoint(points[i]));

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 startPoint = MeshPoints[i];
            Vector2 endPoint = MeshPoints[(i + 1) % points.Length];

            Edge edge = new(startPoint + offset, endPoint + offset);
            Edges.Add(edge);
        }
    }

    protected bool IsPointInsidePolygon(Vector2 point)
    {
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
