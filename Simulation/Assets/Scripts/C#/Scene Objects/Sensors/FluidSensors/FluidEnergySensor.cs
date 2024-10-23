using UnityEngine;

public class FluidEnergySensor : FluidSensor
{
    public EnergyType energyType;
    public override void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas)
    {
        float energy = 0.0f;

        // Kinetic energy
        if (energyType == EnergyType.Total_Kinetic || energyType == EnergyType.Total_Both || energyType == EnergyType.Average_Kinetic || energyType == EnergyType.Average_Both)
        {
            energy += sumFluidDatas.totMass * Mathf.Pow(sumFluidDatas.totVelAbs, 2) / 2.0f;
        }

        // Thermal energy
        if (energyType == EnergyType.Total_Thermal || energyType == EnergyType.Total_Both || energyType == EnergyType.Average_Thermal || energyType == EnergyType.Average_Both)
        {
            energy += sumFluidDatas.totThermalEnergy;
        }

        // Average
        if (energyType == EnergyType.Average_Kinetic || energyType == EnergyType.Average_Thermal || energyType == EnergyType.Average_Both)
        {
            energy /= sumFluidDatas.numContributions;
        }

        sensorText.text = FloatToStr(energy, numDecimals) + " e.u";
    }
}