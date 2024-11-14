using System.Collections.Generic;
using Resources2;
using UnityEngine;

[CreateAssetMenu(fileName = "ProgramManagerAsset", menuName = "ProgramManager")]
public class ProgramManager : ScriptableObject
{
    // References
    public Material lineMaterial;
    [HideInInspector] public Main main;
    [HideInInspector] public SensorManager sensorManager;
    [HideInInspector] public FluidSpawnerManager fluidSpawnerManager;

    // Sensors
    [HideInInspector] public List<SensorData> sensorDatas = new();

    // Globally accessed variables
    [HideInInspector] public bool programStarted = false;
    [HideInInspector] public bool doOnSettingsChanged = false;
    [HideInInspector] public float globalBrightnessFactor = 1;
    [HideInInspector] public float timeScale = 1;
    [HideInInspector] public bool isAnySensorSettingsViewActive = false;
    [HideInInspector] public bool programPaused = false;
    [HideInInspector] public bool frameStep = false;
    [HideInInspector] public float totalTimeElapsed = 0;
    [HideInInspector] public float clampedDeltaTime = 0;
    [HideInInspector] public float timeSetRandTimer = 0;
    [HideInInspector] public readonly float MaxDeltaTime = 1 / 30.0f;
    private const float MinTimeScaleForRunningProgram = 0.01f;
    public Vector2 ScreenToViewFactor;

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
        ScreenToViewFactor = GetScreenToViewFactor();

        SetReferences();

        main.StartScript();
        sensorManager.StartScript(main);
        fluidSpawnerManager.StartScript();

        globalBrightnessFactor = 1;
        programStarted = true;
        programPaused = false;
    }

    public void Update()
    {
        CheckKeyInputs();

        isAnySensorSettingsViewActive = CheckAnySensorSettingsViewActive();

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

        bool simulateThisFrame = false;
        if (!programPaused || frameStep) simulateThisFrame = true;
        if (programPaused && frameStep)
        {
            StringUtils.LogIfInEditor("Stepped forward 1 frame");
            frameStep = false;
        }

        if (simulateThisFrame && timeScale > MinTimeScaleForRunningProgram)
        {
            fluidSpawnerManager.UpdateScript();

            main.UpdateScript();

            foreach (SensorData sensorData in sensorDatas) sensorData.sensor.UpdateScript();

            totalTimeElapsed += clampedDeltaTime;
        }
        else main.RunRenderShader();
    }

    private void CheckKeyInputs()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            programPaused = !programPaused;
            if (programPaused) Debug.Log("Program paused");
        }
        if (Input.GetKeyDown(KeyCode.F)) frameStep = !frameStep;
        if (Input.GetKeyDown(KeyCode.Escape)) CloseAllSensorUISettingsPanels();
    }

    private void CloseAllSensorUISettingsPanels()
    {
        foreach (SensorData sensorData in sensorDatas)
        {
            sensorData.sensorUI.settingsViewWindowManager.OpenPanel("DefaultDisplay");
            sensorData.sensorUI.SetSettingsViewAsDisabled();
        }
    }

    private void UpdateAnimatedDashedLineOffset(float deltaTime)
    {
        offset += deltaTime * ScrollSpeed;
        lineMaterial.mainTextureOffset = new Vector2(offset, 0);
    }

    private void SetReferences()
    {
        sensorManager = GameObject.FindGameObjectWithTag("SensorManager").GetComponent<SensorManager>();
        main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        fluidSpawnerManager = GameObject.FindGameObjectWithTag("FluidSpawnerManager").GetComponent<FluidSpawnerManager>();
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

    public bool CheckAnySensorSettingsViewActive()
    {
        foreach (SensorData sensorData in sensorDatas)
        {
            if (sensorData.isSettingsViewActive) return true;
        }
        return false;
    }

    public bool CheckAnySensorHovered()
    {
        foreach (SensorData sensorData in sensorDatas)
        {
            if (sensorData.sensorUI.isPointerHovering) return true;
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

    private Vector2 GetScreenToViewFactor() 
    {
        float boundsAspect = main.BoundaryDims.x / (float)main.BoundaryDims.y;
        float resolutionAspect = main.Resolution.x / (float)main.Resolution.y;

        float scaleX = 1.0f;
        float scaleY = 1.0f;
        Vector2 offset = Vector2.zero;

        if (boundsAspect > resolutionAspect)
        {
            // Bounds are wider than resolutionAspect: scale Y down
            scaleY = resolutionAspect / boundsAspect; // e.g., 1 / 2 = 0.5
            scaleX = 1.0f;

            float scaledHeight = main.BoundaryDims.y * scaleY;
            offset = new Vector2(0.0f, (main.BoundaryDims.y - scaledHeight) / 2.0f);
        }
        else
        {
            // Bounds are taller or equal to resolutionAspect: scale X down
            scaleX = boundsAspect / resolutionAspect;
            scaleY = 1.0f;

            float scaledWidth = main.BoundaryDims.x * scaleX;
            offset = new Vector2((main.BoundaryDims.x - scaledWidth) / 2.0f, 0.0f);
        }

        return new Vector2(scaleX, scaleY);
    }
}