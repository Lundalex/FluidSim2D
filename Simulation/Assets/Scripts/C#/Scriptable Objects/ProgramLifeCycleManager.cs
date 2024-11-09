using UnityEngine;

public class ProgramLifeCycleManager : MonoBehaviour
{
    [SerializeField] private Main main;

    private void Awake()
    {
        ProgramManager.Instance.main = main;
        ProgramManager.Instance.Start();
    }

    private void Update() => ProgramManager.Instance.Update();

    private void OnDestroy() => ProgramManager.Instance.ResetDatas();
}