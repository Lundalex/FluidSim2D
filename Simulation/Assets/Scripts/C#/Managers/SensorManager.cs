using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

public class SensorManager : MonoBehaviour
{
    [Range(10.0f, 100.0f)] public float msDataRetrievalInterval;

    // Retrieved data
    [NonSerialized] public RBData[] retrievedRBDatas;
    [NonSerialized] public PData[] retrievedPDatas;
    [NonSerialized] public int2[] SpatialLookupBuffer;
    [NonSerialized] public int[] StartIndicesBuffer;

    // References
    [NonSerialized] public List<Sensor> sensors;
    private Main main;

    private bool programRunning = false;
    private void Start()
    {
        main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();

        programRunning = true;
        StartCoroutine(RetrieveRigidBodyBufferDatasCoroutine());
        StartCoroutine(RetrieveParticleBufferDatasCoroutine());
    }

    IEnumerator RetrieveRigidBodyBufferDatasCoroutine()
    {
        while (programRunning)
        {
            // Retrieve rigid body data buffer asynchronously
            if (main.RBDataBuffer != null && sensors != null)
            {
                bool hasRigidBodySensor = sensors.OfType<RigidBodySensor>().Any();
                if (hasRigidBodySensor)
                {
                    ComputeHelper.GetBufferContentsAsync<RBData>(main.RBDataBuffer, contents => 
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
            }

            yield return new WaitForSeconds(msDataRetrievalInterval / 1000.0f);
        }
    }

    IEnumerator RetrieveParticleBufferDatasCoroutine()
    {
        while (programRunning)
        {
            // Retrieve rigid body data buffer asynchronously
            if (main.RBDataBuffer != null && sensors != null)
            {
                bool hasFluidSensor = sensors.OfType<FluidSensor>().Any();
                if (hasFluidSensor)
                {
                    ComputeHelper.GetBufferContentsAsync<PData>(main.PDataBuffer, contents => 
                    {
                        retrievedPDatas = contents;
                        foreach (Sensor sensor in sensors)
                        {
                            if (sensor is FluidSensor rigidBodySensor)
                            {
                                rigidBodySensor.UpdateSensor();
                            }
                        }
                    });
                }
            }

            yield return new WaitForSeconds(msDataRetrievalInterval / 1000.0f);
        }
    }

    void OnDestroy()
    {
        programRunning = false;
    }
}