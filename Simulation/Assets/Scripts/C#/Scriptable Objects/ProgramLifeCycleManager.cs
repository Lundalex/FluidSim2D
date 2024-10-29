using UnityEngine;

public class ProgramLifeCycleManager : MonoBehaviour
{
    public ProgramManager programManager;
    [SerializeField] private Main main;

    private void Awake()
    {
        programManager.main = main;
    }

    private void Start() => programManager.Start();

    private void Update() => programManager.Update();
}