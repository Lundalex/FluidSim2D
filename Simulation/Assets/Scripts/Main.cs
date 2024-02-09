using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Unity.Mathematics;
using System;
using System.Runtime.InteropServices;
using System.Linq;
using Unity.VisualScripting;
using System.Numerics;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
using Random = UnityEngine.Random;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using UnityEngine.Jobs;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine.Rendering;

// Import utils from Resources.cs
using Resources;
// Usage: Utils.(functionName)()

public class Main : MonoBehaviour
{
    [Header("Simulation settings")]
    public int ParticlesNum;
    public int MaxInfluenceRadius;
    public float TargetDensity;
    public float PressureMultiplier;
    public float NearPressureMultiplier;
    [Range(0, 1)] public float Damping;
    [Range(0, 1)] public float RbElasticity;
    [Range(0, 0.1f)] public float LookAheadFactor;
    public float Viscocity;
    public float LiquidElasticity;
    public float Plasticity;
    public float Gravity;
    public float RbPStickyRadius;
    public float RbPStickyness;
    [Range(0, 3)] public int MaxChunkSearchSafety;
    public int SpringSafety; // Avg slots per particle Index
    public float ChunkStorageSafety;
    public int SpringCapacitySafety;
    public int TriStorageLength;
    public float radii;

    [Header("Boundrary settings")]
    public int Width;
    public int Height;
    public int SpawnDims; // A x A
    public float BorderPadding;

    [Header("Rendering settings")]
    public bool FixedTimeStep;
    public bool RenderMarchingSquares;
    public float TimeStep;
    public float ProgramSpeed;
    public float VisualParticleRadii;
    public float RBRenderThickness;
    public int TimeStepsPerRender;
    public float MSvalMin;
    public int ResolutionX;
    public int ResolutionY;
    public int MSResolution;
    public int MarchW = 150;
    public int MarchH = 75;

    [Header("Interaction settings")]
    public float MaxInteractionRadius;
    public float InteractionAttractionPower;
    public float InteractionFountainPower;

    [Header("References")]
    public ShaderHelper shaderHelper;
    public GameObject ParticlePrefab;
    public ComputeShader renderShader;
    public ComputeShader pSimShader;
    public ComputeShader rbSimShader;
    public ComputeShader sortShader;
    public ComputeShader marchingSquaresShader;

    // Marching Squares - Buffers
    [System.NonSerialized]
    public ComputeBuffer VerticesBuffer;
    public ComputeBuffer TrianglesBuffer;
    public ComputeBuffer ColorsBuffer;
    public ComputeBuffer MSPointsBuffer;

    // Bitonic Mergesort - Buffers
    public ComputeBuffer SpatialLookupBuffer;
    public ComputeBuffer StartIndicesBuffer;

    // Inter-particle springs - Buffers
    public ComputeBuffer ChunkSizesBuffer;
    public ComputeBuffer SpringCapacitiesBuffer;
    private bool FrameBufferCycle = true;
    public ComputeBuffer SpringStartIndicesBuffer_dbA; // Result A
    public ComputeBuffer SpringStartIndicesBuffer_dbB; // Result B
    public ComputeBuffer SpringStartIndicesBuffer_dbC; // Support
    public ComputeBuffer ParticleSpringsCombinedBuffer; // [[Last frame springs], [New frame springs]]

    // PData - Buffers
    public ComputeBuffer PDataBuffer;
    public ComputeBuffer PTypesBuffer;

    // Rigid Bodies - Buffers
    public ComputeBuffer RBVectorBuffer;
    public ComputeBuffer RBDataBuffer;
    public ComputeBuffer TraversedChunks_AC_Buffer;
    public ComputeBuffer StickynessReqs_AC_Buffer;
    public ComputeBuffer SortedStickyRequestsBuffer;
    public ComputeBuffer StickyRequestsResult_AC_Buffer;
    public ComputeBuffer TCCountBuffer;
    public ComputeBuffer SRCountBuffer;

    // Constants
    [System.NonSerialized] public int MaxInfluenceRadiusSqr;
    [System.NonSerialized] public float InvMaxInfluenceRadius;
    [System.NonSerialized] public float MarchScale;
    [System.NonSerialized] public int ChunkNumW;
    [System.NonSerialized] public int ChunkNumH;
    [System.NonSerialized] public int ChunkNum;
    [System.NonSerialized] public int ChunksNumLog2;
    [System.NonSerialized] public int ChunkNumNextPow2;
    [System.NonSerialized] public int IOOR; // Index Out Of Range
    [System.NonSerialized] public int MSLen;
    [System.NonSerialized] public int RBodiesNum;
    [System.NonSerialized] public int RBVectorNum;
    [System.NonSerialized] public int TraversedChunksCount;
    [System.NonSerialized] public int ParticleSpringsCombinedHalfLength;

    // Private references
    private RenderTexture renderTexture;
    private Mesh marchingSquaresMesh;

    // PData - Properties
    private int2[] SpatialLookup; // [](particleIndex, chunkKey)
    private int2[] TemplateSpatialLookup;
    private int[] StartIndices;
    private int[] ChunkSizes;
    private int[] SpringCapacities;
    private int[] SpringStartIndices;
    private SpringStruct[] ParticleSpringsCombined;
    private PDataStruct[] PData;
    private PTypeStruct[] PTypes;

    // Rigid Bodies - Properties
    private RBVectorStruct[] RBVector;
    private RBDataStruct[] RBData;
    private int[] TCCount = new int[1];
    private int[] SRCount = new int[1];

    // Marching Squares - Buffer retrieval
    private Vector3[] vertices;
    private int[] triangles;
    private Color[] colors;
    private float[] MSPoints;

    // Other
    private float DeltaTime;
    private int frameCounter = 0;
    // private int FrameCounter;
    private int CalcStickyRequestsFrequency = 3;
    // private int GPUChunkDataSortFrequency = 3;
    private bool DoCalcStickyRequests = true;
    // private bool DoGPUChunkDataSort = true;
    private bool ProgramStarted = false;
    private int foundParticleB = -1;

