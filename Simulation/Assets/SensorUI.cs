using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Globalization;
using Unity.Mathematics;
using Michsky.MUIP;

public class SensorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text decimalText;
    [SerializeField] private TMP_Text integerText;
    [SerializeField] private TMP_Text unitText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image containerTrimImage;
    [SerializeField] public RectTransform rectTransform;
    [SerializeField] public DemoElementSway swayElementA;
    [SerializeField] public DemoElementSway swayElementB;
    [SerializeField] public CustomTwinButtonToggleParent swayParent;

    public void SetMeasurement(float val, int numDecimals)
    {
        numDecimals = Mathf.Min(numDecimals, 2);

        int integerPart = Mathf.Clamp((int)val, -99, 999); // Clamp to max 3 characters
        int decimalPart = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(val - integerPart) * Mathf.Pow(10, numDecimals)), 0, (int)Mathf.Pow(10, numDecimals)-1);

        integerText.text = integerPart.ToString();
        decimalText.text = decimalPart.ToString();
    }

    public void SetUnit(string unit)
    {
        unitText.text = unit;
    }

    public void SetTitle(string title)
    {
        titleText.text = title;
    }

    public void SetPrimaryColor(Color color)
    {
        decimalText.color = color;
        integerText.color = color;
        containerTrimImage.color = color;
    }

    public void SetPosition(Vector2 pos)
    {
        rectTransform.localPosition = pos;
    }

    [ContextMenu("Set Measurement (Default)")]
    public void SetMeasurementDefault()
    {
        decimalText.text = "21";
        integerText.text = "543";
    }

    [ContextMenu("Set Unit (Default)")]
    private void SetUnitDefault()
    {
        SetUnit("Unit");
    }

    [ContextMenu("Set Title (Default)")]
    private void SetTitleDefault()
    {
        SetTitle("Title");
    }

    public static string FloatToStr(float value, int numDecimals) => value.ToString($"F{numDecimals}", CultureInfo.InvariantCulture);
    public static string FloatToStr(float2 value, int numDecimals) => "X: " + value.x.ToString($"F{numDecimals}", CultureInfo.InvariantCulture) + "Y: " + value.y.ToString($"F{numDecimals}", CultureInfo.InvariantCulture);
}