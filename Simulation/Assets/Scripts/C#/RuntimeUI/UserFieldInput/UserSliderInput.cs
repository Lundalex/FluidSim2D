using Resources2;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using PM = ProgramManager;

public class UserSliderInput : UserInput
{
    [Header("Settings")]
    [Range(0.0f, 1000.0f), SerializeField] private float msMaxUpdateFrequency = 100.0f;
    [SerializeField] private float startingValue;
    [SerializeField] private float minValue;
    [SerializeField] private float maxValue;

    [Header("References")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_InputField sliderInputField;

    // Private
    private float lastValue;
    private float updateTimer;

    public override void InitDisplay()
    {
        slider.value = startingValue;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        sliderInputField.text = (Mathf.Round(startingValue * 10.0f) / 10.0f).ToString();
        containerTrimImage.color = primaryColor;
    }

    private void Update()
    {
        updateTimer += PM.Instance.clampedDeltaTime;
        if (slider.value != lastValue && updateTimer > Func.MsToSeconds(msMaxUpdateFrequency))
        {
            if (fieldModifier == null) Debug.LogWarning("FieldModifier not set. UserSliderInput: " + this.name);
            else
            {
                if (innerFieldName == "No Inner Field") fieldModifier.ModifyField(slider.value);
                else fieldModifier.ModifyClassField(innerFieldName, slider.value);
            }

            PM.Instance.doOnSettingsChanged = true;
            lastValue = slider.value;
            updateTimer = 0;
        }
    }
}