    void Start()
    {
        SceneSetup();

        InitializeSetArrays();
        SetConstants();
        InitializeArrays();

        for (int i = 0; i < ParticlesNum; i++) {
            PData[i].Position = Utils.ParticleSpawnPosition(i, ParticlesNum, Width, Height, SpawnDims);
        }

        InitializeBuffers();
        shaderHelper.SetPSimShaderBuffers(pSimShader);
        shaderHelper.SetRbSimShaderBuffers(rbSimShader);
        shaderHelper.SetRenderShaderBuffers(renderShader);
        shaderHelper.SetSortShaderBuffers(sortShader);
        shaderHelper.SetMarchingSquaresShaderBuffers(marchingSquaresShader);

        shaderHelper.UpdatePSimShaderVariables(pSimShader);
        shaderHelper.UpdateRbSimShaderVariables(rbSimShader);
        shaderHelper.UpdateRenderShaderVariables(renderShader);
        shaderHelper.UpdateSortShaderVariables(sortShader);
        shaderHelper.UpdateMarchingSquaresShaderVariables(marchingSquaresShader);

        ProgramStarted = true;
    }

    void Update()
    {
        UpdateShaderTimeStep();

        // GPUSortSpringLookUp() have to be called directly after GPUSortChunkLookUp()
        GPUSortChunkLookUp();
        GPUSortSpringLookUp();
        frameCounter++;

        for (int i = 0; i < TimeStepsPerRender; i++)
        {
            if (i == 0)
            {
                pSimShader.SetBool("TransferSpringData", true);
            }
            else
            {
                pSimShader.SetBool("TransferSpringData", false);
            }

            RunPSimShader(i);





            StartIndicesBuffer.GetData(StartIndices);
            PDataBuffer.GetData(PData);
            SpatialLookupBuffer.GetData(SpatialLookup);
            ChunkSizesBuffer.GetData(ChunkSizes);
            SpringCapacitiesBuffer.GetData(SpringCapacities);
            SpringStartIndicesBuffer_dbA.GetData(SpringStartIndices);
            ParticleSpringsCombinedBuffer.GetData(ParticleSpringsCombined);

            if (FrameBufferCycle)
            {
                SpringStartIndicesBuffer_dbA.GetData(SpringStartIndices);
            }
            else
            {
                SpringStartIndicesBuffer_dbB.GetData(SpringStartIndices);
            }



            // int pIndex = 10;
            // PDataStruct PData_i = PData[pIndex];

            // int chunkX = (int)(PData_i.Position.x * InvMaxInfluenceRadius);
            // int chunkY = (int)(PData_i.Position.y * InvMaxInfluenceRadius);

            // for (int x = -1; x <= 1; x++)
            // {
            //     for (int y = -1; y <= 1; y++)
            //     {
            //         int curChunkX = chunkX + x;
            //         int curChunkY = chunkY + y;

            //         if (!(chunkX >= 0 && chunkX < ChunkNumW && chunkY >= 0 && chunkY < ChunkNumH)) {continue;}

            //         int chunkKey = curChunkY * ChunkNumW + curChunkX;
            //         int startIndex = StartIndices[chunkKey];

            //         int Index = startIndex;
            //         while (Index < ParticlesNum && chunkKey == SpatialLookup[Index].y)
            //         {
            //             int otherPIndex = SpatialLookup[Index].x;


            //             // -- Spring handling --

            //             if (i == 0)
            //             {
            //                 if (x == 0 && y == 0)
            //                 {
            //                     if (pIndex == otherPIndex)
            //                     {
            //                         int pOrder = Index - startIndex;
            //                         PData[pIndex].LastPOrder = PData_i.POrder;
            //                         PData[pIndex].POrder = pOrder;
            //                         PData_i.POrder = pOrder;
            //                     }
            //                 }
            //             }

            //             Index += 1;
            //         }
            //     }
            // }





            // Debug.Log("Frame");
            // for (int j = 0; j < ParticleSpringsCombined.Length; j++)
            // {
            //     if (ParticleSpringsCombined[j].PLinkedA == 1000) { 
            //         if (foundParticleB == -1)
            //         {
            //             foundParticleB = ParticleSpringsCombined[j].PLinkedB;
            //         }
            //         else
            //         {
            //             if (ParticleSpringsCombined[j].PLinkedB == foundParticleB)
            //             {
            //                 float2 pA = PData[ParticleSpringsCombined[j].PLinkedA].Position;
            //                 float2 pB = PData[ParticleSpringsCombined[j].PLinkedB].Position;
            //                 float2 diff = pA - pB;
            //                 float dst = (float)Math.Sqrt(diff.x*diff.x+diff.y*diff.y);
            //                 if (dst <= MaxInfluenceRadius)
            //                 {
            //                     // Debug.Log(ParticleSpringsCombined[i].PLinkedB);
            //                     // Debug.Log(i);
            //                     Debug.Log(ParticleSpringsCombined[j].RestLength);
            //                 }
            //             }
            //         }
            //     }
            // }

            // THIS IS PROGRESS __________________________________________________
            // There is probably a problem with startIndices mapping onto the same value for particles in different chunks

            if (frameCounter == 10 && i == 0)
            {
                int nNum = 0;
                // int lastSpringIndex = -1;
                int[] lastSpringIndices = new int[10000000];
                for (int p = 0; p < lastSpringIndices.Length; p++)
                {
                    lastSpringIndices[p] = -1;
                }
                
                for (int k = 0; k < ParticlesNum; k++)
                {
                    
                    // int chunkKey2 = k;
                    // int startIndex2 = StartIndices[chunkKey2];

                    // int Index2 = startIndex2;
                    // while (Index2 < ParticlesNum && chunkKey2 == SpatialLookup[Index2].y)
                    // {
                        int pIndex = k;


                        PDataStruct PData_i = PData[pIndex];
                        int baseX = PData_i.LastChunkKey % ChunkNumW;
                        int baseY = (int)(PData_i.LastChunkKey / ChunkNumW);
                        int pOrder = PData_i.POrder;

                        int chunkKe = baseY * ChunkNumW + baseX;
                        int b = SpringCapacities[chunkKe];
                        int c = ChunkSizes[chunkKe];
                        if (ChunkSizes[chunkKe] == 0)
                        {
                            continue;
                        }
                        int nearbyCapacity = SpringCapacities[chunkKe] / ChunkSizes[chunkKe];

                        nNum = 0;
                        for (int x = -1; x <= 1; x++)
                        {
                            for (int y = -1; y <= 1; y++)
                            {
                                int curChunkX = baseX + x;
                                int curChunkY = baseY + y;

                                if (!(curChunkX >= 0 && curChunkX < ChunkNumW && curChunkY >= 0 && curChunkY < ChunkNumH)) { continue; }

                                int chunkKey = curChunkY * ChunkNumW + curChunkX;
                                int startIndex = StartIndices[chunkKey];

                                int Index = startIndex; 
                                while (Index < ParticlesNum && chunkKey == SpatialLookup[Index].y)
                                {

                                    int springIndex = FrameBufferCycle
                                    ? SpringStartIndices[chunkKe-1] + pOrder * nearbyCapacity + nNum // +hnumn
                                    : SpringStartIndices[chunkKe-1] + pOrder * nearbyCapacity + nNum;

                                    if (lastSpringIndices[springIndex] == 1)
                                    {
                                        Debug.Log(springIndex);
                                    }
                                    lastSpringIndices[springIndex] += 1;
                                    if (lastSpringIndices[springIndex] > 0)
                                    {
                                        int afwwf = 0;
                                    }
                                    
                                    Index++;
                                    nNum++;
                                }
                            }
                        
                        
                        if (nNum > nearbyCapacity)
                        {
                            int oooo = 0;
                        }
                    }
            }
                int d = 0;
                for (int l = 1; l < lastSpringIndices.Length; l++)
                {
                    if(lastSpringIndices[l-1] == -1 && lastSpringIndices[l] != -1)
                    {
                        // Debug.Log(lastSpringIndices[l]);
                        int a22=1;
                    }
                    if (lastSpringIndices[l] > 0)
                    {
                        int a222=1;
                    }
                }
            }





            // Stickyness requests
            if (i == 1) {
                DoCalcStickyRequests = true;
                rbSimShader.SetInt("DoCalcStickyRequests", 1);
                GPUSortStickynessRequests(); 
                int ThreadSize = (int)Math.Ceiling((float)4096 / 512);
                pSimShader.Dispatch(6, ThreadSize, 1, 1);
            }
            else { DoCalcStickyRequests = false; rbSimShader.SetInt("DoCalcStickyRequests", 0); }

            RunRbSimShader();

            int ThreadSize2 = (int)Math.Ceiling((float)ParticlesNum / 512);
            if (ParticlesNum != 0) {pSimShader.Dispatch(5, ThreadSize2, 1, 1);}
            
            if (RenderMarchingSquares)
            {
                RunMarchingSquaresShader();
            }
        }

        // RunRenderShader() is called by OnRenderImage()
    }

