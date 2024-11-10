using System;
using Michsky.MUIP;
using Resources2;
using UnityEngine;

public class RigidBodySensor : Sensor
{
    [SerializeField] private RigidBodySensorType rigidBodySensorType;
    [SerializeField] private bool doInterpolation;
    [Range(1.0f, 20.0f), SerializeField] private float moveSpeed;
    [NonSerialized] public int linkedRBIndex = -1;
    [NonSerialized] public bool firstDataRecieved = false;
    private Vector2 currentTargetPosition;

    public override void InitSensor() => sensorUI.SetPosition(SimSpaceToCanvasSpace(new(-10000.0f, 0.0f)));

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

    private void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex)
    {
        RBData rbData = rBDatas[linkedRBIndex];

        float value = 0;
        switch (rigidBodySensorType)
        {
            case RigidBodySensorType.SpringForce:
                value = rbData.recordedSpringForce;
                break;

            case RigidBodySensorType.Velocity:
                Vector2 vel = Func.Int2ToFloat2(rbData.vel_AsInt2, main.FloatIntPrecisionRB);
                value = vel.magnitude;
                break;

            case RigidBodySensorType.RotationalVelocity:
                value = Func.IntToFloat(rbData.rotVel_AsInt, main.FloatIntPrecisionRB);
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        sensorUI.SetMeasurement(value, numDecimals);
        AddSensorDataToGraph(value);
    }

    public override void InitSensorTitleAndUnit()
    {
        switch (rigidBodySensorType)
        {
            case RigidBodySensorType.SpringForce:
                sensorUI.SetTitle("Drag Force");
                sensorUI.SetUnit("f.u");
                break;

            case RigidBodySensorType.Velocity:
                sensorUI.SetTitle("Velocity");
                sensorUI.SetUnit("v.u");
                break;

            case RigidBodySensorType.RotationalVelocity:
                sensorUI.SetTitle("Rotation");
                sensorUI.SetUnit("r.v.u");
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }
    }

    public override void InitSensorTypeDropdown()
    {
        int itemIndex = 0;
        switch (rigidBodySensorType)
        {
            case RigidBodySensorType.SpringForce:
                itemIndex = 0;
                break;

            case RigidBodySensorType.Velocity:
                itemIndex = 1;
                break;

            case RigidBodySensorType.RotationalVelocity:
                itemIndex = 2;
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        sensorUI.rigidBodySensorTypeSelect.selectedItemIndex = itemIndex;
    }

    public void SetRigidBodySensorType(RigidBodySensorType rigidBodySensorType)
    {
        this.rigidBodySensorType = rigidBodySensorType;
        InitSensorTitleAndUnit();
    }
}