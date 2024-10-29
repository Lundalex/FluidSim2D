using UnityEngine;

public class SensorData
{
    public Sensor sensor;
    public GameObject sensorUI;
    public bool isSettingsViewActive;

    public SensorData(Sensor sensor, GameObject sensorUI, bool isSettingsViewActive)
    {
        this.sensor = sensor;
        this.sensorUI = sensorUI;
        this.isSettingsViewActive = isSettingsViewActive;
    }
}