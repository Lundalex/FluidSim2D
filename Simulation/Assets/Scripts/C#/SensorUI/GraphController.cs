using UnityEngine;
using ChartAndGraph;
using System;
using System.Diagnostics;
using System.Collections.Generic;

public class GraphController : MonoBehaviour
{
    // Serialized Fields
    [SerializeField] private bool overrideGraphDataCategory;
    [SerializeField] private bool isBezierCurve;
    [SerializeField, Range(0.0f, 1.0f)] private float bezierTension = 0.5f;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Material fillMaterial;
    [SerializeField] private Material pointMaterial;
    [SerializeField] private float lineThickness;
    [SerializeField] private float pointSize;

    // Other
    [NonSerialized] public bool isPointerHovering;

    // References
    [NonSerialized] public GraphChart graphChart;

    // Private
    private List<Vector2> pointList = new();
    private bool isFirstPointDrawn = false;
    private readonly Stopwatch stopwatch = new();

    public void InitGraph()
    {
        if (overrideGraphDataCategory && isBezierCurve)
        {
            UnityEngine.Debug.LogWarning("The bezier curve setting cannot be combined with graph data category override. The graph data category will not be overridden");
            overrideGraphDataCategory = false;
        }

        if (overrideGraphDataCategory)
        {
            graphChart.DataSource.Clear();
            if (graphChart.DataSource.HasCategory("SensorDatas"))
            {
                graphChart.DataSource.ClearCategory("SensorDatas");
                graphChart.DataSource.RemoveCategory("SensorDatas");
            }
            graphChart.DataSource.AddCategory("SensorDatas", lineMaterial, lineThickness, new MaterialTiling(), fillMaterial, false, pointMaterial, pointSize, false);
        }

        stopwatch.Start();
    }

public void AddPointsToGraph(params Vector2[] points)
{
    if (points == null || points.Length == 0 ||
        stopwatch.Elapsed.TotalSeconds < ProgramManager.Instance.sensorManager.msGraphUpdateFrequency / 1000.0f ||
        ProgramManager.Instance.programPaused || isPointerHovering)
        return;

    stopwatch.Restart();

    foreach (Vector2 point in points)
    {
        if (float.IsNaN(point.x) || float.IsNaN(point.y)) continue;

        pointList.Add(pointList.Count == 0 ? new(0, point.y) : point);

        if (isBezierCurve)
        {
            if (pointList.Count < 2) continue;

            int i = pointList.Count - 2;

            Vector2 p0 = i > 0 ? pointList[i - 1] : pointList[i];
            Vector2 p1 = pointList[i];
            Vector2 p2 = pointList[i + 1];
            Vector2 p3 = i + 2 < pointList.Count ? pointList[i + 2] : pointList[i + 1];

            if (!isFirstPointDrawn)
            {
                isFirstPointDrawn = true;
                graphChart.DataSource.SetCurveInitialPoint("SensorDatas", p1.x, p1.y);
            }

            // Calculate control points using a Catmull-Rom spline
            Vector2 controlPointA = p1 + (p2 - p0) * (bezierTension / 3f);
            Vector2 controlPointB = p2 - (p3 - p1) * (bezierTension / 3f);

            // Add the curve segment for the new point
            graphChart.DataSource.AddCurveToCategory(
                "SensorDatas",
                new DoubleVector2(controlPointA.x, controlPointA.y),
                new DoubleVector2(controlPointB.x, controlPointB.y),
                new DoubleVector2(p2.x, p2.y),
                pointSize: -1f
            );
        }
        else
        {
            graphChart.DataSource.AddPointToCategory("SensorDatas", point.x, point.y);
        }
    }

    graphChart.HorizontalScrolling = Mathf.Max(ProgramManager.Instance.totalTimeElapsed - 5, 0);
}

}