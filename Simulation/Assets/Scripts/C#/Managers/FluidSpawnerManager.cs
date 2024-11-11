using UnityEngine;
using System.Collections.Generic;
using PM = ProgramManager;
using Resources2;

public class FluidSpawnerManager : MonoBehaviour
{
    // Public
    public FluidSpawner[] enabledFluidSpawners;

    // Private
    private Main main;
    private List<float> timers = new();

    public void StartScript()
    {
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
    }

    public void UpdateScript()
    {
        for (int i = 0; i < enabledFluidSpawners.Length; i++)
        {
            if (i >= timers.Count) timers.Add(0);

            float timer = timers[i];
            FluidSpawner fluidSpawner = enabledFluidSpawners[i];

            timer += Func.SecondsToMs(PM.Instance.clampedDeltaTime);
            if (timer > fluidSpawner.msSpawnInterval)
            {
                timer = 0;
                main.SubmitParticlesToSimulation(fluidSpawner.GenerateParticles());
            }

            timers[i] = timer;
        }
    }
}
