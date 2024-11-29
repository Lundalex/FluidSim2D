using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PM = ProgramManager;

[ExecuteAlways]
public abstract class UserUIElement : MonoBehaviour
{
    
    public PointerHoverArea pointerHoverArea;
    public Image containerTrimImage;
    [SerializeField] private TMP_Text title;

    [SerializeField] private string titleText;
    public Color primaryColor;

    private void OnEnable()
    {
        PM.Instance.AddUserInput(this);
    #if UNITY_EDITOR
        EditorApplication.update += EditorUpdate;
    #endif
    }

    private void OnDisable()
    {
    #if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
    #endif
    }

    #if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (!Application.isPlaying)
        {
            title.text = titleText;
            InitDisplay();
        }
    }
    #endif

    private void Start()
    {
        PM.Instance.AddUserInput(this);
        title.text = titleText;
        InitDisplay();
    }

    public abstract void InitDisplay();
}