using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Unity.Mathematics;
using System;
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

public class Main : MonoBehaviour
{
    [Header("Simulation settings")]
    public int ParticlesNum;
    public int RBodiesNum;
    public int MaxInfluenceRadius;
    public float TargetDensity;
    public float PressureMultiplier;
    public float NearPressureMultiplier;
    [Range(0, 1)] public float Damping;
    [Range(0, 1)] public float Viscocity;
    [Range(0, 1)] public float RBodyElasticity;
    public float Gravity;
    public int PStorageLength;

    [Header("Boundrary settings")]
    public int Width;
    public int Height;
    public int SpawnDims; // A x A
    public float BorderPadding;

    [Header("Rendering settings")]
    public bool FixedTimeStep;
    public float TimeStep;
    public float ProgramSpeed;
    public float VisualParticleRadii;
    public int TimeStepsPerRender;
    public int ResolutionX;
    public int ResolutionY;

    [Header("Interaction settings")]
    public float MaxInteractionRadius;
    public float InteractionPower;

    [Header("References")]
    public GameObject ParticlePrefab;
    public ComputeShader RenderShader;
    public ComputeShader SimShader;

    // Private references
    private RenderTexture renderTexture;
    private GameObject simulationBoundrary;

    // Constants
    private int ChunkNumW;
    private int ChunkNumH;
    private int IOOR; // Index Out Of Range

    // Particles - Properties
    private int2[] SpatialLookup; // (particleIndex, chunkKey)
    private int[] StartIndices;
    private float2[] PredPositions;
    private float2[] Positions;
    private float2[] Velocities;
    private float2[] LastVelocities;
    private float[] Densities;
    private float[] NearDensities;
    private int2[] TemplateSpatialLookup;

    // Particles - Buffers
    private ComputeBuffer SpatialLookupBuffer;
    private ComputeBuffer StartIndicesBuffer;
    private ComputeBuffer PredPositionsBuffer;
    private ComputeBuffer PositionsBuffer;
    private ComputeBuffer VelocitiesBuffer;
    private ComputeBuffer LastVelocitiesBuffer;
    private ComputeBuffer DensitiesBuffer;
    private ComputeBuffer NearDensitiesBuffer;


    // Rigid Bodies - Properties
    private float2[] RBPositions;
    private float2[] RBVelocities;
    private float[] RBRadii;
    private float[] RBMass;
    private float3[] ParticleImpulseStorage;
    private float3[] ParticleTeleportStorage;

    // Rigid Bodies - Buffers
    private ComputeBuffer RBPositionsBuffer;
    private ComputeBuffer RBVelocitiesBuffer;
    private ComputeBuffer RBRadiiBuffer;
    private ComputeBuffer RBMassBuffer;
    private ComputeBuffer ParticleImpulseStorageBuffer;
    private ComputeBuffer ParticleTeleportStorageBuffer;

    // Other
    private float DeltaTime;

    // Perhaps should create a seperate class for SimShader functions/methods
    void Start()
    {
        CreateVisualBoundrary();

        Camera.main.transform.position = new Vector3(Width / 2, Height / 2, -1);
        Camera.main.orthographicSize = Mathf.Max(Width * 0.75f, Height * 1.5f);

        SetConstants();

        InitializeArrays();

        for (int i = 0; i < ParticlesNum; i++)
        {
            Positions[i] = ParticleSpawnPosition(i, ParticlesNum);
        }

        InitializeBuffers();
        SetSimShaderBuffers();
        SetRenderShaderBuffers();
    }

    void SetConstants()
    {
        ChunkNumW = Width / MaxInfluenceRadius;
        ChunkNumH = Height / MaxInfluenceRadius;
        IOOR = ParticlesNum;
    }

    void InitializeArrays()
    {
        SpatialLookup = new int2[ParticlesNum];
        StartIndices = new int[ParticlesNum];
        PredPositions = new float2[ParticlesNum];
        Positions = new float2[ParticlesNum];
        Velocities = new float2[ParticlesNum];
        LastVelocities = new float2[ParticlesNum];
        Densities = new float[ParticlesNum];
        NearDensities = new float[ParticlesNum];
        TemplateSpatialLookup = new int2[ParticlesNum];

        RBPositions = new float2[RBodiesNum];
        RBVelocities = new float2[RBodiesNum];
        RBRadii = new float[RBodiesNum];
        RBMass = new float[RBodiesNum];
        ParticleImpulseStorage = new float3[RBodiesNum * PStorageLength];
        ParticleTeleportStorage = new float3[RBodiesNum * PStorageLength];

        for (int i = 0; i < ParticlesNum; i++)
        {
            Positions[i] = new float2(0.0f, 0.0f);
            Velocities[i] = new float2(0.0f, 0.0f);
            LastVelocities[i] = new float2(0.0f, 0.0f);
            PredPositions[i] = new float2(0.0f, 0.0f);
            Densities[i] = 0.0f;
            NearDensities[i] = 0.0f;
            StartIndices[i] = 0;

            TemplateSpatialLookup[i] = new int2(0, 0);
        }

        for (int i = 0; i < RBodiesNum; i++)
        {
            RBPositions[i] = new float2(0.0f, 0.0f);
            RBVelocities[i] = new float2(0.0f, 0.0f);
            RBRadii[i] = 3f;
            RBMass[i] = 10f;
        }

        for (int i = 0; i < RBodiesNum * PStorageLength; i++)
        {
            ParticleImpulseStorage[i] = new float3(0.0f, 0.0f, 0.0f);
            ParticleTeleportStorage[i] = new float3(0.0f, 0.0f, 0.0f);
        }
    }

