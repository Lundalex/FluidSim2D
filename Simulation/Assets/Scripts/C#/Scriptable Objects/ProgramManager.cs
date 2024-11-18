using System;
using System.Collections.Generic;
using Resources2;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Localization.Settings;

[CreateAssetMenu(fileName = "ProgramManagerAsset", menuName = "ProgramManager")]
public class ProgramManager : ScriptableObject
{
    // References
    public Material lineMaterial;
    [NonSerialized] public Main main;
    [NonSerialized] public SensorManager sensorManager;
    [NonSerialized] public FluidSpawnerManager fluidSpawnerManager;
    [NonSerialized] public Transform languageSelectDropdown;

    // UI elements
    [NonSerialized] public List<SensorData> sensorDatas = new();
    [NonSerialized] public List<UserInput> userInputs = new();

    // Globally accessed variables
    [NonSerialized] public bool programStarted = false;
    [NonSerialized] public bool doOnSettingsChanged = false;
    [NonSerialized] public float globalBrightnessFactor = 1;
    [NonSerialized] public float timeScale = 1;
    [NonSerialized] public bool isAnySensorSettingsViewActive = false;
    [NonSerialized] public bool programPaused = false;
    [NonSerialized] public bool frameStep = false;
    [NonSerialized] public float totalTimeElapsed = 0;
    [NonSerialized] public int frameCount = 0;
    [NonSerialized] public float clampedDeltaTime = 0;
    [NonSerialized] public float timeSetRandTimer = 0;
    [NonSerialized] public Vector2 Resolution;
    [NonSerialized] public int2 ResolutionInt2;
    [NonSerialized] public readonly float MaxDeltaTime = 1 / 30.0f;
    private const float MinTimeScaleForRunningProgram = 0.01f;
    [NonSerialized] public Vector2 ScreenToViewFactor;
    public event Action OnNewLanguageSelected;

    // Private - Camera
    private Camera uiCam;
    private Vector2 uiViewMin;
    private Vector2 uiViewDims;
    private bool viewTransformInitiated;

    // Private - Animated Texture Scrolling
    private const float ScrollSpeed = 0.5f;
    private float offset;

    // Performance tests
    private const int PerformanceTestFrameLength = 1000;
    private int performanceMisses = 0;

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

        SetResolutionData();
        ScreenToViewFactor = GetScreenToViewFactor();
        SetStaticUIPositions();

        main.StartScript();
        fluidSpawnerManager.StartScript();
        sensorManager.StartScript(main);
    }

    public void Update()
    {
        CheckKeyInputs();

        // Per frame "constants"
        isAnySensorSettingsViewActive = CheckAnySensorSettingsViewActive();
        clampedDeltaTime = Mathf.Min(Time.deltaTime, MaxDeltaTime);

        // Performance report
        CheckPerformance();

        // Rendering
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

    private void CheckPerformance()
    {
        if (frameCount < PerformanceTestFrameLength)
        {
            if (clampedDeltaTime == MaxDeltaTime) performanceMisses++;
            if (++frameCount == PerformanceTestFrameLength)
            {
                int performanceMissesPercent = Mathf.RoundToInt(100 * performanceMisses / (float)PerformanceTestFrameLength);
                int averageFrameRate = Mathf.RoundToInt(PerformanceTestFrameLength / totalTimeElapsed);
                string targetFPSText = (QualitySettings.vSyncCount == 1) ? " (using vSync)" : " (Target: " + main.TargetFrameRate + " FPS).";

                Debug.Log("Performance statistics: Performance misses: " + performanceMissesPercent + "% of frames. Avg FPS: " + averageFrameRate + targetFPSText + " Platform: " + Application.platform + ".");
            }
        }
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
        languageSelectDropdown = GameObject.FindGameObjectWithTag("LanguageSelect").GetComponent<Transform>();
    }

    public void ResetDatas()
    {
        programStarted = true;
        doOnSettingsChanged = false;
        isAnySensorSettingsViewActive = false;
        programPaused = false;
        
        totalTimeElapsed = 0;
        frameCount = 0;
        globalBrightnessFactor = 1;

        performanceMisses = 0;

        sensorDatas = new();
        userInputs = new();
    }

    public void AddSensor(SensorUI sensorUI, Sensor sensor)
    {
        int sensorIndex = sensorDatas.Count;
        sensorUI.sensorIndex = sensorIndex;
        sensorDatas.Add(new SensorData(sensor, sensorUI, sensorUI.gameObject, false));

        sensorUI.OnSettingsViewStatusChanged += (isActive) => SetSensorSettingsViewStatus(sensorIndex, isActive);
    }

    public void AddUserInput(UserInput userInput) => userInputs.Add(userInput);

    public void SetSensorSettingsViewStatus(int sensorIndex, bool isSettingsViewActive)
    {
        sensorDatas[sensorIndex].isSettingsViewActive = isSettingsViewActive;

        // Move to the front by setting the sibling index to be the highest of all sensorUI elements
        if (isSettingsViewActive) sensorDatas[sensorIndex].sensorUIObject.transform.SetSiblingIndex(sensorDatas.Count - 1);
    }

    public bool CheckAnySensorSettingsViewActive()
    {
        foreach (SensorData sensorData in sensorDatas)
        {
            if (sensorData.isSettingsViewActive) return true;
        }
        return false;
    }

    public bool CheckAnyUIElementHovered()
    {
        foreach (SensorData sensorData in sensorDatas)
        {
            if (sensorData.sensorUI.isPointerHovering) return true;
        }
        foreach (UserInput userInput in userInputs)
        {
            if (userInput.pointerHoverArea.CheckIfHovering()) return true;
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
        }
    }

    private Vector2 GetScreenToViewFactor() 
    {
        float boundsAspect = main.BoundaryDims.x / (float)main.BoundaryDims.y;
        float resolutionAspect = Resolution.x / Resolution.y;

        float scaleX;
        float scaleY;
        if (boundsAspect > resolutionAspect)
        {
            // Scale Y down
            scaleY = resolutionAspect / boundsAspect;
            scaleX = 1.0f;
        }
        else
        {
            // Scale X down
            scaleX = boundsAspect / resolutionAspect;
            scaleY = 1.0f;
        }

        return new Vector2(scaleX, scaleY);
    }

    private void SetResolutionData()
    {
        Resolution = new(main.Resolution.x, main.Resolution.y);
        ResolutionInt2 = main.Resolution;
    }

    private void SetStaticUIPositions()
    {
        // Calculate the UI offset
        Vector2 halfResolution = Resolution / 2.0f;
        Vector2 offset = halfResolution - halfResolution * ScreenToViewFactor;
        
        // Apply the offset
        languageSelectDropdown.localPosition = (Vector2)languageSelectDropdown.localPosition - offset;
        foreach (UserInput userInput in userInputs)
        {
            RectTransform rectTransform = userInput.GetComponent<RectTransform>();
            rectTransform.localPosition = (Vector2)rectTransform.localPosition - offset;
        }
    }

    public void SetNewLanguage(int languageIndex)
    {
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[languageIndex];
        TriggerNewLanguageSelected();
    }

    private void TriggerNewLanguageSelected() => OnNewLanguageSelected?.Invoke();
}