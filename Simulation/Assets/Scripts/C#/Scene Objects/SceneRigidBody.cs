using System;
using System.Collections.Generic;
using System.Linq;
using Resources2;
using UnityEngine;
using UnityEditor;

[RequireComponent(typeof(PolygonCollider2D)), ExecuteAlways]
public class SceneRigidBody : Polygon
{
    // Public
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
    private int frameCount = 0;

#region Editor
    private void OnEnable()
    {
    #if UNITY_EDITOR
        EditorApplication.update += EditorUpdate;
    #endif
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
            // Check whether any positional data field has been modified, or each second of editor time
            if ((lastPosition - (Vector2)transform.position).sqrMagnitude > 0.01f ||
                (lastLocalLinkPosThisRB - (Vector2)RBInput.localLinkPosThisRB).sqrMagnitude > 0.01f ||
                (lastLocalLinkPosOtherRB - (Vector2)RBInput.localLinkPosOtherRB).sqrMagnitude > 0.01f ||
                frameCount++ % 60 == 0)
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
            }
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
            if (newPos.x < float.MaxValue && newPos.y < float.MaxValue) transform.position = newPos;
            cashedRelativeLinkPos = thisCentroid + localLinkPosThis;
        }

        // Record the current positional data
        lastPosition = transform.position;
        lastLocalLinkPosThisRB = (Vector2)RBInput.localLinkPosThisRB;
        lastLocalLinkPosOtherRB = (Vector2)RBInput.localLinkPosOtherRB;
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
}