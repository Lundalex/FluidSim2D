using Michsky.MUIP;
using UnityEngine;
using UnityEngine.Events;
using PM = ProgramManager;

public class UserToggleInput : UserUIElement
{
    [Header("Settings")]
    [SerializeField] private bool startValue;
    public ToggleType toggleType;

    [Header("Inner Field")]
    public bool useInnerField = false;
    public string innerFieldName;

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
            toggleManager.isOn = toggleType == ToggleType.Field && startValue;
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
                if (useInnerField) fieldModifier.ModifyClassField(innerFieldName, toggleManager.isOn);
                else fieldModifier.ModifyField(toggleManager.isOn);
            }
        }
    }
}