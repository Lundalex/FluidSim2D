using UnityEngine;

public class MultiWeights : Assembly
{
    [Header("WeightsType")]
    public WeightsType weightsType;

    [Header("References")]
    [SerializeField] private ConfigHelper configHelper;

    [Header("References - User Inputs")]
    [SerializeField] private UserSelectorInput userSelectorInput;

    // Private static
    private static WeightsType storedWeightsType;
    private static bool dataHasBeenStored = false;

    private void OnEnable()
    {
        ProgramManager.Instance.OnPreStart += AssemblyUpdate;
        RetrieveData();
    }

    private void OnDestroy()
    {
        StoreData();
        ProgramManager.Instance.OnPreStart -= AssemblyUpdate;
    }

    private void StoreData()
    {
        dataHasBeenStored = true;
        storedWeightsType = weightsType;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;
        weightsType = storedWeightsType;
    }

    public override void AssemblyUpdate()
    {
        if (configHelper == null) return;

        // Set the config
        switch (weightsType)
        {
            case WeightsType.Potatoes:
                configHelper.SetActiveConfigByName("Obstacles", "None");
                break;

            default:
                Debug.LogWarning("ObstaclesType '" + weightsType + "' not recognized. MultiObstacles: " + this.name);
                break;
        }

        if (userSelectorInput != null) userSelectorInput.SetSelectorIndex((int)weightsType);
    }
}