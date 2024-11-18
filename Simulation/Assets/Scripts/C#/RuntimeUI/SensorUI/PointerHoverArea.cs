using UnityEngine;

public class PointerHoverArea : MonoBehaviour
{
    private RectTransform rectTransform;
    public bool CheckIfHovering()
    {
        if (rectTransform == null) rectTransform = this.GetComponent<RectTransform>();
        
        Vector2 mousePosition = Input.mousePosition;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            mousePosition,
            null,
            out Vector2 localPoint);

        return rectTransform.rect.Contains(localPoint);
    }
}