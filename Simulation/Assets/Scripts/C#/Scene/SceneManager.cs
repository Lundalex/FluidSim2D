using System.Collections.Generic;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    Vector2 sceneMin;
    Vector2 sceneMax;
    Main main;
    public Vector2[] GenerateSpawnPoints(int maxParticlesNum, float offset = 0)
    {
        SceneFluid[] allPolygons = new SceneFluid[1]{ GameObject.Find("Fluid").GetComponent<SceneFluid>() }; // Replace with a general solution
        List<Vector2> spawnPoints = new();

        Vector2 pointOffset;
        pointOffset.x = transform.localScale.x * 0.5f - transform.position.x;
        pointOffset.y = transform.localScale.y * 0.5f - transform.position.y;

        foreach (SceneFluid polygon in allPolygons)
        {
            Vector2[] points = polygon.GeneratePoints(offset);

            foreach (var point in points)
            {
                spawnPoints.Add(point + pointOffset);
                if (maxParticlesNum-- <= 0) return spawnPoints.ToArray();
            }
        }

        return spawnPoints.ToArray();
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
