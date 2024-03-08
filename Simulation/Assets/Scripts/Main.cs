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
    [Range(0, 3.0f)] public float PassiveDamping;
    [Range(0, 1)] public float RbElasticity;
    [Range(0, 0.1f)] public float LookAheadFactor;
    [Range(0, 5.0f)] public float StateThresholdPadding;
    public float Viscosity;
    public float SpringStiffness;
    public float TolDeformation;
    public float Plasticity;
    public float Gravity;
    public float RbPStickyRadius;
    public float RbPStickyness;
    [Range(0, 3)] public int MaxChunkSearchSafety;
    public float StickynessCapacitySafety; // Avg stickyness requests per particle should not exceed this value
    public int SpringCapacitySafety; // Avg springs per particle should not exceed this value
    public int TriStorageLength;

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

    // ThreadSize settings for compute shaders
    [NonSerialized] public int renderShaderThreadSize = 32; // /32, AxA thread groups
    [NonSerialized] public int pSimShaderThreadSize = 512; // /1024
    [NonSerialized] public int rbSimShaderThreadSize = 32; // /1024
    [NonSerialized] public int sortShaderThreadSize = 1024; // /1024
    [NonSerialized] public int marchingSquaresShaderThreadSize = 512; // /1024

    // Marching Squares - Buffers
    [NonSerialized]
    public ComputeBuffer VerticesBuffer;
    public ComputeBuffer TrianglesBuffer;
    public ComputeBuffer ColorsBuffer;
    public ComputeBuffer MSPointsBuffer;

    // Bitonic Mergesort - Buffers
    public ComputeBuffer SpatialLookupBuffer;
    public ComputeBuffer StartIndicesBuffer;

    // Inter-particle springs - Buffers
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
    [NonSerialized] public int MaxInfluenceRadiusSqr;
    [NonSerialized] public float InvMaxInfluenceRadius;
    [NonSerialized] public float MarchScale;
    [NonSerialized] public int ChunkNumW;
    [NonSerialized] public int ChunkNumH;
    [NonSerialized] public int ChunkNum;
    [NonSerialized] public int PTypesNum;
    [NonSerialized] public int ChunksNumLog2;
    [NonSerialized] public int ChunkNumNextPow2;
    [NonSerialized] public int IOOR; // Index Out Of Range
    [NonSerialized] public int MSLen;
    [NonSerialized] public int RBodiesNum;
    [NonSerialized] public int RBVectorNum;
    [NonSerialized] public int TraversedChunksCount;
    [NonSerialized] public int ParticleSpringsCombinedHalfLength;
    [NonSerialized] public int ParticlesNum_NextPow2;
    [NonSerialized] public int ParticlesNum_NextLog2;

    // Private references
    private RenderTexture renderTexture;
    private Mesh marchingSquaresMesh;

    // PData - Properties
    private int2[] SpatialLookup; // [](particleIndex, chunkKey)
    private int2[] TemplateSpatialLookup;
    private int[] StartIndices;
    private int2[] SpringCapacities; // [](baseChunkCapacity, neighboorChunksCapacity)
    private int[] SpringStartIndices;
    private SpringStruct[] ParticleSpringsCombined;
    private PDataStruct[] PData;
    private PTypeStruct[] PTypes;

    // Rigid Bodies - Properties
    private RBVectorStruct[] RBVector;
    private RBDataStruct[] RBData;
    private int[] TCCount = new int[1];

    // Marching Squares - Buffer retrieval
    private Vector3[] vertices;
    private int[] triangles;
    private Color[] colors;
    private float[] MSPoints;

    // Other
    private float DeltaTime;
    private const int CalcStickyRequestsFrequency = 3;
    private bool DoCalcStickyRequests = true;
    private bool ProgramStarted = false;

    void Start()
    {
        SceneSetup();

        InitializeSetArrays();
        SetConstants();
        InitializeArrays();

        for (int i = 0; i < ParticlesNum; i++) {
            PData[i].Position = Utils.GetParticleSpawnPosition(i, ParticlesNum, Width, Height, SpawnDims);
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

        // GPUSortSpringLookUp() have to be called in succession to GPUSortChunkLookUp()
        GPUSortChunkLookUp();
        GPUSortSpringLookUp();

        for (int i = 0; i < TimeStepsPerRender; i++)
        {
            pSimShader.SetBool("TransferSpringData", i == 0);

            RunPSimShader(i);

            // Stickyness requests
            if (i == 1) {
                DoCalcStickyRequests = true;
                rbSimShader.SetInt("DoCalcStickyRequests", 1);
                GPUSortStickynessRequests(); 
                int ThreadNums = Utils.GetThreadGroupsNums(4096, 512);
                pSimShader.Dispatch(6, ThreadNums, 1, 1);
            }
            else {
                DoCalcStickyRequests = false;
                rbSimShader.SetInt("DoCalcStickyRequests", 0);
            }

            RunRbSimShader();

            int ThreadNums2 = Utils.GetThreadGroupsNums(ParticlesNum, pSimShaderThreadSize);
            if (ParticlesNum != 0) {pSimShader.Dispatch(5, ThreadNums2, 1, 1);}
            
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
            SetConstants();
            UpdateSettings();
        }
    }

    // Doesn't work
    public void UpdateSettings()
    {
        SetPTypesData();
        PTypesBuffer.SetData(PTypes);

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
        ChunksNumLog2 = Func.Log2(ChunkNum, true);
        ChunkNumNextPow2 = (int)Math.Pow(2, ChunksNumLog2);
        IOOR = ParticlesNum;
        MarchW = Width / MSResolution;
        MarchH = Height / MSResolution;
        MSLen = MarchW * MarchH * TriStorageLength * 3;
        RBVectorNum = RBVector.Length;
        ParticleSpringsCombinedHalfLength = ParticlesNum * SpringCapacitySafety / 2;
        ParticlesNum_NextPow2 = 1;
        while (ParticlesNum_NextPow2 < ParticlesNum)
        {
            ParticlesNum_NextPow2 *= 2;
        }
        ParticlesNum_NextLog2 = Func.Log2(ParticlesNum_NextPow2);

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

    void SetPTypesData()
    {
        PTypes = new PTypeStruct[6];
        float IR_1 = 2.0f;
        float IR_2 = 2.0f;
        int FSG_1 = 1;
        int FSG_2 = 2;
        PTypes[0] = new PTypeStruct // Solid
        {
            FluidSpringsGroup = FSG_1,

            SpringPlasticity = Plasticity,
            SpringTolDeformation = TolDeformation,
            SpringStiffness = SpringStiffness,

            ThermalConductivity = 1.0f,
            SpecificHeatCapacity = 10.0f,
            FreezeThreshold = Utils.CelciusToKelvin(0.0f),
            VaporizeThreshold = Utils.CelciusToKelvin(100.0f),

            Pressure = PressureMultiplier,
            NearPressure = NearPressureMultiplier,

            TargetDensity = TargetDensity,
            Damping = Damping,
            PassiveDamping = PassiveDamping,
            Viscosity = Viscosity,
            Stickyness = 2.0f,
            Gravity = Gravity,

            InfluenceRadius = IR_1,
            colorG = 0.1f
        };
        PTypes[1] = new PTypeStruct // Liquid
        {
            FluidSpringsGroup = FSG_1,

            SpringPlasticity = Plasticity,
            SpringTolDeformation = TolDeformation,
            SpringStiffness = SpringStiffness,

            ThermalConductivity = 1.0f,
            SpecificHeatCapacity = 10.0f,
            FreezeThreshold = Utils.CelciusToKelvin(0.0f),
            VaporizeThreshold = Utils.CelciusToKelvin(100.0f),

            Pressure = PressureMultiplier,
            NearPressure = NearPressureMultiplier,

            TargetDensity = TargetDensity,
            Damping = Damping,
            PassiveDamping = PassiveDamping,
            Viscosity = Viscosity,
            Stickyness = 2.0f,
            Gravity = Gravity,

            InfluenceRadius = IR_1,
            colorG = 0.0f
        };
        PTypes[2] = new PTypeStruct // Gas
        {
            FluidSpringsGroup = FSG_1,

            SpringPlasticity = Plasticity,
            SpringTolDeformation = TolDeformation,
            SpringStiffness = SpringStiffness,

            ThermalConductivity = 1.0f,
            SpecificHeatCapacity = 10.0f,
            FreezeThreshold = Utils.CelciusToKelvin(0.0f),
            VaporizeThreshold = Utils.CelciusToKelvin(100.0f),

            Pressure = PressureMultiplier,
            NearPressure = NearPressureMultiplier,

            TargetDensity = TargetDensity,
            Damping = Damping,
            PassiveDamping = PassiveDamping,
            Viscosity = Viscosity,
            Stickyness = 2.0f,
            Gravity = Gravity,

            InfluenceRadius = IR_1,
            colorG = 0.1f
        };

        PTypes[3] = new PTypeStruct // Solid
        {
            FluidSpringsGroup = FSG_2,

            SpringPlasticity = Plasticity,
            SpringTolDeformation = TolDeformation,
            SpringStiffness = SpringStiffness,

            ThermalConductivity = 1.0f,
            SpecificHeatCapacity = 10.0f,
            FreezeThreshold = Utils.CelciusToKelvin(999.0f),
            VaporizeThreshold = Utils.CelciusToKelvin(-999.0f),

            Pressure = PressureMultiplier,
            NearPressure = NearPressureMultiplier,

            TargetDensity = TargetDensity * 1.5f,
            Damping = Damping,
            PassiveDamping = PassiveDamping,
            Viscosity = Viscosity,
            Stickyness = 4.0f,
            Gravity = Gravity,

            InfluenceRadius = IR_2,
            colorG = 0.9f
        };
        PTypes[4] = new PTypeStruct // Liquid
        {
            FluidSpringsGroup = FSG_2,

            SpringPlasticity = Plasticity,
            SpringTolDeformation = TolDeformation,
            SpringStiffness = SpringStiffness,

            ThermalConductivity = 1.0f,
            SpecificHeatCapacity = 10.0f,
            FreezeThreshold = Utils.CelciusToKelvin(-999.0f),
            VaporizeThreshold = Utils.CelciusToKelvin(999.0f),

            Pressure = PressureMultiplier,
            NearPressure = NearPressureMultiplier,

            TargetDensity = TargetDensity * 1.5f,
            Damping = Damping,
            PassiveDamping = PassiveDamping,
            Viscosity = Viscosity,
            Stickyness = 4.0f,
            Gravity = Gravity,

            InfluenceRadius = IR_2,
            colorG = 1.0f
        };
        PTypes[5] = new PTypeStruct // Gas
        {
            FluidSpringsGroup = FSG_2,

            SpringPlasticity = Plasticity,
            SpringTolDeformation = TolDeformation,
            SpringStiffness = SpringStiffness,

            ThermalConductivity = 1.0f,
            SpecificHeatCapacity = 10.0f,
            FreezeThreshold = Utils.CelciusToKelvin(-999.0f),
            VaporizeThreshold = Utils.CelciusToKelvin(999.0f),

            Pressure = PressureMultiplier,
            NearPressure = NearPressureMultiplier,

            TargetDensity = TargetDensity * 1.5f,
            Damping = Damping,
            PassiveDamping = PassiveDamping,
            Viscosity = Viscosity,
            Stickyness = 4.0f,
            Gravity = Gravity,

            InfluenceRadius = IR_2,
            colorG = 0.9f
        };
        PTypesNum = PTypes.Length;
    }

    void InitializeSetArrays()
    {
        SetPTypesData();

        RBData = new RBDataStruct[2];
        RBData[0] = new RBDataStruct
        {
            Position = new float2(140f, 100f),
            Velocity = new float2(0.0f, 0.0f),
            NextPos = new float2(140f, 100f),
            NextVel = new float2(0.0f, 0.0f),
            NextAngImpulse = 0f,
            AngularImpulse = 0.0f,
            Stickyness = 6f,
            StickynessRange = 6f,
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
        SpatialLookup = new int2[ParticlesNum_NextPow2];
        StartIndices = new int[ChunkNum];
        SpringCapacities = new int2[ChunkNum];
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
            if (i < ParticlesNum * 0.5f)
            {
                PData[i] = new PDataStruct
                {
                    PredPosition = new float2(0.0f, 0.0f),
                    Position = new float2(0.0f, 0.0f),
                    Velocity = new float2(0.0f, 0.0f),
                    LastVelocity = new float2(0.0f, 0.0f),
                    Density = 0.0f,
                    NearDensity = 0.0f,
                    Temperature = Utils.CelciusToKelvin(20.0f),
                    TemperatureExchangeBuffer = 0.0f,
                    LastChunkKey_PType_POrder = 1 * ChunkNum // flattened equivelant to PType = 1
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
                    Temperature = Utils.CelciusToKelvin(80.0f),
                    TemperatureExchangeBuffer = 0.0f,
                    LastChunkKey_PType_POrder = (3 + 1) * ChunkNum // flattened equivelant to PType = 3+1
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
            
            SpringCapacities[i] = new int2(0, 0);
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
            PDataBuffer = new ComputeBuffer(ParticlesNum, sizeof(float) * 12 + sizeof(int) * 1);
            PTypesBuffer = new ComputeBuffer(PTypes.Length, sizeof(float) * 17 + sizeof(int) * 1);

            PDataBuffer.SetData(PData);
            PTypesBuffer.SetData(PTypes);
        }

        SpatialLookupBuffer = new ComputeBuffer(ParticlesNum_NextPow2, sizeof(int) * 2);
        StartIndicesBuffer = new ComputeBuffer(ChunkNum, sizeof(int));
        SpringCapacitiesBuffer = new ComputeBuffer(ChunkNum, sizeof(int) * 2);
        SpringStartIndicesBuffer_dbA = new ComputeBuffer(ChunkNum, sizeof(int));
        SpringStartIndicesBuffer_dbB = new ComputeBuffer(ChunkNum, sizeof(int));
        SpringStartIndicesBuffer_dbC = new ComputeBuffer(ChunkNum, sizeof(int));
        ParticleSpringsCombinedBuffer = new ComputeBuffer(ParticlesNum * SpringCapacitySafety, sizeof(float) + sizeof(int) * 2);
        
        SpatialLookupBuffer.SetData(SpatialLookup);
        StartIndicesBuffer.SetData(StartIndices);
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
        int ThreadNums = Utils.GetThreadGroupsNums(StickyRequestsCount, 512);
        int ThreadSizeHLen = (int)((float)ThreadNums/2);

        sortShader.Dispatch(9, ThreadNums, 1, 1);

        int len = StickyRequestsCount;
        int lenLog2 = Func.Log2(len);
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
        int ThreadNums = Utils.GetThreadGroupsNums(ParticlesNum_NextPow2, sortShaderThreadSize);
        int ThreadSizeHLen = (int)Math.Ceiling(ThreadNums * 0.5f);

        sortShader.Dispatch(0, ThreadNums, 1, 1);

        int len = ParticlesNum_NextPow2;
        int lenLog2 = ParticlesNum_NextLog2;
        sortShader.SetInt("SortedSpatialLookupLength", len);
        sortShader.SetInt("SortedSpatialLookupLog2Length", lenLog2);

        int basebBlockLen = 2;
        while (basebBlockLen != 2*len) // basebBlockLen = len is the last outer iteration
        {
            int blockLen = basebBlockLen;
            while (blockLen != 1) // BlockLen = 2 is the last inner iteration
            {
                bool BrownPinkSort = blockLen == basebBlockLen;

                sortShader.SetInt("BlockLen", blockLen);
                sortShader.SetBool("BrownPinkSort", BrownPinkSort);

                sortShader.Dispatch(1, ThreadSizeHLen, 1, 1);

                blockLen /= 2;
            }
            basebBlockLen *= 2;
        }

        sortShader.Dispatch(3, ThreadNums, 1, 1);
    }

    void GPUSortSpringLookUp()
    {
        // Spring buffer kernels
        int ThreadSizeChunkSizes = Utils.GetThreadGroupsNums(ChunkNum, sortShaderThreadSize);
        sortShader.Dispatch(4, ThreadSizeChunkSizes, 1, 1); // Set ChunkSizes
        sortShader.Dispatch(5, ThreadSizeChunkSizes, 1, 1); // Set SpringCapacities
        sortShader.Dispatch(6, ThreadSizeChunkSizes, 1, 1); // Copy SpringCapacities to double buffers

        // Calculate prefix sums (SpringStartIndices)
        bool StepBufferCycle = false;
        for (int offset = 1; offset < SpringStartIndices.Length; offset *= 2)
        {
            StepBufferCycle = !StepBufferCycle;

            sortShader.SetBool("StepBufferCycle", StepBufferCycle);
            sortShader.SetInt("Offset", offset);

            sortShader.Dispatch(7, ThreadSizeChunkSizes, 1, 1);
        }
        if (StepBufferCycle == true) { sortShader.Dispatch(8, ThreadSizeChunkSizes, 1, 1); } // copy to result buffer if necessary
    }

    void RunPSimShader(int step)
    {
        int ThreadNums = Utils.GetThreadGroupsNums(ParticlesNum, pSimShaderThreadSize);

        if (ParticlesNum != 0) {pSimShader.Dispatch(0, ThreadNums, 1, 1);}
        if (ParticlesNum != 0) {pSimShader.Dispatch(1, ThreadNums, 1, 1);} // CalculateDensities

        // Particle springs
        if (step == 0)
        {
            int ThreadSize2 = Utils.GetThreadGroupsNums(ParticleSpringsCombinedHalfLength, pSimShaderThreadSize);
            // Transfer spring data kernel
            if (ParticlesNum != 0) {pSimShader.Dispatch(2, ThreadSize2, 1, 1);}
            if (ParticlesNum != 0) {pSimShader.Dispatch(3, ThreadSize2, 1, 1);}
        }

        if (ParticlesNum != 0) {pSimShader.Dispatch(4, ThreadNums, 1, 1);} // ParticleForces
    }

    void RunRbSimShader()
    {
        if (RBVectorNum > 1 && ParticlesNum != 0) 
        {
            int ThreadNums_A = Utils.GetThreadGroupsNums(RBVectorNum, rbSimShaderThreadSize);
            int ThreadNums_B = Utils.GetThreadGroupsNums(RBVectorNum-1, rbSimShaderThreadSize);

            rbSimShader.Dispatch(0, ThreadNums_A, 1, 1);

            TraversedChunks_AC_Buffer.SetCounterValue(0);
            rbSimShader.Dispatch(1, ThreadNums_B, 1, 1);

            if (TraversedChunksCount == 0)
            {
                Debug.Log("TraversedChunksCount updated");
                ComputeBuffer.CopyCount(TraversedChunks_AC_Buffer, TCCountBuffer, 0);
                TCCountBuffer.GetData(TCCount);
                TraversedChunksCount = (int)Math.Ceiling(TCCount[0] * (1+StickynessCapacitySafety));
            }

            int ThreadNums_C = Utils.GetThreadGroupsNums(TraversedChunksCount, rbSimShaderThreadSize);

            if (DoCalcStickyRequests) {
                StickynessReqs_AC_Buffer.SetCounterValue(0);
            }
            rbSimShader.Dispatch(2, ThreadNums_C, 1, 1);
            rbSimShader.Dispatch(3, ThreadNums_A, 1, 1);
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
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(ResolutionX, ResolutionY, 24)
            {
                enableRandomWrite = true
            };
            renderTexture.Create();
        }

        renderShader.SetTexture(0, "Result", renderTexture);
        if (ParticlesNum != 0) {renderShader.Dispatch(0, renderTexture.width / renderShaderThreadSize, renderTexture.height / renderShaderThreadSize, 1);}
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