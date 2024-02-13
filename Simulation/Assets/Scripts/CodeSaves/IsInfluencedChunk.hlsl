bool IsInfluencedChunk(int x, int y, int localPosX, int localPosY)
{
    if (x != 0 && y != 0)
    {
        int xUse = 0;
        int yUse = 0;
        if (x == 1 && y == 1) { xUse = 1; yUse = 1; }
        else if (x == -1 && y == -1) { xUse = 0; yUse = 0; }
        else if (x == 1 && y == -1) { xUse = 1; yUse = 0; }
        else if (x == -1 && y == 1) { xUse = 0; yUse = 1; }
        float2 dst = float2(localPosX - xUse, localPosY - yUse);
        float absDstSqr = dot(dst, dst);
        if (absDstSqr > MaxInfluenceRadiusSqr) { return false; }
    }
    return true;
}