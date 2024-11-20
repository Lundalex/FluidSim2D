using UnityEditor;
using UnityEngine;

public class EditorManager : Editor
{
    private const float sceneObjectLineThickness = 0.5f;
    private const float sceneObjectSensorLineThickness = 0.5f;
    private const float springsceneObjectLineThickness = 2.0f;
    private const float springAmplitude = 7.0f;
    private const int numSpringPoints = 15;
    private const float springForceFactor = 1 / 50000.0f;
    private const float hoveredPointLineLength = 1000f;
    private const float hoveredPointAxisLineThickness = 0.4f;

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

    static void DrawMeshWireframe(Vector2[] meshVertices, Color color, float sceneObjectLineThickness)
    {
        int vertexCount = meshVertices.Length;

        Handles.color = color;
        for (int i = 0; i < vertexCount; i++)
        {
            Vector2 start = meshVertices[i];
            Vector2 end = meshVertices[(i + 1) % vertexCount];

            Vector2 edgeDir = (end - start).normalized;
            Vector2 perpDir = 0.5f * sceneObjectLineThickness * new Vector2(-edgeDir.y, edgeDir.x);

            Vector2 v1 = start + perpDir;
            Vector2 v2 = start - perpDir;
            Vector2 v3 = end + perpDir;
            Vector2 v4 = end - perpDir;

            Vector3[] quadVertices = new Vector3[] { v1, v3, v4, v2 };

            // Draw the quad for the edge
            Handles.DrawSolidRectangleWithOutline(quadVertices, color, color);
        }
    }

    public static void DrawZigZagSpring(Vector2 startPoint, Vector2 endPoint, Color color, float sceneObjectLineThickness, float amplitude, int pointCount)
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
            Vector2 segmentPerp = new Vector2(-segmentDirection.y, segmentDirection.x) * (sceneObjectLineThickness * 0.5f);

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
        DrawMeshWireframe(meshVertices, rigidBody.LineColor, sceneObjectLineThickness);

        // Draw spring
        if (rigidBody.RBInput.linkType == LinkType.Spring && rigidBody.RBInput.linkedRigidBody != null)
        {   
            float gridDensity = 3.0f; // A higher value results in a lower performance cost, but also slightly decreases centroid approximation accuracy
            (Vector2 startPoint, Vector2 endPoint) = GetSpringEndPoints(rigidBody, gridDensity);

            float approxLength = Mathf.Sqrt(Vector2.SqrMagnitude(startPoint - endPoint));
            float approxForce = rigidBody.RBInput.springStiffness * Mathf.Abs(rigidBody.RBInput.springRestLength - approxLength);
            
            rigidBody.approximatedSpringLength = approxLength.ToString();
            rigidBody.approximatedSpringForce = approxForce.ToString();

            Color springBaseColor = Color.green;
            Color springStressedColor = Color.red;
            Color lerpColor = Color.Lerp(springBaseColor, springStressedColor, approxForce * springForceFactor);

            // Draw spring
            DrawZigZagSpring(startPoint, endPoint, lerpColor, springsceneObjectLineThickness, springAmplitude, numSpringPoints);

            float radius = 2.5f;
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(startPoint, radius);
            Gizmos.DrawSphere(endPoint, radius);
        }
        else
        {
            if (rigidBody.RBInput.linkType == LinkType.Rigid)
            {
                // Calculate line points
                Vector2 thisRBCentroid = rigidBody.cashedCentroid;
                Vector2 LinkPos = rigidBody.RBInput.linkedRigidBody.cashedCentroid + (Vector2)rigidBody.RBInput.localLinkPosOtherRB;
                Vector2 otherRBCentroid = rigidBody.RBInput.linkedRigidBody.cashedCentroid;

                // Draw dashed line
                Color orangeColor = new(1.0f, 0.15f, 0.0f);
                DrawDashedLine(orangeColor, LinkPos, thisRBCentroid, 10, 3, rigidBody.EditorLineAnimationSpeed);
                DrawDashedLine(orangeColor, LinkPos, otherRBCentroid, 10, 3, rigidBody.EditorLineAnimationSpeed);
                
                // Draw start and end points
                Gizmos.color = Color.red;
                if (thisRBCentroid != Vector2.positiveInfinity) Gizmos.DrawSphere(thisRBCentroid, 2.5f);
                if (otherRBCentroid != Vector2.positiveInfinity) Gizmos.DrawSphere(otherRBCentroid, 2.5f);
                Gizmos.color = new(1.0f, 1.0f, 0.0f);
                if (LinkPos != Vector2.positiveInfinity) Gizmos.DrawSphere(LinkPos, 4.5f);
                Gizmos.color = new(1.0f, 0.05f, 0.0f);
                if (LinkPos != Vector2.positiveInfinity) Gizmos.DrawSphere(LinkPos, 3.5f);
            }

            rigidBody.approximatedSpringLength = "No Active Spring Link";
            rigidBody.approximatedSpringForce = "No Active Spring Link";
        }

