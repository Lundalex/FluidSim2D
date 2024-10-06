using UnityEngine;
using System.Collections.Generic;
using System;
using Resources;
using System.Linq;
using UnityEditor;

[RequireComponent(typeof(PolygonCollider2D))]
public class Polygon : MonoBehaviour
{
    public ObjectType ObjectTypeSelect;
    public int MaxGizmosIterations = 20000;
    [Range(0.1f, 10.0f)] public float pointOffset = 2.0f;
    [Range(0.05f, 0.5f)] public float editorPointRadius = 0.05f;
    // public customMaterial;
    [NonSerialized] public List<Edge> Edges = new();
    [NonSerialized] public Vector2[] Points;
    private SceneManager sceneManager;

    void Awake()
    {
        SetPolygonData();
    }

    public void SetPolygonData()
    {
        Edges = new();
        Vector2[] points = GetComponent<PolygonCollider2D>().points;

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 startPoint = transform.TransformPoint(points[i]);
            Vector2 endPoint = transform.TransformPoint(points[(i + 1) % points.Length]);

            Edge edge = new(startPoint, endPoint);
            Edges.Add(edge);
        }
    }

    public Vector2[] GeneratePoints(float offset = 0)
    {
        if (sceneManager == null) sceneManager = GameObject.Find("SceneManager").GetComponent<SceneManager>();

        bool editorView = offset == -1;
        if (offset == 0 || offset == -1) offset = pointOffset;

        List<Vector2> generatedPoints = new();

        // Find the bounding box of the polygon
        Vector2 min = Func.MinVector2(Edges.Select(edge => Func.MinVector2(edge.start, edge.end)).ToArray());
        Vector2 max = Func.MaxVector2(Edges.Select(edge => Func.MaxVector2(edge.start, edge.end)).ToArray());

        min -= new Vector2(min.x % offset, min.y % offset);

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

    private bool IsPointInsidePolygon(Vector2 point)
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

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        if (Edges != null)
        {
            foreach (Edge edge in Edges) Gizmos.DrawLine(edge.start, edge.end);
        }

        int iterationCount = 0;
        Gizmos.color = Color.green;
        if (Points != null)
        {
            foreach (Vector2 point in Points)
            {
                if (iterationCount++ > MaxGizmosIterations) return;
                else Gizmos.DrawSphere(point, editorPointRadius);
            }
        }
    }
#endif
}
