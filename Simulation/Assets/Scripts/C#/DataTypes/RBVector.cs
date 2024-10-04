using Unity.Mathematics;

public struct RBVector
{
    public float2 Position;
    public float2 LocalPosition;
    public float3 ParentImpulse;
    public int ParentRBIndex;
    public int WallCollision;
};