    private void OnValidate()
    {
        if (ProgramStarted)
        {
            CallOnInspectorUpdate();
        }
    }

    // Doesn't work
    public void CallOnInspectorUpdate()
    {
        SetConstants();
        shaderHelper.UpdatePSimShaderVariables(pSimShader);
        shaderHelper.UpdateRbSimShaderVariables(rbSimShader);
        shaderHelper.UpdateRenderShaderVariables(renderShader);
        shaderHelper.UpdateSortShaderVariables(sortShader);
        shaderHelper.UpdateMarchingSquaresShaderVariables(marchingSquaresShader);
    }
    
    public void UpdateShaderTimeStep()
    {
        DeltaTime = GetDeltaTime();
        
        Vector2 mouseWorldPos = Utils.GetMouseWorldPos(Width, Height);
        // (Left?, Right?)
        bool2 mousePressed = Utils.GetMousePressed();

        pSimShader.SetFloat("DeltaTime", DeltaTime);
        pSimShader.SetFloat("SRDeltaTime", DeltaTime * CalcStickyRequestsFrequency);
        pSimShader.SetFloat("MouseX", mouseWorldPos.x);
        pSimShader.SetFloat("MouseY", mouseWorldPos.y);
        pSimShader.SetBool("LMousePressed", mousePressed.x);
        pSimShader.SetBool("RMousePressed", mousePressed.y);

        rbSimShader.SetFloat("DeltaTime", DeltaTime);

        if (DoCalcStickyRequests) {
            rbSimShader.SetInt("DoCalcStickyRequests", 1);
        }
        else {
            rbSimShader.SetInt("DoCalcStickyRequests", 0);
        }

        FrameBufferCycle = !FrameBufferCycle;
        sortShader.SetBool("FrameBufferCycle", FrameBufferCycle);
        pSimShader.SetBool("FrameBufferCycle", FrameBufferCycle);
    }

    void SceneSetup()
    {
        while (Height % MaxInfluenceRadius != 0) {
            Height += 1;
        }
        while (Width % MaxInfluenceRadius != 0) {
            Width += 1;
        }

        Camera.main.transform.position = new Vector3(Width / 2, Height / 2, -1);
        Camera.main.orthographicSize = Mathf.Max(Width * 0.75f, Height * 1.5f);

        marchingSquaresMesh = GetComponent<MeshFilter>().mesh;
    }

    float GetDeltaTime()
    {
        float DeltaTime;
        if (FixedTimeStep) {
            DeltaTime = TimeStep / TimeStepsPerRender;
        }
        else {
            DeltaTime = Time.deltaTime * ProgramSpeed / TimeStepsPerRender;
        }
        return DeltaTime;
    }

