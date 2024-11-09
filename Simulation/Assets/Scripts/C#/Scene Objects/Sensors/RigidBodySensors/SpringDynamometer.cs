public class SpringDynamometer : RigidBodySensor
{
    public override void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex)
    {
        RBData rbData = rBDatas[linkedRBIndex];
        float springForce = rbData.recordedSpringForce;
        sensorUI.SetMeasurement(springForce, numDecimals);
        sensorUI.SetUnit("f.u");
        AddSensorDataToGraph(springForce);
    }

    public override void InitSensorTitle()
    {
        sensorUI.SetTitle("Spring");
    }
}