using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct MatInput
{
    public Texture2D texture;
    public float alpha;
    public float3 edgeCol;
};