public class FluidPressureSensor : FluidSensor
{
    public override void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas)
    {
        sensorUI.SetMeasurement(sumFluidDatas.totPressure / sumFluidDatas.numContributions, numDecimals);
        sensorUI.SetUnit("p.u");
    }

    public override void InitSensorTitle()
    {
        sensorUI.SetTitle("Pressure");
    }
}