using System.Collections.Generic;
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

    private static readonly Color SpringBaseColor = Color.green;
    private static readonly Color SpringStressedColor = Color.red;
    private static readonly Color OrangeColor = new(1.0f, 0.15f, 0.0f);
    private static readonly Color HoveredPointDarkModeAxisColor = new(0.9f, 0.9f, 0.9f);

    void OnEnable() => EditorApplication.update += OnEditorUpdate;

    void OnDisable() => EditorApplication.update -= OnEditorUpdate;
    
    void OnEditorUpdate() {}

    // Draw rigid body objects
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Selected)]
    static void DrawRigidBodyObjects(SceneRigidBody rigidBody, GizmoType gizmoType)
    {
        if (rigidBody == null) return;
        
        // Update points
        rigidBody.SetPolygonData();

        // Calculate the parent offset
        Transform transform = rigidBody.transform;
        Vector2 parentOffset = transform.position - transform.localPosition;

        // Draw the filled body
        if (rigidBody.doDrawBody)
        {
            DrawFilledPolygon(rigidBody.MeshPoints, rigidBody.BodyColor);
        }

        // Draw wiremesh
        DrawMeshWireframe(rigidBody.MeshPoints.ToArray(), rigidBody.LineColor, sceneObjectLineThickness);

        bool isSpringConstraint = rigidBody.rbInput.constraintType == ConstraintType.Spring && rigidBody.rbInput.linkedRigidBody != null;
        bool isLinearMotor = rigidBody.rbInput.constraintType == ConstraintType.LinearMotor;
        bool isRigidConstraint = rigidBody.rbInput.constraintType == ConstraintType.Rigid && rigidBody.rbInput.linkedRigidBody != null;

        if (isSpringConstraint)
        {   
            (Vector2 startPoint, Vector2 endPoint) = GetSpringEndPoints(rigidBody);

            float approxLength = Mathf.Sqrt(Vector2.SqrMagnitude(startPoint - endPoint));
            float approxForce = rigidBody.rbInput.springStiffness * Mathf.Abs(rigidBody.rbInput.springRestLength - approxLength);
            
            rigidBody.approximatedSpringLength = approxLength.ToString();
            rigidBody.approximatedSpringForce = approxForce.ToString();

            Color lerpColor = Color.Lerp(SpringBaseColor, SpringStressedColor, approxForce * springForceFactor);

            // Draw spring
            DrawZigZagSpring(startPoint, endPoint, lerpColor, springsceneObjectLineThickness, springAmplitude, numSpringPoints);

            DrawDot(startPoint, 2.5f, Color.red);
            DrawDot(endPoint, 2.5f, Color.red);
        }
        else if (isLinearMotor)
        {
            Vector2 startPoint = rigidBody.rbInput.startPos + parentOffset;
            Vector2 endPoint = rigidBody.rbInput.endPos + parentOffset;

            DrawDashedLine(OrangeColor, endPoint, startPoint, 10, 3, rigidBody.editorLineAnimationSpeed);

            DrawDot(startPoint, 4.5f, new Color(1.0f, 1.0f, 0.0f));
            DrawDot(startPoint, 3.5f, new Color(1.0f, 0.05f, 0.0f));
            DrawDot(endPoint, 4.5f, new Color(1.0f, 1.0f, 0.0f));
            DrawDot(endPoint, 3.5f, new Color(1.0f, 0.05f, 0.0f));

            rigidBody.approximatedSpringLength = "No Active Spring Link";
            rigidBody.approximatedSpringForce = "No Active Spring Link";
        }
        else if (isRigidConstraint)
        {
            // Calculate line points
            Vector2 thisRBCentroid = rigidBody.cachedCentroid;
            Vector2 LinkPos = rigidBody.rbInput.linkedRigidBody.cachedCentroid + rigidBody.rbInput.localLinkPosOtherRB;
            Vector2 otherRBCentroid = rigidBody.rbInput.linkedRigidBody.cachedCentroid;

            // Draw dashed line
            DrawDashedLine(OrangeColor, LinkPos, thisRBCentroid, 10, 3, rigidBody.editorLineAnimationSpeed);
            DrawDashedLine(OrangeColor, LinkPos, otherRBCentroid, 10, 3, rigidBody.editorLineAnimationSpeed);
            
            // Draw start and end points
            DrawDot(thisRBCentroid, 2.5f, Color.red);
            DrawDot(otherRBCentroid, 2.5f, Color.red);
            DrawDot(LinkPos, 4.5f, new Color(1.0f, 1.0f, 0.0f));
            DrawDot(LinkPos, 3.5f, new Color(1.0f, 0.05f, 0.0f));
        }
        else
        {
            rigidBody.approximatedSpringLength = "No Active Spring Link";
            rigidBody.approximatedSpringForce = "No Active Spring Link";
        }

        // Draw horizontal and vertical lines from any potentially hovered point
        DrawLinesFromHoveredPoint(rigidBody);
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
            DrawFilledPolygon(fluid.MeshPoints, fluid.BodyColor);
        }
        
        // Draw edges
        foreach (Edge edge in fluid.Edges)
        {
            Vector3[] quadVertices = GetQuadVertices(edge.start, edge.end, sceneObjectLineThickness);

            // Draw the quad
            Handles.DrawSolidRectangleWithOutline(quadVertices, fluid.LineColor, fluid.LineColor);
        }

        // Draw horizontal and vertical lines from any potentially hovered point
        DrawLinesFromHoveredPoint(fluid);
    }

    // Draw fluid spawner objects
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Selected)]
    static void DrawFluidSpawnerObjects(FluidSpawner fluidSpawner, GizmoType gizmoType)
    {
        if (fluidSpawner == null) return;

        // Update points
        fluidSpawner.SetPolygonData();

        // Draw the filled body
        if (fluidSpawner.DoDrawBody)
        {
            DrawFilledPolygon(fluidSpawner.MeshPoints, fluidSpawner.BodyColor);
        }

        // Draw wiremesh
        DrawMeshWireframe(fluidSpawner.MeshPoints.ToArray(), fluidSpawner.LineColor, sceneObjectLineThickness);
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
        Vector2 v2 = new Vector2(min.x, max.y);
        Vector2 v3 = new Vector2(max.x, min.y);
        Vector2 v4 = max;

        Vector2[] quadVertices_Vector2 = new Vector2[] { v1, v3, v4, v2 };
        Vector3[] quadVertices_Vector3 = new Vector3[] { v1, v3, v4, v2 };

        DrawMeshWireframe(quadVertices_Vector2, fluidSensor.lineColor, sceneObjectSensorLineThickness);
        Handles.color = fluidSensor.areaColor;
        Handles.DrawSolidRectangleWithOutline(quadVertices_Vector3, fluidSensor.areaColor, fluidSensor.areaColor);
    }

    private static (Vector2 start, Vector2 end) GetSpringEndPoints(SceneRigidBody rigidBody)
    {
        SceneRigidBody otherRigidBody = rigidBody.rbInput.linkedRigidBody;

        // Get the world position of the link points
        Vector2 startPoint = rigidBody.cachedCentroid + rigidBody.rbInput.localLinkPosThisRB;
        Vector2 endPoint = otherRigidBody.cachedCentroid + rigidBody.rbInput.localLinkPosOtherRB;

        return (startPoint, endPoint);
    }

    private static void DrawZigZagSpring(Vector2 startPoint, Vector2 endPoint, Color color, float sceneObjectLineThickness, float amplitude, int pointCount)
    {
        // Calculate the direction and distance between the points
        Vector2 direction = (endPoint - startPoint).normalized;

        // Calculate the perpendicular direction
        Vector2 perpendicular = new(-direction.y, direction.x);

        // Previous point along the zigzag
        Vector2 lastPoint = startPoint;
        Handles.color = color;
        for (int i = 1; i < pointCount; i++)
        {
            // Position along the line
            float t = (float)i / (pointCount - 1);
            Vector2 pointOnLine = Vector2.Lerp(startPoint, endPoint, t);

            // Determine the offset direction
            float offsetMultiplier = (i % 2 == 0) ? -1.0f : 1.0f;
            if (i == pointCount - 1) offsetMultiplier = 0.0f;

            // Calculate the current point
            Vector2 offsetVector = perpendicular * amplitude * offsetMultiplier;
            Vector2 currentPoint = pointOnLine + offsetVector;

            // Get the line quad vertices
            Vector3[] quadVertices = GetQuadVertices(lastPoint, currentPoint, sceneObjectLineThickness);

            // Draw the quad
            Handles.DrawSolidRectangleWithOutline(quadVertices, color, color);

            // Update the previous point
            lastPoint = currentPoint;
        }
    }

    private static void DrawLinesFromHoveredPoint(Polygon rigidBody)
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
        if (currentEvent == null) return;
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

            ProgramLifeCycleManager lifeCycleManager = GameObject.FindGameObjectWithTag("LifeCycleManager").GetComponent<ProgramLifeCycleManager>();
            Handles.color = lifeCycleManager.darkMode ? HoveredPointDarkModeAxisColor : Color.black;

            // Draw horizontal line
            Vector3 leftPoint = new Vector3(-hoveredPointLineLength, selectedPoint.y, 0);
            Vector3 rightPoint = new Vector3(hoveredPointLineLength, selectedPoint.y, 0);
            DrawThickLine(leftPoint, rightPoint, hoveredPointAxisLineThickness);

            // Draw vertical line
            Vector3 topPoint = new Vector3(selectedPoint.x, hoveredPointLineLength, 0);
            Vector3 bottomPoint = new Vector3(selectedPoint.x, -hoveredPointLineLength, 0);
            DrawThickLine(topPoint, bottomPoint, hoveredPointAxisLineThickness);
        }
    }

    private static void DrawDot(Vector2 position, float size, Color color)
    {
        if (position != Vector2.positiveInfinity)
        {
            Gizmos.color = color;
            Gizmos.DrawSphere(position, size);
        }
    }

    private static void DrawDashedLine(Color lineColor, Vector3 from, Vector3 to, float dashLength, float lineThickness, float animationSpeed, bool drawArrowHead = false, float arrowHeadSize = 5.0f)
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
        Vector3[] quad = GetQuadVertices(start, end, thickness);
        Handles.DrawAAConvexPolygon(quad);
    }

    private static Vector3[] GetQuadVertices(Vector2 start, Vector2 end, float thickness)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x) * (thickness * 0.5f);

        Vector3[] quadVertices = new Vector3[4];
        quadVertices[0] = start + perpendicular;
        quadVertices[1] = start - perpendicular;
        quadVertices[2] = end - perpendicular;
        quadVertices[3] = end + perpendicular;

        return quadVertices;
    }

    private static void DrawMeshWireframe(Vector2[] meshVertices, Color color, float sceneObjectLineThickness)
    {
        int vertexCount = meshVertices.Length;

        Handles.color = color;
        for (int i = 0; i < vertexCount; i++)
        {
            Vector2 start = meshVertices[i];
            Vector2 end = meshVertices[(i + 1) % vertexCount];

            Vector3[] quadVertices = GetQuadVertices(start, end, sceneObjectLineThickness);

            // Draw the quad for the edge
            Handles.DrawSolidRectangleWithOutline(quadVertices, color, color);
        }
    }

    private static void DrawFilledPolygon(List<Vector2> meshPoints, Color fillColor)
    {
        // Can't draw a polygon out of less than three points
        if (meshPoints == null || meshPoints.Count < 3)
        {
            Debug.LogWarning("Cannot draw filled polygon with less than 3 points.");
            return;
        }

        // Get mesh points
        Vector2[] polygonPoints = meshPoints.ToArray();

        // Triangulate
        Triangulator triangulator = new Triangulator(polygonPoints);
        int[] indices = triangulator.Triangulate();

        // Get the vertices
        Vector3[] vertices = new Vector3[polygonPoints.Length];
        for (int i = 0; i < polygonPoints.Length; i++)
        {
            vertices[i] = new Vector3(polygonPoints[i].x, polygonPoints[i].y, 0);
        }

        // Draw the triangulated mesh
        Handles.color = fillColor;
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3[] triangleVertices = new Vector3[3];
            triangleVertices[0] = vertices[indices[i]];
            triangleVertices[1] = vertices[indices[i + 1]];
            triangleVertices[2] = vertices[indices[i + 2]];

            Handles.DrawAAConvexPolygon(triangleVertices);
        }
    }
}