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

    [Header("Rigid Body Collision Solver")]
    public float RB_RBCollisionCorrectionFactor = 0.8f;
    public float RB_RBCollisionSlop = 0.01f;
    public bool AllowLinkedRBCollisions = false;

    [Header("Render")]
    public bool FixedTimeStep = true;
    public bool RenderMarchingSquares = false;
    public float TimeStep = 0.02f;
    public float ProgramSpeed = 2.0f;
    public float VisualParticleRadii = 0.4f;
    public float MetaballsThreshold = 1.0f;
    public float MetaballsEdgeDensityWidth = 0.3f;
    public float FluidEdgeWidth = 1.0f;
    public float RBEdgeWidth = 0.5f;
    public float3 BackgroundBrightness;
    public float BackgroundScale;
    public int TimeStepsPerFrame = 3;
    public int SubTimeStepsPerFrame = 3;
    public float MSvalMin = 0.41f;
    public int2 Resolution = new(1920, 1280);
    
    [Header("Rigid Body Spring Render")]
    public int SpringRenderNumPeriods;
    public float SpringRenderWidth;
    public float SpringRenderHalfMatWidth;
    public float SpringRenderRodLength;
    public Color SpringRenderColor;

    [Header("Shader Compilation - Renderer")]
    public bool DoDrawRBCentroids = false;
    public bool DoDrawFluidOutlines = true;
    public bool DoDrawRBOutlines = true;
    public bool DoUseMetaballs = true;

    [Header("Shader Compilation - Particle Simulation")]
    public bool DoSimulateParticleViscosity = true;
    public bool DoSimulateParticleSprings = true;
    public bool DoSimulateParticleTemperature = true;

    [Header("Interaction - Particles")]
    public float MaxInteractionRadius = 40.0f;
    public float InteractionAttractionPower = 3.5f;
    public float InteractionFountainPower = 1.0f;
    public float InteractionTemperaturePower = 1.0f;

    [Header("Interaction - Rigid Bodies")]
    public float RB_MaxInteractionRadius = 40.0f;
    public float RB_InteractionAttractionPower = 3.5f;

    [Header("References")]
    public MaterialInput materialInput;
    public PTypeInput pTypeInput;
    public RenderTexture uiTexture;
    public RenderTexture causticsTexture;
    public Texture2D backgroundTexture;
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
    public ComputeBuffer RecordedElementBuffer;

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
    [NonSerialized] public int TraversedChunksCount;
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

    void Start()
    {
        SceneSetup();

        PDatas = sceneManager.GenerateParticles(MaxParticlesNum);
        ParticlesNum = PDatas.Length;

        BoundaryDims = sceneManager.GetBounds(MaxInfluenceRadius);

        ChunksNum = BoundaryDims / MaxInfluenceRadius;
        ChunksNumAll = ChunksNum.x * ChunksNum.y;

        (RBDatas, RBVectors) = sceneManager.GenerateRigidBodies();
        (AtlasTexture, Mats) = sceneManager.ConstructTextureAtlas(materialInput.materialInputs);

        SetConstants();

        TraversedChunksCount = StartTraversedChunksCount;

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

        // Multi-compilation - renderShader
        if (DoDrawRBCentroids) renderShader.EnableKeyword("DRAW_RB_CENTROIDS");
        else renderShader.DisableKeyword("DRAW_RB_CENTROIDS");
        if (DoDrawFluidOutlines) renderShader.EnableKeyword("DRAW_FLUID_OUTLINES");
        else renderShader.DisableKeyword("DRAW_FLUID_OUTLINE");
        if (DoDrawRBOutlines) renderShader.EnableKeyword("DRAW_RB_OUTLINES");
        else renderShader.DisableKeyword("DRAW_RB_OUTLINE");
        if (DoUseMetaballs) renderShader.EnableKeyword("USE_METABALLS");
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

    void InitializeBuffers()
    {
        ComputeHelper.CreateStructuredBuffer<PData>(ref PDataBuffer, PDatas);
        ComputeHelper.CreateStructuredBuffer<PType>(ref PTypeBuffer, pTypeInput.GetParticleTypes());

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

        ComputeHelper.CreateStructuredBuffer<int>(ref RecordedElementBuffer, Resolution.x * Resolution.y);

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

    void RunRenderShader()
    {
        ComputeHelper.DispatchKernel (renderShader, "RenderBackground", new int2(renderTexture.width, renderTexture.height), renderShaderThreadSize);
        ComputeHelper.DispatchKernel (renderShader, "RenderFluids", new int2(renderTexture.width, renderTexture.height), renderShaderThreadSize);
        ComputeHelper.DispatchKernel (renderShader, "RenderRigidBodies", new int2(renderTexture.width, renderTexture.height), renderShaderThreadSize);
        ComputeHelper.DispatchKernel (renderShader, "RenderRigidBodySprings", new int2(renderTexture.width, renderTexture.height), renderShaderThreadSize);
        ComputeHelper.DispatchKernel (renderShader, "RenderUI", new int2(renderTexture.width, renderTexture.height), renderShaderThreadSize);
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
            RecordedElementBuffer,
            MaterialBuffer
        );
    }
}