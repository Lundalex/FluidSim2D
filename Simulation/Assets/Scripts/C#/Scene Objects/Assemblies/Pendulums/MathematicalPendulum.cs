using UnityEngine;

public class MathematicalPendulum : Assembly
{
    [Header("Parent")]
    public bool isIndependent;

    [Header("Pendulum Weight")]
    public float pendulumLength = 30f;
    public float pendulumMass = 1000f;
    public float pendulumGravity = 9.82f;

    [Header("Pendulum Rod")]
    public float width = 2.0f;

    [Header("References")]
    [SerializeField] private SceneRigidBody rodObject;
    [SerializeField] private SceneRigidBody weightObject;

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
        if (rodObject == null || weightObject == null)
        {
            Debug.LogWarning("All references are not set. MathematicalPendulum: " + this.name);
            return;
        }

        float height = 50.0f;
        float halfWidth = width / 2.0f;
        rodObject.OverridePolygonPoints(new Vector2[] {new(-halfWidth, 0), new(halfWidth, 0), new(halfWidth, -height), new(-halfWidth, -height)});

        weightObject.rbInput.localLinkPosOtherRB.y = -pendulumLength;
        weightObject.rbInput.mass = pendulumMass;
        weightObject.rbInput.gravity = pendulumGravity;
    }
}