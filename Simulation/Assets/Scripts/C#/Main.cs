using UnityEngine;
using Unity.Mathematics;
using System;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

// Import utils from Resources.cs
using Resources;
public class Main : MonoBehaviour
{
    [Header("Simulation")]
    public int MaxParticlesNum = 20000; 
    public int StartTraversedChunksCount = 80000;
    public int MaxInfluenceRadius = 2;
    public float targetDensity = 2;
    public float PressureMultiplier = 3000.0f;
    public float NearPressureMultiplier = 12.0f;
    [Range(0, 1)] public float damping = 0.7f;
    [Range(0, 3.0f)] public float passiveDamping = 0.0f;
    [Range(0, 1)] public float RbElasticity = 0.645f;
    [Range(0, 0.1f)] public float LookAheadFactor = 0.017f;
    [Range(0, 5.0f)] public float StateThresholdPadding = 3.0f;
    public float viscosity = 1.5f;
    public float springStiffness = 5.0f;
    public float TolDeformation = 0.0f;
    public float Plasticity = 3.0f;
    public float gravity = 5.0f;
    public float RbPStickyRadius = 2.0f;
    public float RbPStickyness = 1.0f;
    [Range(0, 3)] public int MaxChunkSearchSafety = 1;
    [Range(1, 1.5f)] public float StickynessCapacitySafety = 1.1f; // Avg stickyness requests per particle should not exceed this value
    public int SpringCapacitySafety = 150; // Avg springs per particle should not exceed this value
    public int TriStorageLength = 4;

    [Header("Boundary")]
    public int2 BoundaryDims = new(300, 200);
    public int SpawnDims = 160; // A x A
    public float BorderPadding = 4.0f;
    public float RigidBodyPadding = 2.0f;
    [Header("Collision Solver")]
    public float RB_RBCollisionCorrectionFactor = 0.8f;
    public float RB_RBCollisionSlop = 0.01f;

    [Header("Render")]
    public bool FixedTimeStep = true;
    public bool RenderMarchingSquares = false;
    public float TimeStep = 0.02f;
    public float ProgramSpeed = 2.0f;
    public float VisualParticleRadii = 0.4f;
    public float RBRenderThickness = 0.5f;
    public int TimeStepsPerFrame = 3;
    public int SubTimeStepsPerFrame = 3;
    public float MSvalMin = 0.41f;
    public int2 Resolution = new(1920, 1280);
    public Color BackgroundColor;
    public bool DoDrawRBCentroids = false;

    [Header("Interaction - Particles")]
    public float MaxInteractionRadius = 40.0f;
    public float InteractionAttractionPower = 3.5f;
    public float InteractionFountainPower = 1.0f;
    public float InteractionTemperaturePower = 1.0f;
    [Header("Interaction - Rigid Bodies")]
    public float RB_MaxInteractionRadius = 40.0f;
    public float RB_InteractionAttractionPower = 3.5f;

    [Header("References")]
    public SceneManager sceneManager;
    public ShaderHelper shaderHelper;
    public ComputeShader renderShader;
    public ComputeShader pSimShader;
    public ComputeShader rbSimShader;
    public ComputeShader sortShader;

    // ThreadSize settings for compute shaders
    [NonSerialized] public int renderShaderThreadSize = 32; // /32, AxA thread groups
    [NonSerialized] public int pSimShaderThreadSize = 512; // /1024
    [NonSerialized] public int sortShaderThreadSize = 512; // /1024
    [NonSerialized] public int marchingSquaresShaderThreadSize = 32; // /32
    [NonSerialized] public int rbSimShaderThreadSize1 = 64; // Rigid Body Simulation
    [NonSerialized] public int rbSimShaderThreadSize2 = 32; // Rigid Body Simulation
    [NonSerialized] public int rbSimShaderThreadSize3 = 512; // Rigid Body Simulation

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
    public ComputeBuffer PTypeBuffer;

