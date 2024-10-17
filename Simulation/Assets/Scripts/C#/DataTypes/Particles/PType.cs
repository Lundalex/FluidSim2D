using System;

[Serializable]
public struct PType
{
    public int fluidSpringGroup;

    public float springPlasticity;
    public float springTolDeformation;
    public float springStiffness;

    public float thermalConductivity;
    public float specificHeatCapacity;
    public float freezeThreshold;
    public float vaporizeThreshold;

    public float pressure;
    public float nearPressure;

    public float mass;
    public float targetDensity;
    public float damping;
    public float passiveDamping;
    public float viscosity;
    public float stickyness;
    public float gravity;

    public float influenceRadius;

    public int matIndex;
};