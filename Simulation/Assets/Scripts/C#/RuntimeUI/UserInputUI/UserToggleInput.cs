using Michsky.MUIP;
using UnityEngine;
using UnityEngine.Events;
using PM = ProgramManager;

public class UserToggleInput : UserUIElement
{
    [Header("Settings")]
    [SerializeField] private bool startingValue;
    public string innerFieldName = "No Inner Field";
    public ToggleType toggleType;

    [Header("References")]
    [SerializeField] private SwitchManager toggleManager;
    [SerializeField] private FieldModifier fieldModifier;
    [SerializeField] private UnityEvent<bool> onToggleChanged;

    // Private
    private bool lastValue;

    public override void InitDisplay()
    {
        if (Application.isPlaying)
        {
            toggleManager.isOn = toggleType == ToggleType.Field && startingValue;
            toggleManager.UpdateUI();
        }
        containerTrimImage.color = primaryColor;
    }

    private void Update()
    {
        if (toggleManager.isOn != lastValue)
        {
            ModifyField();

            PM.Instance.doOnSettingsChanged = true;
            lastValue = toggleManager.isOn;
        }
    }

    private void ModifyField()
    {
        if (toggleType == ToggleType.UnityEvent)
        {
            onToggleChanged.Invoke(toggleManager.isOn);
        }
        else
        {
            if (fieldModifier == null) Debug.LogWarning("FieldModifier not set. UserSliderInput: " + this.name);
            else
            {
                if (innerFieldName == "No Inner Field") fieldModifier.ModifyField(toggleManager.isOn);
                else fieldModifier.ModifyClassField(innerFieldName, toggleManager.isOn);
            }
        }
    }
}