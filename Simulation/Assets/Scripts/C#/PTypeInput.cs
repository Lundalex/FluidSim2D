using Resources;
using UnityEngine;

public class PTypeInput : MonoBehaviour
{
    public PTypeState[] particleTypeStates;
    private Main m;

    private void OnValidate()
    {
        if (m == null) m = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        m.OnValidate();
    }
    public PType[] GetParticleTypes()
    {
        PType[] particleTypes = new PType[particleTypeStates.Length * 3];

        for (int i = 0; i < particleTypeStates.Length; i++)
        {
            int baseIndex = 3 * i;
            particleTypes[baseIndex] = particleTypeStates[i].solidState;
            particleTypes[baseIndex + 1] = particleTypeStates[i].liquidState;
            particleTypes[baseIndex + 2] = particleTypeStates[i].gasState;
        }

        return particleTypes;
    }

    void Awake()
    {
        if (m == null) m = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();

        particleTypeStates = new PTypeState[6];
        float IR_1 = 2.0f;
        float IR_2 = 2.0f;
        int FSG_1 = 1;
        int FSG_2 = 2;
        particleTypeStates[0] = new PTypeState
        {
            solidState = new PType // Solid
            {
                fluidSpringGroup = 1,

                springPlasticity = 0,
                springTolDeformation = 0.1f,
                springStiffness = 2000,

                thermalConductivity = 1.0f,
                specificHeatCapacity = 10.0f,
                freezeThreshold = Utils.CelsiusToKelvin(0.0f),
                vaporizeThreshold = Utils.CelsiusToKelvin(100.0f),

                pressure = 3000,
                nearPressure = 5,

                mass = 1,
                targetDensity = m.targetDensity,
                damping = m.damping,
                passiveDamping = 0.0f,
                viscosity = 5.0f,
                stickyness = 2.0f,
                gravity = m.gravity,

                influenceRadius = 2,
                matIndex = 0
            },
            liquidState = new PType // Liquid
            {
                fluidSpringGroup = FSG_1,

                springPlasticity = m.Plasticity,
                springTolDeformation = m.TolDeformation,
                springStiffness = m.springStiffness,

                thermalConductivity = 1.0f,
                specificHeatCapacity = 10.0f,
                freezeThreshold = Utils.CelsiusToKelvin(0.0f),
                vaporizeThreshold = Utils.CelsiusToKelvin(100.0f),
                
                pressure = m.PressureMultiplier,
                nearPressure = m.NearPressureMultiplier,

                mass = 1,
                targetDensity = m.targetDensity,
                damping = m.damping,
                passiveDamping = m.passiveDamping,
                viscosity = m.viscosity,
                stickyness = 2.0f,
                gravity = m.gravity,

                influenceRadius = IR_1,
                matIndex = 0
            },
            gasState = new PType // Gas
            {
                fluidSpringGroup = 0,

                springPlasticity = -1,
                springTolDeformation = -1,
                springStiffness = -1,

                thermalConductivity = 3.0f,
                specificHeatCapacity = 10.0f,
                freezeThreshold = Utils.CelsiusToKelvin(0.0f),
                vaporizeThreshold = Utils.CelsiusToKelvin(100.0f),

                pressure = 200,
                nearPressure = 0,

                mass = 0.1f,
                targetDensity = 0,
                damping = m.damping,
                passiveDamping = m.passiveDamping,
                viscosity = m.viscosity,
                stickyness = 2.0f,
                gravity = m.gravity * 0.1f,

                influenceRadius = IR_1,
                matIndex = 0
            }
        };

        particleTypeStates[1] = new PTypeState
        {
            solidState = new PType // Solid
            {
                fluidSpringGroup = FSG_2,

                springPlasticity = m.Plasticity,
                springTolDeformation = m.TolDeformation,
                springStiffness = m.springStiffness,

                thermalConductivity = 7.0f,
                specificHeatCapacity = 15.0f,
                freezeThreshold = Utils.CelsiusToKelvin(999.0f),
                vaporizeThreshold = Utils.CelsiusToKelvin(-999.0f),

                pressure = m.PressureMultiplier,
                nearPressure = m.NearPressureMultiplier,

                mass = 1,
                targetDensity = m.targetDensity * 1.5f,
                damping = m.damping,
                passiveDamping = m.passiveDamping,
                viscosity = m.viscosity,
                stickyness = 4.0f,
                gravity = m.gravity,

                influenceRadius = IR_2,
                matIndex = 0
            },
            liquidState = new PType // Liquid
            {
                fluidSpringGroup = FSG_2,

                springPlasticity = m.Plasticity,
                springTolDeformation = m.TolDeformation,
                springStiffness = m.springStiffness,

                thermalConductivity = 7.0f,
                specificHeatCapacity = 15.0f,
                freezeThreshold = Utils.CelsiusToKelvin(-999.0f),
                vaporizeThreshold = Utils.CelsiusToKelvin(999.0f),

                pressure = m.PressureMultiplier,
                nearPressure = m.NearPressureMultiplier,

                mass = 1,
                targetDensity = m.targetDensity * 1.5f,
                damping = m.damping,
                passiveDamping = m.passiveDamping,
                viscosity = m.viscosity,
                stickyness = 4.0f,
                gravity = m.gravity,

                influenceRadius = IR_2,
                matIndex = 0
            },
            gasState = new PType // Liquid
            {
                fluidSpringGroup = FSG_2,

                springPlasticity = m.Plasticity,
                springTolDeformation = m.TolDeformation,
                springStiffness = m.springStiffness,

                thermalConductivity = 7.0f,
                specificHeatCapacity = 15.0f,
                freezeThreshold = Utils.CelsiusToKelvin(-999.0f),
                vaporizeThreshold = Utils.CelsiusToKelvin(999.0f),

                pressure = m.PressureMultiplier,
                nearPressure = m.NearPressureMultiplier,

                mass = 1,
                targetDensity = m.targetDensity * 1.5f,
                damping = m.damping,
                passiveDamping = m.passiveDamping,
                viscosity = m.viscosity,
                stickyness = 4.0f,
                gravity = m.gravity,

                influenceRadius = IR_2,
                matIndex = 0
            }
        };
    }
}
