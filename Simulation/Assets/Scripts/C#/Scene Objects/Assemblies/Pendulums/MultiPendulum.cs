using UnityEngine;

public class MultiPendulum : Assembly
{
    [Header("PendulumType")]
    public PendulumType pendulumType;

    [Header("Mathematical / Physical")]
    public float pendulumLength = 30f;
    public float pendulumMass = 1000f;
    public float pendulumGravity = 9.82f;

    [Header("Spring")]
    public float springStiffness = 500f;
    
    [Header("References - Pendulums")]
    [SerializeField] private MathematicalPendulum mathematical;
    [SerializeField] private PhysicalPendulum physical;
    [SerializeField] private SpringPendulum spring;
    [SerializeField] private GameObject doubleMathematical;
    [SerializeField] private GameObject doubleSpring;

    [Header("References - User Inputs")]
    [SerializeField] private UserSelectorInput userSelectorInput;
    [SerializeField] private UserSliderInput userSliderInput;

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
        if (mathematical == null || physical == null || spring == null || doubleMathematical == null || doubleSpring == null )
        {
            Debug.LogWarning("All references are not set. MultiPendulum: " + this.name);
            return;
        }

        bool setActiveMathematical = false;
        bool setActivePhysical = false;
        bool setActiveSpring = false;
        bool setActiveDoubleMathematical = false;
        bool setActiveDoubleSpring = false;
        switch (pendulumType)
        {
            case PendulumType.Mathematical:
                setActiveMathematical = true;
                mathematical.SetPendulumData(pendulumLength, pendulumMass, pendulumGravity);
                break;

            case PendulumType.Physical:
                setActivePhysical = true;
                physical.SetPendulumData(pendulumLength, pendulumMass, pendulumGravity);
                break;

            case PendulumType.Spring:
                setActiveSpring = true;
                spring.SetPendulumData(pendulumLength, pendulumMass, pendulumGravity);
                break;

            case PendulumType.DoubleMathematical:
                setActiveDoubleMathematical = true;
                break;

            case PendulumType.DoubleSpring:
                setActiveDoubleSpring = true;
                break;

            default:
                Debug.Log("PendulumType '" + pendulumType + "' not recognized. Pendulum: " + this.name);
                break;
        }

        mathematical.gameObject.SetActive(setActiveMathematical);
        physical.gameObject.SetActive(setActivePhysical);
        spring.gameObject.SetActive(setActiveSpring);
        doubleMathematical.SetActive(setActiveDoubleMathematical);
        doubleSpring.SetActive(setActiveDoubleSpring);

        if (userSelectorInput != null) userSelectorInput.SetSelectorIndex((int)pendulumType);
        if (userSliderInput != null) userSliderInput.startValue = pendulumLength;
    }
}