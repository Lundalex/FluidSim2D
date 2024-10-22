using System;
using Resources;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public struct RecordedFluidData_Translated
{
    public float totTemp;
    public float totPressure;
    public float2 totVel;
    public float totMass;

    public int numContributions;

    public RecordedFluidData_Translated(RecordedFluidData recordedFluidData, int sampleDensity)
    {
        // Translate from stored integer values to floating point, and correct the results with respect to the sampleDensity
        float sampleDensityCorrection = Mathf.Pow(sampleDensity, 2);
        
        this.totTemp = Func.IntToFloat(recordedFluidData.totTemp_Int) * sampleDensityCorrection;
        this.totPressure = Func.IntToFloat(recordedFluidData.totPressure_Int) * sampleDensityCorrection;
        this.totVel = Func.Int2ToFloat2(recordedFluidData.totVel_Int2) * sampleDensityCorrection;
        this.totMass = Func.IntToFloat(recordedFluidData.totMass_Int) * sampleDensityCorrection;
        this.numContributions = Mathf.RoundToInt(recordedFluidData.numContributions * sampleDensityCorrection);
    }
};