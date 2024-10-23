public class FluidMassSensor : FluidSensor
{
    public override void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas)
    {
        sensorText.text = FloatToStr(sumFluidDatas.totMass, numDecimals) + " m.e";
    }
}