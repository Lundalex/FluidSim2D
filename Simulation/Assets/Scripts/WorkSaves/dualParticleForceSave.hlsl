[numthreads(ThreadsNum,1,1)]
void ParticleForces (uint3 id : SV_DispatchThreadID)
{
    int i = id.x;

    // Can probably be optimised by using if (i > otherPIndex) to avoid calculating each pair twice

    // Int type conversion removes decimals, effectively doing a Floor() operation
    PDataStruct PData_i = PData[i];
    PTypeStruct PType_i = PTypes[PData_i.PType];
    int ChunkX = (int)(PData_i.PredPosition.x * InvMaxInfluenceRadius);
    int ChunkY = (int)(PData_i.PredPosition.y * InvMaxInfluenceRadius);

    float localPosX = (PData_i.PredPosition.x % MaxInfluenceRadius) * InvMaxInfluenceRadius;
    float localPosY = (PData_i.PredPosition.y % MaxInfluenceRadius) * InvMaxInfluenceRadius;

    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            int CurChunkX = ChunkX + x;
            int CurChunkY = ChunkY + y;

            if (x == 0 && y != 0)
            {
                int xUse = 0;
                int yUse = 0;
                if (x == 1 && y == 1) { xUse = 1; yUse = 1; }
                else if (x == -1 && y == -1) { xUse = 0; yUse = 0; }
                else if (x == 1 && y == -1) { xUse = 1; yUse = 0; }
                else if (x == -1 && y == 1) { xUse = 0; yUse = 1; }
                float2 dst = float2(localPosX - xUse, localPosY - yUse);
                float absDstSqr = dot(dst, dst);
                if (absDstSqr > MaxInfluenceRadiusSqr) { continue; }
            }

            if (!ValidChunk(CurChunkX, CurChunkY)) { continue; }

            int ChunkKey = CurChunkY * ChunkNumW + CurChunkX;
            int startIndex = StartIndices[ChunkKey];

            int Index = startIndex; 
            while (Index < ParticlesNum && ChunkKey == SpatialLookup[Index].y)
            {
                int otherPIndex = SpatialLookup[Index].x;
                if (i == otherPIndex || i > otherPIndex) { Index += 1; continue; }
                PDataStruct PData_otherPIndex = PData[otherPIndex];
                PTypeStruct PType_otherPIndex = PTypes[PData_otherPIndex.PType];

                float2 dst = PData_otherPIndex.PredPosition - PData_i.PredPosition;
                float sqrDst = dot(dst, dst);
                // float absDst = sqrt(sqrDst);

                // int MaxIterationCount = 2;
                // int springKey = i * SpringCapacity + (uint)otherPIndex % SpringCapacity;
                // if (absDst > 1)
                // {
                //     DeleteSpring(springKey, otherPIndex, MaxIterationCount); 
                // }

                if (sqrDst > MaxInfluenceRadiusSqr) { Index += 1; continue; }
                float absDst = sqrt(sqrDst);
                
                float2 totPressureForce = PressureForce(PData_i, PData_otherPIndex, PType_i, PType_otherPIndex, absDst, dst, otherPIndex);
                float2 totViscocityForce = ViscocityForce(PData_i, PData_otherPIndex, PType_i, PType_otherPIndex, absDst);
                float2 totSpringForce = LiquidSpringForce(PType_i, PType_otherPIndex, i, otherPIndex, absDst, dst);

                float2 totForce = totPressureForce;
                // PData[i].Velocity.y -= PType_i.Gravity * DeltaTime;
                PData[i].Velocity += totForce * DeltaTime;
                PData[otherPIndex].Velocity -= totForce * DeltaTime;

                // Increment Index each iteration - Chunk particle search algorithm
                Index += 1;
            }
        }
    }

    float2 interactionForce = InteractionForce(i);

    int stickyLineIndex = PData_i.StickyLineIndex;
    float2 stickynessForce = float2(0.0, 0.0);
    // StickynessForce() decreases performance over time!
    // if (stickyLineIndex != -1) { stickynessForce = StickynessForce(PData_i, PType_i, i, stickyLineIndex); }

    // PData[i].Velocity.y -= PType_i.Gravity * DeltaTime;
    // PData[i].Velocity += totForce * DeltaTime;
}

[numthreads(ThreadsNum,1,1)]
void UpdatePositions (uint3 id : SV_DispatchThreadID)
{
    int i = id.x;

    PData[i].Velocity.y -= DeltaTime * 5;

    PDataStruct PData_i = PData[i];

    PData_i.Position += PData_i.Velocity * DeltaTime;

    float4 PosVelData = BoundraryCheck(PData_i.Position, PData_i.Velocity, 0, PTypes[PData_i.PType].Damping);
    PData[i].Position = float2(PosVelData.x, PosVelData.y);
    PData[i].Velocity = float2(PosVelData.z, PosVelData.w);
}