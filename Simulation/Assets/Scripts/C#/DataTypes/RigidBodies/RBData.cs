using Unity.Mathematics;

public struct RBData
{
    public float2 pos;
    public int2 vel_AsInt2;
    public float2 nextPos;
    public float2 nextVel;
    public float rotVel_AsInt; // (radians / second)
    public float mass; // 0 -> Stationary
    public float inertia;
    public float gravity;
    public float elasticity;
    public float maxRadiusSqr;
    public int startIndex;
    public int endIndex;
    public float3 col;
    public int renderPriority;
};