// static const float MIR = 2.0; // MaxInfluenceRadius
// static const int2 BOUNDARY_DIMS = int2(300, 200);
// static const int ChunksNum_copy = ceil(BOUNDARY_DIMS.x / MIR) * ceil(BOUNDARY_DIMS.y / MIR);
// static const int PTYPES_NUM_COPY = 1 * 3;

// int Compose_LastChunkKey_PType_POrder(int POrder, int PType, int LastChunkKey)
// {
//     int composedPOrder = POrder * (ChunksNum_copy * PTYPES_NUM_COPY);
//     int composedPType = PType * ChunksNum_copy;
//     int composedLastChunkKey = LastChunkKey;

//     return composedPOrder + composedPType + composedLastChunkKey;
// }

// int Extract_LastChunkKey(int LastChunkKey_PType_POrder)
// {
//     return (uint)LastChunkKey_PType_POrder % ChunksNum_copy;
// }

// int Extract_PType(int LastChunkKey_PType_POrder)
// {
//     return ((uint)LastChunkKey_PType_POrder % (ChunksNum_copy * PTYPES_NUM_COPY)) / ChunksNum_copy;
// }

// int Extract_POrder(int LastChunkKey_PType_POrder)
// {
//     return (uint)LastChunkKey_PType_POrder / (ChunksNum_copy * PTYPES_NUM_COPY);
// }

// void Set_LastChunkKey(inout int LastChunkKey_PType_POrder, int NewLastChunkKey)
// {
//     LastChunkKey_PType_POrder = (uint)LastChunkKey_PType_POrder / ChunksNum_copy * ChunksNum_copy + NewLastChunkKey;
// }

// void Set_PType(inout int LastChunkKey_PType_POrder, int NewPType)
// {
//     int pOrderVal = ((uint)LastChunkKey_PType_POrder / (ChunksNum_copy * PTYPES_NUM_COPY)) * (ChunksNum_copy * PTYPES_NUM_COPY);
//     int lastChunkKeyVal = (uint)LastChunkKey_PType_POrder % ChunksNum_copy;

//     LastChunkKey_PType_POrder = pOrderVal + (NewPType * ChunksNum_copy) + lastChunkKeyVal;
// }

// void Set_POrder(inout int LastChunkKey_PType_POrder, int NewPOrder)
// {
//     int pType_lastChunkKey_Val = (uint)LastChunkKey_PType_POrder % (ChunksNum_copy * PTYPES_NUM_COPY);

//     LastChunkKey_PType_POrder = (NewPOrder * (ChunksNum_copy * PTYPES_NUM_COPY)) + pType_lastChunkKey_Val;
// }