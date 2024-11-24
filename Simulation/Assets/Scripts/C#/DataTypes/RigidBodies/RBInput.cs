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
    public float friction;

    [Header("Particle Interaction")]
    public float heatingStrength;

    [Header("Inter-RB Constraint")]
    public ConstraintType constraintType;
    public Vector2 localLinkPosThisRB;
    public Vector2 localLinkPosOtherRB;
    public SceneRigidBody linkedRigidBody;

    [Header("Spring Properties")]
    public float springStiffness;
    public float springRestLength;
    public float damping;

    [Header("Linear Motor Constraint")]
    public float lerpSpeed;
    [Range(0.0f, 1.0f)] public float lerpTimeOffset;
    public bool doRoundTrip;
    public Vector2 startPos;
    public Vector2 endPos;

    [Header("Starting Properties")]
    public Vector2 overrideCentroidPosition;
    public float angularVelocity;
    public Vector2 velocity;

    [Header("Display")]
    public int renderPriority;
    public int matIndex;
    public int springMatIndex;
}