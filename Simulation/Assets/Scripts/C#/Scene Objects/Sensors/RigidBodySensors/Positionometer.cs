using UnityEngine;
public class PositionSensor : RigidBodySensor
{
    public override void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex)
    {
        Debug.Log("PositionSensor not implemented");
        // RBData rbData = rBDatas[linkedRBIndex];
        // sensorText.text = "X: " + FloatToStr(rbData.pos.x, numDecimals) + ", Y: " + FloatToStr(rbData.pos.y, numDecimals);
    }

    public override void InitSensorTitle()
    {
        sensorUI.SetTitle("Position");
    }
}