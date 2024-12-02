using Michsky.MUIP;
using Resources2;
using UnityEngine;
using PM = ProgramManager;

public class UserSelectorInput : UserUIElement
{
    [Header("Settings")]
    [Range(0.0f, 1000.0f), SerializeField] private float msMaxUpdateFrequency = 100.0f;
    public int startIndex;

    [Header("Inner Field")]
    public bool useInnerField = false;
    public string innerFieldName;

    [Header("References")]
    [SerializeField] private HorizontalSelector selector;
    [SerializeField] private FieldModifier fieldModifier;

    // Private
    private float lastValue;
    private Timer updateTimer;

    public void SetSelectorIndex(int index)
    {
        selector.index = selector.defaultIndex = startIndex = index;
        selector.SetupSelector();
    }

    public override void InitDisplay()
    {
        selector.defaultIndex = startIndex;
        containerTrimImage.color = primaryColor;
        updateTimer = new Timer(Func.MsToSeconds(msMaxUpdateFrequency));
    }

    private void Update()
    {
        if (selector.index != lastValue)
        {
            if (updateTimer.Check())
            {
                ModifyField();

                PM.Instance.doOnSettingsChanged = true;
                lastValue = selector.index;
            }
        }
    }

    private void ModifyField()
    {
        if (fieldModifier == null) Debug.LogWarning("FieldModifier not set. UserSelectorInput: " + this.name);
        else
        {
            if (useInnerField) fieldModifier.ModifyClassField(innerFieldName, selector.index);
            else fieldModifier.ModifyField(selector.index);
        }
    }
}