// --- Particle Simulation structs ---

struct PType 
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
struct PData 
{
    float2 PredPosition;
    float2 pos;
    float2 Velocity;
    float2 LastVelocity;
    float Density2;
    float NearDensity;
    float Temperature; // kelvin
    float TemperatureExchangeBuffer;
    int LastChunkKey_PType_POrder; // composed 3 int structure
    // POrder; // POrder is dynamic,
    // LastChunkKey; // 0 <= LastChunkKey <= ChunkNum
    // PType; // 0 <= PType <= PTypesNum
};
struct Spring 
{
    int PLinkedA;
    int PLinkedB;
    float RestLength;
};
struct StickynessRequest 
{
    int pIndex;
    int StickyLineIndex;
    float2 StickyLineDst;
    float absDstToLineSqr;
    float RBStickyness;
    float RBStickynessRange;
};

// --- Rigid Body structs ---

struct RigidBody 
{
    float2 pos;
    float2 vel;
    float2 nextPos;
    float2 nextVel;
    float rot; // (radians)
    float rotVel; // (radians / second)
    float mass; // 0 -> Stationary
    float gravity;
    float elasticity;
    float maxRadiusSqr;
    int startIndex;
    int endIndex;
};

struct RBVector 
{
    float2 pos;
};

struct Ray
{
    float2 pos;
    float2 dir;
    float2 invDir;
};

struct RBHitInfo
{
    float dst;
    float2 hitPoint;
    float2 lineVec;
};

struct ImpulseData
{
    float2 centerImpulse;
    float rotImpulse;
    int rbIndex;
};

Ray InitRay()
{
    Ray ray;
    ray.pos = 0;
    ray.dir = 0;
    ray.invDir = 0;
}

Ray InitRay(float2 pos, float2 dir)
{
    Ray ray;
    ray.pos = pos;
    ray.dir = dir;
    ray.invDir = 1 / dir;
}

RBHitInfo InitRBHitInfo()
{
    RBHitInfo rbHitInfo;
    rbHitInfo.dst = 1.#INF;
    rbHitInfo.hitPoint = 1.#INF;
    rbHitInfo.lineVec = 0;

    return rbHitInfo;
}

RBHitInfo InitRBHitInfo(float dst, float2 hitPoint, float2 lineVec)
{
    RBHitInfo rbHitInfo;
    rbHitInfo.dst = dst;
    rbHitInfo.hitPoint = hitPoint;
    rbHitInfo.lineVec = lineVec;

    return rbHitInfo;
}

ImpulseData InitImpulseData(float3 combinedImpulse, int rbIndex)
{
    ImpulseData impulseData;
    impulseData.centerImpulse = combinedImpulse.xy;
    impulseData.rotImpulse = combinedImpulse.z;
    impulseData.rbIndex = rbIndex;

    return impulseData;
}