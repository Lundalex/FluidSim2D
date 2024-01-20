const float InteractionInfluenceFactor;
const float SmoothLiquidFactor;
const float SmoothLiquidDerFactor;
const float SmoothLiquidNearFactor;
const float SmoothLiquidNearDerFactor;
const float SmoothViscosityLaplacianFactor;

// Geogebra: https://www.geogebra.org/calculator/bsyseckq

// http://www.ligum.umontreal.ca/Clavet-2005-PVFS/pvfs.pdf#page10
// https://iquilezles.org/articles/distfunctions/ SDFs Not used yet

// Neither math functions or math constants have been configured. Set constants in Main.cs - SetSimShaderSettings()
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
		return (1 - dst/radius)*(1-dst/radius);
	}
	return 0;
}

float SmoothLiquidDer(float dst, float radius)
{
	if (dst < radius)
	{
		return -2 * (1 - dst / radius) / radius;
	}
	return 0;
}

float SmoothLiquidNear(float dst, float radius)
{
	if (dst < radius)
	{
		return (1 - dst/radius)*(1 - dst/radius)*(1 - dst/radius);
	}
	return 0;
}

float SmoothLiquidNearDer(float dst, float radius)
{
	if (dst < radius)
	{
		return -3 * (1 - dst/radius)*(1 - dst/radius) / radius;
	}
	return 0;
}

float SmoothViscosityLaplacian(float dst, float radius)
{
	if (dst < radius)
	{
	    float radius6 = radius*radius*radius*radius*radius*radius;
		return 45 / (3.14 * radius6) * (radius - dst);
	}
	return 0;
}

float LiquidSpringForceModel(float springStiffness, float restLen, float maxInfluenceRadius, float Len)
{
    return springStiffness * (1 - restLen/maxInfluenceRadius) * (restLen - Len);
}

float avg(float a, float b)
{
    return (a + b) / 2;
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

// This function was found on the internet. I have no clue how it works
// ccw = CounterClockWise
bool ccw(float2 A, float2 B, float2 C)
{
    return (C.y - A.y) * (B.x - A.x) > (B.y - A.y) * (C.x - A.x);
}