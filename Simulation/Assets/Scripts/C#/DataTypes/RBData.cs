using Unity.Mathematics;

public struct RBData
{
    public float2 Position;
    public float2 Velocity;
    // radians / second
    public float AngularImpulse;

    public float Stickyness;
    public float StickynessRange;
    public float StickynessRangeSqr;
    public float2 NextPos;
    public float2 NextVel;
    public float NextAngImpulse;
    public float Mass;
    public int2 LineIndices;
    public float MaxDstSqr;
    public int WallCollision;
    public int Stationary; // 1 -> Stationary, 0 -> Non-stationary
};