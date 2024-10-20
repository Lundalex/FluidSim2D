using Unity.Mathematics;
using Resources;
public class VelocitySensor : RigidBodySensor
{
    public override void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex)
    {
        RBData rbData = rBDatas[linkedRBIndex];
        float2 vel = Func.Int2ToFloat2(rbData.vel_AsInt2);
        sensorText.text = "X: " + FloatToStr(vel.x, numDecimals) + ", Y: " + FloatToStr(vel.y, numDecimals);
    }
}