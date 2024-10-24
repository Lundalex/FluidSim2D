using Resources2;
using UnityEngine;

public class FluidVelocitySensor : FluidSensor
{
    public VelocityType velocityType;
    public override void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas)
    {
        switch (velocityType)
        {
            case VelocityType.Absolute_Destructive:
                float vel0 = Func.Magnitude(sumFluidDatas.totVelComponents) / sumFluidDatas.numContributions;
                sensorText.text = FloatToStr(vel0, numDecimals) + " l.e/s";
                break;

            case VelocityType.Absolute_Summative:
                float vel1 = sumFluidDatas.totVelAbs / sumFluidDatas.numContributions;
                sensorText.text = FloatToStr(vel1, numDecimals) + " l.e/s";
                break;

            case VelocityType.ComponentWise:
                Vector2 vel2 = sumFluidDatas.totVelComponents / sumFluidDatas.numContributions;
                sensorText.text = FloatToStr(vel2, numDecimals) + " l.e/s";
                break;
            
            default:
            Debug.LogWarning("Unknown VelocityType. FluidVelocitySensor: " + this.name);
            break;
        }
    }
}