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
    public float ChunkStorageSafety;
    public int SpringCapacity;
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
    public GameObject ParticlePrefab;
    public ComputeShader RenderShader;
    public ComputeShader PSimShader;
    public ComputeShader RbSimShader;
    public ComputeShader SortShader;
    public ComputeShader MarchingSquaresShader;

    // Private references
    private RenderTexture renderTexture;
    private GameObject simulationBoundrary;
    private Mesh marchingSquaresMesh;

    // Constants
    private int MaxInfluenceRadiusSqr;
    private float InvMaxInfluenceRadius;
    private float MarchScale;
    private int ChunkNumW;
    private int ChunkNumH;
    private int IOOR; // Index Out Of Range
    private int SIOOR; // Spring Index Out Of Range
    private int SpringPairsLen;
    private int MSLen;
    private int RBodiesNum;
    private int RBVectorNum;
    private int TraversedChunksCount;

    // PData - Properties
    private int2[] SpatialLookup; // [](particleIndex, chunkKey)
    private int2[] TemplateSpatialLookup;
    private int[] StartIndices;
    private SpringStruct[] SpringPairs;
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

    // Marching Squares - Buffers
    private ComputeBuffer VerticesBuffer;
    private ComputeBuffer TrianglesBuffer;
    private ComputeBuffer ColorsBuffer;
    private ComputeBuffer MSPointsBuffer;

    // Bitonic Mergesort - Buffers
    private ComputeBuffer SpatialLookupBuffer;
    private ComputeBuffer StartIndicesBuffer;

    // PData, SpringPairs - Buffers
    private ComputeBuffer PDataBuffer;
    private ComputeBuffer SpringPairsBuffer;
    private ComputeBuffer PTypesBuffer;

    // Rigid Bodies - Buffers
    private ComputeBuffer RBVectorBuffer;
    private ComputeBuffer RBDataBuffer;
    private ComputeBuffer TraversedChunks_AC_Buffer;
    private ComputeBuffer StickynessReqs_AC_Buffer;
    private ComputeBuffer SortedStickyRequestsBuffer;
    private ComputeBuffer StickyRequestsResult_AC_Buffer;
    private ComputeBuffer TCCountBuffer;
    private ComputeBuffer SRCountBuffer;

    // Other
    private float DeltaTime;
    private int FrameCounter;
    private int CalcStickyRequestsFrequency = 1;
    private int GPUChunkDataSortFrequency = 3;
    private bool DoCalcStickyRequests = false;
    private bool DoGPUChunkDataSort = false;

    // Perhaps should create a seperate class for PSimShader functions/methods
    void Start()
    {
        while (Height % MaxInfluenceRadius != 0) {
            Height += 1;
        }
        while (Width % MaxInfluenceRadius != 0) {
            Width += 1;
        }

        CreateVisualBoundrary();
        InitializeSetArrays();

        Camera.main.transform.position = new Vector3(Width / 2, Height / 2, -1);
        Camera.main.orthographicSize = Mathf.Max(Width * 0.75f, Height * 1.5f);

        marchingSquaresMesh = GetComponent<MeshFilter>().mesh;

        SetConstants();

        InitializeArrays();

        for (int i = 0; i < ParticlesNum; i++) {
            PData[i].Position = ParticleSpawnPosition(i, ParticlesNum);
        }

        InitializeBuffers();
        SetPSimShaderBuffers();
        SetRbSimShaderBuffers();
        SetRenderShaderBuffers();
        SetSortShaderBuffers();
        SetMarchingSquaresShaderBuffers();

        UpdateRenderShaderVariables();

        // init chunk data
        GPUSortChunkData();
    }

    void Update()
    {
        for (int i = 0; i < TimeStepsPerRender; i++)
        {
            FrameCounter++;

            if (FrameCounter % CalcStickyRequestsFrequency != 0 || CalcStickyRequestsFrequency == -1) { DoCalcStickyRequests = false; }
            else { DoCalcStickyRequests = true; } 
            if (FrameCounter % GPUChunkDataSortFrequency == 0) { DoGPUChunkDataSort = true; }
            else { DoGPUChunkDataSort = false; }

            if (DoGPUChunkDataSort) { GPUSortChunkData(); }

            RunPSimShader();
            
            if (DoCalcStickyRequests) { GPUSortStickynessRequests(); }

            RunRbSimShader();

            int ThreadSize = (int)Math.Ceiling((float)ParticlesNum / 512);
            if (ParticlesNum != 0) {PSimShader.Dispatch(3, ThreadSize, 1, 1);}
        }
        
        if (RenderMarchingSquares)
        {
            RunMarchingSquaresShader();
        }

        // RunRenderShader() is called by OnRenderImage()
    }

    void SetConstants()
    {
        MaxInfluenceRadiusSqr = MaxInfluenceRadius * MaxInfluenceRadius;
        InvMaxInfluenceRadius = 1.0f / MaxInfluenceRadius;
        ChunkNumW = Width / MaxInfluenceRadius;
        ChunkNumH = Height / MaxInfluenceRadius;
        IOOR = ParticlesNum;
        SIOOR = ParticlesNum * SpringCapacity;
        SpringPairsLen = ParticlesNum * SpringCapacity;
        MarchW = (int)(Width / MSResolution);
        MarchH = (int)(Height / MSResolution);
        MSLen = MarchW * MarchH * TriStorageLength * 3;
        RBVectorNum = RBVector.Length;
        // RBodiesNum set at InitializeSetArrays()

        for (int i = 0; i < RBodiesNum; i++)
        {
            RBData[i].StickynessRangeSqr = RBData[i].StickynessRange*RBData[i].StickynessRange;

            float furthestDstSqr = 0;
            int startIndex = RBData[i].LineIndices.x;
            int endIndex = RBData[i].LineIndices.y;
            for (int j = startIndex; j <= endIndex; j++)
            {
                Vector2 dst = RBVector[j].Position - RBData[i].Position;
                float absDstSqr = dst.sqrMagnitude;
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
            Stickyness = 22f,
            Gravity = Gravity,
            colorG = 1f
        };

        RBData = new RBDataStruct[1];
        RBData[0] = new RBDataStruct
        {
            Position = new float2(140f, 100f),
            Velocity = new float2(0.0f, 0.0f),
            NextPos = new float2(140f, 100f),
            NextVel = new float2(0.0f, 0.0f),
            NextAngImpulse = 0f,
            AngularImpulse = 0.0f,
            Stickyness = 4f,
            StickynessRange = 4f,
            StickynessRangeSqr = 16f,
            Mass = 200f,
            WallCollision = 0,
            Stationary = 1,
            LineIndices = new int2(0, 8)
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
        RBVector = new RBVectorStruct[9];
        RBVector[0] = new RBVectorStruct { Position = new float2(10f, 20f) * 1.5f, ParentRBIndex = 0 };
        RBVector[1] = new RBVectorStruct { Position = new float2(50f, 20f) * 1.5f, ParentRBIndex = 0 };
        RBVector[2] = new RBVectorStruct { Position = new float2(50f, 50f) * 1.5f, ParentRBIndex = 0 };
        RBVector[3] = new RBVectorStruct { Position = new float2(40f, 50f) * 1.5f, ParentRBIndex = 0 };
        RBVector[4] = new RBVectorStruct { Position = new float2(39f, 30f) * 1.5f, ParentRBIndex = 0 };
        RBVector[5] = new RBVectorStruct { Position = new float2(21f, 30f) * 1.5f, ParentRBIndex = 0 };
        RBVector[6] = new RBVectorStruct { Position = new float2(20f, 50f) * 1.5f, ParentRBIndex = 0 };
        RBVector[7] = new RBVectorStruct { Position = new float2(10f, 50f) * 1.5f, ParentRBIndex = 0 };
        RBVector[8] = new RBVectorStruct { Position = new float2(10f, 20f) * 1.5f, ParentRBIndex = 0 };

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
        StartIndices = new int[ParticlesNum];
        PData = new PDataStruct[ParticlesNum];
        SpringPairs = new SpringStruct[SpringPairsLen];

        vertices = new Vector3[MSLen];
        triangles = new int[MSLen];
        colors = new Color[MSLen];
        MSPoints = new float[MSLen];

        TemplateSpatialLookup = new int2[ParticlesNum];

        for (int i = 0; i < ParticlesNum; i++)
        {
            StartIndices[i] = 0;

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

        for (int i = 0; i < SpringPairsLen; i++)
        {
            SpringPairs[i] = new SpringStruct
            {
                linkedIndex = IOOR,
                restLength = 1
            };
        }
    }

    float2 ParticleSpawnPosition(int pIndex, int maxIndex)
    {
        float x = (Width - SpawnDims) / 2 + Mathf.Floor(pIndex % Mathf.Sqrt(maxIndex)) * (SpawnDims / Mathf.Sqrt(maxIndex));
        float y = (Height - SpawnDims) / 2 + Mathf.Floor(pIndex / Mathf.Sqrt(maxIndex)) * (SpawnDims / Mathf.Sqrt(maxIndex));
        if (SpawnDims > Width || SpawnDims > Height)
        {
            throw new ArgumentException("Particle spawn dimensions larger than either border_width or border_height");
        }
        return new float2(x, y);
    }

    void CreateVisualBoundrary()
    {
        // Create an empty GameObject for the border
        simulationBoundrary = new GameObject("SimulationBoundary");
        simulationBoundrary.transform.parent = transform;

        // Add a LineRenderer component to represent the border
        LineRenderer lineRenderer = simulationBoundrary.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 5;

        lineRenderer.SetPositions(new Vector3[]
        {
            new(0f, 0f, 0f),
            new(Width, 0f, 0f),
            new(Width, Height, 0f),
            new(0f, Height, 0f),
            new(0f, 0f, 0f),
        });

        // Optional: Set LineRenderer properties (material, color, width, etc.)
        // lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.green;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
    }

    void InitializeBuffers()
    {
        if (ParticlesNum != 0)
        {
            PDataBuffer = new ComputeBuffer(ParticlesNum, sizeof(float) * 10 + sizeof(int) * 1);
            SpringPairsBuffer = new ComputeBuffer(SpringPairsLen, sizeof(float) + sizeof(int));
            PTypesBuffer = new ComputeBuffer(PTypes.Length, sizeof(float) * 10 + sizeof(int) * 1);

            PDataBuffer.SetData(PData);
            SpringPairsBuffer.SetData(SpringPairs);
            PTypesBuffer.SetData(PTypes);
        }

        SpatialLookupBuffer = new ComputeBuffer(ParticlesNum, sizeof(int) * 2);

        StartIndicesBuffer = new ComputeBuffer(ParticlesNum, sizeof(int));
        SpatialLookupBuffer.SetData(SpatialLookup);
        StartIndicesBuffer.SetData(StartIndices);

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

    void SetPSimShaderBuffers()
    {
        if (ParticlesNum != 0) {
            // Kernel PreCalculations
            PSimShader.SetBuffer(0, "PData", PDataBuffer);
            PSimShader.SetBuffer(0, "PTypes", PTypesBuffer);
        
            // Kernel PreCalculations
            PSimShader.SetBuffer(1, "SpatialLookup", SpatialLookupBuffer);
            PSimShader.SetBuffer(1, "StartIndices", StartIndicesBuffer);

            PSimShader.SetBuffer(1, "PData", PDataBuffer);
            PSimShader.SetBuffer(1, "PTypes", PTypesBuffer);

            // Kernel ParticleForces
            PSimShader.SetBuffer(2, "SpatialLookup", SpatialLookupBuffer);
            PSimShader.SetBuffer(2, "StartIndices", StartIndicesBuffer);

            PSimShader.SetBuffer(2, "SpringPairs", SpringPairsBuffer);
            PSimShader.SetBuffer(2, "PData", PDataBuffer);
            PSimShader.SetBuffer(2, "PTypes", PTypesBuffer);
            PSimShader.SetBuffer(2, "RBData", RBDataBuffer);
            PSimShader.SetBuffer(2, "RBVector", RBVectorBuffer); 

            PSimShader.SetBuffer(3, "PData", PDataBuffer);
            PSimShader.SetBuffer(3, "PTypes", PTypesBuffer);

            PSimShader.SetBuffer(4, "PData", PDataBuffer);
            PSimShader.SetBuffer(4, "PTypes", PTypesBuffer);
            PSimShader.SetBuffer(4, "SortedStickyRequests", SortedStickyRequestsBuffer); 
        }
    }

    void SetRbSimShaderBuffers()
    {
        if (RBodiesNum != 0)
        {
            RbSimShader.SetBuffer(0, "RBVector", RBVectorBuffer);
            RbSimShader.SetBuffer(0, "RBData", RBDataBuffer);

            RbSimShader.SetBuffer(1, "RBVector", RBVectorBuffer);
            RbSimShader.SetBuffer(1, "RBData", RBDataBuffer);
            RbSimShader.SetBuffer(1, "TraversedChunksAPPEND", TraversedChunks_AC_Buffer);

            // Maximum reached! (8)
            RbSimShader.SetBuffer(2, "PData", PDataBuffer);
            RbSimShader.SetBuffer(2, "PTypes", PTypesBuffer);
            RbSimShader.SetBuffer(2, "RBData", RBDataBuffer);
            RbSimShader.SetBuffer(2, "RBVector", RBVectorBuffer);
            RbSimShader.SetBuffer(2, "SpatialLookup", SpatialLookupBuffer);
            RbSimShader.SetBuffer(2, "StartIndices", StartIndicesBuffer);
            RbSimShader.SetBuffer(2, "TraversedChunksCONSUME", TraversedChunks_AC_Buffer);
            RbSimShader.SetBuffer(2, "StickynessReqsAPPEND", StickynessReqs_AC_Buffer);

            RbSimShader.SetBuffer(3, "RBData", RBDataBuffer);
            RbSimShader.SetBuffer(3, "RBVector", RBVectorBuffer);
        }
    }

    void SetRenderShaderBuffers()
    {
        if (ParticlesNum != 0) {
            RenderShader.SetBuffer(0, "SpatialLookup", SpatialLookupBuffer);
            RenderShader.SetBuffer(0, "StartIndices", StartIndicesBuffer);

            RenderShader.SetBuffer(0, "PData", PDataBuffer);
            RenderShader.SetBuffer(0, "PTypes", PTypesBuffer);
        }
        if (RBodiesNum != 0)
        {
            RenderShader.SetBuffer(0, "RBData", RBDataBuffer);
            RenderShader.SetBuffer(0, "RBVector", RBVectorBuffer);
        }
    }

    void SetSortShaderBuffers()
    {
        SpatialLookupBuffer.SetData(SpatialLookup);
        StartIndicesBuffer.SetData(StartIndices);
        
        SortShader.SetBuffer(0, "SpatialLookup", SpatialLookupBuffer);

        SortShader.SetBuffer(0, "PData", PDataBuffer);
        SortShader.SetBuffer(0, "PTypes", PTypesBuffer);

        SortShader.SetBuffer(1, "SpatialLookup", SpatialLookupBuffer);

        SortShader.SetBuffer(1, "PData", PDataBuffer);
        SortShader.SetBuffer(1, "PTypes", PTypesBuffer);

        SortShader.SetBuffer(2, "StartIndices", StartIndicesBuffer);

        SortShader.SetBuffer(3, "SpatialLookup", SpatialLookupBuffer);
        SortShader.SetBuffer(3, "StartIndices", StartIndicesBuffer);
        SortShader.SetBuffer(3, "PTypes", PTypesBuffer);
        SortShader.SetBuffer(3, "PData", PDataBuffer);

        SortShader.SetBuffer(4, "StickynessReqsCONSUME", StickynessReqs_AC_Buffer);
        SortShader.SetBuffer(4, "SortedStickyRequests", SortedStickyRequestsBuffer);

        SortShader.SetBuffer(5, "SortedStickyRequests", SortedStickyRequestsBuffer);
    }

    void SetMarchingSquaresShaderBuffers()
    {
        MarchingSquaresShader.SetBuffer(0, "MSPoints", MSPointsBuffer);
        MarchingSquaresShader.SetBuffer(0, "SpatialLookup", SpatialLookupBuffer);
        MarchingSquaresShader.SetBuffer(0, "StartIndices", StartIndicesBuffer);

        MarchingSquaresShader.SetBuffer(0, "PData", PDataBuffer);
        MarchingSquaresShader.SetBuffer(0, "PTypes", PTypesBuffer);
        
        MarchingSquaresShader.SetBuffer(1, "Vertices", VerticesBuffer);
        MarchingSquaresShader.SetBuffer(1, "Triangles", TrianglesBuffer);
        MarchingSquaresShader.SetBuffer(1, "Colors", ColorsBuffer);
        MarchingSquaresShader.SetBuffer(1, "MSPoints", MSPointsBuffer);
    }
    
    void UpdatePSimShaderVariables()
    {
        // Delta time
        if (FixedTimeStep)
        {
            DeltaTime = TimeStep / TimeStepsPerRender;
        }
        else
        {
            DeltaTime = Time.deltaTime * ProgramSpeed / TimeStepsPerRender;
        }

        // Mouse variables
        Vector3 MousePos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x , Input.mousePosition.y , -Camera.main.transform.position.z));
        Vector2 MouseWorldPos = new(((MousePos.x - Width/2) * 0.55f + Width) / 2, ((MousePos.y - Height/2) * 0.55f + Height) / 2);
        bool LMousePressed = Input.GetMouseButton(0);
        bool RMousePressed = Input.GetMouseButton(1);

        // Set sim shader variables
        PSimShader.SetFloat("DeltaTime", DeltaTime);
        PSimShader.SetFloat("MouseX", MouseWorldPos.x);
        PSimShader.SetFloat("MouseY", MouseWorldPos.y);
        PSimShader.SetBool("RMousePressed", RMousePressed);
        PSimShader.SetBool("LMousePressed", LMousePressed);

        // Set PSimShader constants
        PSimShader.SetInt("MaxInfluenceRadiusSqr", MaxInfluenceRadiusSqr);
        PSimShader.SetFloat("InvMaxInfluenceRadius", InvMaxInfluenceRadius);
        PSimShader.SetInt("ChunkNumW", ChunkNumW);
        PSimShader.SetInt("ChunkNumH", ChunkNumH);
        PSimShader.SetInt("IOOR", IOOR);
        PSimShader.SetInt("SIOOR", SIOOR);
        PSimShader.SetInt("Width", Width);
        PSimShader.SetInt("Height", Height);
        PSimShader.SetInt("ParticlesNum", ParticlesNum);
        PSimShader.SetInt("MaxInfluenceRadius", MaxInfluenceRadius);
        PSimShader.SetInt("SpawnDims", SpawnDims);
        PSimShader.SetInt("TimeStepsPerRender", TimeStepsPerRender);
        PSimShader.SetFloat("LookAheadFactor", LookAheadFactor);
        PSimShader.SetFloat("BorderPadding", BorderPadding);
        PSimShader.SetFloat("MaxInteractionRadius", MaxInteractionRadius);
        PSimShader.SetFloat("InteractionAttractionPower", InteractionAttractionPower);
        PSimShader.SetFloat("InteractionFountainPower", InteractionFountainPower);
        PSimShader.SetInt("SpringCapacity", SpringCapacity);
        PSimShader.SetFloat("Plasticity", Plasticity);
        // Set math resources constants
    }

    void UpdateRbSimShaderVariables()
    {
        RbSimShader.SetInt("ChunkNumW", ChunkNumW);
        RbSimShader.SetInt("ChunkNumH", ChunkNumH);
        RbSimShader.SetInt("Width", Width);
        RbSimShader.SetInt("Height", Height);
        RbSimShader.SetInt("ParticlesNum", ParticlesNum);
        RbSimShader.SetInt("RBodiesNum", RBodiesNum);
        RbSimShader.SetInt("MaxInfluenceRadius", MaxInfluenceRadius);
        RbSimShader.SetInt("MaxChunkSearchSafety", MaxChunkSearchSafety);

        RbSimShader.SetFloat("Damping", Damping);
        RbSimShader.SetFloat("Gravity", Gravity);
        RbSimShader.SetFloat("RbElasticity", RbElasticity);
        RbSimShader.SetFloat("BorderPadding", BorderPadding);

        RbSimShader.SetFloat("DeltaTime", DeltaTime);

        if (DoCalcStickyRequests) {
            RbSimShader.SetInt("DoCalcStickyRequests", 1);
        }
        else {
            RbSimShader.SetInt("DoCalcStickyRequests", 0);
        }
    }

    void UpdateRenderShaderVariables()
    {
        RenderShader.SetFloat("VisualParticleRadii", VisualParticleRadii);
        RenderShader.SetFloat("RBRenderThickness", RBRenderThickness);
        RenderShader.SetInt("ResolutionX", ResolutionX);
        RenderShader.SetInt("ResolutionY", ResolutionY);
        RenderShader.SetInt("Width", Width);
        RenderShader.SetInt("Height", Height);
        RenderShader.SetInt("MaxInfluenceRadius", MaxInfluenceRadius);
        RenderShader.SetInt("ChunkNumW", ChunkNumW);
        RenderShader.SetInt("ChunkNumH", ChunkNumH);
        RenderShader.SetInt("ParticlesNum", ParticlesNum);
        RenderShader.SetInt("RBodiesNum", RBodiesNum);
        RenderShader.SetInt("RBVectorNum", RBVectorNum);
        
    }

    void UpdateSortShaderVariables()
    {
        SortShader.SetInt("MaxInfluenceRadius", MaxInfluenceRadius);
        SortShader.SetInt("ChunkNumW", ChunkNumW);
        SortShader.SetInt("IOOR", IOOR);
    }

    void UpdateMarchingSquaresShaderVariables()
    {
        MarchW = (int)(Width / MSResolution);
        MarchH = (int)(Height / MSResolution);
        MSLen = MarchW * MarchH * TriStorageLength * 3;
        
        MarchingSquaresShader.SetInt("MarchW", MarchW);
        MarchingSquaresShader.SetInt("MarchH", MarchH);
        MarchingSquaresShader.SetFloat("MSResolution", MSResolution);
        MarchingSquaresShader.SetInt("MaxInfluenceRadius", MaxInfluenceRadius);
        MarchingSquaresShader.SetInt("ChunkNumW", ChunkNumW);
        MarchingSquaresShader.SetInt("ChunkNumH", ChunkNumH);
        MarchingSquaresShader.SetInt("Width", Width);
        MarchingSquaresShader.SetInt("Height", Height);
        MarchingSquaresShader.SetInt("ParticlesNum", ParticlesNum);
        MarchingSquaresShader.SetFloat("MSvalMin", MSvalMin);
        MarchingSquaresShader.SetFloat("TriStorageLength", TriStorageLength);
    }

    void GPUSortStickynessRequests()
    {
        UpdateSortShaderVariables();
        // Very expensive
        // ComputeBuffer.CopyCount(StickynessReqs_AC_Buffer, SRCountBuffer, 0);
        // SRCountBuffer.GetData(SRCount);
        // int StickyRequestsCount = SRCount[0];
        // if (StickyRequestsCount == 0) { StickyRequestsCount = 2; }
        int StickyRequestsCount = 4096;

        if (StickyRequestsCount == 0) {return;}
        int ThreadSize = (int)Math.Ceiling((float)StickyRequestsCount / 32);
        int ThreadSizeHLen = (int)Math.Ceiling((float)StickyRequestsCount / 32)/2;

        SortShader.Dispatch(4, ThreadSize, 1, 1);

        int len = StickyRequestsCount;
        int lenLog2 = (int)Math.Log(len, 2);
        SortShader.SetInt("SortedStickyRequestsLength", len);
        SortShader.SetInt("SortedStickyRequestsLog2Length", lenLog2);

        int basebBlockLen = 2;
        while (basebBlockLen != 2*len) // basebBlockLen = len is the last outer iteration
        {
            int blockLen = basebBlockLen;
            while (blockLen != 1) // BlockLen = 2 is the last inner iteration
            {
                int blocksNum = len / blockLen;
                bool BrownPinkSort = blockLen == basebBlockLen;

                SortShader.SetInt("SRBlockLen", blockLen);
                SortShader.SetInt("SRblocksNum", blocksNum);
                SortShader.SetBool("SRBrownPinkSort", BrownPinkSort);

                SortShader.Dispatch(5, ThreadSizeHLen, 1, 1);

                blockLen /= 2;
            }
            basebBlockLen *= 2;
        }

        ThreadSize = (int)Math.Ceiling((float)ParticlesNum / 512);
        PSimShader.SetFloat("SRDeltaTime", DeltaTime * CalcStickyRequestsFrequency);
        PSimShader.Dispatch(4, ThreadSize, 1, 1);
    }

    void GPUSortChunkData()
    {
        if (ParticlesNum == 0) {return;}
        UpdateSortShaderVariables();
        int ThreadSize = (int)Math.Ceiling((float)ParticlesNum / 32);
        int ThreadSizeHLen = (int)Math.Ceiling((float)ParticlesNum / 32)/2;

        SortShader.Dispatch(0, ThreadSize, 1, 1);

        int len = ParticlesNum;
        int lenLog2 = (int)Math.Log(len, 2);
        SortShader.SetInt("SortedSpatialLookupLength", len);
        SortShader.SetInt("SortedSpatialLookupLog2Length", lenLog2);

        int basebBlockLen = 2;
        while (basebBlockLen != 2*len) // basebBlockLen = len is the last outer iteration
        {
            int blockLen = basebBlockLen;
            while (blockLen != 1) // BlockLen = 2 is the last inner iteration
            {
                int blocksNum = len / blockLen;
                bool BrownPinkSort = blockLen == basebBlockLen;

                SortShader.SetInt("BlockLen", blockLen);
                SortShader.SetInt("blocksNum", blocksNum);
                SortShader.SetBool("BrownPinkSort", BrownPinkSort);

                SortShader.Dispatch(1, ThreadSizeHLen, 1, 1);

                blockLen /= 2;
            }
            basebBlockLen *= 2;
        }

        // This is unnecessary IF PARTICLESNUM STAYS CONSTANT
        // SortShader.Dispatch(2, ThreadSize, 1, 1);

        SortShader.Dispatch(3, ThreadSize, 1, 1);
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

        if (ParticlesNum != 0) {
            PSimShader.SetBuffer(1, "SpatialLookup", SpatialLookupBuffer);
            PSimShader.SetBuffer(1, "StartIndices", StartIndicesBuffer);
        }
        if (ParticlesNum != 0) {
            PSimShader.SetBuffer(4, "SpatialLookup", SpatialLookupBuffer);
            PSimShader.SetBuffer(4, "StartIndices", StartIndicesBuffer);
        }
        if (ParticlesNum != 0) {
            RenderShader.SetBuffer(0, "SpatialLookup", SpatialLookupBuffer);
            RenderShader.SetBuffer(0, "StartIndices", StartIndicesBuffer);
        }
        if (ParticlesNum != 0) {
            MarchingSquaresShader.SetBuffer(0, "SpatialLookup", SpatialLookupBuffer);
            MarchingSquaresShader.SetBuffer(0, "StartIndices", StartIndicesBuffer);
        }
    }

    void RunPSimShader()
    {
        UpdatePSimShaderVariables();

        int ThreadSize = (int)Math.Ceiling((float)ParticlesNum / 512);

        if (ParticlesNum != 0) {PSimShader.Dispatch(0, ThreadSize, 1, 1);}
        if (ParticlesNum != 0) {PSimShader.Dispatch(1, ThreadSize, 1, 1);}
        if (ParticlesNum != 0) {PSimShader.Dispatch(2, ThreadSize, 1, 1);}
    }

    void RunRbSimShader()
    {
        UpdateRbSimShaderVariables();

        if (RBVectorNum > 1 && ParticlesNum != 0) 
        {
                RbSimShader.Dispatch(0, RBVectorNum, 1, 1);

                TraversedChunks_AC_Buffer.SetCounterValue(0);
                RbSimShader.Dispatch(1, RBVectorNum-1, 1, 1);

                if (TraversedChunksCount == 0)
                {
                    Debug.Log("TraversedChunksCount updated");
                    ComputeBuffer.CopyCount(TraversedChunks_AC_Buffer, TCCountBuffer, 0);
                    TCCountBuffer.GetData(TCCount);
                    TraversedChunksCount = (int)Math.Ceiling(TCCount[0] * (1+ChunkStorageSafety));
                }
                // ComputeBuffer.CopyCount(TraversedChunks_AC_Buffer, TCCountBuffer, 0);
                // TCCountBuffer.GetData(TCCount);
                // TraversedChunksCount = (int)Math.Ceiling(TCCount[0] * (1+ChunkStorageSafety));

                if (DoCalcStickyRequests) {
                    StickynessReqs_AC_Buffer.SetCounterValue(0);
                }
                RbSimShader.Dispatch(2, TraversedChunksCount, 1, 1);
                RbSimShader.Dispatch(3, RBodiesNum, 1, 1);
        }
    }

    void RunMarchingSquaresShader()
    {
        UpdateMarchingSquaresShaderVariables();

        VerticesBuffer.SetData(vertices);
        TrianglesBuffer.SetData(triangles);

        MarchingSquaresShader.Dispatch(0, MarchW, MarchH, 1);
        MarchingSquaresShader.Dispatch(1, MarchW-1, MarchH-1, 1);

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

        RenderShader.SetTexture(0, "Result", renderTexture);
        if (ParticlesNum != 0) {RenderShader.Dispatch(0, renderTexture.width / ThreadSize, renderTexture.height / ThreadSize, 1);}
        // Render rigid bodies - not implemented
        // if (RBodiesNum != 0) {PSimShader.Dispatch(1, RBodiesNum, 1, 1);}
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
        SpringPairsBuffer?.Release();

        PDataBuffer?.Release();
        PTypesBuffer?.Release();

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

struct SpringStruct
{
    public int linkedIndex;
    public float restLength;
    // public float yieldLen;
    // public float plasticity;
    // public float stiffness;
};
struct StickynessRequestStruct
{
    public int pIndex;
    public int StickyLineIndex;
    public float2 StickyLineDst;
    public float absDstToLineSqr;
    public float RBStickyness;
    public float RBStickynessRange;
};
struct PDataStruct
{
    public float2 PredPosition;
    public float2 Position;
    public float2 Velocity;
    public float2 LastVelocity;
    public float Density;
    public float NearDensity;
    public int PType;
}
struct PTypeStruct
{
    public float TargetDensity;
    public int MaxInfluenceRadius;
    public float Pressure;
    public float NearPressure;
    public float Damping;
    public float Viscocity;
    public float Elasticity;
    public float Plasticity;
    public float Stickyness;
    public float Gravity;
    public float colorG;
};
struct RBDataStruct
{
    public float2 Position;
    public float2 Velocity;
    // radians / second
    public float AngularImpulse;

    public float Stickyness;
    public float StickynessRange;
    public float StickynessRangeSqr;
    public float2 NextPos;
    public float2 NextVel;
    public float NextAngImpulse;
    public float Mass;
    public int2 LineIndices;
    public float MaxDstSqr;
    public int WallCollision;
    public int Stationary; // 1 -> Stationary, 0 -> Non-stationary
};
struct RBVectorStruct
{
    public float2 Position;
    public float2 LocalPosition;
    public float3 ParentImpulse;
    public int ParentRBIndex;
    public int WallCollision;
};