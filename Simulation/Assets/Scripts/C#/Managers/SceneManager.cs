using System;
using System.Collections.Generic;
using Resources;
using Unity.Mathematics;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    public int MaxAtlasDims;
    Vector2 sceneMin;
    Vector2 sceneMax;
    Main main;
    SensorManager sensorManager;
    public int2 GetBounds(int maxInfluenceRadius)
    {
        int2 bounds = new(Mathf.CeilToInt(transform.localScale.x), Mathf.CeilToInt(transform.localScale.y));

        int2 boundsMod = bounds % maxInfluenceRadius;

        // Round bounds up to next multiple of maxInfluenceRadius
        if (boundsMod.x != 0) bounds.x += maxInfluenceRadius - boundsMod.x;
        if (boundsMod.y != 0) bounds.y += maxInfluenceRadius - boundsMod.y;

        return bounds;
    }

    public bool IsPointInsideBounds(Vector2 point)
    {
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();

        sceneMin.x = transform.position.x - transform.localScale.x * 0.5f + main.FluidPadding;
        sceneMin.y = transform.position.y - transform.localScale.y * 0.5f + main.FluidPadding;
        sceneMax.x = transform.position.x + transform.localScale.x * 0.5f - main.FluidPadding;
        sceneMax.y = transform.position.y + transform.localScale.y * 0.5f - main.FluidPadding;

        bool isInsideBounds = point.x > sceneMin.x
                              && point.y > sceneMin.y
                              && point.x < sceneMax.x
                              && point.y < sceneMax.y;

        return isInsideBounds;
    }

    public (Texture2D, Mat[]) ConstructTextureAtlas(MatInput[] matInputs)
    {
        List<Texture2D> textures = new();
        foreach (MatInput mat in matInputs)
        {
            if (mat.colorTexture != null)
            {
                if (!mat.colorTexture.isReadable) Debug.LogWarning("Color Texture " + mat.colorTexture.name + " is not readable. Read/Write needs to be set to true");
                textures.Add(mat.colorTexture);
            }
        }

        Texture2D atlas = new(MaxAtlasDims, MaxAtlasDims, TextureFormat.RGBAHalf, false);
        Rect[] rects = new Rect[0];
        if (textures.Count > 0) rects = atlas.PackTextures(textures.ToArray(), 1, MaxAtlasDims);

        Debug.Log("Texture atlas constructed with " + rects.Length + " textures. Width: " + atlas.width + ". Height: " + atlas.height);

        int2 GetTexLoc(int rectIndex) => new((int)(rects[rectIndex].x * atlas.width), (int)(rects[rectIndex].y * atlas.height));
        int2 GetTexDims(int rectIndex) => new((int)(rects[rectIndex].width * atlas.width), (int)(rects[rectIndex].height * atlas.height));

        int rectIndex = 0;
        Mat[] renderMats = new Mat[matInputs.Length];
        for (int i = 0; i < matInputs.Length; i++)
        {
            MatInput matInput = matInputs[i];

            Mat mat = new();
            if (matInput.colorTexture != null)
            {
                mat = InitMat(matInput, matInput.baseColor, GetTexLoc(rectIndex), GetTexDims(rectIndex));
                rectIndex++;
            }
            else mat = InitMat(matInput, matInput.baseColor, -1, -1);

            renderMats[i] = mat;
        }

        return (atlas, renderMats);
    }

    private Mat InitMat(MatInput matInput, float3 baseCol, int2 colTexLoc, int2 colTexDims)
    {
        return new Mat
        {
            colTexLoc = colTexLoc,
            colTexDims = colTexDims,
            colTexUpScaleFactor = matInput.colorTextureUpScaleFactor,
            baseCol = baseCol,
            opacity = Mathf.Clamp(matInput.opacity, 0.0f, 1.0f),
            sampleColMul = matInput.sampleColorMultiplier,
            edgeCol = matInput.edgeColor
        };
    }

    public PData[] GenerateParticles(int maxParticlesNum, float gridDensity = 0)
    {
        // Get all fluid instances
        GameObject[] fluidObjects = GameObject.FindGameObjectsWithTag("Fluid");
        SceneFluid[] allFluids = new SceneFluid[fluidObjects.Length];
        for (int i = 0; i < fluidObjects.Length; i++) allFluids[i] = fluidObjects[i].GetComponent<SceneFluid>();
        
        List<PData> allPDatas = new();

        Vector2 offset = GetBoundsOffset();

        // Get the particle positions for each fluid object in the scene
        foreach (SceneFluid fluid in allFluids)
        {
            PData[] pDatas = fluid.GenerateParticles(offset, gridDensity);

            foreach (var pData in pDatas)
            {
                allPDatas.Add(pData);
                if (--maxParticlesNum <= 0) return allPDatas.ToArray();
            }
        }

        return allPDatas.ToArray();
    }

    public (RBData[], RBVector[], SensorArea[]) CreateRigidBodies(float? rbCalcGridDensityInput = null)
    {
        float rbCalcGridDensity = rbCalcGridDensityInput ?? 0.2f;

        if (sensorManager == null) sensorManager = GameObject.FindGameObjectWithTag("SensorManager").GetComponent<SensorManager>();

        GameObject[] rigidBodyObjects = GameObject.FindGameObjectsWithTag("RigidBody");
        SceneRigidBody[] allRigidBodies = new SceneRigidBody[rigidBodyObjects.Length];
        for (int i = 0; i < rigidBodyObjects.Length; i++) allRigidBodies[i] = rigidBodyObjects[i].GetComponent<SceneRigidBody>();

        Vector2 offset = GetBoundsOffset();

        // Get the rigidBody data for each rigidBody
        List<RBData> allRBData = new();
        List<RBVector> allRBVectors = new();
        List<Sensor> sensors = new();
        for (int i = 0; i < allRigidBodies.Length; i++)
        {
            SceneRigidBody rigidBody = allRigidBodies[i];

            if (!rigidBody.RBInput.includeInSimulation) continue;

            // Transform points to local space
            Vector2 transformedRBPos = new Vector2(rigidBody.transform.position.x, rigidBody.transform.position.y) + offset;
            Vector2[] vectors = GetTransformedPoints(rigidBody, offset, transformedRBPos);

            (float inertia, float maxRadiusSqr) = rigidBody.ComputeInertiaAndBalanceRigidBody(ref vectors, ref transformedRBPos, offset, rbCalcGridDensity);

            // Get the index of the rigid body linked via a spring
            RBInput rbInput = rigidBody.RBInput;
            int springLinkedRBIndex = Array.IndexOf(allRigidBodies, rbInput.linkedRigidBody);
            if (rigidBody.RBInput.enableSpringLink && springLinkedRBIndex == -1) Debug.LogError("Linked rigid body not set. SceneRigidBody: " + rigidBody.name);
            else if (i == springLinkedRBIndex)
            {
                Debug.LogWarning("Attempted to link rigid body via spring to itself. Link will be removed");
                rbInput.enableSpringLink = false;
            }

            // Initialize the rigid body data
            allRBData.Add(InitRBData(rigidBody.RBInput, inertia, maxRadiusSqr, springLinkedRBIndex, allRBVectors.Count, allRBVectors.Count + vectors.Length, transformedRBPos));
            
            // Initialize the rigid body vector datas
            foreach (Vector2 vector in vectors) allRBVectors.Add(new RBVector(vector, i));

            // Add sensor to sensors, while making sure there are no dupicate assignments
            foreach (var sensor in rigidBody.LinkedSensors)
            {
                if (sensors.Contains(sensor)) Debug.LogWarning("Duplicate sensor rigid body assignments. Sensor name: " + sensor.name);
                else
                {
                    if (sensor is RigidBodySensor rigidBodySensor)
                    rigidBodySensor.linkedRBIndex = i;
                    sensors.Add(sensor);
                    sensor.StartSensor();
                }
            }
        }

        // Initialize fluid sensors and get related data
        List<SensorArea> sensorAreas = new();
        foreach (FluidSensor fluidSensor in sensorManager.enabledFluidSensors)
        {
            sensors.Add(fluidSensor);
            fluidSensor.StartSensor();
            sensorAreas.Add(fluidSensor.GetSensorAreaData());
        }

        // Assign sensors to sensorManager
        sensorManager.sensors = sensors;

        return (allRBData.ToArray(), allRBVectors.ToArray(), sensorAreas.ToArray());
    }

    private Vector2[] GetTransformedPoints(SceneRigidBody rigidBody, Vector2 offset, Vector2 transformedRBPos)
    {
        Vector2[] vectors = rigidBody.GetComponent<PolygonCollider2D>().points;

        for (int i = 0; i < vectors.Length; i++) vectors[i] = (Vector2)rigidBody.transform.TransformPoint(vectors[i]) + offset - transformedRBPos;
        
        return vectors;
    }

    private RBData InitRBData(RBInput rbInput, float inertia, float maxRadiusSqr, int linkedRBIndex, int startIndex, int endIndex, Vector2 pos)
    {
        return new RBData
        {
            pos = pos,
            vel_AsInt2 = rbInput.canMove ? Func.Float2AsInt2(rbInput.velocity, main.FloatIntPrecisionRB) : 0,
            nextPos = 0,
            nextVel = 0,
            rotVel_AsInt = rbInput.canRotate ? Func.FloatAsInt(rbInput.rotationVelocity, main.FloatIntPrecisionRB) : 0,
            totRot = 0,
            mass = rbInput.canMove ? rbInput.mass : 0,
            inertia = rbInput.canRotate ? inertia : 0,
            gravity = rbInput.gravity,
            elasticity = rbInput.elasticity,
            maxRadiusSqr = maxRadiusSqr,
            startIndex = startIndex,
            endIndex = endIndex,
            // Inter-RB spring links
            linkedRBIndex = rbInput.enableSpringLink ? linkedRBIndex : -1,
            springStiffness = rbInput.rigidConstraint ? 0 : rbInput.springStiffness,
            springRestLength = rbInput.rigidConstraint ? 0 : rbInput.springRestLength,
            damping = rbInput.rigidConstraint ? 0 : rbInput.damping,
            localLinkPosThisRB = rbInput.localLinkPosThisRB,
            localLinkPosOtherRB = rbInput.localLinkPosOtherRB,
            // Recorded spring force
            recordedSpringForce = 0,
            // Display
            renderPriority = rbInput.renderPriority,
            matIndex = rbInput.matIndex,
            springMatIndex = rbInput.springMatIndex
        };
    }

    private Vector2 GetBoundsOffset() => new(transform.localScale.x * 0.5f - transform.position.x, transform.localScale.y * 0.5f - transform.position.y);
}
