using UnityEngine;
using Unity.Mathematics;
using System;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

// Import utils from Resources.cs
using Resources;
using System.Collections.Generic;
public class Main : MonoBehaviour
{
    [Header("Shader Compilation - Particle Simulation")]
    // Fluids
    public bool DoSimulateParticleViscosity = true;
    public bool DoSimulateParticleSprings = true;
    public bool DoSimulateParticleTemperature = true;

    // Shader Thread Group Sizes
    public int renderShaderThreadSize = 32; // /32, AxA thread groups
    public int pSimShaderThreadSize = 512; // /1024
    public int pSimShaderThreadSize2 = 512; // /1024
    public int sortShaderThreadSize = 512; // /1024
    public int rbSimShaderThreadSize1 = 64; // Rigid Body Simulation
    public int rbSimShaderThreadSize2 = 32; // Rigid Body Simulation
    public int rbSimShaderThreadSize3 = 512; // Rigid Body Simulation
    public float FloatIntPrecisionRB = 50000.0f; // Float-Int storage precision used for rbSimShader
    public float FloatIntPrecisionP = 1000.0f; // Float-Int storage precision used in pSimShader

    [Header("Fluid Simulation")]
    public float LookAheadTime = 0.017f;
    public float StateThresholdPadding = 3.0f;
    public int MaxInfluenceRadius = 2;
    [SerializeField] private int MaxParticlesNum = 20000;
    [SerializeField] private int MaxSpringsPerParticle = 150;

    [Header("Scene Boundary")]
    public int2 BoundaryDims = new(300, 200);
    public float FluidPadding = 4.0f;
    public float RigidBodyPadding = 2.0f;

    [Header("Rigid Body Simulation")]
    public bool AllowLinkedRBCollisions = false;
    public float RB_RBCollisionCorrectionFactor = 0.8f;
    public float RB_RBCollisionSlop = 0.01f;

    [Header("Simulation Time")]
    public int TimeStepsPerFrame = 3;
    public int SubTimeStepsPerFrame = 3;
    public TimeStepType TimeStepType;
    public float TimeStep = 0.02f;
    public float ProgramSpeed = 2.0f;

    [Header("Mouse Interaction")]
    // Particles
    public float MaxInteractionRadius = 40.0f;
    public float InteractionAttractionPower = 3.5f;
    public float InteractionFountainPower = 1.0f;
    public float InteractionTemperaturePower = 1.0f;
    // Rigid Bodies
    public float RB_MaxInteractionRadius = 40.0f;
    public float RB_InteractionAttractionPower = 3.5f;

    [Header("Render Pipeline")]
    [SerializeField] private FluidRenderMethod FluidRenderMethod;
    [SerializeField] private bool DoDrawFluidOutlines = true;
    [SerializeField] private bool DoDisplayFluidVelocities = true;
    [SerializeField] private bool DoDrawUnoccupiedFluidSensorArea = false;
    [SerializeField] private bool DoDrawRBOutlines = true;
    [SerializeField] private bool DoDrawRBCentroids = false;
    // The list that defines the order of render steps
    public List<RenderStep> RenderOrder = new()
    {
        RenderStep.Background,
        RenderStep.Fluids,
        RenderStep.RigidBodies,
        RenderStep.RigidBodySprings,
        RenderStep.UI
    };

    [Header("Render Display")]
    public int2 Resolution = new(1920, 1280);
    public float3 GlobalBrightness;
    // Rigid Body Springs
    public float SpringRenderWidth;
    public float SpringRenderMatWidth;
    public float SpringRenderRodLength;
    public int SpringRenderNumPeriods;
    public float TaperThresoldNormalised = 0.2f;
    // Fluids
    public float VisualParticleRadii = 0.4f;
    public float MetaballsThreshold = 1.0f;
    public float MetaballsEdgeDensityWidth = 0.3f;
    public float FluidEdgeWidth = 1.0f;
    // Rigid Bodies
    public float RBEdgeWidth = 0.5f;
    // Sensor Areas
    public float FluidSensorEdgeWidth = 3.0f;
    public float SensorAreaAnimationSpeed = 2.0f;
    // Background
    public Texture2D backgroundTexture;
    public float3 BackgroundBrightness;
    public float BackgroundUpScaleFactor;

