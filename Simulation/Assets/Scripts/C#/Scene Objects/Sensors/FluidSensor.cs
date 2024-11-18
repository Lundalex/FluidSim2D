using System;
using System.Collections.Generic;
using Resources2;
using Unity.Mathematics;
using UnityEngine;
using PM = ProgramManager;

public class FluidSensor : Sensor
{
    [SerializeField] private FluidSensorType fluidSensorType;
    public Color lineColor;
    public Color areaColor;
    public Rect measurementZone;
    [SerializeField] private float patternModulo;
    [Range(1, 20), SerializeField] private int SampleDensity;
    private List<int> measurementChunkKeys;
    private float sampleDensityCorrection;

    int GetChunkKey(int x, int y) => x + y * main.ChunksNum.x;

    private void InitMeasurementChunkKeys()
    {
        if (main == null) return;
        int2 chunksNum = main.ChunksNum;
        float maxInfluenceRadius = main.MaxInfluenceRadius;

        int minX = Mathf.Max(Mathf.FloorToInt(measurementZone.min.x / maxInfluenceRadius), 0);
        int minY = Mathf.Max(Mathf.FloorToInt(measurementZone.min.y / maxInfluenceRadius), 0);
        int maxX = Mathf.Min(Mathf.CeilToInt(measurementZone.max.x / maxInfluenceRadius), chunksNum.x);
        int maxY = Mathf.Min(Mathf.CeilToInt(measurementZone.max.y / maxInfluenceRadius), chunksNum.y);
        
        measurementChunkKeys = new();
        for (int x = minX; x <= maxX; x += SampleDensity)
        {
            for (int y = minY; y <= maxY; y += SampleDensity)
            {
                if (0 <= x && x < chunksNum.x && 0 <= y && y < chunksNum.y) measurementChunkKeys.Add(GetChunkKey(x, y));
            }
        }
        sampleDensityCorrection = (maxX - minX) * (maxY - minY) / (float)measurementChunkKeys.Count;
    }

    private void OnValidate()
    {
        if (PM.Instance.programStarted) InitMeasurementChunkKeys();
    }

    public override void InitSensor()
    {
        UpdatePosition();
        InitMeasurementChunkKeys();
    } 

    public SensorArea GetSensorAreaData()
    {
        return new SensorArea
        {
            min = measurementZone.min,
            max = measurementZone.max,
            patternMod = patternModulo,
            lineColor = new float4(Func.ColorToFloat3(lineColor), lineColor.a),
            colorTint = new float4(Func.ColorToFloat3(areaColor), areaColor.a)
        };
    }

    public override void UpdatePosition()
    {
        if (positionType == PositionType.Relative)
        {
            Vector2 relativeTargetPosition = measurementZone.center + targetPosition;
            sensorUI.SetPosition(SimSpaceToCanvasSpace(relativeTargetPosition));
        }
        else sensorUI.SetPosition(SimSpaceToCanvasSpace(targetPosition));
    }

    public override void UpdateSensor()
    {
        if (sensorUI != null)
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
        a.totThermalEnergy += b.totThermalEnergy;
        a.totPressure += b.totPressure;
        a.totVelComponents += b.totVelComponents;
        a.totVelAbs += b.totVelAbs;
        a.totMass += b.totMass;

        a.numContributions += b.numContributions;
    }

