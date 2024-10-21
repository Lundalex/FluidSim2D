using UnityEngine;

public abstract class FluidSensor : Sensor
{
    public abstract void UpdateSensorContents();

    public override void UpdateSensor()
    {
        if (sensorText != null)
        {
            if (linkedRBIndex == -1) Debug.LogWarning("Sensor not linked to any rigid body; It will not be updated");
            else
            {
                UpdateSensorContents();
            }
        }
    }
}