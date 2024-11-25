using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Michsky.MUIP;
using System;
using PM = ProgramManager;

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
    [SerializeField] private WindowManager dataViewWindowManager;
    [SerializeField] public WindowManager settingsViewWindowManager;
    [SerializeField] public Slider scaleSlider;
    [SerializeField] public RectTransform rectTransform;
    [SerializeField] public RectTransform outerContainerRectTransform;
    [SerializeField] public DemoElementSway swayElementA;
    [SerializeField] public DemoElementSway swayElementB;
    [SerializeField] public DemoElementSway swayElementC;
    [SerializeField] public DemoElementSway swayElementD;
    [SerializeField] public CustomTwinButtonToggleParent swayParentAB;
    [SerializeField] public CustomTwinButtonToggleParent swayParentCD;
    [SerializeField] public GameObject rigidBodySensorTypeSelectObject;
    [SerializeField] public GameObject fluidSensorTypeSelectObject;
    [SerializeField] public CustomDropdown rigidBodySensorTypeSelect;
    [SerializeField] public CustomDropdown fluidSensorTypeSelect;
    [SerializeField] public PointerHoverArea pointerHoverArea;
    [SerializeField] public Transform graphChartContainer;

    // Events
    public event Action<bool> OnSettingsViewStatusChanged;

    // NonSerialized
    [NonSerialized] public Sensor sensor;
    [NonSerialized] public GameObject dashedRectangleObject;
    [NonSerialized] public DashedRectangle dashedRectangle;
    [NonSerialized] public int sensorIndex;
    [NonSerialized] public float sliderScale;
    [NonSerialized] public float userScale;
    [NonSerialized] public bool isPointerHovering = false;

    // Private - Dropdown Select
    private RigidBodySensorType selectedRigidBodySensorType;
    private bool rigidBodySensorTypeDropdownUsed = false;
    private FluidSensorType selectedFluidSensorType;
    private bool fluidSensorTypeDropdownUsed = false;

    // Private - Pointer Hover
    private Timer pointerHoverTimer;
    private const float PointerHoverCooldown = 0.25f;

    // Private - Scale
    private readonly Vector3 BaseScale = new(0.6f, 0.6f, 0.6f);
    private readonly Vector3 ScaleFactor = new(0.65f, 1.0f, 1.0f);
    private const float SettingsViewActiveFixedScale = 2.0f;
    private const float GraphViewActiveFixedScale = 1.5f;

    public void Initialize() => pointerHoverTimer = new Timer(PointerHoverCooldown, true, true, PointerHoverCooldown);

    public void OnPositionChanged()
    {
        if (dashedRectangle == null) return;
        Vector2 pos = GetPositionFromInputFields();
        dashedRectangle.SetPosition(pos);
    }

    public Vector3 GetTotalScale(bool settingsViewActive = false)
    {
        float graphViewScaleFactor = CheckIfGraphViewIsActive() ? GraphViewActiveFixedScale : 1;
        return (settingsViewActive ? SettingsViewActiveFixedScale : sliderScale) * graphViewScaleFactor * BaseScale;
    }

    public void SetDataWindow(string windowName) => dataViewWindowManager.OpenPanel(windowName); // 0 -> graph view, 1 -> numeric view

    private bool CheckIfGraphViewIsActive() => dataViewWindowManager.currentWindowIndex == 0 && settingsViewWindowManager.currentWindowIndex == 0;

    public Vector3 GetTotalDashedRectangleScale()
    {
        return sensor.useFixedScaleForDashedRectangle ? BaseScale : userScale * BaseScale;
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
        rectTransform.localPosition = ClampToScreenBounds(sensor.SimSpaceToCanvasSpace(pos));
        sensor.positionType = PositionType.Fixed;
        sensor.targetPosition = pos;

        sensor.graphController.ResetGraph();

        if (rigidBodySensorTypeDropdownUsed && sensor is RigidBodySensor rigidBodySensor)
        {
            rigidBodySensor.SetRigidBodySensorType(selectedRigidBodySensorType);
        }
        else if (fluidSensorTypeDropdownUsed && sensor is FluidSensor fluidSensor)
        {
            fluidSensor.SetFluidSensorType(selectedFluidSensorType);
        }
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

    public void SetUnit(string baseUnit, string unit)
    {
        sensor.graphController.SetSuffix(baseUnit);
        unitText.text = unit;
    }

    public void SetTitle(string title) => titleText.text = title;

    public void SetPrimaryColor(Color color)
    {
        decimalText.color = color;
        integerText.color = color;
        containerTrimImage.color = color;
    }

    public void SetPosition(Vector2 pos)
    {
        if ((pointerHoverArea.CheckIfHovering() && pointerHoverTimer.Check(false)) || PM.Instance.isAnySensorSettingsViewActive)
        {
            pos = rectTransform.localPosition;
            isPointerHovering = true;
        }
        else if (isPointerHovering)
        {
            isPointerHovering = false;
            pointerHoverTimer.Reset();
        }
        sensor.graphController.isPointerHovering = isPointerHovering;

        rectTransform.localPosition = ClampToScreenBounds(pos);
    }

    private Vector2 ClampToScreenBounds(Vector2 pos)
    {
        // Apply ScreenToView transform
        pos /= PM.Instance.ScreenToViewFactor;

        Vector2 rectTransformSize = new(outerContainerRectTransform.rect.width, outerContainerRectTransform.rect.height);
        Vector2 containerSize = (Vector2)transform.localScale * ScaleFactor * rectTransformSize * 1.4f / PM.Instance.ScreenToViewFactor;
        Vector2 containerMin = pos - 0.5f * containerSize;
        Vector2 containerMax = pos + 0.5f * containerSize;

        Vector2 halfResolution = 0.5f * PM.Instance.Resolution;
        Vector2 screenMin = -halfResolution + PM.Instance.main.UIPadding;
        Vector2 screenMax = halfResolution - PM.Instance.main.UIPadding;
        Vector2 minDiff = containerMin - screenMin;
        Vector2 maxDiff = containerMax - screenMax;

        Vector2 offset = Vector2.zero;

        // Adjust X axis
        if (minDiff.x < 0)
        {
            offset.x = -minDiff.x;
        }
        else if (maxDiff.x > 0)
        {
            offset.x = -maxDiff.x;
        }

        // Adjust Y axis
        if (minDiff.y < 0)
        {
            offset.y = -minDiff.y;
        }
        else if (maxDiff.y > 0)
        {
            offset.y = -maxDiff.y;
        }
        
        // Apply offset
        pos += offset;

        // Revert ScreenToView transform
        pos *= PM.Instance.ScreenToViewFactor;

        return pos;
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
        SetUnit("Unit", "Unit");
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

    public void OnNewRigidBodySensorType(int rigidBodySensorTypeInt)
    {
        if (sensor is RigidBodySensor)
        {
            selectedRigidBodySensorType = (RigidBodySensorType)rigidBodySensorTypeInt;
            rigidBodySensorTypeDropdownUsed = true;
        }
        else Debug.LogWarning("Mismatch between sensor type and active custom dropdown: " + this.name);
    }

    public void OnNewFluidSensorType(int fluidSensorTypeInt)
    {
        if (sensor is FluidSensor)
        {
            selectedFluidSensorType = (FluidSensorType)fluidSensorTypeInt;
            fluidSensorTypeDropdownUsed = true;
        }
        else Debug.LogWarning("Mismatch between sensor type and active custom dropdown: " + this.name);
    }
}