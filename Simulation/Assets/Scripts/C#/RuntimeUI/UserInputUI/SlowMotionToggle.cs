using UnityEngine;
using Michsky.MUIP;
using PM = ProgramManager;

public class SlowMotionToggle : MonoBehaviour
{
    [SerializeField] private WindowManager windowManager;
    private void OnEnable()
    {
        PM.Instance.OnSetNewSlowMotionState += ChangeState;
    }

    private void ChangeState(bool state)
    {
        string windowName = state ? "EnabledDisplay" : "DisabledDisplay";
        windowManager.OpenWindow(windowName);
    }

    public void SetState(bool state)
    {
        PM.Instance.TriggerSetSlowMotionState(state);
        ChangeState(state);
    }
}

// PM on button press -> event called here (and in PM)
// Press here, call the same event