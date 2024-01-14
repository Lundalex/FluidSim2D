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
    [Range(0, 1)] public float RbElasticity;
    [Range(0, 0.1f)] public float LookAheadFactor;
    public float Viscocity;
    public float Gravity;
    public int PStorageLength;
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
    private int ChunkNumW;
    private int ChunkNumH;
    private int IOOR; // Index Out Of Range
    private int MSLen;
    private float MarchScale;

    // Particles - Properties
    private int2[] SpatialLookup; // [](particleIndex, chunkKey)
    private int[] StartIndices;
    private float2[] PredPositions;
    private float2[] Positions;
    private float2[] Velocities;
    private float2[] LastVelocities;
    private float[] Densities;
    private float[] NearDensities;
    private int2[] TemplateSpatialLookup;

    // Rigid Bodies - Properties
    private float2[] RBPositions;
    private float2[] RBVelocities;
    private float2[] RBProperties; // RBRadii, RBMass
    private float3[] ParticleImpulseStorage;
    private float3[] ParticleTeleportStorage;

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

    // Particles - Buffers
    private ComputeBuffer PredPositionsBuffer;
    private ComputeBuffer PositionsBuffer;
    private ComputeBuffer VelocitiesBuffer;
    private ComputeBuffer LastVelocitiesBuffer;
    private ComputeBuffer DensitiesBuffer;
    private ComputeBuffer NearDensitiesBuffer;

    // Rigid Bodies - Buffers
    private ComputeBuffer RBPositionsBuffer;
    private ComputeBuffer RBVelocitiesBuffer;
    private ComputeBuffer RBPropertiesBuffer;

    // Other
    private float DeltaTime;

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

        Camera.main.transform.position = new Vector3(Width / 2, Height / 2, -1);
        Camera.main.orthographicSize = Mathf.Max(Width * 0.75f, Height * 1.5f);

        marchingSquaresMesh = GetComponent<MeshFilter>().mesh;

        SetConstants();

        InitializeArrays();

        for (int i = 0; i < ParticlesNum; i++) {
            Positions[i] = ParticleSpawnPosition(i, ParticlesNum);
        }

        for (int i = 0; i < RBodiesNum; i++) {
            RBPositions[i] = RBodySpawnPosition(i, RBodiesNum);
        }

        InitializeBuffers();
        SetPSimShaderBuffers();
        SetRbSimShaderBuffers();
        SetRenderShaderBuffers();
        SetSortShaderBuffers();
        SetMarchingSquaresShaderBuffers();
    }

    void SetConstants()
    {
        ChunkNumW = Width / MaxInfluenceRadius;
        ChunkNumH = Height / MaxInfluenceRadius;
        IOOR = ParticlesNum;
        MarchW = (int)(Width / MSResolution);
        MarchH = (int)(Height / MSResolution);
        MSLen = MarchW * MarchH * TriStorageLength * 3;
    }

    void InitializeArrays()
    {
        SpatialLookup = new int2[ParticlesNum];
        StartIndices = new int[ParticlesNum];
        vertices = new Vector3[MSLen];
        triangles = new int[MSLen];
        colors = new Color[MSLen];
        MSPoints = new float[MSLen];

        PredPositions = new float2[ParticlesNum];
        Positions = new float2[ParticlesNum];
        Velocities = new float2[ParticlesNum];
        LastVelocities = new float2[ParticlesNum];
        Densities = new float[ParticlesNum];
        NearDensities = new float[ParticlesNum];
        TemplateSpatialLookup = new int2[ParticlesNum];

        RBPositions = new float2[RBodiesNum];
        RBVelocities = new float2[RBodiesNum];
        RBProperties = new float2[RBodiesNum];
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
            RBProperties[i] = new float2(radii, 14f);
        }

        for (int i = 0; i < RBodiesNum * PStorageLength; i++)
        {
            ParticleImpulseStorage[i] = new float3(0.0f, 0.0f, 0.0f);
            ParticleTeleportStorage[i] = new float3(0.0f, 0.0f, 0.0f);
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

    float2 RBodySpawnPosition(int rbIndex, int maxIndex)
    {
        float ctrDst = radii * 2;
        float x = Width / 2 + ctrDst * rbIndex - maxIndex * ctrDst / 2;
        float y = 0.7f * Height;
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
        GPUSortChunkData();
        // CPUSortChunkData();
        for (int i = 0; i < TimeStepsPerRender; i++)
        {
            RunPSimShader();
            RunRbSimShader();
        }
        if (RenderMarchingSquares)
        {
            RunMarchingSquaresShader();
        }
        // RunRenderShader() is called by OnRenderImage()
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
            RBPropertiesBuffer = new ComputeBuffer(RBodiesNum, sizeof(float) * 2);

            RBPositionsBuffer.SetData(RBPositions);
            RBVelocitiesBuffer.SetData(RBVelocities);
            RBPropertiesBuffer.SetData(RBProperties);
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
    }

    void SetPSimShaderBuffers()
    {
        if (ParticlesNum != 0) {
            // Kernel PreCalculations
            PSimShader.SetBuffer(0, "Positions", PositionsBuffer);
            PSimShader.SetBuffer(0, "Velocities", VelocitiesBuffer);
            PSimShader.SetBuffer(0, "LastVelocities", LastVelocitiesBuffer);
            PSimShader.SetBuffer(0, "PredPositions", PredPositionsBuffer);
        
            // Kernel PreCalculations   
            PSimShader.SetBuffer(1, "Positions", PositionsBuffer);
            PSimShader.SetBuffer(1, "Velocities", VelocitiesBuffer);
            PSimShader.SetBuffer(1, "Densities", DensitiesBuffer);
            PSimShader.SetBuffer(1, "NearDensities", NearDensitiesBuffer);
            PSimShader.SetBuffer(1, "PredPositions", PredPositionsBuffer);
            PSimShader.SetBuffer(1, "SpatialLookup", SpatialLookupBuffer);
            PSimShader.SetBuffer(1, "StartIndices", StartIndicesBuffer);

            // Kernel ParticleForces    
            PSimShader.SetBuffer(2, "Positions", PositionsBuffer);
            PSimShader.SetBuffer(2, "Velocities", VelocitiesBuffer);
            PSimShader.SetBuffer(2, "Densities", DensitiesBuffer);
            PSimShader.SetBuffer(2, "NearDensities", NearDensitiesBuffer);
            PSimShader.SetBuffer(2, "LastVelocities", LastVelocitiesBuffer);
            PSimShader.SetBuffer(2, "PredPositions", PredPositionsBuffer);
            PSimShader.SetBuffer(2, "SpatialLookup", SpatialLookupBuffer);
            PSimShader.SetBuffer(2, "StartIndices", StartIndicesBuffer);
        }
    }

    void SetRbSimShaderBuffers()
    {
        if (RBodiesNum != 0) {
            // Kernel RbRbCollisions
            RbSimShader.SetBuffer(0, "RBPositions", RBPositionsBuffer);
            RbSimShader.SetBuffer(0, "RBVelocities", RBVelocitiesBuffer);
            RbSimShader.SetBuffer(0, "RBProperties", RBPropertiesBuffer);

            // Kernel RbParticleCollisions
            RbSimShader.SetBuffer(1, "RBPositions", RBPositionsBuffer);
            RbSimShader.SetBuffer(1, "RBVelocities", RBVelocitiesBuffer);
            RbSimShader.SetBuffer(1, "RBProperties", RBPropertiesBuffer);
            RbSimShader.SetBuffer(1, "Positions", PositionsBuffer);
            RbSimShader.SetBuffer(1, "Velocities", VelocitiesBuffer);
            RbSimShader.SetBuffer(1, "SpatialLookup", SpatialLookupBuffer);
            RbSimShader.SetBuffer(1, "StartIndices", StartIndicesBuffer);
        }
    }

    void SetRenderShaderBuffers()
    {
        if (ParticlesNum != 0) {
            RenderShader.SetBuffer(0, "Positions", PositionsBuffer);
            RenderShader.SetBuffer(0, "Velocities", VelocitiesBuffer);
            RenderShader.SetBuffer(0, "SpatialLookup", SpatialLookupBuffer);
            RenderShader.SetBuffer(0, "StartIndices", StartIndicesBuffer);
        }

        if (RBodiesNum != 0) {
            RenderShader.SetBuffer(0, "RBPositions", RBPositionsBuffer);
            RenderShader.SetBuffer(0, "RBVelocities", RBVelocitiesBuffer);
            RenderShader.SetBuffer(0, "RBProperties", RBPropertiesBuffer);
        }
    }

    void SetSortShaderBuffers()
    {
        SpatialLookupBuffer.SetData(SpatialLookup);
        StartIndicesBuffer.SetData(StartIndices);
        
        SortShader.SetBuffer(0, "PredPositions", PredPositionsBuffer);
        SortShader.SetBuffer(0, "SpatialLookup", SpatialLookupBuffer);

        SortShader.SetBuffer(1, "PredPositions", PredPositionsBuffer);
        SortShader.SetBuffer(1, "SpatialLookup", SpatialLookupBuffer);

        SortShader.SetBuffer(2, "StartIndices", StartIndicesBuffer);

        SortShader.SetBuffer(3, "SpatialLookup", SpatialLookupBuffer);
        SortShader.SetBuffer(3, "StartIndices", StartIndicesBuffer);
    }

    void SetMarchingSquaresShaderBuffers()
    {
        MarchingSquaresShader.SetBuffer(0, "MSPoints", MSPointsBuffer);
        MarchingSquaresShader.SetBuffer(0, "Positions", PositionsBuffer);
        MarchingSquaresShader.SetBuffer(0, "Velocities", VelocitiesBuffer);
        MarchingSquaresShader.SetBuffer(0, "SpatialLookup", SpatialLookupBuffer);
        MarchingSquaresShader.SetBuffer(0, "StartIndices", StartIndicesBuffer);

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
        // cap DeltaTime to avoid instabilities caused by sudden lag spikes
        if (DeltaTime > 0.03f)
        {
            DeltaTime = 0.03f;
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
        PSimShader.SetInt("ChunkNumW", ChunkNumW);
        PSimShader.SetInt("ChunkNumH", ChunkNumH);
        PSimShader.SetInt("IOOR", IOOR);
        PSimShader.SetInt("Width", Width);
        PSimShader.SetInt("Height", Height);
        PSimShader.SetInt("ParticlesNum", ParticlesNum);
        PSimShader.SetInt("MaxInfluenceRadius", MaxInfluenceRadius);
        PSimShader.SetInt("SpawnDims", SpawnDims);
        PSimShader.SetInt("TimeStepsPerRender", TimeStepsPerRender);
        PSimShader.SetInt("PStorageLength", PStorageLength);
        PSimShader.SetFloat("LookAheadFactor", LookAheadFactor);
        PSimShader.SetFloat("TargetDensity", TargetDensity);
        PSimShader.SetFloat("PressureMultiplier", PressureMultiplier);
        PSimShader.SetFloat("NearPressureMultiplier", NearPressureMultiplier);
        PSimShader.SetFloat("Damping", Damping);
        PSimShader.SetFloat("Viscocity", Viscocity);
        PSimShader.SetFloat("Gravity", Gravity);
        PSimShader.SetFloat("BorderPadding", BorderPadding);
        PSimShader.SetFloat("MaxInteractionRadius", MaxInteractionRadius);
        PSimShader.SetFloat("InteractionAttractionPower", InteractionAttractionPower);
        PSimShader.SetFloat("InteractionFountainPower", InteractionFountainPower);

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

        RbSimShader.SetFloat("Damping", Damping);
        RbSimShader.SetFloat("Gravity", Gravity);
        RbSimShader.SetFloat("RbElasticity", RbElasticity);
        RbSimShader.SetFloat("BorderPadding", BorderPadding);
        RbSimShader.SetFloat("DeltaTime", DeltaTime);
    }

    void UpdateRenderShaderVariables()
    {
        RenderShader.SetFloat("VisualParticleRadii", VisualParticleRadii);
        RenderShader.SetInt("ResolutionX", ResolutionX);
        RenderShader.SetInt("ResolutionY", ResolutionY);
        RenderShader.SetInt("Width", Width);
        RenderShader.SetInt("Height", Height);
        RenderShader.SetInt("MaxInfluenceRadius", MaxInfluenceRadius);
        RenderShader.SetInt("ChunkNumW", ChunkNumW);
        RenderShader.SetInt("ChunkNumH", ChunkNumH);
        RenderShader.SetInt("ParticlesNum", ParticlesNum);

        RenderShader.SetInt("RBodiesNum", RBodiesNum);
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

    void GPUSortChunkData()
    {
        if (ParticlesNum == 0) {return;}
        UpdateSortShaderVariables();

        SortShader.Dispatch(0, ParticlesNum, 1, 1);

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

                int hLen = ParticlesNum / 2;
                SortShader.Dispatch(1, hLen, 1, 1);

                blockLen /= 2;
            }
            basebBlockLen *= 2;
        }

        SortShader.Dispatch(2, ParticlesNum, 1, 1);

        SortShader.Dispatch(3, ParticlesNum, 1, 1);
    }

    void CPUSortChunkData()
    {
        PredPositionsBuffer.GetData(PredPositions);

        for (int i = 0; i < ParticlesNum; i++)
        {
            int ChunkX = (int)(PredPositions[i].x / MaxInfluenceRadius);
            int ChunkY = (int)(PredPositions[i].y / MaxInfluenceRadius);
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
            // This can probably be optimised by sending to a compute shader [i->ParticlesNum] and have it check whether the ChunkKey[i-1] == ChunkKey[i], and if so, set StartIndices[ChunkKey] = i;
            if (ChunkKey != lastChunkKey)
            {
                StartIndices[ChunkKey] = i;
                lastChunkKey = ChunkKey;
            }
        }

        // SpatialLookupBuffer.SetData(SpatialLookup);
        // StartIndicesBuffer.SetData(StartIndices);

        if (ParticlesNum != 0) {
            PSimShader.SetBuffer(1, "SpatialLookup", SpatialLookupBuffer);
            PSimShader.SetBuffer(1, "StartIndices", StartIndicesBuffer);
        }
        if (RBodiesNum != 0) {
            PSimShader.SetBuffer(3, "SpatialLookup", SpatialLookupBuffer);
            PSimShader.SetBuffer(3, "StartIndices", StartIndicesBuffer);
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

        if (ParticlesNum != 0) {PSimShader.Dispatch(0, ParticlesNum, 1, 1);}
        if (ParticlesNum != 0) {PSimShader.Dispatch(1, ParticlesNum, 1, 1);}
        if (ParticlesNum != 0) {PSimShader.Dispatch(2, ParticlesNum, 1, 1);}
    }

    void RunRbSimShader()
    {
        UpdateRbSimShaderVariables();

        if (RBodiesNum != 0) {RbSimShader.Dispatch(0, RBodiesNum, 1, 1);} //RbRb collisions
        if (RBodiesNum != 0 && ParticlesNum != 0) {RbSimShader.Dispatch(1, RBodiesNum, 1, 1);} // RbParticle collisions
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
    }

    void RunRenderShader()
    {
        UpdateRenderShaderVariables();

        // ThreadSize has to be the same here as in RenderShader for correct rendering
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

        PredPositionsBuffer?.Release();
        PositionsBuffer?.Release();
        VelocitiesBuffer?.Release();
        LastVelocitiesBuffer?.Release();
        DensitiesBuffer?.Release();
        NearDensitiesBuffer?.Release();

        RBPositionsBuffer?.Release();
        RBVelocitiesBuffer?.Release();
        RBPropertiesBuffer?.Release();
    }
}