    [Header("References")]
    // Textures
    public RenderTexture uiTexture;
    public RenderTexture causticsTexture;
    // Scripts
    public MaterialInput materialInput;
    public PTypeInput pTypeInput;
    public SceneManager sceneManager;
    public ShaderHelper shaderHelper;
    // Compute Shaders
    public ComputeShader renderShader;
    public ComputeShader pSimShader;
    public ComputeShader rbSimShader;
    public ComputeShader sortShader;

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
    public ComputeBuffer RecordedFluidDataBuffer;

    // Rigid bodies
    public ComputeBuffer RBVectorBuffer;
    public ComputeBuffer RBDataBuffer;
    public ComputeBuffer RBAdjustmentBuffer;

    // Fluid Sensors
    public ComputeBuffer SensorAreaBuffer;
    // Materials
    public ComputeBuffer MaterialBuffer;

    // Constants
    [NonSerialized] public int ParticlesNum;
    [NonSerialized] public int MaxInfluenceRadiusSqr;
    [NonSerialized] public float InvMaxInfluenceRadius;
    [NonSerialized] public float MarchScale;
    [NonSerialized] public int2 ChunksNum;
    [NonSerialized] public int ChunksNumAll;
    [NonSerialized] public int ChunksNumAllNextPow2;
    [NonSerialized] public int ParticleSpringsCombinedHalfLength;
    [NonSerialized] public int ParticlesNum_NextPow2;
    [NonSerialized] public int ParticlesNum_NextLog2;

    // Private references
    [NonSerialized] public RenderTexture renderTexture;
    [NonSerialized] public Texture2D AtlasTexture;

    // Particle data
    private PData[] PDatas;

    // Rigid Bodies
    public RBVector[] RBVectors;
    public RBData[] RBDatas;

    // Fluid Sensors
    public SensorArea[] SensorAreas;

    // Materials
    private Mat[] Mats;

    // Other
    private float DeltaTime;
    private const int CalcStickyRequestsFrequency = 3;
    private bool ProgramStarted = false;
    private int FrameCount = 0;
    private bool ProgramPaused = false;
    private bool FrameStep = false;
    public bool DoUpdateShaderData = false;

    void Awake()
    {
        SceneSetup();

        PDatas = sceneManager.GenerateParticles(MaxParticlesNum);
        ParticlesNum = PDatas.Length;

        BoundaryDims = sceneManager.GetBounds(MaxInfluenceRadius);

        ChunksNum = BoundaryDims / MaxInfluenceRadius;
        ChunksNumAll = ChunksNum.x * ChunksNum.y;

        (RBDatas, RBVectors, SensorAreas) = sceneManager.CreateRigidBodies();
        (AtlasTexture, Mats) = sceneManager.ConstructTextureAtlas(materialInput.materialInputs);

        SetConstants();

        InitializeBuffers();
        renderTexture = TextureHelper.CreateTexture(Resolution, 3);

        shaderHelper.SetPSimShaderBuffers(pSimShader);
        shaderHelper.SetNewRBSimShaderBuffers(rbSimShader);
        shaderHelper.SetRenderShaderBuffers(renderShader);
        shaderHelper.SetRenderShaderTextures(renderShader);
        shaderHelper.SetSortShaderBuffers(sortShader);

        shaderHelper.UpdatePSimShaderVariables(pSimShader);
        shaderHelper.UpdateNewRBSimShaderVariables(rbSimShader);
        shaderHelper.UpdateRenderShaderVariables(renderShader);
        shaderHelper.UpdateSortShaderVariables(sortShader);

        Debug.Log("Simulation started with " + ParticlesNum + " particles");
        ProgramStarted = true;
    }

