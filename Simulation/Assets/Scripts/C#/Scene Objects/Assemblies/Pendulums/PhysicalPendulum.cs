using Resources2;
using UnityEngine;

public class PhysicalPendulum : Assembly
{
    [Header("Parent")]
    public bool isIndependent;

    [Header("Pendulum Rod")]
    public float pendulumLength = 90f;
    public float pendulumMass = 1000f;
    public float pendulumGravity = 9.82f;
    public float width = 15.0f;

    [Header("References")]
    [SerializeField] private SceneRigidBody rodObject;
    [SerializeField] private SceneRigidBody weightObject;
    [SerializeField] private UserSliderInput userSliderInput;

    // Private static
    private static float storedPendulumLength;
    private static float storedPendulumMass;
    private static float storedPendulumGravity;
    private static float storedWidth;
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
        storedWidth = width;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;

        pendulumLength = storedPendulumLength;
        pendulumMass = storedPendulumMass;
        pendulumGravity = storedPendulumGravity;
        width = storedWidth;
    }

    public override void AssemblyUpdate()
    {
        if (rodObject == null)
        {
            Debug.LogWarning("All references are not set. PhysicalPendulum: " + this.name);
            return;
        }

        if (userSliderInput != null) userSliderInput.startValue = pendulumLength;

        float halfWidth = width / 2.0f;
        Vector2[] rodMeshPoints = new Vector2[] {new(-halfWidth, halfWidth), new(halfWidth, halfWidth), new(halfWidth, -pendulumLength), new(-halfWidth, -pendulumLength)};
        rodObject.OverridePolygonPoints(rodMeshPoints);

        weightObject.rbInput.localLinkPosOtherRB.y = -pendulumLength * Const.Sqrt2Div3;
        weightObject.transform.localPosition = new(150, 160 - pendulumLength * Const.Sqrt2Div3);
        weightObject.rbInput.mass = pendulumMass;
        weightObject.rbInput.gravity = pendulumGravity;
    }
}