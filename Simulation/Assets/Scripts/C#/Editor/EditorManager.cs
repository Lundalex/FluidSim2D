using UnityEditor;
using UnityEngine;

public class EditorManager : Editor
{
    private const float lineThickness = 0.5f;
    private const float springLineThickness = 2.0f;
    private const float springAmplitude = 10.0f;
    private const int numSpringPoints = 15;

    void OnEnable() => EditorApplication.update += OnEditorUpdate;

    void OnDisable() => EditorApplication.update -= OnEditorUpdate;

    void OnEditorUpdate() {}

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

        // Calculate the perpendicular direction
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);

        // Previous point along the zigzag
        Vector2 prevPoint = startPoint;
        Handles.color = color;
        for (int i = 1; i < pointCount; i++)
        {
            // Position along the line
            float t = (float)i / (pointCount - 1);
            Vector2 pointOnLine = Vector2.Lerp(startPoint, endPoint, t);

            // Determine the offset direction
            float offsetMultiplier = (i % 2 == 0) ? -1.0f : 1.0f;

            // No offset for the end point
            if (i == pointCount - 1) offsetMultiplier = 0.0f;

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

        // --- Draw the filled body using triangulation ---
        if (rigidBody.DoDrawBody)
        {
            if (rigidBody.MeshPoints.Count < 3)
            {
                // Cannot create a polygon with less than 3 points
                return;
            }

            // Triangulate the polygon
            Vector2[] polygonPoints = rigidBody.MeshPoints.ToArray();
            Triangulator triangulator = new Triangulator(polygonPoints);
            int[] indices = triangulator.Triangulate();

            // Convert Vector2 to Vector3 (z = 0)
            Vector3[] vertices = new Vector3[polygonPoints.Length];
            for (int i = 0; i < polygonPoints.Length; i++)
            {
                vertices[i] = new Vector3(polygonPoints[i].x, polygonPoints[i].y, 0);
            }

            // Draw triangles
            Handles.color = rigidBody.BodyColor;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3[] triangleVertices = new Vector3[3];
                triangleVertices[0] = vertices[indices[i]];
                triangleVertices[1] = vertices[indices[i + 1]];
                triangleVertices[2] = vertices[indices[i + 2]];

                // Draw the triangle
                Handles.DrawAAConvexPolygon(triangleVertices);
            }
        }

        // Draw wiremesh
        Vector2[] meshVertices = rigidBody.MeshPoints.ToArray();
        DrawMeshWireframe(meshVertices, rigidBody.LineColor, lineThickness);

        // Draw spring
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

            // Draw spring
            DrawZigZagSpring(startPoint, endPoint, lerpColor, springLineThickness, springAmplitude, numSpringPoints);

            Gizmos.color = Color.red;
            float radius = 2.5f;
            Gizmos.DrawSphere(startPoint, radius);
            Gizmos.DrawSphere(endPoint, radius);
        }
    }

    // Draw fluid objects
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Selected)]
    static void DrawFluidObjects(SceneFluid fluid, GizmoType gizmoType)
    {
        if (fluid == null) return;

        fluid.SetPolygonData();

        if (fluid.editorRenderMethod == EditorRenderMethod.Particles)
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
        else if (fluid.editorRenderMethod == EditorRenderMethod.Triangulation)
        {
            // Cannot create a polygon with less than 3 points
            if (fluid.MeshPoints.Count < 3) return;

            // Triangulate the polygon
            Vector2[] polygonPoints = fluid.MeshPoints.ToArray();
            Triangulator triangulator = new Triangulator(polygonPoints);
            int[] indices = triangulator.Triangulate();

            // Convert Vector2 to Vector3 (z = 0)
            Vector3[] vertices = new Vector3[polygonPoints.Length];
            for (int i = 0; i < polygonPoints.Length; i++) vertices[i] = new Vector3(polygonPoints[i].x, polygonPoints[i].y, 0);

            // Draw triangles
            Handles.color = fluid.BodyColor;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3[] triangleVertices = new Vector3[3];
                triangleVertices[0] = vertices[indices[i]];
                triangleVertices[1] = vertices[indices[i + 1]];
                triangleVertices[2] = vertices[indices[i + 2]];

                // Draw the triangle
                Handles.DrawAAConvexPolygon(triangleVertices);
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
}
