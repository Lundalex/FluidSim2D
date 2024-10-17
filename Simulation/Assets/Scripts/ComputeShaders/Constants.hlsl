static const uint MAX_RIGIDBODIES_NUM = 10;

// --- Thread Nums ---

static const uint TN_MS = 32; // Marching Squares
static const uint TN_PS = 512; // Particle Simulation
static const uint TN_R = 32; // Renderer
static const uint TN_RBS1 = 64; // Rigid Body Simulation
static const uint TN_RBS2 = 32; // Rigid Body Simulation
static const uint TN_RBS3 = 512; // Rigid Body Simulation
static const uint TN_S = 512; // Sorter

static const float CENTROID_RADIUS = 2.0;
static const float CENTROID_RADIUS_SQR = CENTROID_RADIUS*CENTROID_RADIUS;
static const float4 COL_RED = float4(1, 0, 0, 1);