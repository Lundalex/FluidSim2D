using Resources;
using UnityEngine;
public class VelocitySensor : RigidBodySensor
{
    public override void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex)
    {
        RBData rbData = rBDatas[linkedRBIndex];
        Vector2 vel = Func.Int2ToFloat2(rbData.vel_AsInt2, main.FloatIntPrecisionRB);
        sensorText.text = FloatToStr(vel.magnitude, numDecimals) + "l.e/s";
    }
}