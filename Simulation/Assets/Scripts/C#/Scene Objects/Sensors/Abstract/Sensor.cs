using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;
using System;
public abstract class Sensor : MonoBehaviour
{
    [Header("Display")]
    public int numDecimals;

    [Header("References")]
    public GameObject sensorUIPrefab;

    // Private references
    [NonSerialized] public Transform sensorContainer;
    [NonSerialized] public Main main;
    [NonSerialized] public SensorManager sensorManager;
    [NonSerialized] public Vector2 canvasResolution;

    // Display
    [NonSerialized] public GameObject sensorUI;
    [NonSerialized] public Text sensorText;
    [NonSerialized] public RectTransform sensorUIRect;
    [NonSerialized] public bool programStarted = false;

    public void StartSensor()
    {
        SetReferences();
        InitSensorUI();
        InitSensor();
        programStarted = true;
    }

    private void SetReferences()
    {
        sensorContainer = GameObject.FindGameObjectWithTag("SensorUIContainer").GetComponent<Transform>();
        main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        sensorManager = GameObject.FindGameObjectWithTag("SensorManager").GetComponent<SensorManager>();
        Rect uiCanvasRect = GameObject.FindGameObjectWithTag("UICanvas").GetComponent<RectTransform>().rect;
        canvasResolution = new Vector2(uiCanvasRect.width, uiCanvasRect.height);
    }

    private void InitSensorUI()
    {
        sensorUI = Instantiate(sensorUIPrefab, sensorContainer);
        sensorUI.name = "UI - " + this.name;

        sensorUIRect = sensorUI.GetComponent<RectTransform>();

        sensorText = sensorUI.transform.Find("Label").GetComponent<Text>();
    }

    public abstract void InitSensor();
    public abstract void UpdatePosition();
    public abstract void UpdateSensor();

    void Update()
    {
        if (programStarted) UpdatePosition();
    }

    public static string FloatToStr(float value, int numDecimals) => value.ToString($"F{numDecimals}", CultureInfo.InvariantCulture);

    public Vector2 SimSpaceToCanvasSpace(Vector2 simCoords) => (simCoords / new Vector2(main.BoundaryDims.x, main.BoundaryDims.y) - new Vector2(0.5f, 0.5f)) * canvasResolution;
}
