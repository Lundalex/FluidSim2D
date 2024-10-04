// --- Particle Simulation structs ---

struct PTypeStruct
{
    int FluidSpringsGroup;

    float SpringPlasticity;
    float SpringTolDeformation;
    float SpringStiffness;

    float ThermalConductivity;
    float SpecificHeatCapacity;
    float FreezeThreshold;
    float VaporizeThreshold;

    float Pressure;
    float NearPressure;

    float Mass;
    float TargetDensity;
    float Damping;
    float PassiveDamping;
    float Viscosity;
    float Stickyness;
    float Gravity;

    float InfluenceRadius;
    float colorG;
};
struct PDataStruct
{
    float2 PredPosition;
    float2 Position;
    float2 Velocity;
    float2 LastVelocity;
    float Density2;
    float NearDensity;
    float Temperature; // kelvin
    float TemperatureExchangeBuffer;
    int LastChunkKey_PType_POrder; // composed 3 int structure
    // POrder; // POrder is dynamic,
    // LastChunkKey; // 0 <= LastChunkKey <= ChunkNum
    // PType; // 0 <= PType <= PTypeNum
};
struct SpringStruct
{
    int PLinkedA;
    int PLinkedB;
    float RestLength;
};
struct StickynessRequestStruct
{
    int pIndex;
    int StickyLineIndex;
    float2 StickyLineDst;
    float absDstToLineSqr;
    float RBStickyness;
    float RBStickynessRange;
};

// --- Rigid Body structs ---

struct RBDataStruct
{
    float2 Position;
    float2 Velocity;
    // radians / second
    float AngularImpulse;
    float Stickyness;
    float StickynessRange;
    float StickynessRangeSqr;
    float2 NextPos;
    float2 NextVel;
    float NextAngImpulse;
    float Mass;
    int2 LineIndices;
    float MaxDstSqr;
    int WallCollision;
    int Stationary; // 1 -> Stationary, 0 -> Non-stationary
};
struct RBVectorStruct
{
    float2 Position;
    float2 LocalPosition;
    float3 ParentImpulse;
    int ParentRBIndex;
    int WallCollision;
};