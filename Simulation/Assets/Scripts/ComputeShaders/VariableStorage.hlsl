// Float <-> Int
float IntToFloat(int a, float precision)
{
    return (float)a / precision;
}
float2 Int2ToFloat2(int2 a, float precision)
{
    return float2(IntToFloat(a.x, precision), IntToFloat(a.y, precision));
}
int FloatAsInt(float a, float precision)
{
    return (int)(a * precision);
}
int2 Float2AsInt2(float2 a, float precision)
{
    return int2(FloatAsInt(a.x, precision), FloatAsInt(a.y, precision));
}
int AddFloatToFloatStoredAsInt(int a, float b, float precision)
{
    return FloatAsInt(IntToFloat(a, precision) + b, precision);
}

// Rb/fluid-matIndex <-> matIndex
int StoreRBMatIndex(int rbMatIndex)
{
    return -rbMatIndex - 1;
}
int RetrieveStoredRBMatIndex(int storedRBMatIndex)
{
    return -(storedRBMatIndex + 1);
}
int StoreFluidMatIndex(int fluidMatIndex)
{
    return fluidMatIndex + 1;
}
int RetrieveStoredFluidMatIndex(int storedMatIndex)
{
    return storedMatIndex - 1;
}