const float InteractionInfluenceFactor;
const float SmoothLiquidFactor;
const float SmoothLiquidDerFactor;
const float SmoothLiquidNearFactor;
const float SmoothLiquidNearDerFactor;
const float SmoothViscosityLaplacianFactor;

// Geogebra: https://www.geogebra.org/calculator/bsyseckq

// Neither math functions or math constants have been configured. Set constants in Main.cs - SetSimShaderSettings()
float InteractionInfluence(float dst, float radius)
{
	if (dst <= radius)
	{
		return sqrt(radius - dst);
	}
	return 0;
}

float SmoothLiquid(float dst, float radius)
{
	if (dst <= radius)
	{
		return (1 - dst/radius)*(1-dst/radius);
	}
	return 0;
}

float SmoothLiquidDer(float dst, float radius)
{
	if (dst <= radius)
	{
		return -2 * (1 - dst / radius) / radius;
	}
	return 0;
}

float SmoothLiquidNear(float dst, float radius)
{
	if (dst <= radius)
	{
		return (1 - dst/radius)*(1 - dst/radius)*(1 - dst/radius);
	}
	return 0;
}

float SmoothLiquidNearDer(float dst, float radius)
{
	if (dst <= radius)
	{
		return -3 * (1 - dst/radius)*(1 - dst/radius) / radius;
	}
	return 0;
}

float SmoothViscosityLaplacian(float dst, float radius)
{
	if (dst <= radius)
	{
	    float radius6 = radius*radius*radius*radius*radius*radius;
		return 45 / (3.14 * radius6) * (radius - dst);
	}
	return 0;
}

float avg(float a, float b)
{
    return (a + b) / 2;
}