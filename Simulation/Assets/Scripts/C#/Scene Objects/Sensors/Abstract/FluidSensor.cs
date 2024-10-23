using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public abstract class FluidSensor : Sensor
{
    [Range(1, 20)] public int SampleDensity;
    public PositionType positionType;
    public Vector2 targetPosition;
    public Rect measurementZone;
    private List<int> measurementChunkKeys;
    private float sampleDensityCorrection;
    public abstract void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas);

    int GetChunkKey(int x, int y) => x + y * main.BoundaryDims.x;

    private void InitMeasurementChunkKeys()
    {
        int2 chunksNum = main.ChunksNum;
        float maxInfluenceRadius = main.MaxInfluenceRadius;

        int minX = Mathf.Max(Mathf.RoundToInt(measurementZone.min.x / maxInfluenceRadius), 0);
        int minY = Mathf.Max(Mathf.RoundToInt(measurementZone.min.y / maxInfluenceRadius), 0);
        int maxX = Mathf.Min(Mathf.RoundToInt(measurementZone.max.x / maxInfluenceRadius), chunksNum.x);
        int maxY = Mathf.Min(Mathf.RoundToInt(measurementZone.max.y / maxInfluenceRadius), chunksNum.y);
        
        measurementChunkKeys = new();
        for (int x = minX; x < maxX; x += SampleDensity)
        {
            for (int y = minY; y < maxY; y += SampleDensity)
            {
                measurementChunkKeys.Add(GetChunkKey(x, y));
            }
        }
        sampleDensityCorrection = (maxX - minX) * (maxY - minY) / (float)measurementChunkKeys.Count;
    }

    private void OnValidate()
    {
        if (programStarted) InitMeasurementChunkKeys();
    }

    public override void InitSensor()
    {
        UpdatePosition();
        InitMeasurementChunkKeys();
    }

    public override void UpdatePosition()
    {
        if (positionType == PositionType.Relative)
        {
            Vector2 relativeTargetPosition = measurementZone.center + new Vector2(0, measurementZone.height * 0.5f) + targetPosition;
            sensorUIRect.localPosition = SimSpaceToCanvasSpace(relativeTargetPosition);
        }
        else sensorUIRect.localPosition = SimSpaceToCanvasSpace(targetPosition);
    }

    public override void UpdateSensor()
    {
        if (sensorText != null)
        {
            if (measurementZone.height == 0.0f && measurementZone.width == 0.0f) Debug.Log("Measurement zone has no width or height. It will not be updated. FluidSensor: " + this.name);
            else
            {
                RecordedFluidData_Translated sumFluidDatas = new();
                foreach (int chunkKey in measurementChunkKeys)
                {
                    // The velAbs calculation is a conservative estimate. The estimation accuracy becomes higher the fewer particles with differing velocities there are in each chunk
                    RecordedFluidData_Translated fluidData = new(sensorManager.retrievedFluidDatas[chunkKey], sampleDensityCorrection, main.FloatIntPrecisionP);
                    if (fluidData.numContributions > 0) AddRecordedFluidData(ref sumFluidDatas, fluidData);
                }
                
                UpdateSensorContents(sumFluidDatas);
            }
        }
    }

    void AddRecordedFluidData(ref RecordedFluidData_Translated a, RecordedFluidData_Translated b)
    {
        a.totTemp += b.totTemp;
        a.totPressure += b.totPressure;
        a.totVelComponents += b.totVelComponents;
        a.totVelAbs += b.totVelAbs;
        a.totMass += b.totMass;

        a.numContributions += b.numContributions;
    }
}