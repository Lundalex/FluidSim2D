using UnityEngine;

public class Pendulum : Assembly
{
    [Header("Pendulum Length")]
    public float pendulumLength = 40f;

    [Header("References")]
    [SerializeField] private SceneRigidBody rodObject;
    [SerializeField] private SceneRigidBody weightObject;
    [SerializeField] private UserSliderInput pendulumLengthInput;

    // Private static
    private static float storedPendulumLength;
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

        storedPendulumLength = pendulumLength;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;

        pendulumLength = storedPendulumLength;
    }

    #if UNITY_EDITOR
        private void OnValidate() => AssemblyUpdate();

        public override void OnEditorUpdate() => AssemblyUpdate();
    #endif

    public override void AssemblyUpdate()
    {
        if (rodObject == null || weightObject == null)
        {
            Debug.LogWarning("All references are not set. Pendulum: " + this.name);
            return;
        }

        if (pendulumLengthInput != null) pendulumLengthInput.startingValue = pendulumLength;
    }
}