    // Rigid bodies
    public ComputeBuffer RBVectorBuffer;
    public ComputeBuffer RBDataBuffer;
    public ComputeBuffer RBAdjustmentBuffer;
    public ComputeBuffer TraversedChunks_AC_Buffer;
    public ComputeBuffer StickynessReqs_AC_Buffer;
    public ComputeBuffer SortedStickyRequestsBuffer;
    public ComputeBuffer StickyRequestsResult_AC_Buffer;
    public ComputeBuffer TCCountBuffer;
    public ComputeBuffer SRCountBuffer;

    // Constants
    [NonSerialized] public int ParticlesNum;
    [NonSerialized] public int MaxInfluenceRadiusSqr;
    [NonSerialized] public float InvMaxInfluenceRadius;
    [NonSerialized] public float MarchScale;
    [NonSerialized] public int2 ChunksNum;
    [NonSerialized] public int ChunksNumAll;
    [NonSerialized] public int ChunksNumAllNextPow2;
    [NonSerialized] public int TraversedChunksCount;
    [NonSerialized] public int ParticleSpringsCombinedHalfLength;
    [NonSerialized] public int ParticlesNum_NextPow2;
    [NonSerialized] public int ParticlesNum_NextLog2;

    // Private references
    private RenderTexture renderTexture;

    // Particle data
    private PData[] PDatas;
    private PType[] PTypes;

    // Rigid Bodies - Properties
    public RBVector[] RBVectors;
    public RBData[] RBDatas;

    // Other
    private float DeltaTime;
    private const int CalcStickyRequestsFrequency = 3;
    private bool ProgramStarted = false;
    private int FrameCount = 0;
    private bool ProgramPaused = false;
    private bool FrameStep = false;

    void Start()
    {
        SceneSetup();

        ChunksNum = (int2)BoundaryDims / MaxInfluenceRadius;
        ChunksNumAll = ChunksNum.x * ChunksNum.y;

        PDatas = sceneManager.GenerateParticles(MaxParticlesNum);
        ParticlesNum = PDatas.Length;

        BoundaryDims = sceneManager.GetBounds(MaxInfluenceRadius);

        SetPTypesData();
        (RBDatas, RBVectors) = sceneManager.GenerateRigidBodies();

        SetConstants();

        TraversedChunksCount = StartTraversedChunksCount;

        InitializeBuffers();
        shaderHelper.SetPSimShaderBuffers(pSimShader);
        shaderHelper.SetNewRBSimShaderBuffers(rbSimShader);
        // shaderHelper.SetRbSimShaderBuffers(oldRbSimShader);
        shaderHelper.SetRenderShaderBuffers(renderShader);
        shaderHelper.SetSortShaderBuffers(sortShader);

        shaderHelper.UpdatePSimShaderVariables(pSimShader);
        shaderHelper.UpdateNewRBSimShaderVariables(rbSimShader);
        // shaderHelper.UpdateRbSimShaderVariables(oldRbSimShader);
        shaderHelper.UpdateRenderShaderVariables(renderShader);
        shaderHelper.UpdateSortShaderVariables(sortShader);

        renderTexture = TextureHelper.CreateTexture(Resolution, 3);

        renderShader.SetTexture(0, "Result", renderTexture);

        Debug.Log("Simulation started with " + ParticlesNum + " particles");
        ProgramStarted = true;
    }

    void Update()
    {
        PauseControls();

        bool simulateThisFrame = false;
        if (!ProgramPaused || FrameStep) simulateThisFrame = true;
        if (ProgramPaused && FrameStep) { Debug.Log("Stepped forward 1 frame"); FrameStep = false; }
        
        if (!simulateThisFrame) return;

        for (int _ = 0; _ < TimeStepsPerFrame; _++)
        {
            UpdateShaderTimeStep();

            // GPUSortSpringLookUp() have to be called in succession to GPUSortChunkLookUp()
            GPUSortChunkLookUp();
            GPUSortSpringLookUp();

            for (int i = 0; i < SubTimeStepsPerFrame; i++)
            {
                pSimShader.SetBool("TransferSpringData", i == 0);

                RunPSimShader(i);

                RunRbSimShader();

                int ThreadNums2 = Utils.GetThreadGroupsNums(ParticlesNum, pSimShaderThreadSize);
                if (ParticlesNum != 0) {pSimShader.Dispatch(5, ThreadNums2, 1, 1);}
                
                FrameCount++;
                pSimShader.SetInt("FrameCount", FrameCount);
                pSimShader.SetInt("FrameRand", Func.RandInt(0, 99999));
            }
        }
    }

