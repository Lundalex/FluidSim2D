using UnityEngine;

public class MaterialInput : MonoBehaviour
{
    [SerializeField] private MatInput[] materialInputs;

    private Main m;
    private void OnValidate()
    {
        if (m == null) m = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        m.OnValidate();
    }

    public Mat[] GetMaterials()
    {
        Mat[] materials = new Mat[materialInputs.Length];
        for (int i = 0; i < materialInputs.Length; i++)
        {
            materials[i] = InitMat(materialInputs[i]);
        }

        return materials;
    }

    private Mat InitMat(MatInput matInput)
    {
        return new Mat
        {
            matTexLoc = 0,
            matTexDims = 0,
            alpha = Mathf.Clamp(matInput.alpha, 0.0f, 1.0f),
            edgeCol = 0
        };
    }
}
