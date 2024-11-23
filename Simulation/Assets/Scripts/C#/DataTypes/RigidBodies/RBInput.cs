using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct RBInput
{
    [Header("Object Type")]
    public bool includeInSimulation;
    public bool overrideCentroid;
    public bool isInteractable;
    public bool isCollider;
    public bool canMove;
    public bool canRotate;

    [Header("Runtime Properties")]
    public float mass;
    public float gravity;
    public float elasticity;

    [Header("Particle Interaction")]
    public float heatingStrength;

    [Header("Inter-RB Constraint")]
    public ConstraintType constraintType;
    public float2 localLinkPosThisRB;
    public float2 localLinkPosOtherRB;
    public SceneRigidBody linkedRigidBody;

    [Header("Spring Properties")]
    public float springStiffness;
    public float springRestLength;
    public float damping;

    [Header("Linear Motor Constraint")]
    public float lerpSpeed;
    [Range(0.0f, 1.0f)] public float lerpTimeOffset;
    public bool doRoundTrip;
    public float2 startPos;
    public float2 endPos;

    [Header("Starting Properties")]
    public float2 overrideCentroidPosition;
    public float angularVelocity;
    public float2 velocity;

    [Header("Display")]
    public int renderPriority;
    public int matIndex;
    public int springMatIndex;
}