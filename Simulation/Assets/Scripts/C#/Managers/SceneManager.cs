using System;
using System.Collections.Generic;
using System.Linq;
using Resources2;
using Unity.Mathematics;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    // Public
    public int MaxAtlasDims;

    // Private variables
    private Vector2 sceneMin;
    private Vector2 sceneMax;
    private bool referencesHaveBeenSet = false;

    // Private references
    private Transform sensorUIContainer;
    private Transform sensorOutlineContainer;
    private Main main;
    private SensorManager sensorManager;
    private Vector2 canvasResolution;

    private void SetReferences()
    {
        sensorUIContainer = GameObject.FindGameObjectWithTag("SensorUIContainer").GetComponent<Transform>();
        sensorOutlineContainer = GameObject.FindGameObjectWithTag("SensorOutlineContainer").GetComponent<Transform>();
        main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        sensorManager = GameObject.FindGameObjectWithTag("SensorManager").GetComponent<SensorManager>();
        Rect uiCanvasRect = GameObject.FindGameObjectWithTag("UICanvas").GetComponent<RectTransform>().rect;
        canvasResolution = new Vector2(uiCanvasRect.width, uiCanvasRect.height);

        referencesHaveBeenSet = true;
    }

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
        if (!referencesHaveBeenSet) SetReferences();

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

    public bool IsSpaceEmpty(Vector2 point, SceneFluid thisFluid, SceneRigidBody[] allRigidBodies, SceneFluid[] allFluids)
    {
        // Check whether the point if inside of any rigid body. If so, the rigid body will take priority for this point in space.
        foreach (SceneRigidBody rigidBody in allRigidBodies)
        {
            ColliderType colliderType = rigidBody.rbInput.colliderType;
            bool isFluidCollider = colliderType == ColliderType.Fluid || colliderType == ColliderType.All;
            if (rigidBody.IsPointInsidePolygon(point) && isFluidCollider) return false;
        }

        // Sort fluids with respect to the sibling indices
        SceneFluid[] sortedFluids = allFluids
            .OrderBy(fluid => fluid.transform.GetSiblingIndex())
            .ToArray();
        
        // Check if any other fluid with a lower sibling index occupies the same space.
        int thisFluidIndex = Array.IndexOf(sortedFluids, thisFluid);
        for (int i = 0; i < thisFluidIndex; i++)
        {
            if (sortedFluids[i].IsPointInsidePolygon(point)) return false;
        }

        return true;
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

        StringUtils.LogIfInEditor("Texture atlas constructed with " + rects.Length + " textures. Width: " + atlas.width + ". Height: " + atlas.height);

        int2 GetTexLoc(Rect rect) => new((int)(rect.x * atlas.width), (int)(rect.y * atlas.height));
        int2 GetTexDims(Rect rect) => new((int)(rect.width * atlas.width), (int)(rect.height * atlas.height));

        int rectIndex = 0;
        Mat[] renderMats = new Mat[matInputs.Length];
        for (int i = 0; i < matInputs.Length; i++)
        {
            MatInput matInput = matInputs[i];

            Mat mat;
            if (matInput.colorTexture != null)
            {
                Rect rect = rects[rectIndex];
                mat = InitMat(matInput, matInput.baseColor, GetTexLoc(rect), GetTexDims(rect));
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

    public PData[] GenerateParticles(int maxParticlesNum, float gridSpacing = 0)
    {
        if (maxParticlesNum == 0) return new PData[0];

        // Get all fluid instances
        SceneFluid[] allFluids = GetAllSceneFluids();

        Vector2 offset = GetBoundsOffset();

        // Get the particle positions for each fluid object in the scene
        List<PData> allPDatas = new();
        foreach (SceneFluid fluid in allFluids)
        {
            PData[] pDatas = fluid.GenerateParticles(offset, gridSpacing);

            foreach (var pData in pDatas)
            {
                allPDatas.Add(pData);
                if (--maxParticlesNum <= 0) return allPDatas.ToArray();
            }
        }

        return allPDatas.ToArray();
    }

    public (RBData[], RBVector[], SensorArea[]) CreateRigidBodies(float? rbCalcGridSpacingInput = null)
    {
        float rbCalcGridSpacing = rbCalcGridSpacingInput ?? 0.2f;

        if (!referencesHaveBeenSet) SetReferences();

        SceneRigidBody[] allRigidBodies = GetAllSceneRigidBodies();

        // Remove duplicate points
        foreach (SceneRigidBody rigidBody in allRigidBodies)
        {
            if (rigidBody.polygonCollider == null) rigidBody.polygonCollider = rigidBody.GetComponent<PolygonCollider2D>();
            rigidBody.polygonCollider.points = ArrayUtils.RemoveAdjacentDuplicates(rigidBody.polygonCollider.points);
        }

        Vector2 boundsOffset = GetBoundsOffset();

        // Get the rigidBody data for each rigidBody
        List<RBData> allRBData = new();
        List<RBVector> allRBVectors = new();
        List<Sensor> sensors = new();
        for (int i = 0; i < allRigidBodies.Length; i++)
        {
            SceneRigidBody rigidBody = allRigidBodies[i];

            if (!rigidBody.rbInput.includeInSimulation) continue;

            // Calculate the parent offset
            Transform transform = rigidBody.transform;
            Vector2 parentOffset = transform.position - transform.localPosition;

            // Transform points to local space
            Vector2 transformedRBPos = new Vector2(rigidBody.transform.position.x, rigidBody.transform.position.y) + boundsOffset;
            Vector2[] vectors = GetTransformedPoints(rigidBody, boundsOffset, transformedRBPos);
            if (rigidBody.addInBetweenPoints) AddInBetweenPoints(ref vectors, rigidBody.doRecursiveSubdivisison, rigidBody.minDstForSubDivision);

            (float inertia, float maxRadiusSqr) = rigidBody.ComputeInertiaAndBalanceRigidBody(ref vectors, ref transformedRBPos, boundsOffset, rbCalcGridSpacing);

            // Get the index of the rigid body linked via a spring
            RBInput rbInput = rigidBody.rbInput;
            int springLinkedRBIndex = rbInput.linkedRigidBody == null ? -1 : Array.IndexOf(allRigidBodies, rbInput.linkedRigidBody);
            if (rigidBody.rbInput.constraintType == ConstraintType.Spring && springLinkedRBIndex == -1) Debug.LogError("Linked rigid body not set. SceneRigidBody: " + rigidBody.name);
            else if (i == springLinkedRBIndex)
            {
                Debug.LogWarning("Attempted to link rigid body via spring to itself. Link will be removed");
                rbInput.constraintType = ConstraintType.None;
            }

            if (springLinkedRBIndex != -1 && rbInput.localLinkPosThisRB.x != 0 && rbInput.localLinkPosThisRB.y != 0 &&
                rbInput.localLinkPosOtherRB.x != 0 && rbInput.localLinkPosOtherRB.y != 0 && rbInput.constraintType == ConstraintType.Rigid)
                Debug.LogWarning("Rigid links should not have points with offsets from both linked rigid bodies. This may cause to simulation instabilities");
            
            // Initialize the rigid body data
            allRBData.Add(InitRBData(rigidBody.rbInput, inertia, maxRadiusSqr, springLinkedRBIndex, allRBVectors.Count, allRBVectors.Count + vectors.Length, transformedRBPos, parentOffset));
            
            // Initialize the rigid body vector datas
            foreach (Vector2 vector in vectors) allRBVectors.Add(new RBVector(vector, i));

            // Add sensor to sensors, while making sure there are no dupicate assignments
            foreach (var sensor in rigidBody.linkedSensors)
            {
                if (sensors.Contains(sensor)) Debug.LogWarning("Duplicate sensor rigid body assignments. Sensor name: " + sensor.name);
                else
                {
                    if (sensor is RigidBodySensor rigidBodySensor && rigidBodySensor != null)
                    {
                        rigidBodySensor.linkedRBIndex = i;
                        sensors.Add(sensor);
                        sensor.SetReferences(sensorUIContainer, sensorOutlineContainer, main, sensorManager, canvasResolution);
                        sensor.Initialize();
                    }
                }
            }
        }

        // Initialize fluid sensors and get related data
        List<SensorArea> sensorAreas = new();
        foreach (FluidSensor fluidSensor in sensorManager.enabledFluidSensors)
        {
            if (fluidSensor != null)
            {
                sensors.Add(fluidSensor);
                fluidSensor.SetReferences(sensorUIContainer, sensorOutlineContainer, main, sensorManager, canvasResolution);
                fluidSensor.Initialize();
                sensorAreas.Add(fluidSensor.GetSensorAreaData());
            }
        }

        // Assign sensors to sensorManager
        sensorManager.sensors = sensors;

        return (allRBData.ToArray(), allRBVectors.ToArray(), sensorAreas.ToArray());
    }

    public static void AddInBetweenPointsRecursively(ref Vector2[] vectors, float minDst)
    {
        bool needsSubdivision = false;
        List<Vector2> newVectors = new();

        int count = vectors.Length;

        for (int i = 0; i < count; i++)
        {
            Vector2 current = vectors[i];
            Vector2 next = vectors[(i + 1) % count];

            newVectors.Add(current);

            float distance = Vector2.Distance(current, next);

            if (distance > minDst)
            {
                // Compute the in-between point
                Vector2 inBetween = (current + next) / 2;

                newVectors.Add(inBetween);
                needsSubdivision = true;
            }
        }

        vectors = newVectors.ToArray();

        // Recursively call the function if any subdivisions were made
        if (needsSubdivision) AddInBetweenPointsRecursively(ref vectors, minDst);
    }

    public static void AddInBetweenPoints(ref Vector2[] vectors, bool doRecursiveSubdivisison, float minDst)
    {
        if (doRecursiveSubdivisison)
        {
            minDst = Mathf.Max(minDst, 0.5f);
            AddInBetweenPointsRecursively(ref vectors, minDst);
        }
        else
        {
            int count = vectors.Length;
            List<Vector2> newVectors = new();
            for (int i = 0; i < count; i++)
            {
                Vector2 current = vectors[i];
                Vector2 next = vectors[(i + 1) % count];

                newVectors.Add(current);

                Vector2 inBetween = (current + next) / 2;

                newVectors.Add(inBetween);
            }

            vectors = newVectors.ToArray();
        }
    }

    private Vector2[] GetTransformedPoints(SceneRigidBody rigidBody, Vector2 offset, Vector2 transformedRBPos)
    {
        Vector2[] vectors = rigidBody.GetComponent<PolygonCollider2D>().points;

        for (int i = 0; i < vectors.Length; i++) vectors[i] = (Vector2)rigidBody.transform.TransformPoint(vectors[i]) + offset - transformedRBPos;
        
        return vectors;
    }

    public static SceneRigidBody[] GetAllSceneRigidBodies()
    {
        GameObject[] rigidBodyObjects = GameObject.FindGameObjectsWithTag("RigidBody");
        SceneRigidBody[] allRigidBodies = new SceneRigidBody[rigidBodyObjects.Length];
        for (int i = 0; i < rigidBodyObjects.Length; i++) allRigidBodies[i] = rigidBodyObjects[i].GetComponent<SceneRigidBody>();

        return allRigidBodies;
    }

    public static SceneFluid[] GetAllSceneFluids()
    {
        GameObject[] fluidObjects = GameObject.FindGameObjectsWithTag("Fluid");
        SceneFluid[] allFluids = new SceneFluid[fluidObjects.Length];
        for (int i = 0; i < fluidObjects.Length; i++) allFluids[i] = fluidObjects[i].GetComponent<SceneFluid>();

        return allFluids;
    }

    private RBData InitRBData(RBInput rbInput, float inertia, float maxRadiusSqr, int linkedRBIndex, int startIndex, int endIndex, Vector2 pos, Vector2 parentOffset)
    {
        bool canMove = rbInput.canMove && rbInput.constraintType != ConstraintType.LinearMotor;
        bool isRBCollider = rbInput.colliderType == ColliderType.RigidBody || rbInput.colliderType == ColliderType.All;
        bool isFluidCollider = rbInput.colliderType == ColliderType.Fluid || rbInput.colliderType == ColliderType.All;
        bool isLinearMotor = rbInput.constraintType == ConstraintType.LinearMotor;
        bool isRigidConstraint = rbInput.constraintType == ConstraintType.Rigid;
        bool isSpringConstraint = rbInput.constraintType == ConstraintType.Spring;
        return new RBData
        {
            pos = pos,
            vel_AsInt2 = rbInput.canMove ? Func.Float2AsInt2(rbInput.velocity, main.FloatIntPrecisionRB) : 0,
            nextPos = 0,
            nextVel = 0,
            rotVel_AsInt = rbInput.canRotate ? Func.FloatAsInt(rbInput.angularVelocity, main.FloatIntPrecisionRB) : 0,
            totRot = 0,
            mass = canMove ? rbInput.mass : (isLinearMotor ? (rbInput.doRoundTrip ? -2 : -1) : 0),
            inertia = rbInput.canRotate ? inertia : 0,
            gravity = rbInput.gravity,
            rbElasticity = isRBCollider ? Mathf.Max(rbInput.rbElasticity, 0.05f) : -1,
            fluidElasticity = isFluidCollider ? Mathf.Max(rbInput.fluidElasticity, 0.05f) : -1,
            friction = isRBCollider ? rbInput.friction : -1,
            maxRadiusSqr = rbInput.isInteractable ? maxRadiusSqr : -maxRadiusSqr,
            startIndex = startIndex,
            endIndex = endIndex,
            // Inter-RB spring links
            linkedRBIndex = (isSpringConstraint || isRigidConstraint) ? linkedRBIndex : -1,
            springStiffness = isRigidConstraint ? 0 : rbInput.springStiffness,
            springRestLength = isRigidConstraint ? 0 : rbInput.springRestLength,
            damping = isRigidConstraint ? 0 : rbInput.damping,
            localLinkPosThisRB = isLinearMotor ? rbInput.startPos + parentOffset : rbInput.localLinkPosThisRB,
            localLinkPosOtherRB = isLinearMotor ? rbInput.endPos + parentOffset : rbInput.localLinkPosOtherRB,
            // Linear motor
            lerpSpeed = isLinearMotor ? rbInput.lerpSpeed : 0,
            lerpTimeOffset = rbInput.lerpTimeOffset,
            // Heating
            heatingStrength = rbInput.heatingStrength,
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
