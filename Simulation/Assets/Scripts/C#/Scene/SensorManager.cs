using System;
using System.Collections;
using UnityEngine;

public class SensorManager : MonoBehaviour
{
    [Range(10.0f, 100.0f)] public float msDataRetrievalInterval;

    // Retrieved data
    [NonSerialized] public RBData[] retrievedRBData;

    // Private
    private Main main;
    private Sensor[] sensors;

    private bool programRunning = false;
    private void Start()
    {
        main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();

        GameObject[] sensorObjects = GameObject.FindGameObjectsWithTag("Sensor");
        sensors = new Sensor[sensorObjects.Length];
        for (int i = 0; i < sensorObjects.Length; i++) sensors[i] = sensorObjects[i].GetComponent<Sensor>();

        programRunning = true;
        StartCoroutine(RetrieveBufferDatasCoroutine());
    }

    IEnumerator RetrieveBufferDatasCoroutine()
    {
        while (programRunning)
        {
            // Retrieve rigid body data buffer asynchronously
            if (main.RBDataBuffer != null)
            {
                ComputeHelper.GetBufferContents<RBData>(main.RBDataBuffer, contents => 
                {
                    retrievedRBData = contents;

                    foreach (Sensor sensor in sensors)
                    {
                        if (SensorHelper.CheckIfSensorRequiresDataOfType(sensor.sensorType, "RigidBody"))
                        {
                            sensor.UpdateSensor();
                        }
                    }

                    // Maybe call update functions for all dependant sensors?
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