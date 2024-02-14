static const int ChunkNum_copy = 15000;
static const int PTypesNum_copy = 2;

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
    int lastChunkKeyVal = LastChunkKey_PType_POrder % ChunkNum_copy;
    LastChunkKey_PType_POrder = pOrderVal + (NewPType * ChunkNum_copy) + lastChunkKeyVal;
}

void Set_POrder(inout int LastChunkKey_PType_POrder, int NewPOrder)
{
    int pType_lastChunkKey_Val = (uint)LastChunkKey_PType_POrder % (ChunkNum_copy * PTypesNum_copy);
    LastChunkKey_PType_POrder = (NewPOrder * (ChunkNum_copy * PTypesNum_copy)) + pType_lastChunkKey_Val;
}




// void Set_LastChunkKey(inout int LastChunkKey_PType_POrder, int NewLastChunkKey) {
//     int POrder = LastChunkKey_PType_POrder / (ChunkNum_copy * PTypesNum_copy);
//     int PType = (LastChunkKey_PType_POrder % (ChunkNum_copy * PTypesNum_copy)) / ChunkNum_copy;
//     LastChunkKey_PType_POrder = POrder * (ChunkNum_copy * PTypesNum_copy) + PType * ChunkNum_copy + NewLastChunkKey;
// }

// void Set_PType(inout int LastChunkKey_PType_POrder, int NewPType) {
//     int POrder = LastChunkKey_PType_POrder / (ChunkNum_copy * PTypesNum_copy);
//     int LastChunkKey = LastChunkKey_PType_POrder % ChunkNum_copy;
//     LastChunkKey_PType_POrder = POrder * (ChunkNum_copy * PTypesNum_copy) + NewPType * ChunkNum_copy + LastChunkKey;
// }

// void Set_POrder(inout int LastChunkKey_PType_POrder, int NewPOrder) {
//     int withoutPOrder = LastChunkKey_PType_POrder % (ChunkNum_copy * PTypesNum_copy);
//     LastChunkKey_PType_POrder = NewPOrder * (ChunkNum_copy * PTypesNum_copy) + withoutPOrder;
// }
