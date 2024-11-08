using UnityEngine;
using System;
public abstract class Sensor : MonoBehaviour
{
    [Header("Display")]
    public int numDecimals;
    public Color primaryColor;

    [Header("References")]
    public ProgramManager programManager;
    public GameObject sensorUIPrefab;
    public Canvas mainCanvas;

    // Private references
    [NonSerialized] public Transform sensorContainer;
    [NonSerialized] public Main main;
    [NonSerialized] public SensorManager sensorManager;
    [NonSerialized] public Vector2 canvasResolution;

    // Display
    [NonSerialized] public SensorUI sensorUI;

    public void StartSensor()
    {
        SetReferences();
        InitSensorUI();
        InitSensor();

        programManager.AddSensor(ref sensorUI, this);
    }

    public void SetReferences()
    {
        sensorContainer = GameObject.FindGameObjectWithTag("SensorUIContainer").GetComponent<Transform>();
        main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        sensorManager = GameObject.FindGameObjectWithTag("SensorManager").GetComponent<SensorManager>();
        Rect uiCanvasRect = GameObject.FindGameObjectWithTag("UICanvas").GetComponent<RectTransform>().rect;
        canvasResolution = new Vector2(uiCanvasRect.width, uiCanvasRect.height);
    }

    private void InitSensorUI()
    {
        GameObject sensorUIGameObject = Instantiate(sensorUIPrefab, sensorContainer);
        sensorUI = sensorUIGameObject.GetComponent<SensorUI>();
        sensorUI.swayElementA.mainCanvas = mainCanvas;
        sensorUI.swayElementB.mainCanvas = mainCanvas;
        sensorUI.swayElementC.mainCanvas = mainCanvas;
        sensorUI.swayElementD.mainCanvas = mainCanvas;
        sensorUI.SetPrimaryColor(primaryColor);
        sensorUI.sensor = this;
        InitSensorTitle();
        sensorUIGameObject.name = "UI - " + this.name;
    }

    public abstract void InitSensor();
    public abstract void UpdatePosition();
    public abstract void UpdateSensor();
    public abstract void InitSensorTitle();

    public void UpdateScript() => UpdatePosition();

    public Vector2 SimSpaceToCanvasSpace(Vector2 simCoords) => (simCoords / new Vector2(main.BoundaryDims.x, main.BoundaryDims.y) - new Vector2(0.5f, 0.5f)) * canvasResolution;
}
