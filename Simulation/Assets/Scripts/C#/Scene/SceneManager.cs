using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    Vector2 sceneMin;
    Vector2 sceneMax;
    Main main;
    public (int, int) GetBounds(int maxInfluenceRadius)
    {
        Vector2Int bounds = new(Mathf.CeilToInt(transform.localScale.x), Mathf.CeilToInt(transform.localScale.y));

        bounds.x += maxInfluenceRadius - bounds.x % maxInfluenceRadius;
        bounds.y += maxInfluenceRadius - bounds.y % maxInfluenceRadius;

        return (bounds.x, bounds.y);
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
                if (maxParticlesNum-- <= 0) return allPDatas.ToArray();
            }
        }

        return allPDatas.ToArray();
    }

    public (RBData[], RBVector[]) GenerateRigidBodies()
    {
        SceneRigidBody[] allRigidBodies = new SceneRigidBody[1]{ GameObject.Find("RigidBody").GetComponent<SceneRigidBody>() }; // Replace with a general solution

        Vector2 offset = GetBoundsOffset();

        // Get the rigidBody data for each rigidBody
        List<RBData> allRBData = new();
        List<RBVector> allRBVectors = new();
        foreach (SceneRigidBody rigidBody in allRigidBodies)
        {
            // Transform points to local space
            Vector2 transformedRBPos = new Vector2(rigidBody.transform.position.x, rigidBody.transform.position.y) + offset;
            Vector2[] points = GetTransformedPoints(rigidBody, offset, transformedRBPos);

            // Initialize the rigid body data
            allRBData.Add(InitRBData(rigidBody.RBInput, GetMaxRadiusSqr(points), allRBVectors.Count, allRBVectors.Count + points.Length, transformedRBPos));
            
            // Initialize the rigid body vector datas
            foreach (Vector2 point in points) allRBVectors.Add(new RBVector(point));
        }

        return (allRBData.ToArray(), allRBVectors.ToArray());
    }

    public Vector2[] GetTransformedPoints(SceneRigidBody rigidBody, Vector2 offset, Vector2 transformedRBPos)
    {
        Vector2[] points = rigidBody.GetComponent<PolygonCollider2D>().points;

        for (int i = 0; i < points.Length; i++) points[i] = rigidBody.transform.TransformPoint(points[i]);
        for (int i = 0; i < points.Length; i++) points[i] += offset - transformedRBPos;
        
        return points;
    }

    public float GetMaxRadiusSqr(Vector2[] points)
    {
        float maxRadiusSqr = 0;
        foreach (Vector2 point in points) maxRadiusSqr = Mathf.Max(maxRadiusSqr, point.sqrMagnitude);

        return maxRadiusSqr;
    }

    public RBData InitRBData(RBInput rbInput, float maxRadiusSqr, int startIndex, int endIndex, Vector2 pos)
    {
        return new RBData
        {
            pos = pos,
            vel = rbInput.velocity,
            nextPos = 0,
            nextVel = 0,
            mass = rbInput.isStationary ? 0 : rbInput.mass,
            maxRadiusSqr = maxRadiusSqr,
            startIndex = startIndex,
            endIndex = endIndex
        };
    }

    public Vector2 GetBoundsOffset() => new(transform.localScale.x * 0.5f - transform.position.x, transform.localScale.y * 0.5f - transform.position.y);

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
}