    private void PauseControls()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            ProgramPaused = !ProgramPaused;
            Debug.Log("Program paused");
        }
        if (Input.GetKeyDown(KeyCode.F)) FrameStep = !FrameStep;
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
        PTypeBuffer.SetData(PTypes);

        shaderHelper.UpdatePSimShaderVariables(pSimShader);
        shaderHelper.UpdateRenderShaderVariables(renderShader);
        shaderHelper.UpdateSortShaderVariables(sortShader);
    }
    
    public void UpdateShaderTimeStep()
    {
        DeltaTime = GetDeltaTime();
        
        Vector2 mouseWorldPos = Utils.GetMouseWorldPos(BoundaryDims);
        // (Left?, Right?)
        bool2 mousePressed = Utils.GetMousePressed();

        pSimShader.SetFloat("DeltaTime", DeltaTime);
        pSimShader.SetFloat("SRDeltaTime", DeltaTime * CalcStickyRequestsFrequency);
        pSimShader.SetVector("MousePos", new Vector2(mouseWorldPos.x, mouseWorldPos.y));
        pSimShader.SetBool("LMousePressed", mousePressed.x);
        pSimShader.SetBool("RMousePressed", mousePressed.y);
        rbSimShader.SetFloat("DeltaTime", DeltaTime);
        rbSimShader.SetVector("MousePos", new Vector2(mouseWorldPos.x, mouseWorldPos.y));
        rbSimShader.SetBool("RMousePressed", mousePressed.x);
        rbSimShader.SetBool("LMousePressed", mousePressed.y);

        // Multi-compilation
        if (DoDrawRBCentroids) renderShader.EnableKeyword("DRAW_RB_CENTROIDS");

        FrameBufferCycle = !FrameBufferCycle;
        sortShader.SetBool("FrameBufferCycle", FrameBufferCycle);
        pSimShader.SetBool("FrameBufferCycle", FrameBufferCycle);

        pSimShader.SetInt("FrameRand", Func.RandInt(0, 99999));
    }

    void SceneSetup()
    {
        Camera.main.transform.position = new Vector3(BoundaryDims.x / 2, BoundaryDims.y / 2, -1);
        Camera.main.orthographicSize = Mathf.Max(BoundaryDims.x * 0.75f, BoundaryDims.y * 1.5f);
    }

    float GetDeltaTime()
    {
        float deltaTime = TimeStep / SubTimeStepsPerFrame;
        if (!FixedTimeStep) deltaTime = Mathf.Min(deltaTime, Time.deltaTime * ProgramSpeed / SubTimeStepsPerFrame);
        return deltaTime;
    }

    void SetConstants()
    {
        MaxInfluenceRadiusSqr = MaxInfluenceRadius * MaxInfluenceRadius;
        InvMaxInfluenceRadius = 1.0f / MaxInfluenceRadius;
        ParticleSpringsCombinedHalfLength = ParticlesNum * SpringCapacitySafety / 2;
        ParticlesNum_NextPow2 = Func.NextPow2(ParticlesNum);
    }

    void SetPTypesData()
    {
        PTypes = new PType[6];
        float IR_1 = 2.0f;
        float IR_2 = 2.0f;
        int FSG_1 = 1;
        int FSG_2 = 2;
        PTypes[0] = new PType // Solid
        {
            fluidSpringGroup = 1,

            springPlasticity = 0,
            springTolDeformation = 0.1f,
            springStiffness = 2000,

            thermalConductivity = 1.0f,
            specificHeatCapacity = 10.0f,
            freezeThreshold = Utils.CelsiusToKelvin(0.0f),
            vaporizeThreshold = Utils.CelsiusToKelvin(100.0f),

            pressure = 3000,
            nearPressure = 5,

            mass = 1,
            targetDensity = targetDensity,
            damping = damping,
            passiveDamping = 0.0f,
            viscosity = 5.0f,
            stickyness = 2.0f,
            gravity = gravity,

            influenceRadius = 2,
            colorG = 0.5f
        };
        PTypes[1] = new PType // Liquid
        {
            fluidSpringGroup = FSG_1,

            springPlasticity = Plasticity,
            springTolDeformation = TolDeformation,
            springStiffness = springStiffness,

            thermalConductivity = 1.0f,
            specificHeatCapacity = 10.0f,
            freezeThreshold = Utils.CelsiusToKelvin(0.0f),
            vaporizeThreshold = Utils.CelsiusToKelvin(100.0f),
            
            pressure = PressureMultiplier,
            nearPressure = NearPressureMultiplier,

            mass = 1,
            targetDensity = targetDensity,
            damping = damping,
            passiveDamping = passiveDamping,
            viscosity = viscosity,
            stickyness = 2.0f,
            gravity = gravity,

            influenceRadius = IR_1,
            colorG = 0.0f
        };
        PTypes[2] = new PType // Gas
        {
            fluidSpringGroup = 0,

            springPlasticity = -1,
            springTolDeformation = -1,
            springStiffness = -1,

            thermalConductivity = 3.0f,
            specificHeatCapacity = 10.0f,
            freezeThreshold = Utils.CelsiusToKelvin(0.0f),
            vaporizeThreshold = Utils.CelsiusToKelvin(100.0f),

            pressure = 200,
            nearPressure = 0,

            mass = 0.1f,
            targetDensity = 0,
            damping = damping,
            passiveDamping = passiveDamping,
            viscosity = viscosity,
            stickyness = 2.0f,
            gravity = gravity * 0.1f,

            influenceRadius = IR_1,
            colorG = 0.3f
        };

        PTypes[3] = new PType // Solid
        {
            fluidSpringGroup = FSG_2,

            springPlasticity = Plasticity,
            springTolDeformation = TolDeformation,
            springStiffness = springStiffness,

            thermalConductivity = 7.0f,
            specificHeatCapacity = 15.0f,
            freezeThreshold = Utils.CelsiusToKelvin(999.0f),
            vaporizeThreshold = Utils.CelsiusToKelvin(-999.0f),

            pressure = PressureMultiplier,
            nearPressure = NearPressureMultiplier,

            mass = 1,
            targetDensity = targetDensity * 1.5f,
            damping = damping,
            passiveDamping = passiveDamping,
            viscosity = viscosity,
            stickyness = 4.0f,
            gravity = gravity,

            influenceRadius = IR_2,
            colorG = 0.9f
        };
        PTypes[4] = new PType // Liquid
        {
            fluidSpringGroup = FSG_2,

            springPlasticity = Plasticity,
            springTolDeformation = TolDeformation,
            springStiffness = springStiffness,

            thermalConductivity = 7.0f,
            specificHeatCapacity = 15.0f,
            freezeThreshold = Utils.CelsiusToKelvin(-999.0f),
            vaporizeThreshold = Utils.CelsiusToKelvin(999.0f),

            pressure = PressureMultiplier,
            nearPressure = NearPressureMultiplier,

            mass = 1,
            targetDensity = targetDensity * 1.5f,
            damping = damping,
            passiveDamping = passiveDamping,
            viscosity = viscosity,
            stickyness = 4.0f,
            gravity = gravity,

            influenceRadius = IR_2,
            colorG = 1.0f
        };
        PTypes[5] = new PType // Gas
        {
            fluidSpringGroup = FSG_2,

            springPlasticity = Plasticity,
            springTolDeformation = TolDeformation,
            springStiffness = springStiffness,

            thermalConductivity = 7.0f,
            specificHeatCapacity = 15.0f,
            freezeThreshold = Utils.CelsiusToKelvin(-999.0f),
            vaporizeThreshold = Utils.CelsiusToKelvin(999.0f),

            pressure = PressureMultiplier,
            nearPressure = NearPressureMultiplier,

            mass = 1,
            targetDensity = targetDensity * 1.5f,
            damping = damping,
            passiveDamping = passiveDamping,
            viscosity = viscosity,
            stickyness = 4.0f,
            gravity = gravity,

            influenceRadius = IR_2,
            colorG = 0.9f
        };
    }

    void InitializeBuffers()
    {
        ComputeHelper.CreateStructuredBuffer<PData>(ref PDataBuffer, PDatas);
        ComputeHelper.CreateStructuredBuffer<PType>(ref PTypeBuffer, PTypes);

        ComputeHelper.CreateStructuredBuffer<int2>(ref SpatialLookupBuffer, ParticlesNum_NextPow2);
        ComputeHelper.CreateStructuredBuffer<int>(ref StartIndicesBuffer, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int2>(ref SpringCapacitiesBuffer, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int>(ref SpringStartIndicesBuffer_dbA, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int>(ref SpringStartIndicesBuffer_dbB, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int>(ref SpringStartIndicesBuffer_dbC, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<Spring>(ref ParticleSpringsCombinedBuffer, ParticlesNum * SpringCapacitySafety);

        ComputeHelper.CreateStructuredBuffer<RBData>(ref RBDataBuffer, RBDatas);
        ComputeHelper.CreateStructuredBuffer<RBVector>(ref RBVectorBuffer, RBVectors);
        ComputeHelper.CreateStructuredBuffer<RBAdjustment>(ref RBAdjustmentBuffer, RBDatas.Length);


        ComputeHelper.CreateCountBuffer(ref TCCountBuffer);
        ComputeHelper.CreateCountBuffer(ref SRCountBuffer);

        ComputeHelper.CreateAppendBuffer<int3>(ref TraversedChunks_AC_Buffer, 4096);

        ComputeHelper.CreateStructuredBuffer<StickynessRequest>(ref SortedStickyRequestsBuffer, 4096);
        ComputeHelper.CreateAppendBuffer<StickynessRequest>(ref StickynessReqs_AC_Buffer, 4096);
        ComputeHelper.CreateAppendBuffer<StickynessRequest>(ref StickyRequestsResult_AC_Buffer, 4096);
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
            sortShader.SetInt("Offset2", offset);

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
        if (RBVectors.Length > 0) ComputeHelper.DispatchKernel (rbSimShader, "UpdateRBVertices", RBVectors.Length, rbSimShaderThreadSize1);

        if (RBDatas.Length > 0) ComputeHelper.DispatchKernel (rbSimShader, "SimulateRB_RB", RBDatas.Length, rbSimShaderThreadSize2);

        if (RBDatas.Length > 0) ComputeHelper.DispatchKernel (rbSimShader, "AdjustRBDatas", RBDatas.Length, rbSimShaderThreadSize2);

        if (ParticlesNum > 0) ComputeHelper.DispatchKernel (rbSimShader, "SimulateRB_P", ParticlesNum, rbSimShaderThreadSize3);

        if (RBDatas.Length > 0) ComputeHelper.DispatchKernel (rbSimShader, "UpdatePositions", RBDatas.Length, rbSimShaderThreadSize2);
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
            PDataBuffer,
            PTypeBuffer,
            SpringCapacitiesBuffer,
            SpringStartIndicesBuffer_dbA,
            SpringStartIndicesBuffer_dbB,
            SpringStartIndicesBuffer_dbC,
            ParticleSpringsCombinedBuffer,
            RBDataBuffer,
            RBAdjustmentBuffer,
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