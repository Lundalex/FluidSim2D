using Resources;

public class FluidVelocitySensor : FluidSensor
{
    public VelocityType velocityType;
    public override void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas)
    {
        switch (velocityType)
        {
            
            default:
            break
        }


        var vel = velocityType switch
        {
            VelocityType.Absolute_Destructive => Func.Magnitude(sumFluidDatas.totVelComponents) / sumFluidDatas.numContributions,
            VelocityType.Absolute_Summative => sumFluidDatas.totVelAbs / sumFluidDatas.numContributions,
            VelocityType.ComponentWise => sumFluidDatas.totVelComponents,
            _ => -999,
        };

        sensorText.text = FloatToStr(vel, numDecimals) + " l.e/s";
    }
}