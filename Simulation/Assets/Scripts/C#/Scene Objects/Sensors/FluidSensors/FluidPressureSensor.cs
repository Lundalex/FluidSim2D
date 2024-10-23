public class FluidPressureSensor : FluidSensor
{
    public override void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas)
    {
        sensorText.text = FloatToStr(sumFluidDatas.totPressure / sumFluidDatas.numContributions, numDecimals) + " p.u";
    }
}