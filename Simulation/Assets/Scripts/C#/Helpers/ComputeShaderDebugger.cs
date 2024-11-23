using UnityEngine;

public class ComputeShaderDebugger
{
    public struct DebugData
    {
        public int incorrectVariableKey;
    }

    public void CheckShaderConstants(Main m, ComputeShader debugShader)
    {
        // Get refernces
        PTypeInput pTypeInput = GameObject.FindGameObjectWithTag("PTypeInput").GetComponent<PTypeInput>();

        // Set buffer
        ComputeBuffer debugDataBuffer = ComputeHelper.CreateStructuredBuffer<DebugData>(1);
        debugShader.SetBuffer(0, "DebugDatas", debugDataBuffer);

        // --- Set variables ---

        debugShader.SetInt("CS_MAX_RIGIDBODIES_NUM", m.NumRigidBodies);

        debugShader.SetInt("CS_TN_PS", m.pSimShaderThreadSize1);
        debugShader.SetInt("CS_TN_PS2", m.pSimShaderThreadSize2);
        debugShader.SetInt("CS_TN_R", m.renderShaderThreadSize);
        debugShader.SetInt("CS_TN_RBS1", m.rbSimShaderThreadSize1);
        debugShader.SetInt("CS_TN_RBS2", m.rbSimShaderThreadSize2);
        debugShader.SetInt("CS_TN_RBS3", m.rbSimShaderThreadSize3);
        debugShader.SetInt("CS_TN_S", m.sortShaderThreadSize);

        debugShader.SetFloat("CS_INT_FLOAT_PRECISION_RB", m.FloatIntPrecisionRB);
        debugShader.SetFloat("CS_INT_FLOAT_PRECISION_P", m.FloatIntPrecisionP);

        debugShader.SetFloat("CS_MIR", m.MaxInfluenceRadius);
        debugShader.SetVector("CS_BOUNDARY_DIMS", new Vector2(m.BoundaryDims.x, m.BoundaryDims.y));
        debugShader.SetInt("CS_PTYPES_NUM_COPY", pTypeInput.particleTypeStates.Length * 3);

        // Dispatch kernel
        ComputeHelper.DispatchKernel(debugShader, "CheckShaderConstants", 1);

        // Retrieve data
        DebugData[] retrievedDebugData = new DebugData[1];
        debugDataBuffer.GetData(retrievedDebugData);
        int incorrectVariableKey = retrievedDebugData[0].incorrectVariableKey;

        // Check result
        if (incorrectVariableKey != -1)
        {
            Debug.LogError("Shader variable not set correctly. Key: " + incorrectVariableKey);
        }

        // Release buffer
        debugDataBuffer.Release();
    }
}