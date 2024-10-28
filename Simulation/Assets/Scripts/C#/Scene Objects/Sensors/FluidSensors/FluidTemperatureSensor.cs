using Resources2;

public class FluidTemperatureSensor : FluidSensor
{
    public override void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas)
    {
        sensorUI.SetMeasurement(Utils.KelvinToCelcius(sumFluidDatas.totTemp / sumFluidDatas.numContributions), numDecimals);
        sensorUI.SetUnit("°C");
    }

    public override void InitSensorTitle()
    {
        sensorUI.SetTitle("Temperature");
    }
}