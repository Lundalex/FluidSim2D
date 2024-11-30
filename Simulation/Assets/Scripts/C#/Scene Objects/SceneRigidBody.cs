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
    public bool doCenterPosition = false;
    public bool doDrawBody = true;
    public float editorLineAnimationSpeed = 10;
    
    [Header("Simulation Object Settings")]
    [Range(0.1f, 10.0f)] public float defaultGridSpacing = 0.5f;
    public bool addInBetweenPoints = true;
    public bool doRecursiveSubdivisison = false;
    [Range(0.5f, 10.0f)] public float minDstForSubDivision = 0;
    public Sensor[] linkedSensors;
    public RBInput rbInput;

    [Header("Estimated Spring Values At Start")]
    public string approximatedSpringLength;
    public string approximatedSpringForce;

    // NonSerialized
    [NonSerialized] public Vector2[] points;
    [NonSerialized] public Vector2 lastPosition = Vector2.positiveInfinity;
    [NonSerialized] public Vector2 cachedCentroid = Vector2.positiveInfinity;
    [NonSerialized] public Vector2 cachedRelativeLinkPos;
    [NonSerialized] public Vector2 cachedLinearMotorOffset;
    [NonSerialized] public ConstraintType lastLinkType;
    [NonSerialized] public Vector2 lastLocalLinkPosThisRB;
    [NonSerialized] public Vector2 lastLocalLinkPosOtherRB;
    [NonSerialized] public bool lastLinkTypeSet = false;

    // Editor
    private int frameCount = 0;
    private int framesSinceLastPositionChange = 0;
    private Vector2 lastFramePosition = Vector2.zero;
    private float lastFrameLerpTimeOffset = 0;
    private Vector2 lastStartPos = Vector2.zero;
    private Vector2 lastEndPos = Vector2.zero;
    private bool lastOverrideCentroid = false;
    private bool lastOverrideCentroidSet = false;

    #region Editor
    #if UNITY_EDITOR
    public override void OnEditorUpdate()
    {
        if (polygonCollider == null) polygonCollider = GetComponent<PolygonCollider2D>();

        // Check whether any linear motor data has been modified
        float newLerpTimeOffset = rbInput.lerpTimeOffset;
        Vector2 newStartPos = rbInput.startPos;
        Vector2 newEndPos = rbInput.endPos;
        if (newLerpTimeOffset != lastFrameLerpTimeOffset || newStartPos != lastStartPos || newEndPos != lastEndPos)
        {
            lastFrameLerpTimeOffset = newLerpTimeOffset;
            lastStartPos = newStartPos;
            lastEndPos = newEndPos;
            cachedLinearMotorOffset = GetLinearMotorOffset();
            transform.localPosition = rbInput.startPos + cachedLinearMotorOffset;
        }

        if (!lastOverrideCentroidSet)
        {
            lastOverrideCentroid = rbInput.overrideCentroid;
            lastOverrideCentroidSet = true;
        }

        if (rbInput.overrideCentroid != lastOverrideCentroid)
        {
            lastOverrideCentroid = rbInput.overrideCentroid;
            rbInput.overrideCentroidPosition = transform.position;
        }

        // Avoid continuing if the position field is currently being modified
        if (lastFramePosition != (Vector2)transform.localPosition)
        {
            lastFramePosition = transform.localPosition;
            framesSinceLastPositionChange = 0;
        }
        else framesSinceLastPositionChange++;
        if (framesSinceLastPositionChange < 20) return;

        // Check whether to center the position
        if (doCenterPosition)
        {
            CenterPolygonPosition();
            doCenterPosition = false;
        }

        // Check whether any positional data field has been modified, or each second of editor time
        bool forceUpdateCachedData = frameCount++ % 10 == 0;
        if ((lastPosition - (Vector2)transform.localPosition).sqrMagnitude > 10.0f ||
            (lastLocalLinkPosThisRB - rbInput.localLinkPosThisRB).sqrMagnitude > 0.01f ||
            (lastLocalLinkPosOtherRB - rbInput.localLinkPosOtherRB).sqrMagnitude > 0.01f ||
            forceUpdateCachedData)
        {
            UpdateCachedData();
        }

        // Reset certain data is the constraintType has been modified
        if (!lastLinkTypeSet)
        {
            lastLinkType = rbInput.constraintType;
            lastLinkTypeSet = true;
        }
        if (lastLinkType != rbInput.constraintType)
        {
            lastLinkType = rbInput.constraintType;
            CenterPolygonPosition();
        }

        SnapColliderPointsToGrid();
    }
    #endif
    #endregion

    private void UpdateCachedData()
    {
        cachedCentroid = ComputeCentroid(defaultGridSpacing);

        if (rbInput.constraintType == ConstraintType.Rigid)
        {
            if (rbInput.linkedRigidBody == null)
            {
                Debug.LogWarning("Linked rigid body not set. SceneRigidBody: " + name);
                return;
            }

            Vector2 thisCentroid = cachedCentroid;
            Vector2 otherCentroid = rbInput.linkedRigidBody.cachedCentroid;
            Vector2 thisCentroidRelative = thisCentroid - lastPosition;
            Vector2 localLinkPosOther = rbInput.localLinkPosOtherRB;
            Vector2 localLinkPosThis = rbInput.localLinkPosThisRB;
            
            Vector2 newPos = otherCentroid - thisCentroidRelative + localLinkPosOther - localLinkPosThis;
            bool doUpdatePosition = newPos.x < float.MaxValue && newPos.y < float.MaxValue && (lastPosition - newPos).sqrMagnitude > 10.0f;
            if (doUpdatePosition)
            {
                transform.localPosition = newPos;
                cachedRelativeLinkPos = thisCentroid + localLinkPosThis;
            }
        }
        else if (rbInput.constraintType == ConstraintType.LinearMotor)
            transform.localPosition = rbInput.startPos + cachedLinearMotorOffset;

        if ((lastPosition - (Vector2)transform.localPosition).sqrMagnitude > 10.0f)
            lastPosition = transform.localPosition;
        lastLocalLinkPosThisRB = rbInput.localLinkPosThisRB;
        lastLocalLinkPosOtherRB = rbInput.localLinkPosOtherRB;
    }

    private void SnapColliderPointsToGrid()
    {
        if (snapPointToGrid)
        {
            Vector2[] points = polygonCollider.points;
            Transform colliderTransform = polygonCollider.transform;

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

            if (!rbInput.overrideCentroid) CenterPolygonPosition();
        }
    }

    public Vector2[] GeneratePoints(float gridSpacing, Vector2 offset)
    {
        if (gridSpacing == 0) gridSpacing = defaultGridSpacing;

        SetPolygonData();

        List<Vector2> generatedPoints = new();

        Vector2 min = Func.MinVector2(Edges.Select(edge => Func.MinVector2(edge.start, edge.end)).ToArray());
        Vector2 max = Func.MaxVector2(Edges.Select(edge => Func.MaxVector2(edge.start, edge.end)).ToArray());

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
        // Check for alternative centroids
        (bool hasAltCentroid, Vector2 altCentroid) = GetAlternativeCentroid();
        if (hasAltCentroid) return altCentroid;

        // Get points
        Vector2[] points = GeneratePoints(gridSpacing, Vector2.zero);
        int numPoints = points.Length;
        
        // Compute the centroid as normal
        Vector2 centroid = Vector2.zero;
        foreach (Vector2 point in points) centroid += point;
        centroid /= numPoints;

        cachedCentroid = centroid;
        return centroid;
    }

    public (float, float) ComputeInertiaAndBalanceRigidBody(ref Vector2[] vectors, ref Vector2 rigidBodyPosition, Vector2 offset, float? gridDensityInput = null)
    {
        float gridSpacing = gridDensityInput ?? 0.2f;
        
        // Get points
        Vector2[] points = GeneratePoints(gridSpacing, offset);
        int numPoints = points.Length;
        float pointMass = rbInput.mass / numPoints;

        // Check for alternative centroids
        Vector2 centroid = Vector2.zero;
        (bool hasAltCentroid, Vector2 altCentroid) = GetAlternativeCentroid();
        if (hasAltCentroid)
        {
            cachedCentroid = centroid = altCentroid;
        }
        else // Compute the centroid as normal
        {
            foreach (Vector2 point in points) centroid += point;
            centroid /= numPoints;
            cachedCentroid = centroid;
        }

        // Shift all vectors of the polygon to be centered with respect to the centroid
        Vector2 shift = rigidBodyPosition - centroid;
        for (int i = 0; i < vectors.Length; i++) vectors[i] += shift;
        rigidBodyPosition = centroid;

        // Calculate the inertia
        float inertia = 0.0f;
        foreach (Vector2 point in points)
        {
            float dstSqr = (point - rigidBodyPosition).sqrMagnitude;
            inertia += dstSqr;
        }
        inertia *= pointMass;

        // Calculate the squared distance from the centroid to the furthest vector
        float maxRadiusSqr = 0.0f;
        foreach (Vector2 vector in vectors) maxRadiusSqr = Mathf.Max(maxRadiusSqr, vector.sqrMagnitude);

        return (inertia, maxRadiusSqr);
    }

    private (bool hasAltCentroid, Vector2 altCentroid) GetAlternativeCentroid()
    {
        if (rbInput.overrideCentroid)
        {
            cachedCentroid = rbInput.overrideCentroidPosition + (Vector2)transform.position - (Vector2)transform.localPosition;
            return (true, cachedCentroid);
        }
        else if (rbInput.constraintType == ConstraintType.LinearMotor)
        {
            cachedLinearMotorOffset = GetLinearMotorOffset();
            transform.localPosition = rbInput.startPos + cachedLinearMotorOffset;
            cachedCentroid = rbInput.startPos + cachedLinearMotorOffset + (Vector2)transform.position - (Vector2)transform.localPosition;
            return (true, cachedCentroid);
        }
        return (false, Vector2.positiveInfinity);
    }

    public Vector2 GetLinearMotorOffset()
    {
        if (rbInput.constraintType == ConstraintType.LinearMotor)
        {
            float t;
            if (rbInput.doRoundTrip)
            {
                t = (Mathf.Sin((rbInput.lerpTimeOffset + 0.75f) * Mathf.PI * 2.0f) + 1.0f) * 0.5f;
            }
            else
            {
                t = rbInput.lerpTimeOffset % 1.0f;
            }
            return Func.LerpVector2(rbInput.startPos, rbInput.endPos, t) - rbInput.startPos;
        }
        return Vector2.zero;
    }
}
