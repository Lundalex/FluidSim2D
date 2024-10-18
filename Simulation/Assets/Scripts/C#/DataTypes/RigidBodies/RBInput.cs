using System;
using Unity.Mathematics;

[Serializable]
public struct RBInput
{
    public bool includeInSimulation;
    public float mass;
    public float gravity;
    public float elasticity;
    public bool canMove;
    public float2 velocity;
    public bool canRotate;
    public float rotationVelocity;

    // Inter-RB spring links
    public bool enableSpringLink;
    public bool rigidConstraint;
    public int linkedRBIndex;
    public float springStiffness;
    public float springRestLength;
    public float damping;
    public float2 localLinkPosThisRB;
    public float2 localLinkPosOtherRB;

    // Display
    public int renderPriority;
    public int matIndex;
}