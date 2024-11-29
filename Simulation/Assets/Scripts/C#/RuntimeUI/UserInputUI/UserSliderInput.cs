using Resources2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PM = ProgramManager;

public class UserSliderInput : UserUIElement
{
    [Header("Settings")]
    [Range(0.0f, 1000.0f), SerializeField] private float msMaxUpdateFrequency = 100.0f;
    [SerializeField] private float startingValue;
    [SerializeField] private float minValue;
    [SerializeField] private float maxValue;
    public string innerFieldName = "No Inner Field";

    [Header("References")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_InputField sliderInputField;
    [SerializeField] private FieldModifier fieldModifier;

    // Private
    private float lastValue;
    private Timer updateTimer;

    public override void InitDisplay()
    {
        slider.value = startingValue;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        sliderInputField.text = StringUtils.FloatToString(startingValue, 1);
        containerTrimImage.color = primaryColor;
        updateTimer = new Timer(Func.MsToSeconds(msMaxUpdateFrequency));
    }

    private void Update()
    {
        if (slider.value != lastValue)
        {
            if (updateTimer.Check())
            {
                ModifyField();

                PM.Instance.doOnSettingsChanged = true;
                lastValue = slider.value;
            }
        }
    }

    private void ModifyField()
    {
        if (fieldModifier == null) Debug.LogWarning("FieldModifier not set. UserSliderInput: " + this.name);
        else
        {
            if (innerFieldName == "No Inner Field") fieldModifier.ModifyField(slider.value);
            else fieldModifier.ModifyClassField(innerFieldName, slider.value);
        }
    }
}