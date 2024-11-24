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
    private bool lastOverrideCentroid = false;
    private bool lastOverrideCentroidSet = false;

    #region Editor
    #if UNITY_EDITOR
    public override void OnEditorUpdate()
    {
        if (polygonCollider == null) polygonCollider = GetComponent<PolygonCollider2D>();

        if (!Application.isPlaying)
        {      
            // Check whether to update the linear motor offset
            float newLerpTimeOffset = rbInput.lerpTimeOffset;
            if (newLerpTimeOffset != lastFrameLerpTimeOffset)
            {
                lastFrameLerpTimeOffset = newLerpTimeOffset;
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
        if (rbInput.overrideCentroid)
            return (Vector3)rbInput.overrideCentroidPosition + transform.position - transform.localPosition;

        Vector2[] points = GeneratePoints(gridSpacing, Vector2.zero);
        int numPoints = points.Length;

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
        float pointMass = rbInput.mass / numPoints;

        Vector2 centroid = Vector2.zero;
        if (rbInput.overrideCentroid)
            centroid = (Vector3)rbInput.overrideCentroidPosition + transform.position - transform.localPosition;
        else
        {
            foreach (Vector2 point in points) centroid += point;
            centroid /= numPoints;
        }

        Vector2 shift = rigidBodyPosition - centroid;
        for (int i = 0; i < vectors.Length; i++) vectors[i] += shift;
        rigidBodyPosition = centroid;

        float inertia = 0.0f;
        float maxRadiusSqr = 0.0f;
        foreach (Vector2 point in points)
        {
            float dstSqr = (point - rigidBodyPosition).sqrMagnitude;
            inertia += pointMass * dstSqr;
        }

        foreach (Vector2 vector in vectors) maxRadiusSqr = Mathf.Max(maxRadiusSqr, vector.sqrMagnitude);

        return (inertia, maxRadiusSqr);
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
