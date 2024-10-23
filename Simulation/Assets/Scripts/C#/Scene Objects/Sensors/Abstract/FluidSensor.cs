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
        // Debug.Log(measurementChunkKeys.Count * Mathf.Pow(SampleDensity, 2));
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
                RecordedFluidData sumFluidDatas = new();
                foreach (int chunkKey in measurementChunkKeys)
                {
                    RecordedFluidData fluidData = sensorManager.retrievedFluidDatas[chunkKey];
                    if (fluidData.numContributions > 0) AddRecordedFluidData(ref sumFluidDatas, fluidData);
                }

                RecordedFluidData_Translated translatedSumFluidDatas = new(sumFluidDatas, SampleDensity, main.FloatIntPrecisionP);

                UpdateSensorContents(translatedSumFluidDatas);
            }
        }
    }

    void AddRecordedFluidData(ref RecordedFluidData a, RecordedFluidData b)
    {
        a.totTemp_Int += b.totTemp_Int;
        a.totPressure_Int += b.totPressure_Int;
        a.totVel_Int2 += b.totVel_Int2;
        a.totMass_Int += b.totMass_Int;

        a.numContributions += b.numContributions;
    }
}