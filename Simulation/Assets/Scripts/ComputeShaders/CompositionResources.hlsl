const int ChunkNum;
const int PTypesNum;

// Example use to increase the value of pType by 2:
// int pType = Extract_PType(PData_i.LastChunkKey_PType_POrder);
// Set_PType(PData_i.LastChunkKey_PType_POrder, pType + 2);

int3 Extract_LastChunkKey_PType_POrder(int LastChunkKey_PType_POrder)
{
    int POrder = (int)((float)LastChunkKey_PType_POrder / (ChunkNum * PTypesNum));

    int PType = (LastChunkKey_PType_POrder % (ChunkNum * PTypesNum)) / ChunkNum;

    int LastChunkKey = LastChunkKey_PType_POrder % ChunkNum;

    return int3(LastChunkKey, PType, POrder);
}

int Extract_LastChunkKey(int LastChunkKey_PType_POrder)
{
    return LastChunkKey_PType_POrder % ChunkNum;
}

int Extract_PType(int LastChunkKey_PType_POrder)
{
    return (LastChunkKey_PType_POrder % (ChunkNum * PTypesNum)) / ChunkNum;
}

int Extract_POrder(int LastChunkKey_PType_POrder)
{
    return LastChunkKey_PType_POrder / (ChunkNum * PTypesNum);
}

void Set_LastChunkKey(inout int LastChunkKey_PType_POrder, int NewLastChunkKey)
{
    int clearLastChunkKey = LastChunkKey_PType_POrder / ChunkNum * ChunkNum;

    LastChunkKey_PType_POrder = clearLastChunkKey + NewLastChunkKey;
}

void Set_PType(inout int LastChunkKey_PType_POrder, int NewPType)
{
    int POrder = LastChunkKey_PType_POrder / (ChunkNum * PTypesNum);
    int LastChunkKey = LastChunkKey_PType_POrder % ChunkNum;

    LastChunkKey_PType_POrder = POrder * (ChunkNum * PTypesNum) + NewPType * ChunkNum + LastChunkKey;
}

void Set_POrder(inout int LastChunkKey_PType_POrder, int NewPOrder)
{
    int withoutPOrder = LastChunkKey_PType_POrder % (ChunkNum * PTypesNum);

    LastChunkKey_PType_POrder = NewPOrder * (ChunkNum * PTypesNum) + withoutPOrder;
}