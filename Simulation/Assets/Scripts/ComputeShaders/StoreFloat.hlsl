float IntToFloat(int a)
{
    return (float)a / int_float_precision;
}
float2 Int2ToFloat2(int2 a)
{
    return float2(IntToFloat(a.x), IntToFloat(a.y));
    return (float)a / int_float_precision;
}
int FloatAsInt(float a)
{
    return (int)(a * int_float_precision);
}
int2 Float2AsInt2(float2 a)
{
    return int2(FloatAsInt(a.x), FloatAsInt(a.y));
}
int AddFloatToFloatStoredAsInt(int a, float b)
{
    return FloatAsInt(IntToFloat(a) + b);
}