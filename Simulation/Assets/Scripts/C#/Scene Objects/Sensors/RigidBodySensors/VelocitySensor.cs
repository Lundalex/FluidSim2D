using Resources2;
using UnityEngine;
public class VelocitySensor : RigidBodySensor
{
    public override void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex)
    {
        RBData rbData = rBDatas[linkedRBIndex];
        Vector2 vel = Func.Int2ToFloat2(rbData.vel_AsInt2, main.FloatIntPrecisionRB);
        float velMgn = vel.magnitude;
        sensorUI.SetMeasurement(velMgn, numDecimals);
        sensorUI.SetUnit("l.e/s");
        AddSensorDataToGraph(velMgn);
    }

    public override void InitSensorTitle()
    {
        sensorUI.SetTitle("Velocity");
    }
}