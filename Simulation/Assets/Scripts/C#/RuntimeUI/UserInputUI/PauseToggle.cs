using UnityEngine;
using Michsky.MUIP;
using PM = ProgramManager;

public class PauseToggle : MonoBehaviour
{
    [SerializeField] private WindowManager windowManager;
    private void OnEnable()
    {
        PM.Instance.OnSetNewPauseState += ChangeState;
    }

    private void ChangeState(bool state)
    {
        string windowName = state ? "EnabledDisplay" : "DisabledDisplay";
        windowManager.OpenWindow(windowName);
    }

    public void SetState(bool state)
    {
        PM.Instance.TriggerSetPauseState(state);
        ChangeState(state);
    }
}

// PM on button press -> event called here (and in PM)
// Press here, call the same event