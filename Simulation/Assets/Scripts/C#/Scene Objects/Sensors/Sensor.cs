using UnityEngine;
using System;
using Unity.Mathematics;
using ChartAndGraph;
using PM = ProgramManager;

public abstract class Sensor : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private DataView defaultDataView;
    [Range(1, 2)] public int numDecimals;
    [Range(0.1f, 5.0f)] public float newLowerPrefixThreshold = 0.5f;
    public Color primaryColor;
    [Range(0.5f, 2.0f)] public float sensorScale = 1;
    public Vector2 targetPosition;
    public PositionType positionType;
    public bool useFixedScaleForDashedRectangle;

    [Header("References")]
    [SerializeField] private GameObject sensorUIPrefab;
    [SerializeField] private GameObject dashedRectanglePrefab;
    [SerializeField] private GameObject graphChartPrefab;
    [NonSerialized] public GraphController graphController;
    private Canvas mainCanvas;

    // Private references
    [NonSerialized] public Transform sensorUIContainer;
    [NonSerialized] public Transform sensorOutlineContainer;
    [NonSerialized] public Main main;
    [NonSerialized] public SensorManager sensorManager;
    [NonSerialized] public Vector2 canvasResolution;
    [NonSerialized] private GraphChart graphChart;

    // Display
    [NonSerialized] public SensorUI sensorUI;

    // Unit
    [NonSerialized] public string lastUnit = "";
    [NonSerialized] public int lastPrefixIndex = -1;
    [NonSerialized] public Timer newPrefixTimer;

    // Private
    private Vector2 boundaryDims = Vector2.zero;

    public void Initialize()
    {
        InitSensorUI();
        InitSensor();
        graphController.InitGraph(graphChart);
        PM.Instance.sensorManager.SubscribeGraphToCoroutine(graphController);
        
        newPrefixTimer = new Timer(newLowerPrefixThreshold, true, true);

        PM.Instance.AddSensor(sensorUI, this);
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
        GameObject sensorUIOutline = Instantiate(dashedRectanglePrefab, sensorOutlineContainer);
        sensorUI = sensorUIObject.GetComponent<SensorUI>();
        GameObject sensorUIGraphChartObject = Instantiate(graphChartPrefab, sensorUI.graphChartContainer);
        graphChart = sensorUIGraphChartObject.GetComponent<GraphChart>();
        sensorUIOutline.SetActive(false);
        sensorUI.dashedRectangleObject = sensorUIOutline;
        sensorUI.dashedRectangle = sensorUIOutline.GetComponent<DashedRectangle>();
        mainCanvas = GameObject.FindGameObjectWithTag("UICanvas").GetComponent<Canvas>();
        sensorUI.swayElementA.mainCanvas = mainCanvas;
        sensorUI.swayElementB.mainCanvas = mainCanvas;
        sensorUI.swayElementC.mainCanvas = mainCanvas;
        sensorUI.swayElementD.mainCanvas = mainCanvas;
        bool isRigidBodySensor = this is RigidBodySensor;
        sensorUI.rigidBodySensorTypeSelectObject.SetActive(isRigidBodySensor);
        sensorUI.fluidSensorTypeSelectObject.SetActive(!isRigidBodySensor);
        sensorUI.SetPrimaryColor(primaryColor);
        sensorUI.sensor = this;
        sensorUI.scaleSlider.value = sensorScale;
        sensorUI.sliderScale = sensorScale;
        sensorUI.SetDataWindow(defaultDataView == DataView.Numeric ? "NumericDisplay" : "GraphDisplay");
        sensorUI.Initialize();
        SetSensorTitle();
        sensorUIObject.name = "UI - " + this.name;
    }

    public abstract void InitSensor();
    public abstract void UpdatePosition();
    public abstract void UpdateSensor();
    public abstract void UpdateSensorTypeDropdown();
    public abstract void SetSensorTitle();
    public abstract void SetSensorUnit(string unit = "");

    public (string prefix, float newValue) GetMagnitudePrefix(float value)
    {
        string[] prefixes = { "n", "μ", "m", "", "k", "M", "G", "T" };
        int prefixIndex = 3; // "" (no prefix) is the default

        string minusPrefix = "";
        if (value < 0)
        {
            value *= -1;
            minusPrefix = "-";
        }

        float originalValue = value * Mathf.Pow(1000, 3 - lastPrefixIndex);

        while (value >= 1000f && prefixIndex < prefixes.Length - 1)
        {
            value /= 1000f;
            prefixIndex++;
        }

        while (value > 0 && value < 1f && prefixIndex > 0)
        {
            value *= 1000f;
            prefixIndex--;
        }

        if (prefixIndex < lastPrefixIndex)
        {
            if (newPrefixTimer.Check())
            {
                lastPrefixIndex = prefixIndex;
                return (minusPrefix + prefixes[prefixIndex], value);
            }
            else return (minusPrefix + prefixes[lastPrefixIndex], originalValue);
        }
        else
        {
            newPrefixTimer.Reset();
            lastPrefixIndex = prefixIndex;
            return (minusPrefix + prefixes[prefixIndex], value);
        }
    }

    public void UpdateScript()
    {
        UpdatePosition();
        if (PM.Instance.programStarted) UpdateSensorTypeDropdown();
    }

    public Vector2 SimSpaceToCanvasSpace(Vector2 simCoords)
        => (simCoords / GetBoundaryDims() - new Vector2(0.5f, 0.5f)) * canvasResolution;

    public Vector2 CanvasSpaceToSimSpace(Vector2 canvasCoords)
        => (canvasCoords / canvasResolution + new Vector2(0.5f, 0.5f)) * GetBoundaryDims();

    private Vector2 GetBoundaryDims()
    {
        if (boundaryDims == Vector2.zero)
        {
            int2 boundaryDimsInt2 = PM.Instance.main.BoundaryDims;
            boundaryDims = new(boundaryDimsInt2.x, boundaryDimsInt2.y);
        }
        return boundaryDims;
    }

    public void AddSensorDataToGraph(float y) => graphController.AddPointsToGraph(new Vector2(PM.Instance.totalTimeElapsed, y));
}