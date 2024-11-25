using System;
using Resources2;
using UnityEngine;

public class RigidBodySensor : Sensor
{
    [Header("Sensor Settings")]
    [SerializeField] private RigidBodySensorType rigidBodySensorType;
    [SerializeField] private Vector2 rigidBodyPositionOffset;
    [SerializeField] private bool doInterpolation;
    [Range(1.0f, 20.0f), SerializeField] private float moveSpeed;
    [NonSerialized] public int linkedRBIndex = -1;
    [NonSerialized] public bool firstDataRecieved = false;

    // Private
    private Vector2 currentTargetPosition;

    public override void InitSensor() => sensorUI.SetPosition(SimSpaceToCanvasSpace(new(-Const.LARGE_FLOAT, 0.0f)));

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

        Vector2 vel = Func.Int2ToFloat2(rbData.vel_AsInt2, main.FloatIntPrecisionRB);
        Vector2 pos = (Vector2)rbData.pos + rigidBodyPositionOffset;

        const float minAbsVelocity = 2.0f;

        float value = 0;
        switch (rigidBodySensorType)
        {
            case RigidBodySensorType.Mass:
                value = rbData.mass;
                break;

            case RigidBodySensorType.Velocity:
                value = vel.magnitude;
                if (value < minAbsVelocity) value = 0;
                break;

            case RigidBodySensorType.Velocity_X:
                value = vel.x;
                if (Mathf.Abs(value) < minAbsVelocity) value = 0;
                break;

            case RigidBodySensorType.Velocity_Y:
                value = vel.y;
                if (Mathf.Abs(value) < minAbsVelocity) value = 0;
                break;

            case RigidBodySensorType.RotationalVelocity:
                value = Func.IntToFloat(rbData.rotVel_AsInt, main.FloatIntPrecisionRB);
                break;

            case RigidBodySensorType.Position_X:
                value = pos.x;
                break;

            case RigidBodySensorType.Position_Y:
                value = pos.y;
                break;

            case RigidBodySensorType.SpringForce:
                value = rbData.recordedSpringForce;
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        (string prefix, float displayValue) = GetMagnitudePrefix(value, minPrefixIndex);
        SetSensorUnit(prefix);

        sensorUI.SetMeasurement(displayValue, numDecimals);
        AddSensorDataToGraph(value);
    }

    public override void SetSensorUnit(string prefix = "")
    {
        string baseUnit = "";
        switch (rigidBodySensorType)
        {
            case RigidBodySensorType.Mass:
                baseUnit = "g";
                break;

            case RigidBodySensorType.Velocity:
            case RigidBodySensorType.Velocity_X:
            case RigidBodySensorType.Velocity_Y:
                baseUnit = "l.e/s";
                break;

            case RigidBodySensorType.RotationalVelocity:
                baseUnit = "r/s";
                break;

            case RigidBodySensorType.Position_X:
            case RigidBodySensorType.Position_Y:
                baseUnit = "l.e";
                break;

            case RigidBodySensorType.SpringForce:
                baseUnit = "N";
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        string unit = prefix + baseUnit;

        // If the new baseUnit differs from the previous baseUnit, update the sensor baseUnit
        if (unit != lastUnit)
        {
            sensorUI.SetUnit(baseUnit, unit);
            lastUnit = unit;
        }
    }

    public override void SetSensorTitle()
    {
        switch (rigidBodySensorType)
        {
            case RigidBodySensorType.Mass:
                sensorUI.SetTitle("Massa");
                break;

            case RigidBodySensorType.Velocity:
                sensorUI.SetTitle("Fart");
                break;

            case RigidBodySensorType.Velocity_X:
                sensorUI.SetTitle("Hastighet X");
                break;

            case RigidBodySensorType.Velocity_Y:
                sensorUI.SetTitle("Hastighet Y");
                break;

            case RigidBodySensorType.RotationalVelocity:
                sensorUI.SetTitle("Vinkelhastighet");
                break;

            case RigidBodySensorType.Position_X:
                sensorUI.SetTitle("Position X");
                break;

            case RigidBodySensorType.Position_Y:
                sensorUI.SetTitle("Position Y");
                break;

            case RigidBodySensorType.SpringForce:
                sensorUI.SetTitle("Dragkraft");
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }
    }

    public override void UpdateSensorTypeDropdown()
    {
        int itemIndex = 0;
        switch (rigidBodySensorType)
        {
            case RigidBodySensorType.Mass:
                itemIndex = 0;
                break;

            case RigidBodySensorType.Velocity:
                itemIndex = 1;
                break;

            case RigidBodySensorType.Velocity_X:
                itemIndex = 2;
                break;

            case RigidBodySensorType.Velocity_Y:
                itemIndex = 3;
                break;

            case RigidBodySensorType.RotationalVelocity:
                itemIndex = 4;
                break;

            case RigidBodySensorType.Position_X:
                itemIndex = 5;
                break;

            case RigidBodySensorType.Position_Y:
                itemIndex = 6;
                break;

            case RigidBodySensorType.SpringForce:
                itemIndex = 7;
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
        SetSensorTitle();
    }
}