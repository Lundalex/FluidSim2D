using System;
using UnityEngine;

public abstract class RigidBodySensor : Sensor
{
    public bool doInterpolation;
    [Range(1.0f, 20.0f)] public float moveSpeed;
    [NonSerialized] public int linkedRBIndex = -1;
    [NonSerialized] public bool firstDataRecieved = false;
    public abstract void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex);
    public abstract override void InitSensorTitle();
    private Vector2 currentTargetPosition;

    public override void InitSensor()
    {
        sensorUI.SetPosition(SimSpaceToCanvasSpace(new(-10000.0f, 0.0f)));
    }

    public override void UpdatePosition()
    {
        if (firstDataRecieved)
        {
            Vector2 canvasTargetPosition = SimSpaceToCanvasSpace(currentTargetPosition);
            
            // Interpolate the current position
            sensorUI.SetPosition((doInterpolation && positionType == PositionType.Relative) ? Vector2.Lerp(sensorUI.rectTransform.localPosition, canvasTargetPosition, Time.deltaTime * moveSpeed) : canvasTargetPosition);
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
                currentTargetPosition = positionType == PositionType.Relative ? (Vector2)rbData.pos + targetPosition : targetPosition;

                // Init sensor UI position
                if (!firstDataRecieved) sensorUI.SetPosition(SimSpaceToCanvasSpace(targetPosition));
                firstDataRecieved = true;

                UpdateSensorContents(retrievedRBDatas, linkedRBIndex);
            }
        }
    }
}