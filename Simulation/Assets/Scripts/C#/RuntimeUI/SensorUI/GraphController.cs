using UnityEngine;
using ChartAndGraph;
using System;
using System.Collections.Generic;
using Resources2;
using PM = ProgramManager;

public class GraphController : MonoBehaviour
{
    // Serialized Fields
    [SerializeField] private bool overrideGraphDataCategory;
    [SerializeField] private bool isBezierCurve;
    [SerializeField, Range(0.0f, 1.0f)] private float bezierTension = 0.5f;
    [SerializeField, Range(1.0f, 10.0f)] private float HorizontalViewLength = 5.0f;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Material fillMaterial;
    [SerializeField] private Material pointMaterial;
    [SerializeField] private float lineThickness;
    [SerializeField] private float pointSize;

    // Other
    [NonSerialized] public bool isPointerHovering;

    // References
    [NonSerialized] private GraphChart graphChart;

    // Private
    private List<Vector2> pointList;
    private List<Vector2> storedPoints;
    private bool isFirstPointDrawn;
    private Timer pointSubmissionTimer;

    public void InitGraph(GraphChart graphChartInput)
    {
        this.graphChart = graphChartInput;
        if (overrideGraphDataCategory && isBezierCurve)
        {
            UnityEngine.Debug.LogWarning("The bezier curve setting cannot be combined with graph data category override. The graph data category will not be overridden");
            overrideGraphDataCategory = false;
        }
        if (overrideGraphDataCategory)
        {
            graphChart.DataSource.AddCategory("SensorDatas", lineMaterial, lineThickness, new MaterialTiling(), fillMaterial, false, pointMaterial, pointSize, false);
        }

        float pointSubmissionFrequency = Func.MsToSeconds(PM.Instance.sensorManager.msGraphPointSubmissionFrequency);
        pointSubmissionTimer = new(pointSubmissionFrequency, false, false);

        ResetGraph();
    }

    public void ResetGraph()
    {
        // Reset graph
        if (graphChart.DataSource.HasCategory("SensorDatas"))
        {
            graphChart.DataSource.ClearCategory("SensorDatas");
        }

        // Reset data
        pointList = new();
        storedPoints = new();
        isFirstPointDrawn = false;
        pointSubmissionTimer.Reset();
    }

    public void AddPointsToGraph(params Vector2[] points)
    {
        if (points == null || points.Length == 0) return;
        
        // Add points to storedPoints
        foreach (Vector2 point in points)
        {
            if (float.IsNaN(point.x) || float.IsNaN(point.y)) continue;

            if (!pointSubmissionTimer.Check()) break;

            storedPoints.Add(point);
        }
    }

    public void UpdateGraph()
    {
        if (PM.Instance.programPaused || isPointerHovering || storedPoints.Count == 0) return;

        // Update the graph
        foreach (Vector2 point in storedPoints)
        {

            pointList.Add(pointList.Count == 0 ? new(point.x > 2.0f ? point.x : 0, point.y) : point);

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

        // Empty all stored points
        storedPoints = new();

        // Automatic scrolling
        graphChart.HorizontalScrolling = Mathf.Max(PM.Instance.totalTimeElapsed - HorizontalViewLength, 0);
    }
}