    void SetConstants()
    {
        MaxInfluenceRadiusSqr = MaxInfluenceRadius * MaxInfluenceRadius;
        InvMaxInfluenceRadius = 1.0f / MaxInfluenceRadius;
        ChunkNumW = Width / MaxInfluenceRadius;
        ChunkNumH = Height / MaxInfluenceRadius;
        ChunkNum = ChunkNumW * ChunkNumH;
        ChunksNumLog2 = (int)Math.Ceiling(Math.Log(ChunkNum) / Math.Log(2));
        ChunkNumNextPow2 = (int)Math.Pow(2, ChunksNumLog2);
        IOOR = ParticlesNum;
        MarchW = (int)(Width / MSResolution);
        MarchH = (int)(Height / MSResolution);
        MSLen = MarchW * MarchH * TriStorageLength * 3;
        RBVectorNum = RBVector.Length;
        ParticleSpringsCombinedHalfLength = (int)(ParticlesNum * SpringSafety / 2);

        for (int i = 0; i < RBodiesNum; i++)
        {
            RBData[i].StickynessRangeSqr = RBData[i].StickynessRange*RBData[i].StickynessRange;

            float furthestDstSqr = 0;
            int startIndex = RBData[i].LineIndices.x;
            int endIndex = RBData[i].LineIndices.y;
            for (int j = startIndex; j <= endIndex; j++)
            {
                Vector2 dst = RBVector[j].Position - RBData[i].Position;
                float absDstSqr = 2*dst.sqrMagnitude;
                if (absDstSqr > furthestDstSqr)
                {
                    furthestDstSqr = absDstSqr;
                }
            }
            RBData[i].MaxDstSqr = furthestDstSqr;
        }
    }

