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

    [Header("References - User Texts")]
    [SerializeField] private GameObject mathematicalText;
    [SerializeField] private GameObject physicalText;
    [SerializeField] private GameObject springText;
    [SerializeField] private GameObject doubleMathematicalText;
    [SerializeField] private GameObject doubleSpringText;
    
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
                if (userSliderInput != null)
                {
                    userSliderInput.gameObject.SetActive(true);
                    userSliderInput.startValue = pendulumLength;
                }
                break;

            case PendulumType.Physical:
                setActivePhysical = true;
                physical.SetPendulumData(pendulumLength, pendulumMass, pendulumGravity);
                if (userSliderInput != null)
                {
                    userSliderInput.gameObject.SetActive(true);
                    userSliderInput.startValue = pendulumLength;
                }
                break;

            case PendulumType.Spring:
                setActiveSpring = true;
                spring.SetPendulumData(pendulumLength, pendulumMass, pendulumGravity);
                if (userSliderInput != null)
                {
                    userSliderInput.gameObject.SetActive(true);
                    userSliderInput.startValue = pendulumLength;
                }
                break;

            case PendulumType.DoubleMathematical:
                setActiveDoubleMathematical = true;
                userSliderInput.gameObject.SetActive(false);
                break;

            case PendulumType.DoubleSpring:
                setActiveDoubleSpring = true;
                userSliderInput.gameObject.SetActive(false);
                break;

            default:
                Debug.Log("PendulumType '" + pendulumType + "' not recognized. Pendulum: " + this.name);
                break;
        }

        // Set only chosen pendulum to be active
        mathematical.gameObject.SetActive(setActiveMathematical);
        physical.gameObject.SetActive(setActivePhysical);
        spring.gameObject.SetActive(setActiveSpring);
        doubleMathematical.SetActive(setActiveDoubleMathematical);
        doubleSpring.SetActive(setActiveDoubleSpring);

        // Set the corresponding user text to be active
        if (mathematicalText != null) mathematicalText.SetActive(setActiveMathematical);
        if (physicalText != null) physicalText.SetActive(setActivePhysical);
        if (springText != null) springText.SetActive(setActiveSpring);
        if (doubleMathematicalText != null) doubleMathematicalText.SetActive(setActiveDoubleMathematical);
        if (doubleSpringText != null) doubleSpringText.SetActive(setActiveDoubleSpring);

        if (userSelectorInput != null) userSelectorInput.SetSelectorIndex((int)pendulumType);
    }
}