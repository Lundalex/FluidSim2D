using UnityEngine;

public class MathematicalPendulum : Assembly
{
    [Header("Parent")]
    public bool isIndependent;

    [Header("Pendulum Weight")]
    public float pendulumLength = 90f;
    public float pendulumMass = 1000f;
    public float pendulumGravity = 9.82f;

    [Header("Pendulum Rod")]
    public float rodWidth = 2.0f;

    [Header("References")]
    [SerializeField] private SceneRigidBody rodObject;
    [SerializeField] private SceneRigidBody weightObject;
    [SerializeField] private UserSliderInput userSliderInput;

    // Private static
    private static float storedPendulumLength;
    private static float storedPendulumMass;
    private static float storedPendulumGravity;
    private static float storedRodWidth;
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
        storedPendulumMass = pendulumMass;
        storedPendulumGravity = pendulumGravity;
        storedRodWidth = rodWidth;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;

        pendulumLength = storedPendulumLength;
        pendulumMass = storedPendulumMass;
        pendulumGravity = storedPendulumGravity;
        rodWidth = storedRodWidth;
    }

    public override void AssemblyUpdate()
    {
        if (rodObject == null || weightObject == null)
        {
            Debug.LogWarning("All references are not set. MathematicalPendulum: " + this.name);
            return;
        }

        if (userSliderInput != null) userSliderInput.startValue = pendulumLength;

        float halfWidth = rodWidth / 2.0f;
        Vector2[] rodMeshPoints = new Vector2[] {new(-halfWidth, halfWidth), new(halfWidth, halfWidth), new(halfWidth, -pendulumLength), new(-halfWidth, -pendulumLength)};
        rodObject.OverridePolygonPoints(rodMeshPoints);

        weightObject.rbInput.localLinkPosOtherRB.y = -pendulumLength;
        weightObject.transform.localPosition = new(150, 160 - pendulumLength);
        weightObject.rbInput.mass = pendulumMass;
        weightObject.rbInput.gravity = pendulumGravity;
    }
}