    void InitializeSetArrays()
    {
        PTypes = new PTypeStruct[2];
        PTypes[0] = new PTypeStruct
        {
            TargetDensity = TargetDensity,
            MaxInfluenceRadius = 1,
            Pressure = PressureMultiplier,
            NearPressure = NearPressureMultiplier,
            Damping = Damping,
            Viscocity = Viscocity,
            Elasticity = LiquidElasticity,
            Plasticity = Plasticity,
            Stickyness = 5f,
            Gravity = Gravity,
            colorG = 0f
        };
        PTypes[1] = new PTypeStruct
        {
            TargetDensity = TargetDensity * 1.5f,
            MaxInfluenceRadius = 1,
            Pressure = PressureMultiplier,
            NearPressure = NearPressureMultiplier,
            Damping = Damping,
            Viscocity = Viscocity,
            Elasticity = LiquidElasticity,
            Plasticity = Plasticity,
            Stickyness = 4f,
            Gravity = Gravity,
            colorG = 1f
        };

        RBData = new RBDataStruct[2];
        RBData[0] = new RBDataStruct
        {
            Position = new float2(140f, 100f),
            Velocity = new float2(0.0f, 0.0f),
            NextPos = new float2(140f, 100f),
            NextVel = new float2(0.0f, 0.0f),
            NextAngImpulse = 0f,
            AngularImpulse = 0.0f,
            Stickyness = 16f,
            StickynessRange = 4f,
            StickynessRangeSqr = 16f,
            Mass = 200f,
            WallCollision = 0,
            Stationary = 1,
            LineIndices = new int2(0, 8)
        };
        RBData[1] = new RBDataStruct
        {
            Position = new float2(50f, 100f),
            Velocity = new float2(0.0f, 0.0f),
            NextPos = new float2(50f, 100f),
            NextVel = new float2(0.0f, 0.0f),
            NextAngImpulse = 0f,
            AngularImpulse = 0.0f,
            Stickyness = 16f,
            StickynessRange = 4f,
            StickynessRangeSqr = 16f,
            Mass = 200f,
            WallCollision = 0,
            Stationary = 1,
            LineIndices = new int2(9, 17)
        };
        // RBData[1] = new RBDataStruct
        // {
        //     Position = new float2(30f, 100f),
        //     Velocity = new float2(0.0f, 0.0f),
        //     LineIndices = new int2(4, 8)
        // };

        // RBVector = new RBVectorStruct[4];
        // RBVector[0] = new RBVectorStruct { Position = new float2(10f, 10f), ParentRBIndex = 0 };
        // RBVector[1] = new RBVectorStruct { Position = new float2(30f, 30f), ParentRBIndex = 0 };
        // RBVector[2] = new RBVectorStruct { Position = new float2(60f, 10f), ParentRBIndex = 0 };
        // RBVector[3] = new RBVectorStruct { Position = new float2(10f, 10f), ParentRBIndex = 0 };

        // // LARGE TRIANGLE
        // RBVector = new RBVectorStruct[5];
        // float2 somevector = new float2(-20f, -20f);
        // RBVector[0] = new RBVectorStruct { Position = new float2(3f, 3f) * 3, LocalPosition = new float2(3f, 3f) * 3-somevector, ParentImpulse = new float3(0.0f, 0.0f, 0.0f), WallCollision = 0, ParentRBIndex = 0 };
        // RBVector[1] = new RBVectorStruct { Position = new float2(40f, 10f) * 3, LocalPosition = new float2(40f, 10f) * 3-somevector, ParentImpulse = new float3(0.0f, 0.0f, 0.0f), WallCollision = 0, ParentRBIndex = 0 };
        // RBVector[2] = new RBVectorStruct { Position = new float2(18f, 20f) * 3, LocalPosition = new float2(18f, 20f) * 3-somevector, ParentImpulse = new float3(0.0f, 0.0f, 0.0f), WallCollision = 0, ParentRBIndex = 0 };
        // RBVector[3] = new RBVectorStruct { Position = new float2(8f, 20f) * 3, LocalPosition = new float2(8f, 20f) * 3-somevector, ParentImpulse = new float3(0.0f, 0.0f, 0.0f), WallCollision = 0, ParentRBIndex = 0 };
        // RBVector[4] = new RBVectorStruct { Position = new float2(3f, 3f) * 3, LocalPosition = new float2(3f, 3f) * 3-somevector, ParentImpulse = new float3(0.0f, 0.0f, 0.0f), WallCollision = 0, ParentRBIndex = 0 };

        // BUCKET
        RBVector = new RBVectorStruct[18];
        RBVector[0] = new RBVectorStruct { Position = new float2(10f, 20f) * 1.5f, ParentRBIndex = 0 };
        RBVector[1] = new RBVectorStruct { Position = new float2(50f, 20f) * 1.5f, ParentRBIndex = 0 };
        RBVector[2] = new RBVectorStruct { Position = new float2(50f, 50f) * 1.5f, ParentRBIndex = 0 };
        RBVector[3] = new RBVectorStruct { Position = new float2(40f, 50f) * 1.5f, ParentRBIndex = 0 };
        RBVector[4] = new RBVectorStruct { Position = new float2(39f, 30f) * 1.5f, ParentRBIndex = 0 };
        RBVector[5] = new RBVectorStruct { Position = new float2(21f, 30f) * 1.5f, ParentRBIndex = 0 };
        RBVector[6] = new RBVectorStruct { Position = new float2(20f, 50f) * 1.5f, ParentRBIndex = 0 };
        RBVector[7] = new RBVectorStruct { Position = new float2(10f, 50f) * 1.5f, ParentRBIndex = 0 };
        RBVector[8] = new RBVectorStruct { Position = new float2(10f, 20f) * 1.5f, ParentRBIndex = 0 };

        // BUCKET
        RBVector[9] = new RBVectorStruct { Position = new float2(10f, 20f) * 1.5f, ParentRBIndex = 1 };
        RBVector[10] = new RBVectorStruct { Position = new float2(50f, 20f) * 1.5f, ParentRBIndex = 1 };
        RBVector[11] = new RBVectorStruct { Position = new float2(50f, 50f) * 1.5f, ParentRBIndex = 1 };
        RBVector[12] = new RBVectorStruct { Position = new float2(40f, 50f) * 1.5f, ParentRBIndex = 1 };
        RBVector[13] = new RBVectorStruct { Position = new float2(39f, 30f) * 1.5f, ParentRBIndex = 1 };
        RBVector[14] = new RBVectorStruct { Position = new float2(21f, 30f) * 1.5f, ParentRBIndex = 1 };
        RBVector[15] = new RBVectorStruct { Position = new float2(20f, 50f) * 1.5f, ParentRBIndex = 1 };
        RBVector[16] = new RBVectorStruct { Position = new float2(10f, 50f) * 1.5f, ParentRBIndex = 1 };
        RBVector[17] = new RBVectorStruct { Position = new float2(10f, 20f) * 1.5f, ParentRBIndex = 1 };
        // // HEXAGON
        // RBVector = new RBVectorStruct[9];
        // RBVector[8] = new RBVectorStruct { Position = new float2(2f, 1f) * 5, ParentRBIndex = 0 };
        // RBVector[7] = new RBVectorStruct { Position = new float2(1f, 3f) * 5, ParentRBIndex = 0 };
        // RBVector[6] = new RBVectorStruct { Position = new float2(2f, 5f) * 5, ParentRBIndex = 0 };
        // RBVector[5] = new RBVectorStruct { Position = new float2(2f, 6f) * 5, ParentRBIndex = 0 };
        // RBVector[4] = new RBVectorStruct { Position = new float2(6f, 5f) * 5, ParentRBIndex = 0 };
        // RBVector[3] = new RBVectorStruct { Position = new float2(7f, 3f) * 5, ParentRBIndex = 0 };
        // RBVector[2] = new RBVectorStruct { Position = new float2(6f, 1f) * 5, ParentRBIndex = 0 };
        // RBVector[1] = new RBVectorStruct { Position = new float2(4f, 0f) * 5, ParentRBIndex = 0 };
        // RBVector[0] = new RBVectorStruct { Position = new float2(2f, 1f) * 5, ParentRBIndex = 0 };

        // // BOAT - Requires rotation by 180 degrees (AngImpulse = pi at start)
        // float2 somevec = new float2(0.5f, -3) * 5;
        // RBVector = new RBVectorStruct[21];
        // RBVector[0] = new RBVectorStruct { Position = new float2(5f, 0f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[1] = new RBVectorStruct { Position = new float2(4.71f, 1.71f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[2] = new RBVectorStruct { Position = new float2(4.04f, 3.24f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[3] = new RBVectorStruct { Position = new float2(3.04f, 4.43f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[4] = new RBVectorStruct { Position = new float2(1.76f, 5.24f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[5] = new RBVectorStruct { Position = new float2(0.29f, 5.65f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[6] = new RBVectorStruct { Position = new float2(-1.29f, 5.65f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[7] = new RBVectorStruct { Position = new float2(-2.76f, 5.24f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[8] = new RBVectorStruct { Position = new float2(-4.04f, 4.43f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[9] = new RBVectorStruct { Position = new float2(-5.04f, 3.24f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[10] = new RBVectorStruct { Position = new float2(-5.71f, 1.71f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[11] = new RBVectorStruct { Position = new float2(-6f, 0f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[12] = new RBVectorStruct { Position = new float2(-5.29f, 0f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[13] = new RBVectorStruct { Position = new float2(-4.57f, 0f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[14] = new RBVectorStruct { Position = new float2(-3.86f, 0f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[15] = new RBVectorStruct { Position = new float2(-3.14f, 0f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[16] = new RBVectorStruct { Position = new float2(-2.43f, 0f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[17] = new RBVectorStruct { Position = new float2(-1.71f, 0f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[18] = new RBVectorStruct { Position = new float2(-1f, 0f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[19] = new RBVectorStruct { Position = new float2(0f, 0f) * 5 + somevec, ParentRBIndex = 0 };
        // RBVector[20] = new RBVectorStruct { Position = new float2(5f, 0f) * 5 + somevec, ParentRBIndex = 0 };
        // for (int i = 0; i < 21; i++)
        // {
        //     RBVector[i].Position.y *= 0.5f * 1.2f;
        //     RBVector[i].Position.x *= 1.4f * 1.2f;
        // }

        RBodiesNum = RBData.Length;
    }

