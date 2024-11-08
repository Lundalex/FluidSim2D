using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class DashedRectangle : MonoBehaviour
{
    public bool DoAddPositionValueTEMP = false;
    [SerializeField] RectTransform rectTransform;
    [SerializeField] private float width = 5f;
    [SerializeField] private float height = 3f;
    [SerializeField] private float cornerRadius = 0.5f;
    [SerializeField] private int cornerSegments = 10;
    [SerializeField] private float scrollSpeed = 0.5f;
    [SerializeField] private Vector2 posOffset;

    private LineRenderer lineRenderer;
    private Material lineMaterial;
    private float offset;
    private float scale = 1;
    private Vector2 lastPosition;

    public void SetPosition(Vector2 pos) => rectTransform.anchoredPosition = pos;
    public void SetScale(float newScale) => scale = newScale;

    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;

        // Generate positions
        List<Vector3> positions = GenerateRoundedRectanglePositions(width * scale, height * scale, cornerRadius, cornerSegments);
        lineRenderer.positionCount = positions.Count;
        lineRenderer.SetPositions(positions.ToArray());

        lineMaterial = lineRenderer.sharedMaterial;
    }
    
    private void Update()
    {
        offset += Time.deltaTime * scrollSpeed;
        lineMaterial.mainTextureOffset = new Vector2(offset, 0);

        Vector2 newPosition = rectTransform.anchoredPosition;
        Debug.Log(newPosition);
        if (lastPosition != newPosition)
        {
            lastPosition = newPosition;
            Start();
        }
    }

    private void OnValidate() => Start();

    private List<Vector3> GenerateRoundedRectanglePositions(float width, float height, float radius, int segments)
    {
        List<Vector3> positions = new();

        // Clamp radius to avoid overlapping corners
        float maxRadius = Mathf.Min(width, height) / 2f;
        radius = Mathf.Clamp(radius, 0f, maxRadius);

        Vector2[] corners = new Vector2[4];
        corners[0] = new Vector2(-width / 2 + radius, -height / 2 + radius); // Bottom Left
        corners[1] = new Vector2(width / 2 - radius, -height / 2 + radius);  // Bottom Right
        corners[2] = new Vector2(width / 2 - radius, height / 2 - radius);   // Top Right
        corners[3] = new Vector2(-width / 2 + radius, height / 2 - radius);  // Top Left

        float[] angles = { 180f, 270f, 0f, 90f };
        for (int i = 0; i < 4; i++)
        {
            Vector2 center = corners[i];
            float startAngle = angles[i];
            float endAngle = startAngle + 90f;

            // Generate points for the corner arc
            for (int j = 0; j <= segments; j++)
            {
                float angle = Mathf.Lerp(startAngle, endAngle, (float)j / segments);
                float rad = Mathf.Deg2Rad * angle;
                Vector2 point = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
                positions.Add(point);
            }
        }

        return positions;
    }
}
