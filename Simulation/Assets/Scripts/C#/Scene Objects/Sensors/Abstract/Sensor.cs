using Unity.Mathematics;
using UnityEngine;
using System.Globalization;
using System;
public abstract class Sensor : MonoBehaviour
{
    [Header("Display")]
    public int numDecimals;
    public Color primaryColor;

    [Header("References")]
    public GameObject sensorUIPrefab;
    public Canvas mainCanvas;

    // Private references
    [NonSerialized] public Transform sensorContainer;
    [NonSerialized] public Main main;
    [NonSerialized] public SensorManager sensorManager;
    [NonSerialized] public Vector2 canvasResolution;

    // Display
    [NonSerialized] public SensorUI sensorUI;

    // Script
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
        GameObject sensorUIGameObject = Instantiate(sensorUIPrefab, sensorContainer);
        sensorUI = sensorUIGameObject.GetComponent<SensorUI>();
        sensorUI.swayElementA.swayParent = sensorUI.swayParent;
        sensorUI.swayElementB.swayParent = sensorUI.swayParent;
        sensorUI.swayElementA.mainCanvas = mainCanvas;
        sensorUI.swayElementB.mainCanvas = mainCanvas;
        sensorUI.SetPrimaryColor(primaryColor);
        InitSensorTitle();
        sensorUIGameObject.name = "UI - " + this.name;
    }

    public abstract void InitSensor();
    public abstract void UpdatePosition();
    public abstract void UpdateSensor();
    public abstract void InitSensorTitle();

    void Update()
    {
        if (programStarted) UpdatePosition();
    }

    public Vector2 SimSpaceToCanvasSpace(Vector2 simCoords) => (simCoords / new Vector2(main.BoundaryDims.x, main.BoundaryDims.y) - new Vector2(0.5f, 0.5f)) * canvasResolution;
}
