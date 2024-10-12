using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct RBInput
{
    public bool isPassive;
    public float mass;
    public float gravity;
    public float elasticity;
    public float2 velocity;
    public float rotationVelocity;
    public Color color;
}