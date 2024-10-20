using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SensorManager : MonoBehaviour
{
    [Range(10.0f, 100.0f)] public float msDataRetrievalInterval;

    // Retrieved data
    [NonSerialized] public RBData[] retrievedRBDatas;
    [NonSerialized] public List<Sensor> sensors;

    // Private
    private Main main;

    private bool programRunning = false;
    private void Start()
    {
        main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();

        programRunning = true;
        StartCoroutine(RetrieveBufferDatasCoroutine());
    }

    IEnumerator RetrieveBufferDatasCoroutine()
    {
        while (programRunning)
        {
            // Retrieve rigid body data buffer asynchronously
            if (main.RBDataBuffer != null && sensors != null)
            {
                ComputeHelper.GetBufferContents<RBData>(main.RBDataBuffer, contents => 
                {
                    retrievedRBDatas = contents;
                    foreach (Sensor sensor in sensors)
                    {
                        if (sensor is RigidBodySensor rigidBodySensor)
                        {
                            rigidBodySensor.UpdateSensor();
                        }
                    }
                });
            }

            yield return new WaitForSeconds(msDataRetrievalInterval / 1000.0f);
        }
    }

    void OnDestroy()
    {
        programRunning = false;
    }
}