using UnityEngine;
using PM = ProgramManager;

public class ProgramLifeCycleManager : MonoBehaviour
{
    [Header("Editor Settings")]
    [SerializeField] private bool darkMode;

    [Header("Serialized Fields")]
    [SerializeField] private Main main;
    [SerializeField] private GameObject darkBackground;
    [SerializeField] private GameObject startConfirmationWindow;

    private void OnValidate()
    {
        #if UNITY_EDITOR
            darkBackground.SetActive(darkMode);
        #endif
    }
    
    private void Awake()
    {
        PM.Instance.ResetDatas();
        startConfirmationWindow.SetActive(true);
        darkBackground.SetActive(true);
    }

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

        PM.Instance.Update();
    }

    private void Update() => PM.Instance.Update();

    public void OnStartConfirmation()
    {
        darkBackground.transform.SetParent(startConfirmationWindow.transform);
        darkBackground.transform.SetSiblingIndex(0);

        PM.Instance.startConfirmed = true;

        PM.Instance.startConfirmationStopWatch = new();
        PM.Instance.startConfirmationStopWatch.Start();
    }
}