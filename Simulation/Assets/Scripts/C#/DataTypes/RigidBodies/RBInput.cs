using System;
using Unity.Mathematics;
using UnityEngine;

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
    public Color color;
    public int renderPriority;
}