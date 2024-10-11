using System;
using Unity.Mathematics;

[Serializable]
public struct RBInput
{
    public bool isStationary;
    public float mass;
    public float gravity;
    public float elasticity;
    public float2 velocity;
    public float rotationVelocity;
}