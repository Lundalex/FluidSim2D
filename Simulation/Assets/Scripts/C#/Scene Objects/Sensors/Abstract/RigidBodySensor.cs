using System;
using UnityEngine;

public abstract class RigidBodySensor : Sensor
{
    public bool doInterpolation;
    [Range(1.0f, 20.0f)] public float moveSpeed;
    public Vector2 offset;
    [NonSerialized] public int linkedRBIndex = -1;
    [NonSerialized] public Vector2 targetPosition;
    [NonSerialized] public bool firstDataRecieved = false;
    public abstract void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex);
    public abstract override void InitSensorTitle();

    public override void InitSensor()
    {
        sensorUI.SetPosition(SimSpaceToCanvasSpace(new(-10000.0f, 0.0f)));
    }

    public override void UpdatePosition()
    {
        if (firstDataRecieved)
        {
            Vector2 canvasTargetPosition = SimSpaceToCanvasSpace(targetPosition);
            
            // Interpolate the current position
            sensorUI.SetPosition(doInterpolation ? Vector2.Lerp(sensorUI.rectTransform.localPosition, canvasTargetPosition, Time.deltaTime * moveSpeed) : canvasTargetPosition);
        }
    }

    public override void UpdateSensor()
    {
        if (sensorUI != null)
        {
            if (linkedRBIndex == -1) Debug.LogWarning("Sensor not linked to any rigid body; It will not be updated. RigidBodySensor: " + this.name);
            else
            {
                RBData[] retrievedRBDatas = sensorManager.retrievedRBDatas;
                RBData rbData = retrievedRBDatas[linkedRBIndex];
                targetPosition = (Vector2)rbData.pos + offset;

                // Init sensor UI position
                if (!firstDataRecieved) sensorUI.SetPosition(SimSpaceToCanvasSpace(targetPosition));
                firstDataRecieved = true;

                UpdateSensorContents(retrievedRBDatas, linkedRBIndex);
            }
        }
    }
}