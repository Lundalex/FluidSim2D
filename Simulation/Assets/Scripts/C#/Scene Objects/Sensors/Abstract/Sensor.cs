using UnityEngine;
using System;
using Unity.Mathematics;
using ChartAndGraph;
public abstract class Sensor : MonoBehaviour
{
    [Header("Display")]
    public int numDecimals;
    public Color primaryColor;
    public Vector2 targetPosition;
    public PositionType positionType;
    public bool useFixedScaleForDashedRectangle;

    [Header("References")]
    [SerializeField] private GameObject sensorUIPrefab;
    [SerializeField] private GameObject sensorUIOutlinePrefab;
    [SerializeField] private GameObject graphChartPrefab;
    [NonSerialized] public GraphController graphController;
    public Canvas mainCanvas;

    // Private references
    [NonSerialized] public Transform sensorUIContainer;
    [NonSerialized] public Transform sensorOutlineContainer;
    [NonSerialized] public Main main;
    [NonSerialized] public SensorManager sensorManager;
    [NonSerialized] public Vector2 canvasResolution;
    [NonSerialized] private GraphChart graphChart;

    // Display
    [NonSerialized] public SensorUI sensorUI;

    // Private
    private Vector2 boundaryDims = Vector2.zero;

    public void Initialize()
    {
        InitSensorUI();
        InitSensor();
        graphController.InitGraph(graphChart);
        ProgramManager.Instance.sensorManager.SubscribeGraphToCoroutine(graphController);

        ProgramManager.Instance.AddSensor(ref sensorUI, this);
    }

    public void SetReferences(Transform sensorUIContainer, Transform sensorOutlineContainer, Main main, SensorManager sensorManager, Vector2 canvasResolution)
    {
        this.sensorUIContainer = sensorUIContainer;
        this.sensorOutlineContainer = sensorOutlineContainer;
        this.main = main;
        this.sensorManager = sensorManager;
        this.canvasResolution = canvasResolution;

        graphController = gameObject.GetComponent<GraphController>();
    }

    private void InitSensorUI()
    {
        GameObject sensorUIObject = Instantiate(sensorUIPrefab, sensorUIContainer);
        GameObject sensorUIOutline = Instantiate(sensorUIOutlinePrefab, sensorOutlineContainer);
        sensorUI = sensorUIObject.GetComponent<SensorUI>();
        GameObject sensorUIGraphChartObject = Instantiate(graphChartPrefab, sensorUI.graphChartContainer);
        graphChart = sensorUIGraphChartObject.GetComponent<GraphChart>();
        sensorUIOutline.SetActive(false);
        sensorUI.dashedRectangleObject = sensorUIOutline;
        sensorUI.dashedRectangle = sensorUIOutline.GetComponent<DashedRectangle>();
        sensorUI.swayElementA.mainCanvas = mainCanvas;
        sensorUI.swayElementB.mainCanvas = mainCanvas;
        sensorUI.swayElementC.mainCanvas = mainCanvas;
        sensorUI.swayElementD.mainCanvas = mainCanvas;
        sensorUI.SetPrimaryColor(primaryColor);
        sensorUI.sensor = this;
        sensorUI.sliderScale = sensorUI.scaleSlider.value;
        InitSensorTitle();
        sensorUIObject.name = "UI - " + this.name;
    }

    public abstract void InitSensor();
    public abstract void UpdatePosition();
    public abstract void UpdateSensor();
    public abstract void InitSensorTitle();

    public void UpdateScript() => UpdatePosition();

    public Vector2 SimSpaceToCanvasSpace(Vector2 simCoords)
        => (simCoords / GetBoundaryDims() - new Vector2(0.5f, 0.5f)) * canvasResolution;

    public Vector2 CanvasSpaceToSimSpace(Vector2 canvasCoords)
        => (canvasCoords / canvasResolution + new Vector2(0.5f, 0.5f)) * GetBoundaryDims();

    private Vector2 GetBoundaryDims()
    {
        if (boundaryDims == Vector2.zero)
        {
            int2 boundaryDimsInt2 = ProgramManager.Instance.main.BoundaryDims;
            boundaryDims = new(boundaryDimsInt2.x, boundaryDimsInt2.y);
        }
        return boundaryDims;
    }

    public void AddSensorDataToGraph(float y) => graphController.AddPointsToGraph(new Vector2(ProgramManager.Instance.totalTimeElapsed, y));
}