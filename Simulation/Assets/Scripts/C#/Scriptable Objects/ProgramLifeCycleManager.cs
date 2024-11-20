using UnityEngine;
using PM = ProgramManager;

public class ProgramLifeCycleManager : MonoBehaviour
{
    [SerializeField] private Main main;
    
    private void Awake() => PM.Instance.ResetDatas();

    private void Start()
    {
        if (main.TargetFrameRate > 0)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = main.TargetFrameRate;
        }
        else 
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 0;
        }

        PM.Instance.main = main;
        PM.Instance.Start();
    }

    private void Update() => PM.Instance.Update();

    public void OnStartConfirmation()
    {
        PM.Instance.startConfirmed = true;
        PM.Instance.programPaused = false;
    }
}