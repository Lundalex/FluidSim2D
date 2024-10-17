using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct MatInput
{
    public Texture2D colorTexture;
    public float colorTextureScale;
    public float opacity;
    public float3 baseColor;
    public float3 sampleColorMultiplier;
    public float3 edgeColor;
};