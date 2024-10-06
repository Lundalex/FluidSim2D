using Unity.Mathematics;

public struct PData
{
    public float2 PredPosition;
    public float2 Position;
    public float2 Velocity;
    public float2 LastVelocity;
    public float Density;
    public float NearDensity;
    public float Temperature; // kelvin
    public float TemperatureExchangeBuffer;
    public int LastChunkKey_PType_POrder; // composed 3 int structure
    // POrder; // POrder is dynamic, 
    // LastChunkKey; // 0 <= LastChunkKey <= ChunkNum
    // PType; // 0 <= PType <= PTypeNum
}