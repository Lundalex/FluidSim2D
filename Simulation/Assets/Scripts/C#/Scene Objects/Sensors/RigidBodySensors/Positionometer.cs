public class PositionSensor : RigidBodySensor
{
    public override void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex)
    {
        RBData rbData = rBDatas[linkedRBIndex];
        sensorText.text = "X: " + FloatToStr(rbData.pos.x, numDecimals) + ", Y: " + FloatToStr(rbData.pos.y, numDecimals);
    }
}