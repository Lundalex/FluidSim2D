using UnityEngine;
using Unity.Mathematics;
using System;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

// Import utils from Resources.cs
using Resources;
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

    [Header("Render settings")]
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
    public float InteractionTemperaturePower;

    [Header("References")]
    public ShaderHelper shaderHelper;
    public ComputeShader renderShader;
    public ComputeShader pSimShader;
    public ComputeShader rbSimShader;
    public ComputeShader sortShader;
    public ComputeShader marchingSquaresShader;

    // ThreadSize settings for compute shaders
    [NonSerialized] public int renderShaderThreadSize = 32; // /32, AxA thread groups
    [NonSerialized] public int pSimShaderThreadSize = 512; // /1024
    [NonSerialized] public int rbSimShaderThreadSize = 32; // /1024
    [NonSerialized] public int sortShaderThreadSize = 512; // /1024
    [NonSerialized] public int marchingSquaresShaderThreadSize = 512; // /1024

    // Marching Squares - Buffers
    public ComputeBuffer VerticesBuffer;
    public ComputeBuffer TrianglesBuffer;
    public ComputeBuffer ColorsBuffer;
    public ComputeBuffer MSPointsBuffer;

    // Bitonic mergesort
    public ComputeBuffer SpatialLookupBuffer;
    public ComputeBuffer StartIndicesBuffer;

    // Inter-particle springs
    public ComputeBuffer SpringCapacitiesBuffer;
    private bool FrameBufferCycle = true;
    public ComputeBuffer SpringStartIndicesBuffer_dbA; // Result A
    public ComputeBuffer SpringStartIndicesBuffer_dbB; // Result B
    public ComputeBuffer SpringStartIndicesBuffer_dbC; // Support
    public ComputeBuffer ParticleSpringsCombinedBuffer; // [[Last frame springs], [New frame springs]]

    // Particle data
    public ComputeBuffer PDataBuffer;
    public ComputeBuffer PTypesBuffer;

    // Rigid bodies
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
    [NonSerialized] public int2 ChunksNum;
    [NonSerialized] public int ChunksNumAll;
    [NonSerialized] public int ChunksNumAllNextPow2;
    [NonSerialized] public int MSLen;
    [NonSerialized] public int TraversedChunksCount;
    [NonSerialized] public int ParticleSpringsCombinedHalfLength;
    [NonSerialized] public int ParticlesNum_NextPow2;
    [NonSerialized] public int ParticlesNum_NextLog2;

    // Private references
    private RenderTexture renderTexture;
    private Mesh marchingSquaresMesh;

    // Particle data
    private PDataStruct[] PData;
    private PTypeStruct[] PTypes;

    // Rigid Bodies - Properties
    public RBVectorStruct[] RBVector;
    public RBDataStruct[] RBData;
    private int TCCount;

    // Marching Squares - Buffer retrieval
    private Vector3[] vertices;
    private int[] triangles;

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

        renderTexture = TextureHelper.CreateTexture(new int2(ResolutionX, ResolutionY), 3);

        renderShader.SetTexture(0, "Result", renderTexture);

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

        rbSimShader.SetInt("DoCalcStickyRequests", DoCalcStickyRequests ? 1 : 0);

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
        return FixedTimeStep
        ? TimeStep / TimeStepsPerRender
        : Time.deltaTime * ProgramSpeed / TimeStepsPerRender;
    }

    void SetConstants()
    {
        MaxInfluenceRadiusSqr = MaxInfluenceRadius * MaxInfluenceRadius;
        InvMaxInfluenceRadius = 1.0f / MaxInfluenceRadius;
        ChunksNum.x = Width / MaxInfluenceRadius;
        ChunksNum.y = Height / MaxInfluenceRadius;
        ChunksNumAll = ChunksNum.x * ChunksNum.y;
        MarchW = Width / MSResolution;
        MarchH = Height / MSResolution;
        MSLen = MarchW * MarchH * TriStorageLength * 3;
        ParticleSpringsCombinedHalfLength = ParticlesNum * SpringCapacitySafety / 2;
        ParticlesNum_NextPow2 = Func.NextPow2(ParticlesNum);

        for (int i = 0; i < RBData.Length; i++)
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
            FluidSpringsGroup = 1,

            SpringPlasticity = 0,
            SpringTolDeformation = 0.1f,
            SpringStiffness = 2000,

            ThermalConductivity = 1.0f,
            SpecificHeatCapacity = 10.0f,
            FreezeThreshold = Utils.CelciusToKelvin(0.0f),
            VaporizeThreshold = Utils.CelciusToKelvin(100.0f),

            Pressure = 3000,
            NearPressure = 5,

            Mass = 1,
            TargetDensity = TargetDensity,
            Damping = Damping,
            PassiveDamping = 0.0f,
            Viscosity = 5.0f,
            Stickyness = 2.0f,
            Gravity = Gravity,

            InfluenceRadius = 2,
            colorG = 0.5f
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

            Mass = 1,
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
            FluidSpringsGroup = 0,

            SpringPlasticity = -1,
            SpringTolDeformation = -1,
            SpringStiffness = -1,

            ThermalConductivity = 3.0f,
            SpecificHeatCapacity = 10.0f,
            FreezeThreshold = Utils.CelciusToKelvin(0.0f),
            VaporizeThreshold = Utils.CelciusToKelvin(100.0f),

            Pressure = 200,
            NearPressure = 0,

            Mass = 0.1f,
            TargetDensity = 0,
            Damping = Damping,
            PassiveDamping = PassiveDamping,
            Viscosity = Viscosity,
            Stickyness = 2.0f,
            Gravity = Gravity * 0.1f,

            InfluenceRadius = IR_1,
            colorG = 0.3f
        };

        PTypes[3] = new PTypeStruct // Solid
        {
            FluidSpringsGroup = FSG_2,

            SpringPlasticity = Plasticity,
            SpringTolDeformation = TolDeformation,
            SpringStiffness = SpringStiffness,

            ThermalConductivity = 7.0f,
            SpecificHeatCapacity = 15.0f,
            FreezeThreshold = Utils.CelciusToKelvin(999.0f),
            VaporizeThreshold = Utils.CelciusToKelvin(-999.0f),

            Pressure = PressureMultiplier,
            NearPressure = NearPressureMultiplier,

            Mass = 1,
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

            ThermalConductivity = 7.0f,
            SpecificHeatCapacity = 15.0f,
            FreezeThreshold = Utils.CelciusToKelvin(-999.0f),
            VaporizeThreshold = Utils.CelciusToKelvin(999.0f),

            Pressure = PressureMultiplier,
            NearPressure = NearPressureMultiplier,

            Mass = 1,
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

            ThermalConductivity = 7.0f,
            SpecificHeatCapacity = 15.0f,
            FreezeThreshold = Utils.CelciusToKelvin(-999.0f),
            VaporizeThreshold = Utils.CelciusToKelvin(999.0f),

            Pressure = PressureMultiplier,
            NearPressure = NearPressureMultiplier,

            Mass = 1,
            TargetDensity = TargetDensity * 1.5f,
            Damping = Damping,
            PassiveDamping = PassiveDamping,
            Viscosity = Viscosity,
            Stickyness = 4.0f,
            Gravity = Gravity,

            InfluenceRadius = IR_2,
            colorG = 0.9f
        };
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
    }

    void InitializeArrays()
    {
        PData = new PDataStruct[ParticlesNum];

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
                    LastChunkKey_PType_POrder = 1 * ChunksNumAll // flattened equivelant to PType = 1
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
                    LastChunkKey_PType_POrder = (3 + 1) * ChunksNumAll // flattened equivelant to PType = 3+1
                };
            }
        }
    }

    void InitializeBuffers()
    {
        ComputeHelper.CreateStructuredBuffer<PDataStruct>(ref PDataBuffer, PData);
        ComputeHelper.CreateStructuredBuffer<PTypeStruct>(ref PTypesBuffer, PTypes);

        ComputeHelper.CreateStructuredBuffer<int2>(ref SpatialLookupBuffer, ParticlesNum_NextPow2);
        ComputeHelper.CreateStructuredBuffer<int>(ref StartIndicesBuffer, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int2>(ref SpringCapacitiesBuffer, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int>(ref SpringStartIndicesBuffer_dbA, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int>(ref SpringStartIndicesBuffer_dbB, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int>(ref SpringStartIndicesBuffer_dbC, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<SpringStruct>(ref ParticleSpringsCombinedBuffer, ParticlesNum * SpringCapacitySafety);

        ComputeHelper.CreateStructuredBuffer<int3>(ref VerticesBuffer, MSLen);
        ComputeHelper.CreateStructuredBuffer<int>(ref TrianglesBuffer, MSLen);
        ComputeHelper.CreateStructuredBuffer<float>(ref MSPointsBuffer, MSLen);
        ComputeHelper.CreateStructuredBuffer<float4>(ref ColorsBuffer, MSLen); // float4 for RGBA

        ComputeHelper.CreateStructuredBuffer<RBDataStruct>(ref RBDataBuffer, RBData);
        ComputeHelper.CreateStructuredBuffer<RBVectorStruct>(ref RBVectorBuffer, RBVector);


        ComputeHelper.CreateCountBuffer(ref TCCountBuffer);
        ComputeHelper.CreateCountBuffer(ref SRCountBuffer);

        ComputeHelper.CreateAppendBuffer<int3>(ref TraversedChunks_AC_Buffer, 4096);

        ComputeHelper.CreateStructuredBuffer<StickynessRequestStruct>(ref SortedStickyRequestsBuffer, 4096);
        ComputeHelper.CreateAppendBuffer<StickynessRequestStruct>(ref StickynessReqs_AC_Buffer, 4096);
        ComputeHelper.CreateAppendBuffer<StickynessRequestStruct>(ref StickyRequestsResult_AC_Buffer, 4096);
    }

    void GPUSortChunkLookUp()
    {
        int threadGroupsNum = Utils.GetThreadGroupsNums(ParticlesNum_NextPow2, sortShaderThreadSize);
        int threadGroupsNumHalfCeil = (int)Math.Ceiling(threadGroupsNum * 0.5f);

        ComputeHelper.DispatchKernel (sortShader, "CalculateChunkKeys", threadGroupsNum);

        int len = ParticlesNum_NextPow2;

        int basebBlockLen = 2;
        while (basebBlockLen != 2*len) // basebBlockLen == len is the last outer iteration
        {
            int blockLen = basebBlockLen;
            while (blockLen != 1) // blockLen == 2 is the last inner iteration
            {
                bool BrownPinkSort = blockLen == basebBlockLen;

                sortShader.SetInt("BlockLen", blockLen);
                sortShader.SetBool("BrownPinkSort", BrownPinkSort);

                ComputeHelper.DispatchKernel (sortShader, "SortIteration", threadGroupsNumHalfCeil);

                blockLen /= 2;
            }
            basebBlockLen *= 2;
        }

        ComputeHelper.DispatchKernel (sortShader, "PopulateStartIndices", threadGroupsNum);
    }

    void GPUSortSpringLookUp()
    {
        // Spring buffer kernels
        int threadGroupsNum = Utils.GetThreadGroupsNums(ChunksNumAll, sortShaderThreadSize);

        ComputeHelper.DispatchKernel (sortShader, "PopulateChunkSizes", threadGroupsNum);
        ComputeHelper.DispatchKernel (sortShader, "PopulateSpringCapacities", threadGroupsNum);
        ComputeHelper.DispatchKernel (sortShader, "CopySpringCapacities", threadGroupsNum);

        // Calculate prefix sums (SpringStartIndices)
        bool StepBufferCycle = false;
        for (int offset = 1; offset < ChunksNumAll; offset *= 2)
        {
            StepBufferCycle = !StepBufferCycle;

            sortShader.SetBool("StepBufferCycle", StepBufferCycle);
            sortShader.SetInt("Offset", offset);

            ComputeHelper.DispatchKernel (sortShader, "ParallelPrefixSumScan", threadGroupsNum);
        }

        if (StepBufferCycle == true) { ComputeHelper.DispatchKernel (sortShader, "CopySpringStartIndicesBuffer", threadGroupsNum); } // copy to result buffer if necessary
    }

    void GPUSortStickynessRequests()
    {
        int StickyRequestsCount = Func.NextPow2(4096);
        if (StickyRequestsCount == 0) {return;}
        
        int threadGroupsNum = Utils.GetThreadGroupsNums(StickyRequestsCount, 512);
        int threadGroupsNumHalfCeil = Mathf.CeilToInt(threadGroupsNum * 0.5f);

        ComputeHelper.DispatchKernel (sortShader, "PopulateSortedStickyRequests", threadGroupsNum);

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

                ComputeHelper.DispatchKernel (sortShader, "SRSortIteration", threadGroupsNumHalfCeil);

                blockLen /= 2;
            }
            basebBlockLen *= 2;
        }
    }

    void RunPSimShader(int step)
    {
        ComputeHelper.DispatchKernel (pSimShader, "PreCalculations", ParticlesNum, pSimShaderThreadSize);
        ComputeHelper.DispatchKernel (pSimShader, "CalculateDensities", ParticlesNum, pSimShaderThreadSize);

        if (step == 0)
        {
            ComputeHelper.DispatchKernel (pSimShader, "PrepSpringData", ParticleSpringsCombinedHalfLength, pSimShaderThreadSize);
            ComputeHelper.DispatchKernel (pSimShader, "TransferAllSpringData", ParticleSpringsCombinedHalfLength, pSimShaderThreadSize);
        }

        ComputeHelper.DispatchKernel (pSimShader, "ParticleForces", ParticlesNum, pSimShaderThreadSize);
    }

    void RunRbSimShader()
    {
        if (RBVector.Length > 1) 
        {
            ComputeHelper.DispatchKernel (rbSimShader, "ApplyLocalAngularRotation", RBVector.Length, rbSimShaderThreadSize);

            TraversedChunks_AC_Buffer.SetCounterValue(0);

            ComputeHelper.DispatchKernel (rbSimShader, "PopulateTraversedChunks", RBVector.Length-1, rbSimShaderThreadSize);

            if (TraversedChunksCount == 0)
            {
                TCCount = ComputeHelper.GetAppendBufferCount(TraversedChunks_AC_Buffer, TCCountBuffer);
                TraversedChunksCount = (int)Math.Ceiling(TCCount * (1+StickynessCapacitySafety));
            }

            if (DoCalcStickyRequests) {
                StickynessReqs_AC_Buffer.SetCounterValue(0);
            }

            ComputeHelper.DispatchKernel (rbSimShader, "ResolveLineCollisions", TraversedChunksCount, rbSimShaderThreadSize);
            ComputeHelper.DispatchKernel (rbSimShader, "RBForces", RBVector.Length, rbSimShaderThreadSize);
        }
    }

    void RunMarchingSquaresShader()
    {
        VerticesBuffer.SetData(vertices);
        TrianglesBuffer.SetData(triangles);

        ComputeHelper.DispatchKernel (marchingSquaresShader, "CalculateGridValues", new int2(MarchW, MarchH), 1);
        ComputeHelper.DispatchKernel (marchingSquaresShader, "GenerateMeshData", new int2(MarchW-1, MarchH-1), 1);

        VerticesBuffer.GetData(vertices);
        TrianglesBuffer.GetData(triangles);

        marchingSquaresMesh.vertices = vertices;
        marchingSquaresMesh.triangles = triangles;

        marchingSquaresMesh.RecalculateNormals();
    }

    void RunRenderShader()
    {
        ComputeHelper.DispatchKernel (renderShader, "Render2D", new int2(renderTexture.width, renderTexture.height), renderShaderThreadSize);
    }

    public void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (RenderMarchingSquares)
        {
            Graphics.Blit(src, dest);
        }
        else
        {
            RunRenderShader();

            Graphics.Blit(renderTexture, dest);
        }
    }

    void OnDestroy()
    {
        ComputeHelper.Release(
            SpatialLookupBuffer, 
            StartIndicesBuffer, 
            VerticesBuffer,
            TrianglesBuffer,
            ColorsBuffer,
            MSPointsBuffer,
            PDataBuffer,
            PTypesBuffer,
            SpringCapacitiesBuffer,
            SpringStartIndicesBuffer_dbA,
            SpringStartIndicesBuffer_dbB,
            SpringStartIndicesBuffer_dbC,
            ParticleSpringsCombinedBuffer,
            RBDataBuffer,
            RBVectorBuffer,
            TraversedChunks_AC_Buffer,
            TCCountBuffer,
            SRCountBuffer,
            StickynessReqs_AC_Buffer,
            SortedStickyRequestsBuffer,
            StickyRequestsResult_AC_Buffer
        );
    }
}