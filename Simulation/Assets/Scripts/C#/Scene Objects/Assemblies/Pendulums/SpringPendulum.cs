using UnityEngine;

public class SpringPendulum : Assembly
{
    [Header("Parent")]
    public bool isIndependent;

    [Header("Pendulum Weight")]
    public float pendulumLength = 50f;
    public float mass = 1000f;
    public float gravity = 9.82f;
    public float springStiffness = 1000f;
    public float springDamping = 10f;

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
        if (!isIndependent) return;

        if (weightObject == null)
        {
            Debug.LogWarning("All references are not set. SpringPendulum: " + this.name);
            return;
        }

        if (userSliderInput != null) userSliderInput.startValue = pendulumLength;

        SetPendulumData(pendulumLength);
    }

    public void SetPendulumData(float length)
    {
        weightObject.transform.localPosition = new(150, 160 - length);
        weightObject.rbInput.mass = mass;
        weightObject.rbInput.springStiffness = springStiffness;
        weightObject.rbInput.damping = springDamping;
        weightObject.rbInput.gravity = gravity;
    }
}