using Resources;
using Unity.Mathematics;
using UnityEngine;

public struct RecordedFluidData_Translated
{
    public float totTemp;
    public float totPressure;
    public float2 totVelComponents;
    public float totVelAbs;
    public float totMass;

    public int numContributions;

    public RecordedFluidData_Translated(RecordedFluidData recordedFluidData, float sampleDensityCorrection, float precision)
    {
        // Translate from stored integer values to floating point, and correct the results with respect to the sampleDensity
        this.totTemp = Func.IntToFloat(recordedFluidData.totTemp_Int, precision) * sampleDensityCorrection;
        this.totPressure = Func.IntToFloat(recordedFluidData.totPressure_Int, precision) * sampleDensityCorrection;
        this.totVelComponents = Func.Int2ToFloat2(recordedFluidData.totVel_Int2, precision) * sampleDensityCorrection;
        this.totVelAbs = Mathf.Sqrt(this.totVelComponents.x*this.totVelComponents.x + this.totVelComponents.y*this.totVelComponents.y);
        this.totMass = Func.IntToFloat(recordedFluidData.totMass_Int, precision) * sampleDensityCorrection;
        this.numContributions = Mathf.RoundToInt(recordedFluidData.numContributions * sampleDensityCorrection);
    }
};