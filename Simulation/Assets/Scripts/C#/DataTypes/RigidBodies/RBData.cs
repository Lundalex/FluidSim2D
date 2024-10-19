using Unity.Mathematics;

public struct RBData 
{
    public float2 pos;
    public int2 vel_AsInt2;
    public float2 nextPos;
    public float2 nextVel;
    public int rotVel_AsInt; // (radians / second)
    public float totRot;
    public float mass; // 0 -> Stationary
    public float inertia;
    public float gravity;
    public float elasticity;
    public float maxRadiusSqr;
    public int startIndex;
    public int endIndex;

    // Inter-RB spring links
    public int linkedRBIndex; // -1 -> No link
    public float springStiffness; // 0 -> Fully rigid constraint
    public float springRestLength;
    public float damping;
    public float2 localLinkPosThisRB;
    public float2 localLinkPosOtherRB;

    // Recorded spring force
    public float recordedSpringForce;

    // Display
    public int renderPriority;
    public int matIndex;
};