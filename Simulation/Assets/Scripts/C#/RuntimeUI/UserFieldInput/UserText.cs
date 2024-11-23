using TMPro;
using UnityEngine;

public class UserText : UserUIElement
{
    [Header("Display Text")]
    [SerializeField] private string displayText;

    [Header("References")]
    [SerializeField] private TMP_Text textField;

    public override void InitDisplay()
    {
        textField.text = displayText;
        containerTrimImage.color = primaryColor;
    }
}