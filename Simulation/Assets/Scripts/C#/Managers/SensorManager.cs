using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class SensorManager : MonoBehaviour
{
    [Range(10.0f, 100.0f)] public float msRigidBodyDataRetrievalInterval;
    [Range(10.0f, 100.0f)] public float msFluidDataRetrievalInterval;

    // Retrieved data
    [NonSerialized] public RBData[] retrievedRBDatas;
    [NonSerialized] public RecordedFluidData[] retrievedFluidDatas;

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

            yield return new WaitForSeconds(msRigidBodyDataRetrievalInterval / 1000.0f);
        }
    }

    IEnumerator RetrieveParticleBufferDatasCoroutine()
    {
        while (programRunning)
        {
            // Retrieve rigid body data buffer asynchronously
            if (main.RecordedFluidDataBuffer != null && sensors is AnyState fluidsensor!= null)
            {
                bool hasFluidSensor = sensors.OfType<FluidSensor>().Any();
                if (hasFluidSensor)
                {
                    ComputeHelper.GetBufferContentsAsync<RecordedFluidData>(main.RecordedFluidDataBuffer, contents => 
                    {
                        retrievedFluidDatas = contents;
                        foreach (Sensor sensor in sensors)
                        {
                            if (sensor is FluidSensor fluidSensor)
                            {
                                fluidSensor.UpdateSensor();
                            }
                        }
                    });
                }
            }

            yield return new WaitForSeconds(msFluidDataRetrievalInterval / 1000.0f);
        }
    }

    void OnDestroy()
    {
        programRunning = false;
    }
}