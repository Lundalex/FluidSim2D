using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Resources2;

public class SensorManager : MonoBehaviour
{
    public FluidSensor[] enabledFluidSensors;
    [Range(10.0f, 100.0f)] public float msRigidBodyDataRetrievalInterval;
    [Range(10.0f, 100.0f)] public float msFluidDataRetrievalInterval;
    [Range(20.0f, 500.0f)] public float msGraphPointSubmissionFrequency;
    [Range(100.0f, 2000.0f)] public float msGraphUpdateFrequency;

    // Retrieved data
    [NonSerialized] public RBData[] retrievedRBDatas;
    [NonSerialized] public RecordedFluidData[] retrievedFluidDatas;

    // Graph charts
    private List<GraphController> graphControllers = new();

    // References
    [NonSerialized] public List<Sensor> sensors;
    private Main main;

    private bool programRunning = false;
    public void StartScript(Main main)
    {
        this.main = main;

        programRunning = true;
        StartCoroutine(RetrieveRigidBodyBufferDatasCoroutine());
        StartCoroutine(RetrieveParticleBufferDatasCoroutine());
        StartCoroutine(UpdateGraphsCoroutine());
    }

    public void SubscribeGraphToCoroutine(GraphController graphController) => graphControllers.Add(graphController);

    private IEnumerator RetrieveRigidBodyBufferDatasCoroutine()
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

            yield return new WaitForSeconds(Func.MsToSeconds(msRigidBodyDataRetrievalInterval));
        }
    }

    private IEnumerator RetrieveParticleBufferDatasCoroutine()
    {
        while (programRunning)
        {
            // Retrieve rigid body data buffer asynchronously
            if (main.RecordedFluidDataBuffer != null && sensors != null)
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

            yield return new WaitForSeconds(Func.MsToSeconds(msFluidDataRetrievalInterval));
        }
    }

    private IEnumerator UpdateGraphsCoroutine()
    {
        int graphCount = 0;
        while (programRunning)
        {
            if (graphControllers.Count > 0)
            {
                graphCount++;
                graphCount %= graphControllers.Count;

                graphControllers[graphCount].UpdateGraph();
            }

            yield return new WaitForSeconds(Func.MsToSeconds(msGraphUpdateFrequency / (graphControllers.Count > 0 ? graphControllers.Count : 100)));
        }
    }

    void OnDestroy() => programRunning = false;
}