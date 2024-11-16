using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct RBInput
{
    [Header("Object Type")]
    public bool includeInSimulation;
    public bool overrideCentroid;
    public bool isCollider;
    public bool canMove;
    public bool canRotate;

    [Header("Runtime Properties")]
    public float mass;
    public float gravity;
    public float elasticity;

    [Header("Particle Interaction")]
    public float heatingStrength;

    [Header("Inter-RB Links")]
    public LinkType linkType;
    public float springStiffness;
    public float springRestLength;
    public float damping;
    public float2 localLinkPosThisRB;
    public float2 localLinkPosOtherRB;
    public SceneRigidBody linkedRigidBody;

    [Header("Starting Velocities")]
    public float angularVelocity;
    public float2 velocity;

    [Header("Display")]
    public int renderPriority;
    public int matIndex;
    public int springMatIndex;
}