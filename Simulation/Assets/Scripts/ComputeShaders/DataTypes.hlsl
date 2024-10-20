// --- Particle Simulation structs ---

struct Mat
{
    int2 colTexLoc;
    int2 colTexDims;
    float colTexUpScaleFactor;
    float3 baseCol;
    float opacity;
    float3 sampleColMul;
    float3 edgeCol;
};
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
    float gravity;

    float influenceRadius;

    int matIndex;
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

// --- Rigid Body structs ---

struct RigidBody 
{
    float2 pos;
    int2 vel_AsInt2;
    float2 nextPos;
    float2 nextVel;
    int rotVel_AsInt; // (radians / second)
    float totRot;
    float mass; // 0 -> Stationary
    float inertia;
    float gravity;
    float elasticity;
    float maxRadiusSqr;
    int startIndex;
    int endIndex;

    // Inter-RB spring links
    int linkedRBIndex; // -1 -> No link
    float springStiffness; // 0 -> Fully rigid constraint
    float springRestLength;
    float damping;
    float2 localLinkPosThisRB;
    float2 localLinkPosOtherRB;

    // Recorded spring force
    float recordedSpringForce;

    // Display
    int renderPriority;
    int matIndex;
    int springMatIndex;
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

    int recordedSpringForce_Int;
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
    rbAdjustment.recordedSpringForce_Int = 0;

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