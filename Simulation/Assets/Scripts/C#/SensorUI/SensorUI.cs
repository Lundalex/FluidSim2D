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
    [SerializeField] private Image containerTrimImage;
    [SerializeField] public Slider scaleSlider;
    [SerializeField] public RectTransform rectTransform;
    [SerializeField] public DemoElementSway swayElementA;
    [SerializeField] public DemoElementSway swayElementB;
    [SerializeField] public DemoElementSway swayElementC;
    [SerializeField] public DemoElementSway swayElementD;
    [SerializeField] public CustomTwinButtonToggleParent swayParentAB;
    [SerializeField] public CustomTwinButtonToggleParent swayParentCD;
    [SerializeField] public PointerHoverArea pointerHoverArea;

    // Events
    public event Action<bool> OnSettingsViewStatusChanged;

    // Private/NonSerialized
    [NonSerialized] public Sensor sensor;
    [NonSerialized] public GameObject dashedRectangleObject;
    [NonSerialized] public DashedRectangle dashedRectangle;
    [NonSerialized] public int sensorIndex;
    [NonSerialized] public float sliderScale;
    [NonSerialized] public float userScale;
    private float pointerHoverTimer = 0.3f;
    private bool isPointerHovering = false;
    private readonly Vector3 BaseScale = new(0.6f, 0.6f, 0.6f);
    private const float PointerHoverCooldown = 0.5f;
    private const float MaxDeltaTime = 1f / 30f;
    private const float SettingsViewActiveFixedScale = 2.0f;

    public void OnPositionChanged()
    {
        if (dashedRectangle == null) return;
        Vector2 pos = GetPositionFromInputFields();
        dashedRectangle.SetPosition(pos);
    }

    public Vector3 GetTotalScale(bool settingsViewActive = false)
    {
        return (settingsViewActive ? SettingsViewActiveFixedScale : sliderScale) * BaseScale;
    }

    public Vector3 GetTotalDashedRectangleScale()
    {
        return sensor.UseFixedScaleForDashedRectangle ? BaseScale : userScale * BaseScale;
    }

    public void OnScaleChanged()
    {
        userScale = scaleSlider.value;
        if (dashedRectangle != null) dashedRectangle.SetScale(GetTotalDashedRectangleScale());
    }

    public void OnApplyTransformSettings()
    {
        Vector2 pos = GetPositionFromInputFields();

        sliderScale = userScale;
        rectTransform.localPosition = ClampPosToScreenBounds(sensor.SimSpaceToCanvasSpace(pos));
        sensor.positionType = PositionType.Fixed;
        sensor.targetPosition = pos;
    }

    private Vector2 GetPositionFromInputFields()
    {
        float.TryParse(positionXInput.text, out float positionX);
        float.TryParse(positionYInput.text, out float positionY);

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
        pointerHoverTimer += Mathf.Min(Time.deltaTime, MaxDeltaTime);
        if ((pointerHoverArea.CheckIfHovering() && pointerHoverTimer > PointerHoverCooldown) || ProgramManager.Instance.isAnySensorSettingsViewActive)
        {
            pos = rectTransform.localPosition;
            isPointerHovering = true;
        }
        else if (isPointerHovering)
        {
            isPointerHovering = false;
            pointerHoverTimer = 0.0f;
        }

        rectTransform.localPosition = ClampPosToScreenBounds(pos);
    }

    private Vector2 ClampPosToScreenBounds(Vector2 pos)
    {
        Vector2 offset = new(28, -83);
        Vector2 localContainerMin = (new Vector2(-400, -250) + offset) * transform.localScale;
        Vector2 localContainerMax = (new Vector2(400, 250) + offset) * transform.localScale;

        int2 ResolutionInt2 = ProgramManager.Instance.main.Resolution;
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

    public void SetSettingsViewAsEnabled()
    {
        OnSettingsViewStatusChanged?.Invoke(true);

        dashedRectangleObject.SetActive(true);

        Vector2 simPos = sensor.CanvasSpaceToSimSpace(rectTransform.localPosition);
        positionXInput.text = ((int)simPos.x).ToString();
        positionYInput.text = ((int)simPos.y).ToString();

        dashedRectangle.SetPosition(simPos);
        dashedRectangle.SetScale(GetTotalDashedRectangleScale());
    }
    public void SetSettingsViewAsDisabled()
    {
        OnSettingsViewStatusChanged?.Invoke(false);

        dashedRectangleObject.SetActive(false);
    }

    public static string FloatToStr(float value, int numDecimals) => value.ToString($"F{numDecimals}", CultureInfo.InvariantCulture);
    public static string FloatToStr(float2 value, int numDecimals) => "X: " + value.x.ToString($"F{numDecimals}", CultureInfo.InvariantCulture) + "Y: " + value.y.ToString($"F{numDecimals}", CultureInfo.InvariantCulture);
}