using Resources2;
using UnityEngine;

public class PTypeInput : MonoBehaviour
{
    public ProgramManager programManager;
    public PTypeState[] particleTypeStates;

    public void OnValidate() => programManager.doOnSettingsChanged = true;
    
    public PType[] GetParticleTypes()
    {
        PType[] particleTypes = new PType[particleTypeStates.Length * 3];

        for (int i = 0; i < particleTypeStates.Length; i++)
        {
            int baseIndex = 3 * i;
            particleTypes[baseIndex] = ConvertTemperatePropertiesToCelcius(particleTypeStates[i].solidState);
            particleTypes[baseIndex + 1] = ConvertTemperatePropertiesToCelcius(particleTypeStates[i].liquidState);
            particleTypes[baseIndex + 2] = ConvertTemperatePropertiesToCelcius(particleTypeStates[i].gasState);
        }

        return particleTypes;
    }

    PType ConvertTemperatePropertiesToCelcius(PType pType)
    {
        pType.freezeThreshold = Utils.CelsiusToKelvin(pType.freezeThreshold);
        pType.vaporizeThreshold = Utils.CelsiusToKelvin(pType.vaporizeThreshold);

        return pType;
    }
}