using UnityEngine;

public abstract class FluidSensor : Sensor
{
    public Vector2 targetPosition;
    public Rect measurementZone;
    public abstract void UpdateSensorContents(RecordedFluidData[] retrievedFluidData, Rect measurementZone);

    public override void InitPosition() => UpdatePosition();

    public override void UpdatePosition() => sensorUIRect.localPosition = SimSpaceToCanvasSpace(targetPosition);

    public override void UpdateSensor()
    {
        if (sensorText != null)
        {
            if (measurementZone.height == 0.0f && measurementZone.width == 0.0f) Debug.Log("Measurement zone has no width or height. It will not be updated. FluidSensor: " + this.name);
            else UpdateSensorContents(sensorManager.retrievedFluidDatas, measurementZone);
        }
    }
}