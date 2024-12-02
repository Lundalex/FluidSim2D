using UnityEngine;

public class MultiPendulum : Assembly
{
    [Header("PendulumType")]
    public PendulumType pendulumType;

    [Header("Mathematical / Physical")]
    public float pendulumLength = 30f;

    [Header("DoubleMathematical")]
    public float secondaryPendulumLength = 10f;

    [Header("Spring")]
    public float springStiffness = 500f;

    [Header("DoubleSpring")]
    public float secondarySpringStiffness = 200f;

    [Header("References - Mathematical")]
    [SerializeField] private GameObject mathematicalObject;
    [SerializeField] private SceneRigidBody rodObject_Mathematical;
    [SerializeField] private SceneRigidBody weightObject_Mathematical;

    [Header("References - Physical")]
    [SerializeField] private GameObject physicalObject;
    [SerializeField] private SceneRigidBody weightObject_Physical;


    [Header("References - User Inputs")]
    [SerializeField] private UserSelectorInput userSelectorInput;
    [SerializeField] private UserSliderInput pendulumLengthInput;

    // Private static
    private static PendulumType storedPendulumType;
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

        storedPendulumType = pendulumType;
        storedPendulumLength = pendulumLength;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;

        pendulumType = storedPendulumType;
        pendulumLength = storedPendulumLength;
    }

    public override void AssemblyUpdate()
    {
        // if (rodObject == null || weightObject == null)
        // {
        //     Debug.LogWarning("All references are not set. Pendulum: " + this.name);
        //     return;
        // }

        switch (pendulumType)
        {
            case PendulumType.Mathematical:
                if (pendulumLengthInput != null) pendulumLengthInput.startValue = pendulumLength;
                mathematicalObject.SetActive(true);
                physicalObject.SetActive(false);
                break;

            case PendulumType.Physical:
                if (pendulumLengthInput != null) pendulumLengthInput.startValue = pendulumLength;
                physicalObject.SetActive(true);
                mathematicalObject.SetActive(false);
                break;

            default:
                Debug.Log("PendulumType '" + pendulumType + "' not recognized. Pendulum: " + this.name);
                break;
        }

        if (userSelectorInput != null) userSelectorInput.SetSelectorIndex((int)pendulumType);
    }
}