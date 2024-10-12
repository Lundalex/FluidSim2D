static const float MIR = 2.0; // MaxInfluenceRadius
static const int ChunkNum_copy = ceil(300 / MIR) * ceil(200 / MIR);
static const int PTypesNum_copy = 2 * 3;

int Compose_LastChunkKey_PType_POrder(int POrder, int PType, int LastChunkKey)
{
    int composedPOrder = POrder * (ChunkNum_copy * PTypesNum_copy);
    int composedPType = PType * ChunkNum_copy;
    int composedLastChunkKey = LastChunkKey;

    return composedPOrder + composedPType + composedLastChunkKey;
}

int Extract_LastChunkKey(int LastChunkKey_PType_POrder)
{
    return (uint)LastChunkKey_PType_POrder % ChunkNum_copy;
}

int Extract_PType(int LastChunkKey_PType_POrder)
{
    return ((uint)LastChunkKey_PType_POrder % (ChunkNum_copy * PTypesNum_copy)) / ChunkNum_copy;
}

int Extract_POrder(int LastChunkKey_PType_POrder)
{
    return (uint)LastChunkKey_PType_POrder / (ChunkNum_copy * PTypesNum_copy);
}

void Set_LastChunkKey(inout int LastChunkKey_PType_POrder, int NewLastChunkKey)
{
    LastChunkKey_PType_POrder = (uint)LastChunkKey_PType_POrder / ChunkNum_copy * ChunkNum_copy + NewLastChunkKey;
}

void Set_PType(inout int LastChunkKey_PType_POrder, int NewPType)
{
    int pOrderVal = ((uint)LastChunkKey_PType_POrder / (ChunkNum_copy * PTypesNum_copy)) * (ChunkNum_copy * PTypesNum_copy);
    int lastChunkKeyVal = (uint)LastChunkKey_PType_POrder % ChunkNum_copy;

    LastChunkKey_PType_POrder = pOrderVal + (NewPType * ChunkNum_copy) + lastChunkKeyVal;
}

void Set_POrder(inout int LastChunkKey_PType_POrder, int NewPOrder)
{
    int pType_lastChunkKey_Val = (uint)LastChunkKey_PType_POrder % (ChunkNum_copy * PTypesNum_copy);

    LastChunkKey_PType_POrder = (NewPOrder * (ChunkNum_copy * PTypesNum_copy)) + pType_lastChunkKey_Val;
}