using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ProgramManagerAsset", menuName = "ProgramManager")]
public class ProgramManager : ScriptableObject
{
    // References
    public Material lineMaterial;
    [HideInInspector] public Main main;
    [HideInInspector] public SensorManager sensorManager;

    // Sensors
    [HideInInspector] public List<SensorData> sensorDatas = new();

    // Globally accessed variables
    [HideInInspector] public bool programStarted = false;
    [HideInInspector] public bool doOnSettingsChanged = false;
    [HideInInspector] public float globalBrightnessFactor = 1;
    [HideInInspector] public float timeScale = 1;
    [HideInInspector] public bool isAnySensorSettingsViewActive = false;
    [HideInInspector] public bool programPaused = false;
    [HideInInspector] public bool FrameStep = false;
    [HideInInspector] public float totalTimeElapsed = 0;
    [HideInInspector] public float clampedDeltaTime = 0;
    private const float MaxDeltaTime = 1 / 30.0f;
    private const float MinTimeScaleForRunningProgram = 0.01f;

    // Private - Camera
    private Camera uiCam;
    private Vector2 uiViewMin;
    private Vector2 uiViewDims;
    private bool viewTransformInitiated;

    // Private - Animated Texture Scrolling
    private const float ScrollSpeed = 0.5f;
    private float offset;

    // Singleton
    private static ProgramManager _instance;
    public static ProgramManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<ProgramManager>("ProgramManagerAsset");
            }
            return _instance;
        }
    }

    public void Start()
    {
        SetReferences();

        sensorManager.StartScript(main);
        main.StartScript();

        globalBrightnessFactor = 1;
        programStarted = true;
        programPaused = false;
    }

    public void Update()
    {
        CheckKeyInputs();

        isAnySensorSettingsViewActive = CheckIfAnySensorSettingsViewActive();

        clampedDeltaTime = Mathf.Min(Time.deltaTime, MaxDeltaTime);

        if (isAnySensorSettingsViewActive) UpdateAnimatedDashedLineOffset(clampedDeltaTime);
        LerpGlobalBrightness(clampedDeltaTime);
        LerpTimeScale(clampedDeltaTime);
        LerpSensorUIScale(clampedDeltaTime);

        if (doOnSettingsChanged && programStarted)
        {
            main.OnSettingsChanged();
            doOnSettingsChanged = false;
        }

        if (!programPaused && timeScale > MinTimeScaleForRunningProgram)
        {
            totalTimeElapsed += clampedDeltaTime;
            
            main.ScriptUpdate();

            foreach (SensorData sensorData in sensorDatas) sensorData.sensor.UpdateScript();
        }
    }

    private void CheckKeyInputs()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            programPaused = !programPaused;
            if (programPaused) Debug.Log("Program paused");
        }
        if (Input.GetKeyDown(KeyCode.F)) FrameStep = !FrameStep;
    }

    private void UpdateAnimatedDashedLineOffset(float deltaTime)
    {
        offset += deltaTime * ScrollSpeed;
        lineMaterial.mainTextureOffset = new Vector2(offset, 0);
    }
    private void SetReferences()
    {
        main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        sensorManager = GameObject.FindGameObjectWithTag("SensorManager").GetComponent<SensorManager>();
    }

    public void ResetDatas()
    {
        programStarted = false;
        doOnSettingsChanged = false;
        isAnySensorSettingsViewActive = false;
        programPaused = false;
        totalTimeElapsed = 0;
    }

    public void AddSensor(ref SensorUI sensorUI, Sensor sensor)
    {
        int sensorIndex = sensorDatas.Count;
        sensorUI.sensorIndex = sensorIndex;
        sensorDatas.Add(new SensorData(sensor, sensorUI, sensorUI.gameObject, false));

        sensorUI.OnSettingsViewStatusChanged += (isActive) => SetSensorSettingsViewStatus(sensorIndex, isActive);
    }

    public void SetSensorSettingsViewStatus(int sensorIndex, bool isSettingsViewActive)
        => sensorDatas[sensorIndex].isSettingsViewActive = isSettingsViewActive;

    public bool CheckIfAnySensorSettingsViewActive()
    {
        foreach (SensorData sensorData in sensorDatas)
        {
            if (sensorData.isSettingsViewActive) return true;
        }
        return false;
    }

    public (Vector2, Vector2) GetUIBoundaries()
    {
        if (!programStarted) return (Vector2.zero, Vector2.zero);
        if (viewTransformInitiated) return (uiViewMin, uiViewDims);
        if (uiCam == null) uiCam = GameObject.FindGameObjectWithTag("UICamera").GetComponent<Camera>();

        if (!uiCam.orthographic)
        {
            Debug.LogError("Main Camera is not orthographic.");
            return (Vector2.zero, Vector2.zero);
        }

        float size = uiCam.orthographicSize;
        float aspect = uiCam.aspect;

        float yMax = size;
        float yMin = -size;
        float xMax = size * aspect;
        float xMin = -size * aspect;

        uiViewMin = new(xMin, yMin);
        uiViewDims = new(xMax - xMin, yMax - yMin);

        viewTransformInitiated = true;

        return (uiViewMin, uiViewDims);
    }

    private void LerpGlobalBrightness(float deltaTime)
    {
        float target = 1f - main.SettingsViewDarkTintPercent * (isAnySensorSettingsViewActive ? 1f : 0f);
        globalBrightnessFactor = Mathf.Lerp(globalBrightnessFactor, target, deltaTime * main.GlobalSettingsViewChangeSpeed);
    }

    private void LerpTimeScale(float deltaTime)
    {
        float target = isAnySensorSettingsViewActive ? 0f : 1f;
        timeScale = Mathf.Lerp(timeScale, target, deltaTime * main.GlobalSettingsViewChangeSpeed);
    }

    private void LerpSensorUIScale(float deltaTime)
    {
        foreach (SensorData sensorData in sensorDatas)
        {   
            Vector3 targetScale = sensorData.sensorUI.GetTotalScale(sensorData.isSettingsViewActive);
            Vector3 currentScale = sensorData.sensorUIObject.transform.localScale;

            Vector3 newScale = Vector3.Lerp(currentScale, targetScale, deltaTime * main.GlobalSettingsViewChangeSpeed);
            sensorData.sensorUIObject.transform.localScale = newScale;
            // Manage the draw order
            sensorData.sensorUIObject.transform.SetSiblingIndex(sensorData.isSettingsViewActive ? sensorDatas.Count - 1 : 0);
        }
    }
}