    float2 ParticleSpawnPosition(int particle_index, int max_index)
    {
        float x = (Width - SpawnDims) / 2 + Mathf.Floor(particle_index % Mathf.Sqrt(max_index)) * (SpawnDims / Mathf.Sqrt(max_index));
        float y = (Height - SpawnDims) / 2 + Mathf.Floor(particle_index / Mathf.Sqrt(max_index)) * (SpawnDims / Mathf.Sqrt(max_index));
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

    void Update()
    {
        for (int i = 0; i < TimeStepsPerRender; i++)
        {
            CPUSortChunkdata();
            RunSimShader();
        }
    }

    void InitializeBuffers()
    {
        if (ParticlesNum != 0)
        {
            PredPositionsBuffer = new ComputeBuffer(ParticlesNum, sizeof(float) * 2);
            PositionsBuffer = new ComputeBuffer(ParticlesNum, sizeof(float) * 2);
            VelocitiesBuffer = new ComputeBuffer(ParticlesNum, sizeof(float) * 2);
            LastVelocitiesBuffer = new ComputeBuffer(ParticlesNum, sizeof(float) * 2);
            DensitiesBuffer = new ComputeBuffer(ParticlesNum, sizeof(float));
            NearDensitiesBuffer = new ComputeBuffer(ParticlesNum, sizeof(float));

            PredPositionsBuffer.SetData(PredPositions);
            PositionsBuffer.SetData(Positions);
            VelocitiesBuffer.SetData(Velocities);
            LastVelocitiesBuffer.SetData(Velocities);
            DensitiesBuffer.SetData(Densities);
            NearDensitiesBuffer.SetData(NearDensities);
        }
        if (RBodiesNum != 0)
        {
            RBPositionsBuffer = new ComputeBuffer(RBodiesNum, sizeof(float) * 2);
            RBVelocitiesBuffer = new ComputeBuffer(RBodiesNum, sizeof(float) * 2);
            RBRadiiBuffer = new ComputeBuffer(RBodiesNum, sizeof(float));
            RBMassBuffer = new ComputeBuffer(RBodiesNum, sizeof(float));
            ParticleImpulseStorageBuffer = new ComputeBuffer(RBodiesNum, sizeof(float) * 3);
            ParticleTeleportStorageBuffer = new ComputeBuffer(RBodiesNum, sizeof(float) * 3);

            RBPositionsBuffer.SetData(RBPositions);
            RBVelocitiesBuffer.SetData(RBVelocities);
            RBRadiiBuffer.SetData(RBRadii);
            RBMassBuffer.SetData(RBMass);
            ParticleImpulseStorageBuffer.SetData(ParticleImpulseStorage);
            ParticleTeleportStorageBuffer.SetData(ParticleTeleportStorage);
        }
        SpatialLookupBuffer = new ComputeBuffer(ParticlesNum, sizeof(int) * 2);
        StartIndicesBuffer = new ComputeBuffer(ParticlesNum, sizeof(int));
        SpatialLookupBuffer.SetData(SpatialLookup);
        StartIndicesBuffer.SetData(StartIndices);
    }

    void SetSimShaderBuffers()
    {
        // Kernel PreCalculations
        if (ParticlesNum != 0) {
            SimShader.SetBuffer(0, "Positions", PositionsBuffer);
            SimShader.SetBuffer(0, "Velocities", VelocitiesBuffer);
            SimShader.SetBuffer(0, "LastVelocities", LastVelocitiesBuffer);
            SimShader.SetBuffer(0, "PredPositions", PredPositionsBuffer);
        }
        // Kernel PreCalculations
        if (ParticlesNum != 0) {
            SimShader.SetBuffer(1, "Positions", PositionsBuffer);
            SimShader.SetBuffer(1, "Velocities", VelocitiesBuffer);
            SimShader.SetBuffer(1, "Densities", DensitiesBuffer);
            SimShader.SetBuffer(1, "NearDensities", NearDensitiesBuffer);
            SimShader.SetBuffer(1, "PredPositions", PredPositionsBuffer);
        }

        // Kernel RbRbCollisions
        if (RBodiesNum != 0) {
            SimShader.SetBuffer(2, "RBPositions", RBPositionsBuffer);
            SimShader.SetBuffer(2, "RBVelocities", RBVelocitiesBuffer);
            SimShader.SetBuffer(2, "RBRadii", RBRadiiBuffer);
            SimShader.SetBuffer(2, "RBMass", NearDensitiesBuffer);
        }

        // Kernel RbParticleCollisions
        if (RBodiesNum != 0) {
            SimShader.SetBuffer(3, "RBPositions", RBPositionsBuffer);
            SimShader.SetBuffer(3, "RBVelocities", RBVelocitiesBuffer);
            SimShader.SetBuffer(3, "RBRadii", RBRadiiBuffer);
            SimShader.SetBuffer(3, "RBMass", NearDensitiesBuffer);
            SimShader.SetBuffer(3, "Positions", PositionsBuffer);
            SimShader.SetBuffer(3, "Velocities", VelocitiesBuffer);
            SimShader.SetBuffer(3, "Densities", DensitiesBuffer);
            SimShader.SetBuffer(3, "ParticleImpulseStorage", ParticleImpulseStorageBuffer);
            SimShader.SetBuffer(3, "ParticleTeleportStorage", ParticleTeleportStorageBuffer);
        }

        // Kernel ParticleForces
        if (ParticlesNum != 0) {
            SimShader.SetBuffer(4, "Positions", PositionsBuffer);
            SimShader.SetBuffer(4, "Velocities", VelocitiesBuffer);
            SimShader.SetBuffer(4, "Densities", DensitiesBuffer);
            SimShader.SetBuffer(4, "NearDensities", NearDensitiesBuffer);
            SimShader.SetBuffer(4, "LastVelocities", LastVelocitiesBuffer);
            SimShader.SetBuffer(4, "PredPositions", PredPositionsBuffer);
        }
    }

    void UpdateSimShaderVariables()
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
        Vector3 MousePos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
        Vector2 MouseWorldPos = new(MousePos.x, MousePos.y);
        bool LMousePressed = Input.GetMouseButton(0);
        bool RMousePressed = Input.GetMouseButton(1);

        // Set sim shader variables
        SimShader.SetFloat("DeltaTime", DeltaTime);
        SimShader.SetFloat("MouseX", MouseWorldPos.x);
        SimShader.SetFloat("MouseY", MouseWorldPos.y);
        SimShader.SetBool("RMousePressed", RMousePressed);
        SimShader.SetBool("LMousePressed", LMousePressed);

        // Set SimShader constants
        SimShader.SetInt("ChunkNumW", ChunkNumW);
        SimShader.SetInt("ChunkNumH", ChunkNumH);
        SimShader.SetInt("IOOR", IOOR);
        SimShader.SetInt("Width", Width);
        SimShader.SetInt("Height", Height);
        SimShader.SetInt("ParticlesNum", ParticlesNum);
        SimShader.SetInt("RBodiesNum", RBodiesNum);
        SimShader.SetInt("MaxInfluenceRadius", MaxInfluenceRadius);
        SimShader.SetInt("SpawnDims", SpawnDims);
        SimShader.SetInt("TimeStepsPerRender", TimeStepsPerRender);
        SimShader.SetInt("PStorageLength", PStorageLength);
        SimShader.SetFloat("TargetDensity", TargetDensity);
        SimShader.SetFloat("PressureMultiplier", PressureMultiplier);
        SimShader.SetFloat("NearPressureMultiplier", NearPressureMultiplier);
        SimShader.SetFloat("Damping", Damping);
        SimShader.SetFloat("Viscocity", Viscocity);
        SimShader.SetFloat("Gravity", Gravity);
        SimShader.SetFloat("RBodyElasticity", RBodyElasticity);
        SimShader.SetFloat("BorderPadding", BorderPadding);
        SimShader.SetFloat("TimeStep", TimeStep);
        SimShader.SetFloat("MaxInteractionRadius", MaxInteractionRadius);
        SimShader.SetFloat("InteractionPower", InteractionPower);
        SimShader.SetBool("FixedTimeStep", FixedTimeStep);

        // Set math resources constants
    }

