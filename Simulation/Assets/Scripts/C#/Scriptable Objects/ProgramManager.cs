using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ProgramDataAsset", menuName = "ProgramData")]
public class ProgramManager : ScriptableObject
{
    // References
    [HideInInspector] public Main main;

    // Sensors
    [HideInInspector] public List<SensorData> sensorDatas = new();

    // Script
    [HideInInspector] public bool programStarted = false;
    [HideInInspector] public bool doOnSettingsChanged;
    [HideInInspector] public float globalBrightnessFactor = 1;
    [HideInInspector] public float timeScale = 1;
    [HideInInspector] public bool isAnySensorSettingsViewActive;
    [HideInInspector] public bool programPaused = false;

    public void Start()
    {
        main.ScriptStart();

        programStarted = true;
        programPaused = false;
    }

    public void Update()
    {
        isAnySensorSettingsViewActive = CheckIfAnySensorSettingsViewActive();

        float clampedDeltaTime = Mathf.Min(Time.deltaTime, 1 / 30.0f);
        LerpGlobalBrightness(clampedDeltaTime);
        LerpTimeScale(clampedDeltaTime);
        LerpSensorUIScale(clampedDeltaTime);

        if (doOnSettingsChanged && programStarted)
        {
            main.OnSettingsChanged();
            doOnSettingsChanged = false;
        }

        float minTimeScale = 0.01f;
        if (timeScale > minTimeScale) main.ScriptUpdate();

        foreach (SensorData sensorData in sensorDatas) sensorData.sensor.UpdateScript();
    }

    public void AddSensor(ref SensorUI sensorUI, Sensor sensor)
    {
        sensorUI.sensorIndex = sensorDatas.Count;
        sensorDatas.Add(new SensorData(sensor, sensorUI.gameObject, false));
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

    private void LerpGlobalBrightness(float deltaTime)
    {
        float target = 1f - main.SettingsViewDarkTintPercent * (isAnySensorSettingsViewActive ? 1f : 0f);
        globalBrightnessFactor = Mathf.Lerp(globalBrightnessFactor, target, deltaTime * main.GlobalBrightnessChangeSpeed);
    }

    private void LerpTimeScale(float deltaTime)
    {
        float target = isAnySensorSettingsViewActive ? 0f : 1f;
        timeScale = Mathf.Lerp(timeScale, target, deltaTime * main.GlobalBrightnessChangeSpeed);
    }

    private void LerpSensorUIScale(float deltaTime)
    {
        foreach (SensorData sensorData in sensorDatas)
        {
            float targetScaleValue = sensorData.isSettingsViewActive ? 1.2f : 0.6f;
            Vector3 currentScale = sensorData.sensorUI.transform.localScale;
            Vector3 targetScale = Vector3.one * targetScaleValue;

            Vector3 newScale = Vector3.Lerp(currentScale, targetScale, deltaTime * main.GlobalBrightnessChangeSpeed);
            sensorData.sensorUI.transform.localScale = newScale;
            // Manage the draw order
            sensorData.sensorUI.transform.SetSiblingIndex(sensorData.isSettingsViewActive ? sensorDatas.Count - 1 : 0);
        }
    }
}