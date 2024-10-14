// --- Particle Simulation structs ---

struct PType 
{
    int fluidSpringGroup;

    float springPlasticity;
    float springTolDeformation;
    float springStiffness;

    float thermalConductivity;
    float specificHeatCapacity;
    float freezeThreshold;
    float vaporizeThreshold;

    float pressure;
    float nearPressure;

    float mass;
    float targetDensity;
    float damping;
    float passiveDamping;
    float viscosity;
    float stickyness;
    float gravity;

    float influenceRadius;
    float colorG;
};
struct PData
{
    float2 predPos;
    float2 pos;
    float2 vel;
    float2 lastVel;
    float density;
    float nearDensity;
    float temperature; // kelvin
    float temperatureExchangeBuffer;
    int lastChunkKey_PType_POrder; // composed 3 int structure
    // POrder; // POrder is dynamic, 
    // LastChunkKey; // 0 <= LastChunkKey <= ChunkNum
    // PType; // 0 <= PType <= PTypesNum
};
struct Spring 
{
    int pLinkedA;
    int pLinkedB;
    float restLength;
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
    int2 vel_AsInt;
    float2 nextPos;
    float2 nextVel;
    int rotVel_AsInt; // (radians / second)
    float mass; // 0 -> Stationary
    float inertia;
    float gravity;
    float elasticity;
    float maxRadiusSqr;
    int startIndex;
    int endIndex;
    float3 col;
    int renderPriority;
};

struct RBVector 
{
    float2 pos;
    int parentIndex;
};

struct RBAdjustment
{
    int2 deltaPos_Int2;
    int2 deltaVel_Int2;
    int deltaRotVel_Int;
};

struct RBHitInfo
{
    float dst;
    float2 hitPoint;
    float2 pointPos;
    float2 lineVec;
};

struct ImpulseData
{
    float2 centerImpulse;
    float rotImpulse;
    int rbIndex;
};

RBAdjustment InitRBAdjustment()
{
    RBAdjustment rbAdjustment;
    rbAdjustment.deltaPos_Int2 = 0;
    rbAdjustment.deltaVel_Int2 = 0;
    rbAdjustment.deltaRotVel_Int = 0;

    return rbAdjustment;
}

RBHitInfo InitRBHitInfo()
{
    RBHitInfo rbHitInfo;
    rbHitInfo.dst = 1.#INF;
    rbHitInfo.hitPoint = 1.#INF;
    rbHitInfo.pointPos = 1.#INF;
    rbHitInfo.lineVec = 0;

    return rbHitInfo;
}

RBHitInfo InitRBHitInfo(float dst, float2 hitPoint, float2 pointPos, float2 lineVec)
{
    RBHitInfo rbHitInfo;
    rbHitInfo.dst = dst;
    rbHitInfo.hitPoint = hitPoint;
    rbHitInfo.pointPos = pointPos;
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