using System;
using Resources2;
using Unity.Mathematics;
using UnityEngine;
using PM = ProgramManager;
using Debug = UnityEngine.Debug;

public class FluidSensor : Sensor
{
    [SerializeField] private FluidSensorType fluidSensorType;
    public Color lineColor;
    public Color areaColor;
    public Rect measurementZone;
    [SerializeField] private float patternModulo;
    [Range(1, 20), SerializeField] private int SampleSpacing;

    private int minX, maxX, minY, maxY;
    private float sampleDensityCorrection;
    private int2 chunksNum;
    private float maxInfluenceRadius;

    int GetChunkKey(int x, int y) => x + y * main.ChunksNum.x;

    private void OnValidate()
    {
        if (PM.Instance.programStarted) InitializeMeasurementParameters();
    }

    public override void InitSensor(Vector2 _)
    {
        UpdatePosition();
        InitializeMeasurementParameters();
    }

    private void InitializeMeasurementParameters()
    {
        if (main == null) return;
        chunksNum = main.ChunksNum;
        maxInfluenceRadius = main.MaxInfluenceRadius;

        minX = Mathf.Max(Mathf.FloorToInt(measurementZone.min.x / maxInfluenceRadius), 0);
        minY = Mathf.Max(Mathf.FloorToInt(measurementZone.min.y / maxInfluenceRadius), 0);
        maxX = Mathf.Min(Mathf.CeilToInt(measurementZone.max.x / maxInfluenceRadius), chunksNum.x - 1);
        maxY = Mathf.Min(Mathf.CeilToInt(measurementZone.max.y / maxInfluenceRadius), chunksNum.y - 1);

        int numX = ((maxX - minX) / SampleSpacing) + 1;
        int numY = ((maxY - minY) / SampleSpacing) + 1;
        int numberOfIterations = numX * numY;

        if (numberOfIterations > 0)
            sampleDensityCorrection = (maxX - minX) * (maxY - minY) / (float)numberOfIterations;
        else
            sampleDensityCorrection = 1.0f;
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
            lastJointPos = measurementZone.center;
            Vector2 relativelocalTargetPos = lastJointPos + localTargetPos;
            sensorUI.SetPosition(SimSpaceToCanvasSpace(relativelocalTargetPos));
        }
        else sensorUI.SetPosition(SimSpaceToCanvasSpace(localTargetPos));
    }

    public override void UpdateSensor()
    {
        if (sensorUI != null)
        {
            if (measurementZone.height == 0.0f && measurementZone.width == 0.0f)
                Debug.Log("Measurement zone has no width or height. It will not be updated. FluidSensor: " + this.name);
            else
            {
                // Collect all data constributions
                int numContributions = 0;
                RecordedFluidData_Translated sumFluidDatas = new();
                for (int x = minX; x <= maxX; x += SampleSpacing)
                {
                    for (int y = minY; y <= maxY; y += SampleSpacing)
                    {
                        if (0 <= x && x < chunksNum.x && 0 <= y && y < chunksNum.y)
                        {
                            int chunkKey = GetChunkKey(x, y);
                            RecordedFluidData_Translated fluidData = new(sensorManager.retrievedFluidDatas[chunkKey], sampleDensityCorrection, main.FloatIntPrecisionP);
                            if (fluidData.numContributions > 0)
                            {
                                AddRecordedFluidData(ref sumFluidDatas, fluidData);
                                numContributions += fluidData.numContributions;
                            }
                        }
                    }
                }

                sumFluidDatas.numContributions = numContributions;
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
                case FluidSensorType.Liquid_Depth:
                    value = 423;
                    break;

                case FluidSensorType.Energy_Total_Kinetic:
                    value = kineticEnergy;
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

        value += valueOffset;

        (string prefix, float displayValue) = GetMagnitudePrefix(value, minPrefixIndex);
        SetSensorUnit(prefix);

        sensorUI.SetMeasurement(displayValue, numDecimals);
        AddSensorDataToGraph(value);
    }

    public override void SetSensorUnit(string prefix = "")
    {
        string baseUnit = prefix;
        switch (fluidSensorType)
        {
            case FluidSensorType.Liquid_Depth:
                baseUnit = "m";
                break;

            case FluidSensorType.Energy_Total_Kinetic:
            case FluidSensorType.Energy_Total_Thermal:
            case FluidSensorType.Energy_Total_Both:
            case FluidSensorType.Energy_Average_Kinetic:
            case FluidSensorType.Energy_Average_Thermal:
            case FluidSensorType.Energy_Average_Both:
                baseUnit = "J";
                break;

            case FluidSensorType.TotalMass:
                baseUnit = "kg";
                break;

            case FluidSensorType.AveragePressure:
                baseUnit = "Pa";
                break;

            case FluidSensorType.AverageTemperatureCelcius:
                baseUnit = "°C";
                break;

            case FluidSensorType.AverageTemperatureKelvin:
                baseUnit = "°K";
                break;

            case FluidSensorType.Velocity_Absolute_Destructive:
            case FluidSensorType.Velocity_Absolute_Summative:
                baseUnit = "m/s";
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        string unit = prefix + baseUnit;

        ApplyUnitExceptions(ref unit);

        if (unit != lastUnit)
        {
            sensorUI.SetUnit(baseUnit, unit);
            lastUnit = unit;
        }
    }

    private void ApplyUnitExceptions(ref string unit)
    {
        switch (unit)
        {
            case "mkg":
                unit = "g";
                break;
            
            default:
                break;
        }
    }

    public override void SetSensorTitle()
    {
        string title = "NoTitleSet";
        switch (fluidSensorType)
        {
            case FluidSensorType.Liquid_Depth:
                title = "Djup";
                break;

            case FluidSensorType.Energy_Total_Kinetic:
            case FluidSensorType.Energy_Average_Kinetic:
                title = "K. Energi";
                break;

            case FluidSensorType.Energy_Total_Thermal:
            case FluidSensorType.Energy_Average_Thermal:
                title = "T. Energi";
                break;

            case FluidSensorType.Energy_Total_Both:
            case FluidSensorType.Energy_Average_Both:
                title = "Energi";
                break;

            case FluidSensorType.TotalMass:
                title = "Massa";
                break;

            case FluidSensorType.AveragePressure:
                title = "Tryck";
                break;

            case FluidSensorType.AverageTemperatureCelcius:
            case FluidSensorType.AverageTemperatureKelvin:
                title = "Temperatur";
                break;

            case FluidSensorType.Velocity_Absolute_Destructive:
            case FluidSensorType.Velocity_Absolute_Summative:
                title = "Hastighet";
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        sensorUI.SetTitle(title);
    }

    public override void UpdateSensorTypeDropdown()
    {
        int itemIndex = 0;
        switch (fluidSensorType)
        {
            case FluidSensorType.Liquid_Depth:
                itemIndex = 0;
                break;

            case FluidSensorType.Energy_Total_Kinetic:
                itemIndex = 1;
                break;

            case FluidSensorType.Energy_Total_Thermal:
                itemIndex = 2;
                break;

            case FluidSensorType.Energy_Total_Both:
                itemIndex = 3;
                break;

            case FluidSensorType.Energy_Average_Kinetic:
                itemIndex = 4;
                break;

            case FluidSensorType.Energy_Average_Thermal:
                itemIndex = 5;
                break;

            case FluidSensorType.Energy_Average_Both:
                itemIndex = 6;
                break;

            case FluidSensorType.TotalMass:
                itemIndex = 7;
                break;

            case FluidSensorType.AveragePressure:
                itemIndex = 8;
                break;

            case FluidSensorType.AverageTemperatureCelcius:
                itemIndex = 9;
                break;

            case FluidSensorType.AverageTemperatureKelvin:
                itemIndex = 10;
                break;

            case FluidSensorType.Velocity_Absolute_Destructive:
                itemIndex = 11;
                break;

            case FluidSensorType.Velocity_Absolute_Summative:
                itemIndex = 12;
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        sensorUI.fluidSensorTypeSelect.selectedItemIndex = itemIndex;
    }

    public void SetFluidSensorType(FluidSensorType fluidSensorType)
    {
        this.fluidSensorType = fluidSensorType;
        SetSensorTitle();
        SetSensorUnit();
    }
}
