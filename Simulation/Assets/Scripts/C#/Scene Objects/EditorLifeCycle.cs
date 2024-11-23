using UnityEditor;
using UnityEngine;

public abstract class EditorLifeCycle : MonoBehaviour
{
#region Editor
    private void OnEnable()
    {
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
        private void EditorUpdate() => OnEditorUpdate();
    #endif

    #if UNITY_EDITOR
        public abstract void OnEditorUpdate();
    #endif
#endregion
}
