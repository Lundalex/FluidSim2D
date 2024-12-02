using System.Collections;
using System.Collections.Generic;
using Michsky.MUIP;
using UnityEngine;
using UnityEngine.Events;

public class UserButtonInput : UserUIElement
{
    [Range(0.5f, 10.0f), SerializeField] private float colorShiftSpeed;
    [SerializeField] private List<Color> shiftColors = new();
    [SerializeField] private bool useSinOscillation = false;

    [Header("Button")]
    [SerializeField] private float textSize = 25.0f;
    [SerializeField] private string text = "Button";

    [Header("References")]
    [SerializeField] private ButtonManager button;

    [Header("Unity Event")]
    [SerializeField] private UnityEvent onButtonClicked;

    // Private
    private bool colorShiftCoroutineStarted = false;

    public void OnClick()
    {
        onButtonClicked.Invoke();
        onValueChanged.Invoke();
    }

    public override void InitDisplay()
    {
        containerTrimImage.color = primaryColor;
        button.textSize = textSize;
        button.SetText(text);
    }

    [ContextMenu("Start Color Shift")]
    public void StartColorShift()
    {
        if (!colorShiftCoroutineStarted)
        {
            StartCoroutine(ColorShiftCoroutine());
            colorShiftCoroutineStarted = true;
        }
    }

    [ContextMenu("Stop Color Shift")]
    public void StopColorShift()
    {
        if (colorShiftCoroutineStarted)
        {
            StopCoroutine(ColorShiftCoroutine());
            colorShiftCoroutineStarted = false;
        }
    }

    private IEnumerator ColorShiftCoroutine()
    {
        Timer timer = new(0);
        int colorCount = shiftColors.Count;
        if (colorCount < 2)
        {
            Debug.LogWarning("Cannot color shift - too few shift colors. UserButtonInput: " + this.name);
            yield break;
        }
        while (true)
        {
            colorCount = shiftColors.Count;

            float t0 = timer.GetTime() * colorShiftSpeed;
            float t = useSinOscillation ? SinOscillation(t0) : Mathf.Repeat(t0, 1.0f);

            float colorIndexFloat = t * colorCount;
            int colorIndex = Mathf.FloorToInt(colorIndexFloat) % colorCount;
            int nextColorIndex = (colorIndex + 1) % colorCount;
            float frac = colorIndexFloat - colorIndex;

            Color colStart = shiftColors[colorIndex];
            Color colEnd = shiftColors[nextColorIndex];

            primaryColor = Color.Lerp(colStart, colEnd, frac);
            containerTrimImage.color = primaryColor;
            yield return new WaitForSeconds(0.01f);
        }
    }

    private float SinOscillation(float t0) => (Mathf.Sin((t0 + 0.75f) * Mathf.PI * 2.0f) + 1.0f) * 0.5f;

    private void OnDestroy() => StopColorShift();
}