    void UpdateRenderShaderVariables()
    {
        RenderShader.SetFloat("VisualParticleRadii", VisualParticleRadii);
        RenderShader.SetFloat("ResolutionX", ResolutionX);
        RenderShader.SetFloat("ResolutionY", ResolutionY);
        RenderShader.SetFloat("Width", Width);
        RenderShader.SetFloat("Height", Height);
        RenderShader.SetFloat("MaxInfluenceRadius", MaxInfluenceRadius);
        RenderShader.SetFloat("ChunkNumW", ChunkNumW);
        RenderShader.SetFloat("ChunkNumH", ChunkNumH);
        RenderShader.SetFloat("ParticlesNum", ParticlesNum);
    }

    void SetRenderShaderBuffers()
    {
        if (ParticlesNum != 0) {
            RenderShader.SetBuffer(0, "Positions", PositionsBuffer);
            RenderShader.SetBuffer(0, "Velocities", VelocitiesBuffer);
        }

        // // Rigid bodies buffers - not implemented
        // if (RBodiesNum != 0) {
        //     SimShader.SetBuffer(1, "RBPositions", RBPositionsBuffer);
        //     SimShader.SetBuffer(1, "RBVelocities", RBVelocitiesBuffer);
        // }
        
    }
    void CPUSortChunkdata()
    {
        PositionsBuffer.GetData(Positions);

        for (int i = 0; i < ParticlesNum; i++)
        {
            int ChunkX = (int)Math.Floor(Positions[i].x / MaxInfluenceRadius);
            int ChunkY = (int)Math.Floor(Positions[i].y / MaxInfluenceRadius);
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
            int ParticleIndex = SpatialLookup[i].x;
            int ChunkKey = SpatialLookup[i].y;
            if (SpatialLookup[i].y != lastChunkKey)
            {
                StartIndices[SpatialLookup[i].y] = i;
                lastChunkKey = SpatialLookup[i].y;
            }
        }

        SpatialLookupBuffer.SetData(SpatialLookup);
        StartIndicesBuffer.SetData(StartIndices);

        if (ParticlesNum != 0) {
            SimShader.SetBuffer(1, "SpatialLookup", SpatialLookupBuffer);
            SimShader.SetBuffer(1, "StartIndices", StartIndicesBuffer);
        }
        if (RBodiesNum != 0) {
            SimShader.SetBuffer(3, "SpatialLookup", SpatialLookupBuffer);
            SimShader.SetBuffer(3, "StartIndices", StartIndicesBuffer);
        }
        if (ParticlesNum != 0) {
            SimShader.SetBuffer(4, "SpatialLookup", SpatialLookupBuffer);
            SimShader.SetBuffer(4, "StartIndices", StartIndicesBuffer);
        }

        if (ParticlesNum != 0) {
            RenderShader.SetBuffer(0, "SpatialLookup", SpatialLookupBuffer);
            RenderShader.SetBuffer(0, "StartIndices", StartIndicesBuffer);
        }
    }

