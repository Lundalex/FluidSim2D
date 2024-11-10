using UnityEngine;

public class ProgramLifeCycleManager : MonoBehaviour
{
    [SerializeField] private Main main;

    private void Awake()
    {
        ProgramManager.Instance.main = main;
        ProgramManager.Instance.Start();

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
    }

    private void Update() => ProgramManager.Instance.Update();

    private void OnDestroy() => ProgramManager.Instance.ResetDatas();
}