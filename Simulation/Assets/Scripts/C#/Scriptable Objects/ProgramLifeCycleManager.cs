using System.Collections;
using Resources2;
using UnityEngine;
using PM = ProgramManager;

public class ProgramLifeCycleManager : MonoBehaviour
{
    [Header("Editor Settings")]
    public bool darkMode;

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
        PM.Instance.ResetData();

        startConfirmationWindow.SetActive(true);
        darkBackground.SetActive(true);
    }

    private void Start()
    {
        SetTargetFrameRate();

        PM.Instance.main = main;
        PM.Instance.Start();
    }

    private void SetTargetFrameRate()
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
    }

    private void Update() => PM.Instance.Update();

    public void OnStartConfirmation()
    {
        darkBackground.transform.SetParent(startConfirmationWindow.transform);
        darkBackground.transform.SetSiblingIndex(0);

        StartCoroutine(StartConfirmationDelayCoroutine());
    }

    private IEnumerator StartConfirmationDelayCoroutine()
    {
        PM.Instance.startConfirmationStatus = StartConfirmationStatus.Waiting;
        yield return new WaitForSeconds(Func.MsToSeconds(PM.msStartConfimationDelay));
        PM.Instance.startConfirmationStatus = StartConfirmationStatus.Complete;
    }
}