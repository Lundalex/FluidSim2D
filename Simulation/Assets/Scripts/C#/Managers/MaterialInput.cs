using UnityEngine;

public class MaterialInput : MonoBehaviour
{
    public MatInput[] materialInputs;

    public void OnValidate() => ProgramManager.Instance.doOnSettingsChanged = true;
}
