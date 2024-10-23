public class SpringDynamometer : RigidBodySensor
{
    public override void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex)
    {
        RBData rbData = rBDatas[linkedRBIndex];
        sensorText.text = FloatToStr(rbData.recordedSpringForce, numDecimals);
    }
}