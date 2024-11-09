public class FluidPressureSensor : FluidSensor
{
    public override void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas)
    {
        float avgPressure = sumFluidDatas.totPressure / sumFluidDatas.numContributions;
        sensorUI.SetMeasurement(avgPressure, numDecimals);
        sensorUI.SetUnit("p.u");
        AddSensorDataToGraph(avgPressure);
    }

    public override void InitSensorTitle()
    {
        sensorUI.SetTitle("Pressure");
    }
}