    void InitializeArrays()
    {
        SpatialLookup = new int2[ParticlesNum];
        StartIndices = new int[ChunkNum];
        ChunkSizes = new int[ChunkNum];
        SpringCapacities = new int[ChunkNum];
        SpringStartIndices = new int[ChunkNum];
        ParticleSpringsCombined = new SpringStruct[ParticlesNum * SpringCapacitySafety];

        PData = new PDataStruct[ParticlesNum];

        vertices = new Vector3[MSLen];
        triangles = new int[MSLen];
        colors = new Color[MSLen];
        MSPoints = new float[MSLen];

        TemplateSpatialLookup = new int2[ParticlesNum];

        for (int i = 0; i < ParticlesNum; i++)
        {
            if (i < ParticlesNum *0.5f)
            {
                PData[i] = new PDataStruct
                {
                    PredPosition = new float2(0.0f, 0.0f),
                    Position = new float2(0.0f, 0.0f),
                    Velocity = new float2(0.0f, 0.0f),
                    LastVelocity = new float2(0.0f, 0.0f),
                    Density = 0.0f,
                    NearDensity = 0.0f,
                    POrder = 0,
                    LastPOrder = 0,
                    LastChunkKey = 0,
                    PType = 0
                };
            }
            else
            {
                PData[i] = new PDataStruct
                {
                    PredPosition = new float2(0.0f, 0.0f),
                    Position = new float2(0.0f, 0.0f),
                    Velocity = new float2(0.0f, 0.0f),
                    LastVelocity = new float2(0.0f, 0.0f),
                    Density = 0.0f,
                    NearDensity = 0.0f,
                    PType = 1
                };
            }

            TemplateSpatialLookup[i] = new int2(0, 0);
        }

        for (int i = 0; i < MSLen; i++)
        {
            vertices[i] = new Vector3(0.0f, 0.0f, 0.0f);
            triangles[i] = 0;
            colors[i] = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        }

        for (int i = 0; i < MSLen; i++)
        {
            MSPoints[i] = 0.0f;
        }

        for (int i = 0; i < ChunkNum; i++)
        {
            StartIndices[i] = 0;
            
            ChunkSizes[i] = 0;
            SpringCapacities[i] = 0;
            SpringStartIndices[i] = 0;
        }

        for (int i = 0; i < ParticlesNum * SpringCapacitySafety; i++)
        {
            ParticleSpringsCombined[i] = new SpringStruct
            {
                PLinkedA = -1,
                PLinkedB = -1,
                RestLength = 0
            };
        }
    }

    void InitializeBuffers()
    {
        if (ParticlesNum != 0)
        {
            PDataBuffer = new ComputeBuffer(ParticlesNum, sizeof(float) * 10 + sizeof(int) * 4);
            PTypesBuffer = new ComputeBuffer(PTypes.Length, sizeof(float) * 10 + sizeof(int) * 1);

            PDataBuffer.SetData(PData);
            PTypesBuffer.SetData(PTypes);
        }

        SpatialLookupBuffer = new ComputeBuffer(ParticlesNum, sizeof(int) * 2);
        StartIndicesBuffer = new ComputeBuffer(ParticlesNum, sizeof(int));
        ChunkSizesBuffer = new ComputeBuffer(ChunkNum, sizeof(int));
        SpringCapacitiesBuffer = new ComputeBuffer(ChunkNum, sizeof(int));
        SpringStartIndicesBuffer_dbA = new ComputeBuffer(ChunkNum, sizeof(int));
        SpringStartIndicesBuffer_dbB = new ComputeBuffer(ChunkNum, sizeof(int));
        SpringStartIndicesBuffer_dbC = new ComputeBuffer(ChunkNum, sizeof(int));
        ParticleSpringsCombinedBuffer = new ComputeBuffer(ParticlesNum * SpringCapacitySafety, sizeof(float) + sizeof(int) * 2);
        
        SpatialLookupBuffer.SetData(SpatialLookup);
        StartIndicesBuffer.SetData(StartIndices);
        ChunkSizesBuffer.SetData(ChunkSizes);
        SpringCapacitiesBuffer.SetData(SpringCapacities);
        SpringStartIndicesBuffer_dbA.SetData(SpringStartIndices);
        SpringStartIndicesBuffer_dbB.SetData(SpringStartIndices);
        SpringStartIndicesBuffer_dbC.SetData(SpringStartIndices);
        ParticleSpringsCombinedBuffer.SetData(ParticleSpringsCombined);

        VerticesBuffer = new ComputeBuffer(MSLen, sizeof(float) * 3);
        TrianglesBuffer = new ComputeBuffer(MSLen, sizeof(int));
        MSPointsBuffer = new ComputeBuffer(MSLen, sizeof(float));
        ColorsBuffer = new ComputeBuffer(MSLen, sizeof(float) * 4); // 4 floats for RGBA
        VerticesBuffer.SetData(vertices);
        TrianglesBuffer.SetData(triangles);
        MSPointsBuffer.SetData(MSPoints);

        // RigidBodyIndicesBuffer = new ComputeBuffer(RigidBodyIndices.Length, sizeof(int) * 2);
        RBDataBuffer = new ComputeBuffer(RBData.Length, sizeof(float) * 15 + sizeof(int) * 4);
        RBVectorBuffer = new ComputeBuffer(RBVector.Length, sizeof(float) * 7 + sizeof(int) * 2);
        RBDataBuffer.SetData(RBData);
        RBVectorBuffer.SetData(RBVector);

        TraversedChunks_AC_Buffer = new ComputeBuffer(4096, sizeof(int) * 3, ComputeBufferType.Append);
        TCCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        SRCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        StickynessReqs_AC_Buffer = new ComputeBuffer(4096, sizeof(float) * 5 + sizeof(int) * 2, ComputeBufferType.Append);
        SortedStickyRequestsBuffer = new ComputeBuffer(4096, sizeof(float) * 5 + sizeof(int) * 2);
        StickyRequestsResult_AC_Buffer = new ComputeBuffer(4096, sizeof(float) * 5 + sizeof(int) * 2, ComputeBufferType.Append);
    }

