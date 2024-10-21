using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;
using System;
public abstract class Sensor : MonoBehaviour
{
    [Header("Display")]
    public float2 offset;
    public int numDecimals;
    public bool doInterpolation;
    [Range(1.0f, 20.0f)] public float moveSpeed;

    [Header("References")]
    public GameObject sensorPrefab;

    [NonSerialized] public int linkedRBIndex = -1;

    // Private references
    [NonSerialized] public Transform sensorContainer;
    [NonSerialized] public Main main;
    [NonSerialized] public SensorManager sensorManager;
    [NonSerialized] public Vector2 canvasResolution;

    // Display
    [NonSerialized] public GameObject sensorUI;
    [NonSerialized] public Text sensorText;
    [NonSerialized] public RectTransform sensorUIRect;

    [NonSerialized] public Vector2 targetPosition;
    [NonSerialized] public bool firstDataRecieved = false;

    private void Start()
    {
        SetReferences();

        InitSensorUI();
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
        sensorUI = Instantiate(sensorPrefab, sensorContainer);
        sensorUI.name = "UI - " + this.name;

        sensorUIRect = sensorUI.GetComponent<RectTransform>();
        sensorUIRect.localPosition = SimSpaceToCanvasSpace(new(-10000.0f, 0.0f));

        sensorText = sensorUI.transform.Find("Label").GetComponent<Text>();
    }

    public abstract void UpdateSensor();

    void Update()
    {
        if (firstDataRecieved)
        {
            Vector2 canvasTargetPosition = SimSpaceToCanvasSpace(targetPosition);
            
            // Interpolate the current position
            sensorUIRect.localPosition = doInterpolation ? Vector2.Lerp(sensorUIRect.localPosition, canvasTargetPosition, Time.deltaTime * moveSpeed) : canvasTargetPosition;
        }
    }

    public static string FloatToStr(float value, int numDecimals) => value.ToString($"F{numDecimals}", CultureInfo.InvariantCulture);

    public Vector2 SimSpaceToCanvasSpace(Vector2 simCoords) => (simCoords / new Vector2(main.BoundaryDims.x, main.BoundaryDims.y) - new Vector2(0.5f, 0.5f)) * canvasResolution;
}