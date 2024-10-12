using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneFluid))]
public class EditorManager : Editor
{
    private const float lineThickness = 2.0f;

    void OnEnable() => EditorApplication.update += OnEditorUpdate;

    void OnDisable() => EditorApplication.update -= OnEditorUpdate;

    void OnEditorUpdate() {}

    // Draw fluid objects
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Selected)]
    static void DrawFluidObjects(SceneFluid fluid, GizmoType gizmoType)
    {
        if (fluid == null) return;

        fluid.SetPolygonData();

        // Draw points
        if (fluid.DoDrawBody)
        {
            fluid.Points = fluid.GeneratePoints(-1);

            int iterationCount = 0;
            Gizmos.color = fluid.BodyColor;
            foreach (Vector2 point in fluid.Points)
            {
                if (iterationCount++ > fluid.MaxGizmosIterations) return;
                Gizmos.DrawSphere(point, fluid.editorPointRadius);
            }
        }
        
        // Draw edges
        foreach (Edge edge in fluid.Edges)
        {
            Vector2 edgeDir = (edge.end - edge.start).normalized;
            Vector2 perpDir = 0.5f * lineThickness * new Vector2(-edgeDir.y, edgeDir.x);

            // Compute the four vertices of the quad
            Vector2 v1 = edge.start + perpDir;
            Vector2 v2 = edge.start - perpDir;
            Vector2 v3 = edge.end + perpDir;
            Vector2 v4 = edge.end - perpDir;

            Vector3[] quadVertices = new Vector3[] { v1, v3, v4, v2 };

            // Draw the quad
            Handles.DrawSolidRectangleWithOutline(quadVertices, fluid.LineColor, fluid.LineColor);
        }
    }

    // Draw rigid body objects
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Selected)]
    static void DrawRigidBodyObjects(SceneRigidBody rigidBody, GizmoType gizmoType)
    {
        if (rigidBody == null) return;

        // Update points
        rigidBody.SetPolygonData();

        // Draw the filled body using quads for each point
        if (rigidBody.DoDrawBody)
        {
            rigidBody.Points = rigidBody.GeneratePoints(lineThickness * 0.5f, Vector2.zero, true);

            int iterationCount = 0;
            Color bodyColor = rigidBody.RBInput.color;
            Handles.color = bodyColor;
            foreach (Vector2 point in rigidBody.Points)
            {
                if (iterationCount++ > rigidBody.MaxGizmosIterations) return;

                // Create a quad for each point
                Vector3[] quadVertices = new Vector3[4];

                float halfThickness = 0.25f * lineThickness;

                quadVertices[0] = new Vector3(point.x - halfThickness, point.y - halfThickness, 0); // Bottom-left
                quadVertices[1] = new Vector3(point.x + halfThickness, point.y - halfThickness, 0); // Bottom-right
                quadVertices[2] = new Vector3(point.x + halfThickness, point.y + halfThickness, 0); // Top-right
                quadVertices[3] = new Vector3(point.x - halfThickness, point.y + halfThickness, 0); // Top-left

                // Draw the filled rectangle (the body)
                Handles.DrawSolidRectangleWithOutline(quadVertices, bodyColor, bodyColor);
            }
        }

        // Draw the polygon edges using quads
        foreach (Edge edge in rigidBody.Edges)
        {
            Vector2 edgeDir = (edge.end - edge.start).normalized;
            Vector2 perpDir = 0.5f * lineThickness * new Vector2(-edgeDir.y, edgeDir.x);

            // Compute the four vertices of the quad
            Vector2 v1 = edge.start + perpDir;
            Vector2 v2 = edge.start - perpDir;
            Vector2 v3 = edge.end + perpDir;
            Vector2 v4 = edge.end - perpDir;

            Vector3[] quadVertices = new Vector3[] { v1, v3, v4, v2 };

            // Draw the quad for the edge
            Handles.DrawSolidRectangleWithOutline(quadVertices, rigidBody.LineColor, rigidBody.LineColor);
        }
    }
}
