using UnityEngine;
using TMPro;

public class SensorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text decimalText;
    [SerializeField] private TMP_Text integerText;
    [SerializeField] private TMP_Text unitText;
    [SerializeField] private TMP_Text titleText;

    public void SetMeasurement(float val, int numDecimals)
    {
        numDecimals = Mathf.Min(numDecimals, 2);
        decimalText.text = val.ToString("F" + numDecimals);
        integerText.text = ((int)val).ToString();
    }

    public void SetUnit(string unit)
    {
        unitText.text = unit;
    }

    public void SetTitle(string title)
    {
        titleText.text = title;
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
}