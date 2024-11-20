using System;
using System.Collections.Generic;
using System.Linq;
using Resources2;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(PolygonCollider2D)), ExecuteAlways]
public class SceneRigidBody : Polygon
{
    // Public
    public bool DoCenterPosition = false;
    public bool DoDrawBody = true;
    public float EditorLineAnimationSpeed = 10;
    
    [Header("Simulation Object Settings")]
    [Range(0.1f, 10.0f)] public float defaultGridSpacing = 0.5f;
    public Sensor[] LinkedSensors;
    public RBInput RBInput;

    [Header("Estimated Spring Values At Start")]
    public string approximatedSpringLength;
    public string approximatedSpringForce;

    // NonSerialized
    [NonSerialized] public Vector2[] Points;
    [NonSerialized] public Vector2 lastPosition = Vector2.positiveInfinity;
    [NonSerialized] public Vector2 cashedCentroid = Vector2.positiveInfinity;
    [NonSerialized] public Vector2 cashedRelativeLinkPos;
    [NonSerialized] public LinkType lastLinkType;
    [NonSerialized] public Vector2 lastLocalLinkPosThisRB;
    [NonSerialized] public Vector2 lastLocalLinkPosOtherRB;
    [NonSerialized] public bool lastLinkTypeSet = false;

    // Editor
    private int frameCount = 0;
    private int framesSinceLastPositionChange = 0;
    private Vector2 lastFramePosition = Vector2.zero;

#region Editor
    private void OnEnable()
    {
    #if UNITY_EDITOR
        EditorApplication.update += EditorUpdate;
    #endif

        if (polygonCollider == null) polygonCollider = GetComponent<PolygonCollider2D>();
    }

    private void OnDisable()
    {
    #if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
    #endif
    }

    #if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (!Application.isPlaying)
        {
            // Avoid continuing if the position field is currently being modified
            if (lastFramePosition.x != transform.position.x || lastFramePosition.y != transform.position.y)
            {
                lastFramePosition = transform.position;
                framesSinceLastPositionChange = 0;
            }
            else framesSinceLastPositionChange++;
            if (framesSinceLastPositionChange < 10) return;

            // Check whether to center the position
            if (DoCenterPosition)
            {
                CenterPolygonPosition();
                DoCenterPosition = false;
            }

            // Check whether any positional data field has been modified, or each second of editor time
            bool forceUpdateCashedData = frameCount++ % 60 == 0;
            if ((lastPosition - (Vector2)transform.position).sqrMagnitude > 20.0f ||
                (lastLocalLinkPosThisRB - (Vector2)RBInput.localLinkPosThisRB).sqrMagnitude > 0.01f ||
                (lastLocalLinkPosOtherRB - (Vector2)RBInput.localLinkPosOtherRB).sqrMagnitude > 0.01f ||
                forceUpdateCashedData)
            {
                UpdateCashedData();
            }

            // Reset certain data is the linkType has been modified
            if (!lastLinkTypeSet)
            {
                lastLinkType = RBInput.linkType;
                lastLinkTypeSet = true;
            }
            if (lastLinkType != RBInput.linkType)
            {
                RBInput.localLinkPosOtherRB = Vector2.zero;
                RBInput.localLinkPosThisRB = Vector2.zero;
                lastLinkType = RBInput.linkType;
                CenterPolygonPosition();
            }

            SnapColliderPointsToGrid();
        }
    }
    #endif