    void GPUSortStickynessRequests()
    {
        int StickyRequestsCount = 4096;

        if (StickyRequestsCount == 0) {return;}
        int ThreadSize = (int)Math.Ceiling((float)StickyRequestsCount / 512);
        int ThreadSizeHLen = (int)Math.Ceiling((float)StickyRequestsCount / 512)/2;

        sortShader.Dispatch(9, ThreadSize, 1, 1);

        int len = StickyRequestsCount;
        int lenLog2 = (int)Math.Log(len, 2);
        sortShader.SetInt("SortedStickyRequestsLength", len);
        sortShader.SetInt("SortedStickyRequestsLog2Length", lenLog2);

        int basebBlockLen = 2;
        while (basebBlockLen != 2*len) // basebBlockLen = len is the last outer iteration
        {
            int blockLen = basebBlockLen;
            while (blockLen != 1) // BlockLen = 2 is the last inner iteration
            {
                int blocksNum = len / blockLen;
                bool BrownPinkSort = blockLen == basebBlockLen;

                sortShader.SetInt("SRBlockLen", blockLen);
                sortShader.SetInt("SRblocksNum", blocksNum);
                sortShader.SetBool("SRBrownPinkSort", BrownPinkSort);

                sortShader.Dispatch(10, ThreadSizeHLen, 1, 1);

                blockLen /= 2;
            }
            basebBlockLen *= 2;
        }
    }

    void GPUSortChunkLookUp()
    {
        if (ParticlesNum == 0) {return;}
        int ThreadSize = (int)Math.Ceiling((float)ParticlesNum / 512);
        int ThreadSizeHLen = (int)Math.Ceiling((float)ParticlesNum / 512)/2;

        sortShader.Dispatch(0, ThreadSize, 1, 1);

        int len = ParticlesNum;
        int lenLog2 = (int)Math.Log(len, 2);
        sortShader.SetInt("SortedSpatialLookupLength", len);
        sortShader.SetInt("SortedSpatialLookupLog2Length", lenLog2);

        int basebBlockLen = 2;
        while (basebBlockLen != 2*len) // basebBlockLen = len is the last outer iteration
        {
            int blockLen = basebBlockLen;
            while (blockLen != 1) // BlockLen = 2 is the last inner iteration
            {
                int blocksNum = len / blockLen;
                bool BrownPinkSort = blockLen == basebBlockLen;

                sortShader.SetInt("BlockLen", blockLen);
                sortShader.SetInt("blocksNum", blocksNum);
                sortShader.SetBool("BrownPinkSort", BrownPinkSort);

                sortShader.Dispatch(1, ThreadSizeHLen, 1, 1);

                blockLen /= 2;
            }
            basebBlockLen *= 2;
        }

        // This is unnecessary if particlesNum stays constant, disabled
        // sortShader.Dispatch(2, ThreadSize, 1, 1);

        sortShader.Dispatch(3, ThreadSize, 1, 1);
    }

