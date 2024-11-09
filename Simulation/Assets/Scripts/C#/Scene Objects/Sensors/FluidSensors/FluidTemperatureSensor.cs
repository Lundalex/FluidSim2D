using Resources2;

public class FluidTemperatureSensor : FluidSensor
{
    public override void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas)
    {
        float avgTemperature = Utils.KelvinToCelcius(sumFluidDatas.totTemp / sumFluidDatas.numContributions);
        sensorUI.SetMeasurement(avgTemperature, numDecimals);
        sensorUI.SetUnit("°C");
        AddSensorDataToGraph(avgTemperature);
    }

    public override void InitSensorTitle()
    {
        sensorUI.SetTitle("Temperature");
    }
}