using UnityEngine;

public class PhysicalPendulum : Assembly
{
    [Header("Parent")]
    public bool isIndependent;

    [Header("Pendulum Weight")]
    public float pendulumLength = 50f;
    public float pendulumMass = 1000f;
    public float pendulumGravity = 9.82f;

    [Header("References")]
    [SerializeField] private SceneRigidBody weightObject;
    [SerializeField] private UserSliderInput userSliderInput;

    // Private static
    private static float storedPendulumLength;
    private static bool dataHasBeenStored = false;

    private void OnEnable()
    {
        if (isIndependent)
        {
            ProgramManager.Instance.OnPreStart += AssemblyUpdate;
            RetrieveData();
        }
    }

    private void OnDestroy()
    {
        if (isIndependent)
        {
            StoreData();
        }
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

    public override void AssemblyUpdate()
    {
        if (weightObject == null)
        {
            Debug.LogWarning("All references are not set. PhysicalPendulum: " + this.name);
            return;
        }

        if (userSliderInput != null) userSliderInput.startValue = pendulumLength;

        weightObject.transform.localPosition = new(150, 160 - pendulumLength);
        weightObject.rbInput.mass = pendulumMass;
        weightObject.rbInput.gravity = pendulumGravity;
    }
}