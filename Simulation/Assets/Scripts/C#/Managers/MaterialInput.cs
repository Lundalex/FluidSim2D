using UnityEngine;

public class MaterialInput : MonoBehaviour
{
    public ProgramManager programManager;
    public MatInput[] materialInputs;

    public void OnValidate() => programManager.doOnSettingsChanged = true;
}