    private void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas)
    {
        float kineticEnergy = sumFluidDatas.totMass * Mathf.Pow(sumFluidDatas.totVelAbs, 2) / 2.0f;
        float thermalEnergy = sumFluidDatas.totThermalEnergy;
        
        float avgTemperature = sumFluidDatas.totTemp / sumFluidDatas.numContributions;

        float value = 0;
        if (sumFluidDatas.numContributions > 0)
        {
            switch (fluidSensorType)
            {
                case FluidSensorType.Energy_Total_Kinetic:
                    value = kineticEnergy;
                    sensorUI.SetUnit("e.u");
                    break;

                case FluidSensorType.Energy_Total_Thermal:
                    value = thermalEnergy;
                    break;

                case FluidSensorType.Energy_Total_Both:
                    value = kineticEnergy + thermalEnergy;
                    break;

                case FluidSensorType.Energy_Average_Kinetic:
                    value = kineticEnergy / sumFluidDatas.numContributions;
                    break;

                case FluidSensorType.Energy_Average_Thermal:
                    value = thermalEnergy / sumFluidDatas.numContributions;
                    break;

                case FluidSensorType.Energy_Average_Both:
                    value = (kineticEnergy + thermalEnergy) / sumFluidDatas.numContributions;
                    break;

                case FluidSensorType.TotalMass:
                    value = sumFluidDatas.totMass;
                    break;

                case FluidSensorType.AveragePressure:
                    value = sumFluidDatas.totPressure / sumFluidDatas.numContributions;
                    break;

                case FluidSensorType.AverageTemperatureCelcius:
                    value = Utils.KelvinToCelcius(avgTemperature);
                    break;

                case FluidSensorType.AverageTemperatureKelvin:
                    value = avgTemperature;
                    break;

                case FluidSensorType.Velocity_Absolute_Destructive:
                    value = Func.Magnitude(sumFluidDatas.totVelComponents) / sumFluidDatas.numContributions;
                    break;

                case FluidSensorType.Velocity_Absolute_Summative:
                    value = sumFluidDatas.totVelAbs / sumFluidDatas.numContributions;
                    break;

                default:
                    Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                    break;
            }
        }

        (string prefix, float displayValue) = GetMagnitudePrefix(value);
        SetSensorUnit(prefix);

        sensorUI.SetMeasurement(displayValue, numDecimals);
        AddSensorDataToGraph(value);
    }

    public override void SetSensorUnit(string prefix = "")
    {
        string unit = prefix;
        switch (fluidSensorType)
        {
            case FluidSensorType.Energy_Total_Kinetic:
            case FluidSensorType.Energy_Total_Thermal:
            case FluidSensorType.Energy_Total_Both:
            case FluidSensorType.Energy_Average_Kinetic:
            case FluidSensorType.Energy_Average_Thermal:
            case FluidSensorType.Energy_Average_Both:
                unit += "J";
                break;

            case FluidSensorType.TotalMass:
                unit += "g"; // all particle / rb masses are measured in the base unit of "grams"
                break;

            case FluidSensorType.AveragePressure:
                unit += "Pa";
                break;

            case FluidSensorType.AverageTemperatureCelcius:
                unit += "°C";
                break;

            case FluidSensorType.AverageTemperatureKelvin:
                unit += "°K";
                break;

            case FluidSensorType.Velocity_Absolute_Destructive:
            case FluidSensorType.Velocity_Absolute_Summative:
                unit += "m/s";
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        // If the new unit differs from the previous unit, update the sensor unit
        if (unit != lastUnit)
        {
            sensorUI.SetUnit(unit);
            lastUnit = unit;
        }
    }

    public override void SetSensorTitle()
    {
        switch (fluidSensorType)
        {
            case FluidSensorType.Energy_Total_Kinetic:
            case FluidSensorType.Energy_Total_Thermal:
            case FluidSensorType.Energy_Total_Both:
            case FluidSensorType.Energy_Average_Kinetic:
            case FluidSensorType.Energy_Average_Thermal:
            case FluidSensorType.Energy_Average_Both:
                sensorUI.SetTitle("Energy");
                break;

            case FluidSensorType.TotalMass:
                sensorUI.SetTitle("Mass");
                break;

            case FluidSensorType.AveragePressure:
                sensorUI.SetTitle("Pressure");
                break;

            case FluidSensorType.AverageTemperatureCelcius:
            case FluidSensorType.AverageTemperatureKelvin:
                sensorUI.SetTitle("Temperature");
                break;

            case FluidSensorType.Velocity_Absolute_Destructive:
            case FluidSensorType.Velocity_Absolute_Summative:
                sensorUI.SetTitle("Velocity");
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }
    }

    public override void InitSensorTypeDropdown()
    {
        int itemIndex = 0;
        switch (fluidSensorType)
        {
            case FluidSensorType.Energy_Total_Kinetic:
                itemIndex = 0;
                break;

            case FluidSensorType.Energy_Total_Thermal:
                itemIndex = 1;
                break;

            case FluidSensorType.Energy_Total_Both:
                itemIndex = 2;
                break;

            case FluidSensorType.Energy_Average_Kinetic:
                itemIndex = 3;
                break;

            case FluidSensorType.Energy_Average_Thermal:
                itemIndex = 4;
                break;

            case FluidSensorType.Energy_Average_Both:
                itemIndex = 5;
                break;

            case FluidSensorType.TotalMass:
                itemIndex = 6;
                break;

            case FluidSensorType.AveragePressure:
                itemIndex = 7;
                break;

            case FluidSensorType.AverageTemperatureCelcius:
                itemIndex = 8;
                break;

            case FluidSensorType.AverageTemperatureKelvin:
                itemIndex = 9;
                break;

            case FluidSensorType.Velocity_Absolute_Destructive:
                itemIndex = 10;
                break;

            case FluidSensorType.Velocity_Absolute_Summative:
                itemIndex = 11;
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        // sensorUI.rigidBodySensorTypeSelect.selectedItemIndex = itemIndex;
    }

    public void SetFluidSensorType(FluidSensorType fluidSensorType)
    {
        this.fluidSensorType = fluidSensorType;
        SetSensorTitle();
        SetSensorUnit();
    }
}