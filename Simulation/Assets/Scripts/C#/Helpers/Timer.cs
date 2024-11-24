using UnityEngine;
using PM = ProgramManager;

public class Timer
{
    private float time;
    private readonly float threshold;
    private readonly bool useClampedTime;
    private readonly bool resetTimerOnThresholdReached;

    /// <summary>A timer which is automatically subscribed to the program update life cycle</summary>
    public Timer(float threshold, bool useClampedTime = true, bool resetTimerOnThresholdReached = true, float time = 0)
    {
        this.threshold = threshold;
        this.useClampedTime = useClampedTime;
        this.resetTimerOnThresholdReached = resetTimerOnThresholdReached;
        this.time = time;
        
        PM.Instance.OnProgramUpdate += Update;
    }

    private void Update(bool doUpdateClampedTime)
    {
        if (!doUpdateClampedTime && useClampedTime) return;
        time += useClampedTime ? PM.Instance.clampedDeltaTime : Time.deltaTime;
    }

    /// <summary>Check whether the internal accumulated time has exceeded the threshold</summary>
    public bool Check(bool resetIfThresholdReached = true)
    {
        if (time >= threshold)
        {
            // Reset/subtract from the accumulated time
            if (resetIfThresholdReached)
            {
                if (resetTimerOnThresholdReached) time = 0;
                else time -= threshold;
            }

            return true;
        }
        else return false;
    }

    /// <summary>Reset the accumulated time</summary>
    public void Reset() => time = 0;
}