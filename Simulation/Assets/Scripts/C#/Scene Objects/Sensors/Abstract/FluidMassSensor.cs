using UnityEngine;

public class FluidMassSensor : FluidSensor
{
    public override void UpdateSensorContents(RecordedFluidData[] retrievedFluidData, Rect measurementZone)
    {
        sensorText.text = "X: " + FloatToStr(measurementZone.width, numDecimals) + ", Y: " + FloatToStr(measurementZone.height, numDecimals);
    }
}