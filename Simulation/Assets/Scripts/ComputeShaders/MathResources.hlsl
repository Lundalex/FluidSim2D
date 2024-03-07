static const float PI = 3.14;
static const float SmoothViscosityLaplacianFactor = 45 / PI;


// -- Optimised SPH kernel functions --

// InteractionInfluence_optimised() is equivelant to InteractionInfluence()
// Using a fast sqrt() had worse performance results than the regular sqrt()
float InteractionInfluence_optimised(float dst, float radius)
{
	if (dst < radius)
	{
		return sqrt(radius - dst);
	}
	return 0;
}

float SmoothLiquid_optimised(float dst, float radius)
{
	if (dst < radius)
	{
        float dstR = dst / radius;
        float dstR_1 = 1 - dstR;
		return dstR_1 * dstR_1;
	}
	return 0;
}

float SmoothLiquidNear_optimised(float dst, float radius)
{
	if (dst < radius)
	{
        float dstR = dst / radius;
        float dstR_1 = 1 - dstR;
		return dstR_1 * dstR_1 * dstR_1;
	}
	return 0;
}

float SmoothLiquidDer_optimised(float dst, float radius)
{
	if (dst < radius)
	{
        float dstR = dst / radius;
		return -2 * (1 - dstR) / radius;
	}
	return 0;
}

float SmoothLiquidNearDer_optimised(float dst, float radius)
{
	if (dst < radius)
	{
        float dstR = dst / radius;
        float dstR_1 = 1 - dstR;
		return -3 * dstR_1 * dstR_1 / radius;
	}
	return 0;
}

float SmoothViscosityLaplacian_optimised(float dst, float radius)
{
	if (dst < radius)
	{
		float radius6 = radius*radius*radius*radius*radius*radius;
		return SmoothViscosityLaplacianFactor * (radius - dst) / radius6;
	}
	return 0;
}


// -- Non-optimised SPH kernel functions --

float InteractionInfluence(float dst, float radius)
{
	if (dst < radius)
	{
		return sqrt(radius - dst);
	}
	return 0;
}

float SmoothLiquid(float dst, float radius)
{
	if (dst < radius)
	{
        float dstR = dst / radius;
		return (1 - dstR)*(1 - dstR);
	}
	return 0;
}

float SmoothLiquidDer(float dst, float radius)
{
	if (dst < radius)
	{
        float dstR = dst / radius;
		return -2 * (1 - dstR) / radius;
	}
	return 0;
}

float SmoothLiquidNear(float dst, float radius)
{
	if (dst < radius)
	{
        float dstR = dst / radius;
		return (1 - dstR)*(1 - dstR)*(1 - dstR);
	}
	return 0;
}

float SmoothLiquidNearDer(float dst, float radius)
{
	if (dst < radius)
	{
        float dstR = dst / radius;
		return -3 * (1 - dstR)*(1 - dstR) / radius;
	}
	return 0;
}

float SmoothViscosityLaplacian(float dst, float radius)
{
	if (dst < radius)
	{
	    float radius6 = radius*radius*radius*radius*radius*radius;
		return 45 / (PI * radius6) * (radius - dst);
	}
	return 0;
}


// -- General math functions --


// ΔQ_ij = Δt * avg_k * diff_T * W_ij / dst_ij 
// where:
// avg_k: average thermal conductivity
// W: contact area between the liquids (W(absDst))
// diff_T: difference in temperature
// dst: absDst
// Δt: DeltaTime
float LiquidTemperatureExchangeModel(float avg_k, float diff_T, float W, float dst, float DeltaTime)
{
    return DeltaTime * avg_k * diff_T * W / dst;
}

float LiquidSpringForceModel(float stiffness, float restLen, float maxLen, float curLen)
{
    return stiffness * (restLen - curLen); // * (1 - curLen/maxLen)?
}

float LiquidSpringPlasticityModel(float plasticity, int sgnDiffMng, float absDiffMng, float tolDeformation, float DeltaTime)
{
    return plasticity * sgnDiffMng * max(0, absDiffMng - tolDeformation) * DeltaTime;
}

float RBPStickynessModel(float stickyness, float dst, float maxDst)
{
	return stickyness * dst * (1 - dst/maxDst);
}

float avg(float a, float b)
{
    return (a + b) * 0.5;
}

float sqr(float a)
{
	return a * a;
}

float rand(float n)
{
    return frac(sin(n) * 43758.5453);
}

float lerp1D(float posA, float posB, float valA, float valB, float targetVal)
{
    float t = float(targetVal - valA) / float(valB - valA);
    return posA + t * (posB - posA);
}

float cross2d(float2 VectorA, float2 VectorB)
{
    return VectorA.x * VectorB.y - VectorA.y * VectorB.x;
}

float2 rotate2d(float2 vec, float radians)
{
    // Rotation matrix
    float2x2 rotMatrix = float2x2(
        cos(radians), -sin(radians),
        sin(radians), cos(radians)
    );

    // Rotate the vector
    return mul(rotMatrix, vec);
}

// ccw = CounterClockWise
bool ccw(float2 A, float2 B, float2 C)
{
    return (C.y - A.y) * (B.x - A.x) > (B.y - A.y) * (C.x - A.x);
}

// Returns true if the line AB crosses the line CD
bool CheckLinesIntersect(float2 A, float2 B, float2 C, float2 D)
{
    return ccw(A, C, D) != ccw(B, C, D) && ccw(A, B, C) != ccw(A, B, D);
}

float2 LineIntersectionPoint(float2 A, float2 B, float2 C, float2 D) {
    float a1 = B.y - A.y;
    float b1 = A.x - B.x;
    float c1 = a1 * A.x + b1 * A.y;

    float a2 = D.y - C.y;
    float b2 = C.x - D.x;
    float c2 = a2 * C.x + b2 * C.y;

    float delta = a1 * b2 - a2 * b1;
    if (delta == 0) {
        // return float2(0, 0);
		delta = 0.01;
    }

    float x = (b2 * c1 - b1 * c2) / delta;
    float y = (a1 * c2 - a2 * c1) / delta;
    return float2(x, y);
}

float2 dstToLineSegment(float2 A, float2 B, float2 P)
{
    float2 AB = B - A;
    float2 AP = P - A;
    float ABLengthSquared = dot(AB, AB);

    // Scalar projection
    float AP_dot_AB = dot(AP, AB);
    float t = AP_dot_AB / ABLengthSquared;

    // Clamp t to the closest point on the line segment
    t = clamp(t, 0.0, 1.0);

    // Closest point on line segment to P
    float2 closestPoint = A + t * AB;

    // Return the distance vector from P to the closest point
    return closestPoint - P;
}

bool SideOfLine(float2 A, float2 B, float2 dstVec) {
    float2 lineVec = normalize(B - A);

    float crossProduct = cross2d(lineVec, dstVec);

    // True if P is on the left side of the line from A to B
    // This means all lines only "block" one side
    return crossProduct > 0;
}