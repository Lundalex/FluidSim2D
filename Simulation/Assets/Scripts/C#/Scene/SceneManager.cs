using System.Collections.Generic;
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
        SceneFluid[] allPolygons = new SceneFluid[1]{ GameObject.Find("Fluid").GetComponent<SceneFluid>() }; // Replace with a general solution
        List<PData> allPDatas = new();

        Vector2 pointOffset;
        pointOffset.x = transform.localScale.x * 0.5f - transform.position.x;
        pointOffset.y = transform.localScale.y * 0.5f - transform.position.y;

        foreach (SceneFluid polygon in allPolygons)
        {
            PData[] pDatas = polygon.GenerateParticles(pointOffset, gridDensity);

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
        return (new RBData[1], new RBVector[1]);
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
}
