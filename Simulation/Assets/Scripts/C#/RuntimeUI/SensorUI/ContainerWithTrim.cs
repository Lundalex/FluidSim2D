using UnityEngine;

[ExecuteAlways]
public class ContainerWithTrim : MonoBehaviour
{
    [SerializeField] private RectTransform otherContainer;
    [SerializeField] private RectTransform trimContainer;
    [SerializeField] private RectTransform innerContainer;

    void Update()
    {
        if (!Application.isPlaying)
        {
            if (otherContainer != null && trimContainer != null && innerContainer != null)
            {
                trimContainer.sizeDelta = otherContainer.sizeDelta - new Vector2(15, 15);
                innerContainer.sizeDelta = otherContainer.sizeDelta - new Vector2(30, 30);
            }
        }
    }

    void Start()
    {
        if (otherContainer != null && trimContainer != null && innerContainer != null)
        {
            trimContainer.sizeDelta = otherContainer.sizeDelta - new Vector2(15, 15);
            innerContainer.sizeDelta = otherContainer.sizeDelta - new Vector2(30, 30);
        }
    }
}