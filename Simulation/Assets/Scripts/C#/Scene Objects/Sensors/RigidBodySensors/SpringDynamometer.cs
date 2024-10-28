public class SpringDynamometer : RigidBodySensor
{
    public override void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex)
    {
        RBData rbData = rBDatas[linkedRBIndex];
        sensorUI.SetMeasurement(rbData.recordedSpringForce, numDecimals);
        sensorUI.SetUnit("f.u");
    }

    public override void InitSensorTitle()
    {
        sensorUI.SetTitle("Spring Force");
    }
}