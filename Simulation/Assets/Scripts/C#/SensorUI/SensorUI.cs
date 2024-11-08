using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Globalization;
using Unity.Mathematics;
using Michsky.MUIP;
using System;

public class SensorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text decimalText;
    [SerializeField] private TMP_Text integerText;
    [SerializeField] private TMP_Text unitText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_InputField positionXInput;
    [SerializeField] private TMP_InputField positionYInput;
    [SerializeField] private Slider scaleSlider;
    [SerializeField] private Image containerTrimImage;
    [SerializeField] public RectTransform rectTransform;
    [SerializeField] public DemoElementSway swayElementA;
    [SerializeField] public DemoElementSway swayElementB;
    [SerializeField] public DemoElementSway swayElementC;
    [SerializeField] public DemoElementSway swayElementD;
    [SerializeField] public CustomTwinButtonToggleParent swayParentAB;
    [SerializeField] public CustomTwinButtonToggleParent swayParentCD;
    [SerializeField] public ProgramManager programManager;
    [SerializeField] public PointerHoverArea pointerHoverArea;
    [SerializeField] public DashedRectangle dashedRectangle;
    
    // Private/NonSerialized
    [NonSerialized] public Sensor sensor;
    [NonSerialized] public int sensorIndex;
    private float pointerHoverCooldown = 0.5f;
    private float pointerHoverTimer = 0.3f;
    private bool pointerHover = false;
    private readonly Vector3 baseScale = new(0.6f, 0.6f, 0.6f);

    public void OnPositionXChanged()
    {
        Vector2Int pos = GetPositionFromInputFields();
        Debug.Log(pos.x);

        dashedRectangle.SetPosition(pos);

        positionXInput.text = pos.x.ToString();
    }

    public void OnPositionYChanged()
    {
        Vector2Int pos = GetPositionFromInputFields();
        Debug.Log(pos.y);

        dashedRectangle.SetPosition(pos);

        positionYInput.text = pos.y.ToString();
    }

    public void OnScaleChanged()
    {
        float scale = scaleSlider.value;
        Debug.Log(scale);
        dashedRectangle.SetScale(scale);
    }

    public void OnApplyTransformSettings()
    {
        Vector2Int pos = GetPositionFromInputFields();
        float scale = scaleSlider.value;

        transform.localScale = baseScale * scale;
        rectTransform.localPosition = ClampPosToScreenBounds(pos);
    }

    private Vector2Int GetPositionFromInputFields()
    {
        int.TryParse(positionXInput.text, out int positionX);
        int.TryParse(positionYInput.text, out int positionY);

        return new(positionX, positionY);
    }

    public void SetMeasurement(float val, int numDecimals)
    {
        numDecimals = Mathf.Min(numDecimals, 2);

        int integerPart = Mathf.Clamp((int)val, -99, 999); // Clamp to max 3 characters
        int decimalPart = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(val - integerPart) * Mathf.Pow(10, numDecimals)), 0, (int)Mathf.Pow(10, numDecimals)-1);

        integerText.text = integerPart.ToString();
        decimalText.text = decimalPart.ToString();
    }

    public void SetUnit(string unit) => unitText.text = unit;

    public void SetTitle(string title) => titleText.text = title;

    public void SetPrimaryColor(Color color)
    {
        decimalText.color = color;
        integerText.color = color;
        containerTrimImage.color = color;
    }

    public void SetPosition(Vector2 pos)
    {
        pointerHoverTimer += Mathf.Min(Time.deltaTime, 1 / 30.0f);
        if ((pointerHoverArea.CheckIfHovering() && pointerHoverTimer > pointerHoverCooldown) || programManager.isAnySensorSettingsViewActive)
        {
            pos = rectTransform.localPosition;
            pointerHover = true;
        }
        else if (pointerHover)
        {
            pointerHover = false;
            pointerHoverTimer = 0.0f;
        }

        rectTransform.localPosition = ClampPosToScreenBounds(pos);
    }

    private Vector2 ClampPosToScreenBounds(Vector2 pos)
    {
        Vector2 offset = new(28, -83);
        Vector2 localContainerMin = (new Vector2(-400, -250) + offset) * transform.localScale;
        Vector2 localContainerMax = (new Vector2(400, 250) + offset) * transform.localScale;

        int2 ResolutionInt2 = programManager.main.Resolution;
        Vector2 Resolution = new(ResolutionInt2.x, ResolutionInt2.y);

        Vector2 min = -Resolution * 0.5f - localContainerMin;
        Vector2 max = Resolution * 0.5f - localContainerMax;

        Vector2 clampedPos;
        clampedPos.x = Mathf.Clamp(pos.x, min.x, max.x);
        clampedPos.y = Mathf.Clamp(pos.y, min.y, max.y);

        return clampedPos;
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

    public void SetSettingsViewAsEnabled() => programManager.SetSensorSettingsViewStatus(sensorIndex, true);
    public void SetSettingsViewAsDisabled() => programManager.SetSensorSettingsViewStatus(sensorIndex, false);

    public static string FloatToStr(float value, int numDecimals) => value.ToString($"F{numDecimals}", CultureInfo.InvariantCulture);
    public static string FloatToStr(float2 value, int numDecimals) => "X: " + value.x.ToString($"F{numDecimals}", CultureInfo.InvariantCulture) + "Y: " + value.y.ToString($"F{numDecimals}", CultureInfo.InvariantCulture);
}