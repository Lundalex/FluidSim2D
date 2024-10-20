using Resources;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneFluid))]
public class EditorManager : Editor
{
    private const float lineThickness = 1.0f;
    private const float springAmplitude = 10.0f;
    private const int numSpringPoints = 15;

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

    static (Vector2 start, Vector2 end) GetSpringEndPoints(SceneRigidBody rigidBody, float gridDensity)
    {
        SceneRigidBody otherRigidBody = rigidBody.RBInput.linkedRigidBody;

        // Get the world position of the link points
        Vector2 startPoint = rigidBody.ComputeCentroid(gridDensity) + (Vector2)rigidBody.RBInput.localLinkPosThisRB;
        Vector2 endPoint = otherRigidBody.ComputeCentroid(gridDensity) + (Vector2)rigidBody.RBInput.localLinkPosOtherRB;

        return (startPoint, endPoint);
    }

    static void DrawMeshWireframe(Vector2[] meshVertices, Color color, float lineThickness)
    {
        int vertexCount = meshVertices.Length;

        Handles.color = color;
        for (int i = 0; i < vertexCount; i++)
        {
            Vector2 start = meshVertices[i];
            Vector2 end = meshVertices[(i + 1) % vertexCount];

            Vector2 edgeDir = (end - start).normalized;
            Vector2 perpDir = 0.5f * lineThickness * new Vector2(-edgeDir.y, edgeDir.x);

            Vector2 v1 = start + perpDir;
            Vector2 v2 = start - perpDir;
            Vector2 v3 = end + perpDir;
            Vector2 v4 = end - perpDir;

            Vector3[] quadVertices = new Vector3[] { v1, v3, v4, v2 };

            // Draw the quad for the edge
            Handles.DrawSolidRectangleWithOutline(quadVertices, color, color);
        }
    }

    public static void DrawZigZagSpring(Vector2 startPoint, Vector2 endPoint, Color color, float lineThickness, float amplitude, int pointCount)
    {
        // Calculate the direction and distance between the points
        Vector2 direction = (endPoint - startPoint).normalized;
        float totalLength = Vector2.Distance(startPoint, endPoint);

        // Calculate the perpendicular direction
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);

        // Set the color for the zigzag line
        Handles.color = color;

        // Calculate the length of each segment
        float segmentLength = totalLength / (pointCount - 1);

        // Previous point along the zigzag
        Vector2 prevPoint = startPoint;

        for (int i = 1; i < pointCount; i++)
        {
            // Position along the line
            float t = (float)i / (pointCount - 1);
            Vector2 pointOnLine = Vector2.Lerp(startPoint, endPoint, t);

            // Determine the offset direction
            float offsetMultiplier = (i % 2 == 0) ? -1f : 1f;

            // No offset for the end point
            if (i == pointCount - 1)
            {
                offsetMultiplier = 0f;
            }

            // Offset perpendicular to the line
            Vector2 offsetVector = perpendicular * amplitude * offsetMultiplier;

            // Current point along the zigzag
            Vector2 currentPoint = pointOnLine + offsetVector;

            // Calculate the quad (rectangle) between prevPoint and currentPoint
            Vector2 segmentDirection = (currentPoint - prevPoint).normalized;
            Vector2 segmentPerp = new Vector2(-segmentDirection.y, segmentDirection.x) * (lineThickness * 0.5f);

            Vector3[] quadVertices = new Vector3[4];
            quadVertices[0] = prevPoint + segmentPerp;
            quadVertices[1] = prevPoint - segmentPerp;
            quadVertices[2] = currentPoint - segmentPerp;
            quadVertices[3] = currentPoint + segmentPerp;

            // Draw the quad
            Handles.DrawSolidRectangleWithOutline(quadVertices, color, color);

            // Update the previous point
            prevPoint = currentPoint;
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
            Color bodyColor = Color.white;
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

        // Draw wiremesh
        Vector2[] meshVertices = rigidBody.MeshPoints.ToArray();
        DrawMeshWireframe(meshVertices, Color.black, lineThickness);

        // Draw linked spring
        if (rigidBody.RBInput.enableSpringLink && rigidBody.RBInput.linkedRigidBody != null)
        {   
            float gridDensity = 3.0f; // A lower value results in a higher performance cost, but also slightly increases centroid approximation accuracy
            (Vector2 startPoint, Vector2 endPoint) = GetSpringEndPoints(rigidBody, gridDensity);
            float approxLength = Mathf.Sqrt(Vector2.SqrMagnitude(startPoint - endPoint));
            float approxForce = rigidBody.RBInput.springStiffness * Mathf.Abs(rigidBody.RBInput.springRestLength - approxLength);
            rigidBody.approximatedSpringLength = approxLength;
            rigidBody.approximatedSpringForce = approxForce;

            Color springBaseColor = Color.green;
            Color springStressedColor = Color.red;
            Color lerpColor = Color.Lerp(springBaseColor, springStressedColor, approxForce / 50000.0f);

            // Draw spring shape
            DrawZigZagSpring(startPoint, endPoint, lerpColor, lineThickness, springAmplitude, numSpringPoints);

            Handles.color = Color.red;
            float radius = 2.5f;
            Handles.DrawSolidDisc(startPoint, Vector3.forward, radius);
            Handles.DrawSolidDisc(endPoint, Vector3.forward, radius);
        }
    }
}
