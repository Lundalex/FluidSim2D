using Michsky.MUIP;
using UnityEngine;
using PM = ProgramManager;

public class UserToggleInput : UserInput
{
    [Header("Settings")]
    [SerializeField] private bool startingValue;

    [Header("References")]
    [SerializeField] private SwitchManager toggleManager;

    // Private
    private bool lastValue;

    public override void InitDisplay()
    {
        if (Application.isPlaying)
        {
            toggleManager.isOn = startingValue;
            toggleManager.UpdateUI();
        }
        containerTrimImage.color = primaryColor;
    }

    private void Update()
    {
        if (toggleManager.isOn != lastValue)
        {
            if (fieldModifier == null) Debug.LogWarning("FieldModifier not set. UserToggleInput: " + this.name);
            else fieldModifier.ModifyField(toggleManager.isOn);

            PM.Instance.doOnSettingsChanged = true;
            lastValue = toggleManager.isOn;
        }
    }
}