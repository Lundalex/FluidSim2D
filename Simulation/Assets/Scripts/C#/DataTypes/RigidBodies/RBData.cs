using Unity.Mathematics;

public struct RBData
{
    public float2 pos;
    public float2 vel;
    public float2 nextPos;
    public float2 nextVel;
    public float rot; // (radians)
    public float rotVel; // (radians / second)
    public float mass; // 0 -> Stationary
    public float gravity;
    public float maxRadiusSqr;
    public int startIndex;
    public int endIndex;
};