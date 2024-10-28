public class FluidMassSensor : FluidSensor
{
    public override void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas)
    {
        sensorUI.SetMeasurement(sumFluidDatas.totMass, numDecimals);
        sensorUI.SetUnit("m/s");
    }

    public override void InitSensorTitle()
    {
        sensorUI.SetTitle("Mass");
    }
}