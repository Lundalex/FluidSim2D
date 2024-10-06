using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Polygon))]
public class EditorManager : Editor
{
    private Polygon polygon;
    private const float UpdateFrequency = 1.0f/30.0f;
    private float timer = 0.0f;

    void OnEnable()
    {
        polygon = (Polygon)target;
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    void OnEditorUpdate()
    {
        timer += Time.deltaTime;
        if (timer > UpdateFrequency && polygon != null) 
        {
            timer %= UpdateFrequency;

            PolygonCollider2D polygonCollider = polygon.GetComponent<PolygonCollider2D>();
            
            if (polygonCollider != null)
            {
                polygon.SetPolygonData();
                polygon.Points = polygon.GeneratePoints(-1);
                SceneView.RepaintAll();
            }
        }
    }
}