    void Update()
    {
        if (DoUpdateShaderData) UpdateShaderData();

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
                if (ParticlesNum != 0) pSimShader.Dispatch(5, ThreadNums2, 1, 1);
                
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

    private void UpdateShaderData()
    {
        SetConstants();
        UpdateSettings();

        DoUpdateShaderData = false;
    }

    public void OnValidate()
    {
        if (ProgramStarted) DoUpdateShaderData = true;
    }

    public void UpdateSettings()
    {
        // Set new pType and material data
        PTypeBuffer.SetData(pTypeInput.GetParticleTypes());
        MaterialBuffer.SetData(Mats);

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
        renderShader.SetFloat("RealTimeElapsed", Time.realtimeSinceStartup);

        // Multi-compilation - renderShader
        if (DoDrawRBCentroids) renderShader.EnableKeyword("DRAW_RB_CENTROIDS");
        else renderShader.DisableKeyword("DRAW_RB_CENTROIDS");
        if (DoDrawFluidOutlines) renderShader.EnableKeyword("DRAW_FLUID_OUTLINES");
        else renderShader.DisableKeyword("DRAW_FLUID_OUTLINE");
        if (DoDisplayFluidVelocities) renderShader.EnableKeyword("DISPLAY_FLUID_VELOCITIES");
        else renderShader.DisableKeyword("DISPLAY_FLUID_VELOCITIES");
        if (DoDrawUnoccupiedFluidSensorArea) renderShader.EnableKeyword("DRAW_UNOCCUPIED_FLUID_SENSOR_AREA");
        else renderShader.DisableKeyword("DRAW_UNOCCUPIED_FLUID_SENSOR_AREA");
        if (DoDrawRBOutlines) renderShader.EnableKeyword("DRAW_RB_OUTLINES");
        else renderShader.DisableKeyword("DRAW_RB_OUTLINE");
        if (FluidRenderMethod == FluidRenderMethod.Metaballs) renderShader.EnableKeyword("USE_METABALLS");
        else renderShader.DisableKeyword("USE_METABALLS");

        // Multi-compilation - pSimShader
        if (DoSimulateParticleViscosity) pSimShader.EnableKeyword("SIMULATE_PARTICLE_VISCOSITY");
        else pSimShader.DisableKeyword("SIMULATE_PARTICLE_VISCOSITY");
        if (DoSimulateParticleSprings) pSimShader.EnableKeyword("SIMULATE_PARTICLE_SPRINGS");
        else pSimShader.DisableKeyword("SIMULATE_PARTICLE_SPRINGS");
        if (DoSimulateParticleTemperature) pSimShader.EnableKeyword("SIMULATE_PARTICLE_TEMPERATURE");
        else pSimShader.DisableKeyword("SIMULATE_PARTICLE_TEMPERATURE");

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
        if (TimeStepType == TimeStepType.Dynamic) deltaTime = Mathf.Min(deltaTime, Time.deltaTime * ProgramSpeed / SubTimeStepsPerFrame);
        return deltaTime;
    }

    void SetConstants()
    {
        MaxInfluenceRadiusSqr = MaxInfluenceRadius * MaxInfluenceRadius;
        InvMaxInfluenceRadius = 1.0f / MaxInfluenceRadius;
        ParticleSpringsCombinedHalfLength = ParticlesNum * MaxSpringsPerParticle / 2;
        ParticlesNum_NextPow2 = Func.NextPow2(ParticlesNum);
    }

    void InitializeBuffers()
    {
        ComputeHelper.CreateStructuredBuffer<PData>(ref PDataBuffer, PDatas);
        ComputeHelper.CreateStructuredBuffer<PType>(ref PTypeBuffer, pTypeInput.GetParticleTypes());
        ComputeHelper.CreateStructuredBuffer<RecordedFluidData>(ref RecordedFluidDataBuffer, ChunksNumAll);

        ComputeHelper.CreateStructuredBuffer<int2>(ref SpatialLookupBuffer, ParticlesNum_NextPow2);
        ComputeHelper.CreateStructuredBuffer<int>(ref StartIndicesBuffer, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int2>(ref SpringCapacitiesBuffer, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int>(ref SpringStartIndicesBuffer_dbA, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int>(ref SpringStartIndicesBuffer_dbB, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int>(ref SpringStartIndicesBuffer_dbC, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<Spring>(ref ParticleSpringsCombinedBuffer, ParticlesNum * MaxSpringsPerParticle);

        ComputeHelper.CreateStructuredBuffer<RBData>(ref RBDataBuffer, RBDatas);
        ComputeHelper.CreateStructuredBuffer<RBVector>(ref RBVectorBuffer, RBVectors);
        ComputeHelper.CreateStructuredBuffer<RBAdjustment>(ref RBAdjustmentBuffer, RBDatas.Length);

        ComputeHelper.CreateStructuredBuffer<SensorArea>(ref SensorAreaBuffer, SensorAreas);

        ComputeHelper.CreateStructuredBuffer<Mat>(ref MaterialBuffer, Mats);
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
        if (DoSimulateParticleSprings)
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

        if (step == 0 && DoSimulateParticleSprings)
        {
            ComputeHelper.DispatchKernel (pSimShader, "PrepSpringData", ParticleSpringsCombinedHalfLength, pSimShaderThreadSize);
            ComputeHelper.DispatchKernel (pSimShader, "TransferAllSpringData", ParticleSpringsCombinedHalfLength, pSimShaderThreadSize);
        }

        ComputeHelper.DispatchKernel (pSimShader, "ParticleForces", ParticlesNum, pSimShaderThreadSize);

        ComputeHelper.DispatchKernel (pSimShader, "ResetFluidData", ChunksNumAll, pSimShaderThreadSize2);
        ComputeHelper.DispatchKernel (pSimShader, "RecordFluidData", ParticlesNum, pSimShaderThreadSize);
    }

    void RunRbSimShader()
    {
        if (RBVectors.Length > 0) ComputeHelper.DispatchKernel (rbSimShader, "UpdateRBVertices", RBVectors.Length, rbSimShaderThreadSize1);
        if (RBDatas.Length > 0) ComputeHelper.DispatchKernel (rbSimShader, "SimulateRB_RB", RBDatas.Length, rbSimShaderThreadSize2);
        if (RBDatas.Length > 0) ComputeHelper.DispatchKernel (rbSimShader, "SimulateRBSprings", RBDatas.Length, rbSimShaderThreadSize2);
        if (RBDatas.Length > 0) ComputeHelper.DispatchKernel (rbSimShader, "AdjustRBDatas", RBDatas.Length, rbSimShaderThreadSize2);
        if (ParticlesNum > 0) ComputeHelper.DispatchKernel (rbSimShader, "SimulateRB_P", ParticlesNum, rbSimShaderThreadSize3);
        if (RBDatas.Length > 0) ComputeHelper.DispatchKernel (rbSimShader, "UpdateRigidBodies", RBDatas.Length, rbSimShaderThreadSize2);
    }

void DispatchRenderStep(RenderStep step, int2 threadsNum)
{
    switch (step)
    {
        case RenderStep.Background:
            ComputeHelper.DispatchKernel(renderShader, "RenderBackground", threadsNum, renderShaderThreadSize);
            break;
        case RenderStep.Fluids:
            ComputeHelper.DispatchKernel(renderShader, "RenderFluids", threadsNum, renderShaderThreadSize);
            break;
        case RenderStep.RigidBodies:
            ComputeHelper.DispatchKernel(renderShader, "RenderRigidBodies", threadsNum, renderShaderThreadSize);
            break;
        case RenderStep.RigidBodySprings:
            ComputeHelper.DispatchKernel(renderShader, "RenderRigidBodySprings", threadsNum, renderShaderThreadSize);
            break;
        case RenderStep.UI:
            ComputeHelper.DispatchKernel(renderShader, "RenderUI", threadsNum, renderShaderThreadSize);
            break;
    }
}
    void RunRenderShader()
    {
        int2 threadsNum = new(renderTexture.width, renderTexture.height);
        foreach (RenderStep step in RenderOrder)
        {
            DispatchRenderStep(step, threadsNum);
        }
    }
    
    public void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        RunRenderShader();

        Graphics.Blit(renderTexture, dest);
    }

    void OnDestroy()
    {
        ComputeHelper.Release(
            SpatialLookupBuffer,
            StartIndicesBuffer,
            PDataBuffer,
            PTypeBuffer,
            RecordedFluidDataBuffer,
            SpringCapacitiesBuffer,
            SpringStartIndicesBuffer_dbA,
            SpringStartIndicesBuffer_dbB,
            SpringStartIndicesBuffer_dbC,
            ParticleSpringsCombinedBuffer,
            RBDataBuffer,
            RBAdjustmentBuffer,
            SensorAreaBuffer,
            RBVectorBuffer,
            MaterialBuffer
        );
    }
}