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
                sensorUI.SetMeasurement(vel0, numDecimals);
                sensorUI.SetUnit("l.e/s");
                AddSensorDataToGraph(vel0);
                break;

            case VelocityType.Absolute_Summative:
                float vel1 = sumFluidDatas.totVelAbs / sumFluidDatas.numContributions;
                sensorUI.SetMeasurement(vel1, numDecimals);
                sensorUI.SetUnit("l.e/s");
                AddSensorDataToGraph(vel1);
                break;
            
            default:
            Debug.LogWarning("Unknown VelocityType. FluidVelocitySensor: " + this.name);
            break;
        }
    }

    public override void InitSensorTitle()
    {
        sensorUI.SetTitle("Velocity");
    }
}