    void RunSimShader()
    {
        UpdateSimShaderVariables();

        // Dispatch compute shader kernels
        if (ParticlesNum != 0) {SimShader.Dispatch(0, ParticlesNum, 1, 1);}
        if (ParticlesNum != 0) {SimShader.Dispatch(1, ParticlesNum, 1, 1);}
        if (RBodiesNum != 0) {SimShader.Dispatch(2, RBodiesNum, 1, 1);}
        if (RBodiesNum != 0) {SimShader.Dispatch(3, RBodiesNum, 1, 1);}
        if (ParticlesNum != 0) {SimShader.Dispatch(4, ParticlesNum, 1, 1);}

        // Example use: Retrieve PositionsBuffer to Positions
        // DensitiesBuffer.GetData(Densities);
        // NearDensitiesBuffer.GetData(NearDensities);
        // PredPositionsBuffer.GetData(PredPositions);
        // LastVelocitiesBuffer.GetData(LastVelocities);
    }

    void RunRenderShader()
    {
        UpdateRenderShaderVariables();

        // ThreadSize has to be the same in her as in RenderShader for correct rendering
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
        // if (RBodiesNum != 0) {SimShader.Dispatch(1, RBodiesNum, 1, 1);}
    }

    public void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        RunRenderShader();

        Graphics.Blit(renderTexture, dest);
    }

    void OnDestroy()
    {
        SpatialLookupBuffer?.Release();
        StartIndicesBuffer?.Release();
        PredPositionsBuffer?.Release();
        PositionsBuffer?.Release();
        VelocitiesBuffer?.Release();
        LastVelocitiesBuffer?.Release();
        DensitiesBuffer?.Release();
        NearDensitiesBuffer?.Release();

        RBPositionsBuffer?.Release();
        RBVelocitiesBuffer?.Release();
        RBRadiiBuffer?.Release();
        RBMassBuffer?.Release();

        ParticleImpulseStorageBuffer?.Release();
        ParticleTeleportStorageBuffer?.Release();
    }
}