using Resources;

public class FluidTemperatureSensor : FluidSensor
{
    public override void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas)
    {
        sensorText.text = FloatToStr(Utils.KelvinToCelcius(sumFluidDatas.totTemp / sumFluidDatas.numContributions), numDecimals) + "°C";
    }
}