    void GPUSortSpringLookUp()
    {
        // Spring buffer kernels
        int ThreadSizeChunkSizes = ChunkNum / 10;
        sortShader.Dispatch(4, ThreadSizeChunkSizes, 1, 1); // Set ChunkSizes
        sortShader.Dispatch(5, ThreadSizeChunkSizes, 1, 1); // Set SpringCapacities

        sortShader.Dispatch(6, ThreadSizeChunkSizes, 1, 1); // Copy SpringCapacities to double buffers

        // Calculate prefix sums (SpringStartIndices)
        int offset = -1;
        bool StepBufferCycle = false;
        for (int iteration = 1; iteration <= ChunksNumLog2; iteration++)
        {
            StepBufferCycle = !StepBufferCycle;
            offset = offset == -1 ? 1 : 2 * offset; // offset *= 2, offset_1 = 1
            int halfOffset = offset == 1 ? 0 : offset / 2;
            int totIndicesToProcess = ChunkNum - offset;

            sortShader.SetBool("StepBufferCycle", StepBufferCycle);
            sortShader.SetInt("IndexOffset", offset);
            sortShader.SetInt("HalfOffset", halfOffset);
            sortShader.SetInt("TotIndicesToProcess", totIndicesToProcess);
            
            sortShader.Dispatch(7, ThreadSizeChunkSizes, 1, 1); // totIndicesToProcess !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }
        if (StepBufferCycle == true) { sortShader.Dispatch(8, ThreadSizeChunkSizes, 1, 1); } // copy to result buffer if necessary
    }

    void CPUSortChunkData()
    {
        PDataBuffer.GetData(PData);

        for (int i = 0; i < ParticlesNum; i++)
        {
            int ChunkX = (int)(PData[i].PredPosition.x / MaxInfluenceRadius);
            int ChunkY = (int)(PData[i].PredPosition.y / MaxInfluenceRadius);
            int ChunkKey = ChunkY * ChunkNumW + ChunkX;

            SpatialLookup[i] = new int2(i, ChunkKey);
        }

        Array.Sort(SpatialLookup, (a, b) => a.y.CompareTo(b.y));

        for (int i = 0; i < ParticlesNum; i++)
        {
            StartIndices[i] = IOOR;
        }

        int lastChunkKey = -1;
        for (int i = 0; i < ParticlesNum; i++)
        {
            int ChunkKey = SpatialLookup[i].y;
            if (ChunkKey != lastChunkKey)
            {
                StartIndices[ChunkKey] = i;
                lastChunkKey = ChunkKey;
            }
        }

        // not updated
        // if (ParticlesNum != 0) {
        //     pSimShader.SetBuffer(2, "SpatialLookup", SpatialLookupBuffer);
        //     pSimShader.SetBuffer(2, "StartIndices", StartIndicesBuffer);
        // }
        // if (ParticlesNum != 0) {
        //     pSimShader.SetBuffer(4, "SpatialLookup", SpatialLookupBuffer);
        //     pSimShader.SetBuffer(4, "StartIndices", StartIndicesBuffer);
        // }
        // if (ParticlesNum != 0) {
        //     renderShader.SetBuffer(0, "SpatialLookup", SpatialLookupBuffer);
        //     renderShader.SetBuffer(0, "StartIndices", StartIndicesBuffer);
        // }
        // if (ParticlesNum != 0) {
        //     marchingSquaresShader.SetBuffer(0, "SpatialLookup", SpatialLookupBuffer);
        //     marchingSquaresShader.SetBuffer(0, "StartIndices", StartIndicesBuffer);
        // }
    }

    void RunPSimShader(int step)
    {
        int ThreadSize = (int)Math.Ceiling((float)ParticlesNum / 512);

        if (ParticlesNum != 0) {pSimShader.Dispatch(0, ThreadSize, 1, 1);}
        if (ParticlesNum != 0) {pSimShader.Dispatch(1, ThreadSize, 1, 1);} // CalculateDensities

        // Particle springs
        if (step == 0)
        {
            int ThreadSize2 = (int)Math.Ceiling((float)SpringCapacitySafety * ParticlesNum / (2 * 30));
            // Transfer spring data kernel
            if (ParticlesNum != 0) {pSimShader.Dispatch(2, ThreadSize2, 1, 1);}
            if (ParticlesNum != 0) {pSimShader.Dispatch(3, ThreadSize2, 1, 1);}
        }

        if (ParticlesNum != 0) {pSimShader.Dispatch(4, ThreadSize, 1, 1);} // ParticleForces
    }

    void RunRbSimShader()
    {
        if (RBVectorNum > 1 && ParticlesNum != 0) 
        {
                rbSimShader.Dispatch(0, RBVectorNum, 1, 1);

                TraversedChunks_AC_Buffer.SetCounterValue(0);
                rbSimShader.Dispatch(1, RBVectorNum-1, 1, 1);

                if (TraversedChunksCount == 0)
                {
                    Debug.Log("TraversedChunksCount updated");
                    ComputeBuffer.CopyCount(TraversedChunks_AC_Buffer, TCCountBuffer, 0);
                    TCCountBuffer.GetData(TCCount);
                    TraversedChunksCount = (int)Math.Ceiling(TCCount[0] * (1+ChunkStorageSafety));
                }

                if (DoCalcStickyRequests) {
                    StickynessReqs_AC_Buffer.SetCounterValue(0);
                }
                rbSimShader.Dispatch(2, TraversedChunksCount, 1, 1);
                rbSimShader.Dispatch(3, RBodiesNum, 1, 1);
        }
    }

    void RunMarchingSquaresShader()
    {
        VerticesBuffer.SetData(vertices);
        TrianglesBuffer.SetData(triangles);

        marchingSquaresShader.Dispatch(0, MarchW, MarchH, 1);
        marchingSquaresShader.Dispatch(1, MarchW-1, MarchH-1, 1);

        VerticesBuffer.GetData(vertices);
        TrianglesBuffer.GetData(triangles);
        // ColorsBuffer.GetData(colors);

        marchingSquaresMesh.vertices = vertices;
        marchingSquaresMesh.triangles = triangles;
        // marchingSquaresMesh.colors = colors;
        marchingSquaresMesh.RecalculateNormals();
    }

    void RunRenderShader()
    {

        int ThreadSize = 32;

        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(ResolutionX, ResolutionY, 24)
            {
                enableRandomWrite = true
            };
            renderTexture.Create();
        }

        renderShader.SetTexture(0, "Result", renderTexture);
        if (ParticlesNum != 0) {renderShader.Dispatch(0, renderTexture.width / ThreadSize, renderTexture.height / ThreadSize, 1);}
        // Render rigid bodies - not implemented
        // if (RBodiesNum != 0) {pSimShader.Dispatch(1, RBodiesNum, 1, 1);}
    }

    public void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (!RenderMarchingSquares)
        {
            RunRenderShader();

            Graphics.Blit(renderTexture, dest);
        }
        else
        {
            Graphics.Blit(src, dest);
        }
    }

    void OnDestroy()
    {
        SpatialLookupBuffer?.Release();
        StartIndicesBuffer?.Release();

        VerticesBuffer?.Release();
        TrianglesBuffer?.Release();
        ColorsBuffer?.Release();
        MSPointsBuffer?.Release();

        PDataBuffer?.Release();
        PTypesBuffer?.Release();

        ChunkSizesBuffer?.Release();
        SpringCapacitiesBuffer?.Release();
        SpringStartIndicesBuffer_dbA?.Release();
        SpringStartIndicesBuffer_dbB?.Release();
        SpringStartIndicesBuffer_dbC?.Release();
        ParticleSpringsCombinedBuffer?.Release();

        RBDataBuffer?.Release();
        RBVectorBuffer?.Release();
        TraversedChunks_AC_Buffer?.Release();
        TCCountBuffer?.Release();
        SRCountBuffer?.Release();
        StickynessReqs_AC_Buffer?.Release();
        SortedStickyRequestsBuffer?.Release();
        StickyRequestsResult_AC_Buffer?.Release();
    }
}