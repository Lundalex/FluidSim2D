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

public class Main : MonoBehaviour
{
    [Header("Simulation settings")]
    public uint ParticlesNum;
    public uint RBodiesNum;
    public uint MaxInfluenceRadius;
    public float TargetDensity;
    public float PressureMultiplier;
    public float NearDensityMultiplier;
    public float Damping;
    public float Viscocity;
    public float Gravity;
    public float RBodyElasticity;

    [Header("Boundrary settings")]
    public int Width;
    public int Height;
    public int SpawnDims; // A x A
    public float BorderThickness;

    [Header("Rendering settings")]
    public bool FixedTimeStep;
    public float TimeStep;
    public float VisualParticleRadii;
    public uint RenderFrequency;

    [Header("Interaction settings")]
    public float MaxInteractionRadius;
    public float InteractionPower;

    [Header("References")]
    public GameObject ParticlePrefab;
    public ComputeShader RenderShader;
    public ComputeShader SimShader;

    // Private references
    Mesh mesh;
    private RenderTexture renderTexture;
    private GameObject simulationBoundrary;

    // Constants
    private uint ChunkNumW;
    private uint ChunkNumH;

    // Particles
    private (uint, uint)[] PChunks;
    public (float, float)[] PredPositions;
    private (float, float)[] Positions;
    private (float, float)[] Velocities;
    private float[] Densities;
    private float[] NearDensities;
    private (uint, uint)[] TemplatePChunks;

    // Particles - Buffers
    private ComputeBuffer PChunksBuffer;
    private ComputeBuffer PredPositionsBuffer;
    private ComputeBuffer PositionsBuffer;
    private ComputeBuffer VelocitiesBuffer;
    private ComputeBuffer DensitiesBuffer;
    private ComputeBuffer NearDensitiesBuffer;


    // Rigid Bodies
    private (float, float)[] RBPositions;
    private (float, float)[] RBVelocities;
    private float[] RBRadii;
    private float[] RBMass;

    // Rigid Bodies - Buffers
    private ComputeBuffer RBPositionsBuffer;
    private ComputeBuffer RBVelocitiesBuffer;
    private ComputeBuffer RBRadiiBuffer;
    private ComputeBuffer RBMassBuffer;


    // Other
    private float DeltaTime;

    // Buffer




    void Start()
    {
        CreateVisualBoundrary();

        Camera.main.transform.position = new Vector3(Width / 2, Height / 2, -1);
        Camera.main.orthographicSize = Mathf.Max(Width * 0.75f, Height * 1.5f);

        PChunks = new (uint, uint)[ParticlesNum];
        PredPositions = new (float, float)[ParticlesNum];
        Positions = new (float, float)[ParticlesNum];
        Velocities = new (float, float)[ParticlesNum];
        Densities = new float[ParticlesNum];
        NearDensities = new float[ParticlesNum];
        TemplatePChunks = new (uint, uint)[ParticlesNum];

        RBPositions = new (float, float)[RBodiesNum];
        RBVelocities = new (float, float)[RBodiesNum];
        RBRadii = new float[RBodiesNum];
        RBMass = new float[RBodiesNum];

        PChunksBuffer = new ComputeBuffer((int)ParticlesNum, sizeof(uint) * 2);
        PredPositionsBuffer = new ComputeBuffer((int)ParticlesNum, sizeof(float) * 2);
        PositionsBuffer = new ComputeBuffer((int)ParticlesNum, sizeof(float) * 2);
        VelocitiesBuffer = new ComputeBuffer((int)ParticlesNum, sizeof(float) * 2);
        DensitiesBuffer = new ComputeBuffer((int)ParticlesNum, sizeof(float));
        NearDensitiesBuffer = new ComputeBuffer((int)ParticlesNum, sizeof(float));

        RBPositionsBuffer = new ComputeBuffer((int)RBodiesNum, sizeof(float) * 2);
        RBVelocitiesBuffer = new ComputeBuffer((int)RBodiesNum, sizeof(float) * 2);
        RBRadiiBuffer = new ComputeBuffer((int)RBodiesNum, sizeof(float));
        RBMassBuffer = new ComputeBuffer((int)RBodiesNum, sizeof(float));

        ChunkNumW = (uint)Width / MaxInfluenceRadius;
        ChunkNumH = (uint)Height / MaxInfluenceRadius;

        SimShader.SetInt("ChunkNumW", (int)ChunkNumW);
        SimShader.SetInt("ChunkNumH", (int)ChunkNumH);

        for (int i = 0; i < ParticlesNum; i++)
        {
            Positions[i] = new(0.0f, 0.0f);
            Velocities[i] = new(0.0f, 0.0f);
            PredPositions[i] = new(0.0f, 0.0f);
            Densities[i] = 0.0f;
            NearDensities[i] = 0.0f;

            TemplatePChunks[i] = new(0, 0);
        }

        for (int i = 0; i < RBodiesNum; i++)
        {
            RBPositions[i] = new(0.0f, 0.0f);
            RBVelocities[i] = new(0.0f, 0.0f);
            RBRadii[i] = 3f;
            RBMass[i] = 10f;
        }

        for (int i = 0; i < ParticlesNum; i++)
        {
            Positions[i] = ParticleSpawnPosition(i, ParticlesNum);
        }
    }
    (float, float) ParticleSpawnPosition(int particle_index, uint max_index)
    {
        float x = (Width - SpawnDims) / 2 + Mathf.Floor(particle_index % Mathf.Sqrt(max_index)) * (SpawnDims / Mathf.Sqrt(max_index));
        float y = (Height - SpawnDims) / 2 + Mathf.Floor(particle_index / Mathf.Sqrt(max_index)) * (SpawnDims / Mathf.Sqrt(max_index));
        if (SpawnDims > Width || SpawnDims > Height)
        {
            throw new ArgumentException("Particle spawn dimensions larger than either border_width or border_height");
        }
        return (x, y);
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
        
    }
}