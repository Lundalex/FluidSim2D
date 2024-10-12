using System.Collections.Generic;
using Resources;
using Unity.Mathematics;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    public Vector2 TEMP;
    Vector2 sceneMin;
    Vector2 sceneMax;
    Main main;
    public int2 GetBounds(int maxInfluenceRadius)
    {
        int2 bounds = new(Mathf.CeilToInt(transform.localScale.x), Mathf.CeilToInt(transform.localScale.y));

        bounds.x += maxInfluenceRadius - bounds.x % maxInfluenceRadius;
        bounds.y += maxInfluenceRadius - bounds.y % maxInfluenceRadius;

        return bounds;
    }

    public bool IsPointInsideBounds(Vector2 point)
    {
        if (main == null) main = GameObject.Find("Main Camera").GetComponent<Main>();

        sceneMin.x = transform.position.x - transform.localScale.x * 0.5f + main.BorderPadding;
        sceneMin.y = transform.position.y - transform.localScale.y * 0.5f + main.BorderPadding;
        sceneMax.x = transform.position.x + transform.localScale.x * 0.5f - main.BorderPadding;
        sceneMax.y = transform.position.y + transform.localScale.y * 0.5f - main.BorderPadding;

        bool isInsideBounds = point.x > sceneMin.x
                              && point.y > sceneMin.y
                              && point.x < sceneMax.x
                              && point.y < sceneMax.y;

        return isInsideBounds;
    }

    public PData[] GenerateParticles(int maxParticlesNum, float gridDensity = 0)
    {
        SceneFluid[] allFluids = new SceneFluid[1]{ GameObject.Find("Fluid").GetComponent<SceneFluid>() }; // Replace with a general solution
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

    public (RBData[], RBVector[]) GenerateRigidBodies(float? rbCalcGridDensityInput = null)
    {
        float rbCalcGridDensity = rbCalcGridDensityInput ?? 0.2f;

        SceneRigidBody[] allRigidBodies = new SceneRigidBody[1]{ GameObject.Find("RigidBody").GetComponent<SceneRigidBody>() }; // Replace with a general solution

        Vector2 offset = GetBoundsOffset();

        // Get the rigidBody data for each rigidBody
        List<RBData> allRBData = new();
        List<RBVector> allRBVectors = new();
        for (int i = 0; i < allRigidBodies.Length; i++)
        {
            SceneRigidBody rigidBody = allRigidBodies[i];

            // Transform points to local space
            Vector2 transformedRBPos = new Vector2(rigidBody.transform.position.x, rigidBody.transform.position.y) + offset;
            Vector2[] vectors = GetTransformedPoints(rigidBody, offset, transformedRBPos);

            (float inertia, float maxRadiusSqr) = rigidBody.ComputeInertiaAndBalanceRB(ref vectors, ref transformedRBPos, offset, rbCalcGridDensity);

            // Initialize the rigid body data
            allRBData.Add(InitRBData(rigidBody.RBInput, inertia, maxRadiusSqr, allRBVectors.Count, allRBVectors.Count + vectors.Length, transformedRBPos));
            
            // Initialize the rigid body vector datas
            foreach (Vector2 vector in vectors) allRBVectors.Add(new RBVector(vector, i));
        }

        return (allRBData.ToArray(), allRBVectors.ToArray());
    }

    private Vector2[] GetTransformedPoints(SceneRigidBody rigidBody, Vector2 offset, Vector2 transformedRBPos)
    {
        Vector2[] vectors = rigidBody.GetComponent<PolygonCollider2D>().points;

        for (int i = 0; i < vectors.Length; i++) vectors[i] = (Vector2)rigidBody.transform.TransformPoint(vectors[i]) + offset - transformedRBPos;
        
        return vectors;
    }

    private RBData InitRBData(RBInput rbInput, float inertia, float maxRadiusSqr, int startIndex, int endIndex, Vector2 pos)
    {
        return new RBData
        {
            pos = pos,
            vel_AsInt = rbInput.isPassive ? 0 : Func.Float2AsInt2(rbInput.velocity),
            nextPos = 0,
            nextVel = 0,
            rotVel_AsInt = rbInput.isPassive ? 0 : Func.FloatAsInt(rbInput.rotationVelocity),
            mass = rbInput.isPassive ? 0 : rbInput.mass,
            inertia = inertia,
            gravity = rbInput.gravity,
            elasticity = rbInput.elasticity,
            maxRadiusSqr = maxRadiusSqr,
            startIndex = startIndex,
            endIndex = endIndex,
            col = Func.ColorToFloat3(rbInput.color)
        };
    }

    private Vector2 GetBoundsOffset() => new(transform.localScale.x * 0.5f - transform.position.x, transform.localScale.y * 0.5f - transform.position.y);
}
