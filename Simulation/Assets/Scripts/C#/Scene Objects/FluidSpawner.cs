using Resources2;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class FluidSpawner : Polygon
{
    // Serialized fields
    [SerializeField] private int pTypeIndex = 0;

    // References
    private Main main;

    PData InitPData(Vector2 pos, Vector2 vel, float tempCelsius)
    {
        return new PData
        {
            predPos = new float2(0.0f, 0.0f),
            pos = pos,
            vel = vel,
            lastVel = new float2(0.0f, 0.0f),
            density = 0.0f,
            nearDensity = 0.0f,
            temperature = Utils.CelsiusToKelvin(tempCelsius),
            temperatureExchangeBuffer = 0.0f,
            lastChunkKey_PType_POrder = pTypeIndex * main.ChunksNumAll // flattened equivelant to PType = 1
        };
    }
}