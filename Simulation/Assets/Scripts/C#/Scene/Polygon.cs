using UnityEngine;
using System.Collections.Generic;
using System;

public class Polygon : MonoBehaviour
{
    public int MaxGizmosIterations = 20000;
    public bool DoDrawBody = true;
    public Color LineColor;
    public Color BodyColor;
    [NonSerialized] public List<Edge> Edges = new();

    protected virtual void Awake() => SetPolygonData();

    public void SetPolygonData()
    {
        Edges = new List<Edge>();
        Vector2[] points = GetComponent<PolygonCollider2D>().points;

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 startPoint = transform.TransformPoint(points[i]);
            Vector2 endPoint = transform.TransformPoint(points[(i + 1) % points.Length]);

            Edge edge = new(startPoint, endPoint);
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
