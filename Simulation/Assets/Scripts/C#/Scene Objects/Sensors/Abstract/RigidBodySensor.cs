using UnityEngine;

public abstract class RigidBodySensor : Sensor
{
    public abstract void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex);

    public override void UpdateSensor()
    {
        if (sensorText != null)
        {
            if (linkedRBIndex == -1) Debug.LogWarning("Sensor not linked to any rigid body; It will not be updated");
            else
            {
                RBData[] retrievedRBDatas = sensorManager.retrievedRBDatas;
                RBData rbData = retrievedRBDatas[linkedRBIndex];
                targetPosition = rbData.pos + offset;

                // Init sensor UI position
                if (!firstDataRecieved) sensorUIRect.localPosition = SimSpaceToCanvasSpace(targetPosition);
                firstDataRecieved = true;

                UpdateSensorContents(retrievedRBDatas, linkedRBIndex);
            }
        }
    }
}