#endregion

    private void UpdateCashedData()
    {
        // Recalculate calculation
        cashedCentroid = ComputeCentroid(defaultGridSpacing);

        if (RBInput.linkType == LinkType.Rigid)
        {
            Vector2 thisCentroid = cashedCentroid;
            Vector2 otherCentroid = RBInput.linkedRigidBody.cashedCentroid;
            Vector2 thisCentroidRelative = thisCentroid - lastPosition;
            Vector2 localLinkPosOther = (Vector2)RBInput.localLinkPosOtherRB;
            Vector2 localLinkPosThis = (Vector2)RBInput.localLinkPosThisRB;
            
            Vector2 newPos = otherCentroid - thisCentroidRelative + localLinkPosOther - localLinkPosThis;
            bool doUpdatePosition = newPos.x < float.MaxValue && newPos.y < float.MaxValue && (lastPosition - newPos).sqrMagnitude > 20.0f;
            if (doUpdatePosition)
            {
                transform.position = newPos;
                cashedRelativeLinkPos = thisCentroid + localLinkPosThis;
            }
        }

        // Record the current positional data
        if ((lastPosition - (Vector2)transform.position).sqrMagnitude > 20.0f) lastPosition = transform.position;
        lastLocalLinkPosThisRB = (Vector2)RBInput.localLinkPosThisRB;
        lastLocalLinkPosOtherRB = (Vector2)RBInput.localLinkPosOtherRB;
    }

    private void SnapColliderPointsToGrid()
    {
        if (snapPointToGrid)
        {
            Vector2[] points = polygonCollider.points;
            Transform colliderTransform = polygonCollider.transform;

            // Snap points to grid in world space
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 worldPoint = colliderTransform.TransformPoint(points[i]);

                worldPoint = new Vector2(
                    Mathf.Round(worldPoint.x / gridSpacing) * gridSpacing,
                    Mathf.Round(worldPoint.y / gridSpacing) * gridSpacing
                );

                points[i] = colliderTransform.InverseTransformPoint(worldPoint);
            }

            polygonCollider.points = points;

            if (!RBInput.overrideCentroid) CenterPolygonPosition();
        }
    }

    public Vector2[] GeneratePoints(float gridSpacing, Vector2 offset)
    {
        if (gridSpacing == 0) gridSpacing = defaultGridSpacing;

        SetPolygonData();

        List<Vector2> generatedPoints = new();

        // Find the bounding box of the polygon
        Vector2 min = Func.MinVector2(Edges.Select(edge => Func.MinVector2(edge.start, edge.end)).ToArray());
        Vector2 max = Func.MaxVector2(Edges.Select(edge => Func.MaxVector2(edge.start, edge.end)).ToArray());

        // Generate grid points within the bounding box
        for (float x = min.x; x <= max.x; x += gridSpacing)
        {
            for (float y = min.y; y <= max.y; y += gridSpacing)
            {
                Vector2 point = new Vector2(x, y) + offset;

                if (IsPointInsidePolygon(point))
                {
                    generatedPoints.Add(point);
                }
            }
        }

        return generatedPoints.ToArray();
    }

    public Vector2 ComputeCentroid(float gridSpacing)
    {
        if (RBInput.overrideCentroid) return transform.position;

        Vector2[] points = GeneratePoints(gridSpacing, Vector2.zero);
        int numPoints = points.Length;

        // Centroid
        Vector2 centroid = Vector2.zero;
        foreach (Vector2 point in points) centroid += point;
        centroid /= numPoints;

        return centroid;
    }

    public (float, float) ComputeInertiaAndBalanceRigidBody(ref Vector2[] vectors, ref Vector2 rigidBodyPosition, Vector2 offset, float? gridDensityInput = null)
    {
        float gridSpacing = gridDensityInput ?? 0.2f;
        
        Vector2[] points = GeneratePoints(gridSpacing, offset);
        int numPoints = points.Length;
        float pointMass = RBInput.mass / numPoints;

        // Centroid
        Vector2 centroid = Vector2.zero;
        if (RBInput.overrideCentroid) centroid = transform.position;
        else
        {
            foreach (Vector2 point in points) centroid += point;
            centroid /= numPoints;
        }

        // Shift vectors to align centroid with rigid body position
        Vector2 shift = rigidBodyPosition - centroid;
        for (int i = 0; i < vectors.Length; i++) vectors[i] += shift;
        rigidBodyPosition = centroid;

        // Inertia
        float inertia = 0.0f;
        float maxRadiusSqr = 0.0f;
        foreach (Vector2 point in points)
        {
            float dstSqr = (point - rigidBodyPosition).sqrMagnitude;
            inertia += pointMass * dstSqr;
        }

        // MaxRadiusSqr
        foreach (Vector2 vector in vectors) maxRadiusSqr = Mathf.Max(maxRadiusSqr, vector.sqrMagnitude);

        return (inertia, maxRadiusSqr);
    }

    public void CenterPolygonPosition()
    {
        // Get the collider's points
        Vector2[] points = polygonCollider.points;

        // Calculate the centroid of the collider in local space
        Vector2 centroid = Vector2.zero;
        foreach (Vector2 point in points)
        {
            centroid += point;
        }
        centroid /= points.Length;

        // Move the transform's position by the centroid offset
        Vector3 worldCentroidOffset = transform.TransformVector(centroid);
        transform.position += worldCentroidOffset;

        // Adjust points so that centroid is at local (0,0)
        for (int i = 0; i < points.Length; i++)
        {
            points[i] -= centroid;
        }

        // Apply the adjusted points back to the collider
        polygonCollider.points = points;

        // Update cashedCentroid
        cashedCentroid = ComputeCentroid(defaultGridSpacing);
    }
}