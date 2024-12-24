using UnityEngine;
using UnityEngine.UI;
using PM = ProgramManager;

public class SplashImage : MonoBehaviour
{
    // Inspector
    [SerializeField] private float fadeDuration;
    [SerializeField] private Image image;

    // Nonserialized
    private float timePassed = 0.0f;

    private void Update()
    {
        timePassed += Mathf.Min(Time.deltaTime, PM.MaxDeltaTime);

        float opcaity = 1 - Mathf.Min(timePassed / fadeDuration, 1);
        SetImageOpacity(opcaity);

        if (timePassed > fadeDuration) gameObject.SetActive(false);
    }

    private void SetImageOpacity(float opacity)
    {
        image.color = new Color(image.color.r, image.color.g, image.color.b, opacity);
    }
}