        // Draw horizontal and vertical lines from any potentially hovered point
        DrawLinesFromHoveredPoint(rigidBody);
    }

    static void DrawLinesFromHoveredPoint(Polygon rigidBody)
    {
        if (rigidBody.polygonCollider == null || rigidBody.polygonCollider.points.Length == 0) return;

        // Get the points
        Vector2[] colliderPoints = rigidBody.polygonCollider.points;

        // Transform the local collider points to world space
        Vector3[] worldPoints = new Vector3[colliderPoints.Length];
        for (int i = 0; i < colliderPoints.Length; i++)
        {
            worldPoints[i] = rigidBody.transform.TransformPoint(colliderPoints[i]);
        }

        // Get mouse position in world space
        Event currentEvent = Event.current;
        Vector2 mousePosition = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition).origin;

        // Find the closest point to the mouse position within the proximity threshold
        int closestIndex = -1;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < worldPoints.Length; i++)
        {
            float distance = Vector2.Distance(mousePosition, worldPoints[i]);
            if (distance < 2 && distance < closestDistance)
            {
                closestIndex = i;
                closestDistance = distance;
            }
        }

        // If a point is close enough to the mouse cursor, draw lines from it
        if (closestIndex != -1)
        {
            Vector3 selectedPoint = worldPoints[closestIndex];

            Color hoveredPointDarkModeAxisColor = new(0.9f, 0.9f, 0.9f);

            ProgramLifeCycleManager lifeCycleManager = GameObject.FindGameObjectWithTag("LifeCycleManager").GetComponent<ProgramLifeCycleManager>();
            Handles.color = lifeCycleManager.darkMode ? hoveredPointDarkModeAxisColor : Color.black;

            // Draw horizontal line
            Vector3 leftPoint = new(-hoveredPointLineLength, selectedPoint.y, 0);
            Vector3 rightPoint = new(hoveredPointLineLength, selectedPoint.y, 0);
            DrawThickLine(leftPoint, rightPoint, hoveredPointAxisLineThickness);

            // Draw vertical line
            Vector3 topPoint = new(selectedPoint.x, hoveredPointLineLength, 0);
            Vector3 bottomPoint = new(selectedPoint.x, -hoveredPointLineLength, 0);
            DrawThickLine(topPoint, bottomPoint, hoveredPointAxisLineThickness);
        }
    }

    public static void DrawDashedLine(Color lineColor, Vector3 from, Vector3 to, float dashLength, float lineThickness, float animationSpeed, bool drawArrowHead = false, float arrowHeadSize = 5.0f) // float arrowHeadSize
    {
        Vector3 direction = (to - from).normalized;
        float distance = Vector3.Distance(from, to);
        int dashCount = Mathf.CeilToInt(distance / dashLength);

        // Calculate repeating animation offset
        float offset = ((Time.realtimeSinceStartup * animationSpeed) % (dashLength * 2)) / dashLength * dashLength;

        Handles.color = lineColor;
        for (int i = 0; i < dashCount; i++)
        {
            float startOffset = i * dashLength * 2 - offset;
            float endOffset = startOffset + dashLength;

            // Skip segments that are completely out of bounds
            if (startOffset >= distance) break;
            if (endOffset <= 0) continue;

            // Clamp the start and end points to the valid range
            startOffset = Mathf.Max(0, startOffset);
            endOffset = Mathf.Min(distance, endOffset);

            Vector3 start = from + direction * startOffset;
            Vector3 end = from + direction * endOffset;
            DrawThickLine(start, end, lineThickness);
        }

        // Draw arrowhead
        if (drawArrowHead)
        {
            Vector3 arrowBase = to - direction * arrowHeadSize;
            Vector3 left = Quaternion.AngleAxis(135, Vector3.forward) * direction * arrowHeadSize * 0.5f;
            Vector3 right = Quaternion.AngleAxis(-135, Vector3.forward) * direction * arrowHeadSize * 0.5f;

            Vector3[] triangle = { to, arrowBase + left, arrowBase + right };
            Handles.DrawAAConvexPolygon(triangle);
        }
    }

    private static void DrawThickLine(Vector3 start, Vector3 end, float thickness)
    {
        Vector3 offset = thickness * 0.5f * Vector3.Cross((end - start).normalized, Vector3.forward);
        Vector3[] quad = { start - offset, start + offset, end + offset, end - offset };
        Handles.DrawAAConvexPolygon(quad);
    }

    // Draw fluid objects
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Selected)]
    static void DrawFluidObjects(SceneFluid fluid, GizmoType gizmoType)
    {
        if (fluid == null) return;

        fluid.SetPolygonData();

        if (fluid.editorRenderMethod == EditorRenderMethod.Particles)
        {
            fluid.Points = fluid.GeneratePoints(-1).ToArray();

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
            Vector2 perpDir = 0.5f * sceneObjectLineThickness * new Vector2(-edgeDir.y, edgeDir.x);

            // Compute the four vertices of the quad
            Vector2 v1 = edge.start + perpDir;
            Vector2 v2 = edge.start - perpDir;
            Vector2 v3 = edge.end + perpDir;
            Vector2 v4 = edge.end - perpDir;

            Vector3[] quadVertices = new Vector3[] { v1, v3, v4, v2 };

            // Draw the quad
            Handles.DrawSolidRectangleWithOutline(quadVertices, fluid.LineColor, fluid.LineColor);
        }

        // Draw horizontal and vertical lines from any potentially hovered point
        DrawLinesFromHoveredPoint(fluid);
    }

    // Draw rigid body objects
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Selected)]
    static void DrawFluidSpawnerObjects(FluidSpawner fluidSpawner, GizmoType gizmoType)
    {
        if (fluidSpawner == null) return;

        // Update points
        fluidSpawner.SetPolygonData();

        // --- Draw the filled body using triangulation ---
        if (fluidSpawner.DoDrawBody)
        {
            if (fluidSpawner.MeshPoints.Count < 3)
            {
                // Cannot create a polygon with less than 3 points
                return;
            }

            // Triangulate the polygon
            Vector2[] polygonPoints = fluidSpawner.MeshPoints.ToArray();
            Triangulator triangulator = new Triangulator(polygonPoints);
            int[] indices = triangulator.Triangulate();

            // Convert Vector2 to Vector3 (z = 0)
            Vector3[] vertices = new Vector3[polygonPoints.Length];
            for (int i = 0; i < polygonPoints.Length; i++)
            {
                vertices[i] = new Vector3(polygonPoints[i].x, polygonPoints[i].y, 0);
            }

            // Draw triangles
            Handles.color = fluidSpawner.BodyColor;
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
        Vector2[] meshVertices = fluidSpawner.MeshPoints.ToArray();
        DrawMeshWireframe(meshVertices, fluidSpawner.LineColor, sceneObjectLineThickness);
    }

    // Draw fluid sensor objects
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Selected)]
    static void DrawFluidSensorObjects(FluidSensor fluidSensor, GizmoType gizmoType)
    {
        if (fluidSensor == null) return;

        Vector2 min = fluidSensor.measurementZone.min;
        Vector2 max = min + new Vector2(fluidSensor.measurementZone.width, fluidSensor.measurementZone.height);

        if (min == max) return;

        Vector2 v1 = min;
        Vector2 v2 = new(min.x, max.y);
        Vector2 v3 = new(max.x, min.y);
        Vector2 v4 = max;

        Vector2[] quadVertices_Vector2 = new Vector2[] { v1, v3, v4, v2 };
        Vector3[] quadVertices_Vector3 = new Vector3[] { v1, v3, v4, v2 };

        DrawMeshWireframe(quadVertices_Vector2, fluidSensor.lineColor, sceneObjectSensorLineThickness);
        Handles.color = fluidSensor.areaColor;
        Handles.DrawSolidRectangleWithOutline(quadVertices_Vector3, fluidSensor.areaColor, fluidSensor.areaColor);
    }
}