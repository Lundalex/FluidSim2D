static const float MIR = 2.0; // MaxInfluenceRadius
static const int ChunkNum_copy = ceil(300 / MIR) * ceil(200 / MIR);
static const int PTypesNum_copy = 2;
static const int ParticlesNum_copy = 32768;


// -- LastChunkKey_PType_POrder --

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


// // -- LinkedA_LinkedB --

// int Compose_LinkedA_LinkedB(int LinkedA, int LinkedB)
// {
//     return LinkedB * ParticlesNum_copy + LinkedA;
// }

// int Extract_LinkedA(int LinkedA_LinkedB)
// {
//     return (uint)LinkedA_LinkedB % ParticlesNum_copy;
// }

// int Extract_LinkedB(int LinkedA_LinkedB)
// {
//     return (uint)LinkedA_LinkedB / ParticlesNum_copy;
// }

// void Set_LinkedA_LinkedB(inout int LinkedA_LinkedB, int newLinkedA, int newLinkedB)
// {
//     int LinkedA_LinkedB_Val = newLinkedB * ParticlesNum_copy + newLinkedA;

//     LinkedA_LinkedB = LinkedA_LinkedB_Val;
// }