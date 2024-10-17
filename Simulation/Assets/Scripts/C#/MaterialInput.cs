using UnityEngine;

public class MaterialInput : MonoBehaviour
{
    public MatInput[] materialInputs;
    private Main m;
    private void OnValidate()
    {
        if (m == null) m = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        m.OnValidate();
    }
}
