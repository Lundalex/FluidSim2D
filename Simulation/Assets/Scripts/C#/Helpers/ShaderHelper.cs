using UnityEngine;

// Import utils from Resources.cs
using Resources;
public class ShaderHelper : MonoBehaviour
{
    public Main m;
    public void SetPSimShaderBuffers(ComputeShader pSimShader)
    {
        if (m.ParticlesNum != 0) {
            // Kernel PreCalculations
            pSimShader.SetBuffer(0, "PDatas", m.PDataBuffer);
            pSimShader.SetBuffer(0, "PTypes", m.PTypeBuffer);
        
            // Kernel PreCalculations
            pSimShader.SetBuffer(1, "SpatialLookup", m.SpatialLookupBuffer);
            pSimShader.SetBuffer(1, "StartIndices", m.StartIndicesBuffer);

            pSimShader.SetBuffer(1, "PDatas", m.PDataBuffer);
            pSimShader.SetBuffer(1, "PTypes", m.PTypeBuffer);

            pSimShader.SetBuffer(2, "ParticleSpringsCombined", m.ParticleSpringsCombinedBuffer);

            // Kernel TransferAllSpringData - 8/8 buffers
            pSimShader.SetBuffer(3, "PDatas", m.PDataBuffer);
            pSimShader.SetBuffer(3, "PTypes", m.PTypeBuffer);
            pSimShader.SetBuffer(3, "SpatialLookup", m.SpatialLookupBuffer);
            pSimShader.SetBuffer(3, "StartIndices", m.StartIndicesBuffer);
            pSimShader.SetBuffer(3, "SpringCapacities", m.SpringCapacitiesBuffer);
            pSimShader.SetBuffer(3, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
            pSimShader.SetBuffer(3, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
            pSimShader.SetBuffer(3, "ParticleSpringsCombined", m.ParticleSpringsCombinedBuffer);
            
            // Kernel ParticleForces - 8/8 buffers
            pSimShader.SetBuffer(4, "SpatialLookup", m.SpatialLookupBuffer);
            pSimShader.SetBuffer(4, "StartIndices", m.StartIndicesBuffer);

            pSimShader.SetBuffer(4, "PDatas", m.PDataBuffer);
            pSimShader.SetBuffer(4, "PTypes", m.PTypeBuffer);

            pSimShader.SetBuffer(4, "SpringCapacities", m.SpringCapacitiesBuffer);
            pSimShader.SetBuffer(4, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
            pSimShader.SetBuffer(4, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
            pSimShader.SetBuffer(4, "ParticleSpringsCombined", m.ParticleSpringsCombinedBuffer);

            pSimShader.SetBuffer(5, "PDatas", m.PDataBuffer);
            pSimShader.SetBuffer(5, "PTypes", m.PTypeBuffer);
            pSimShader.SetBuffer(5, "SpringCapacities", m.SpringCapacitiesBuffer);
        }
    }

    public void SetRenderShaderBuffers(ComputeShader renderShader)
    {
        if (m.ParticlesNum != 0)
        {
            renderShader.SetBuffer(1, "SpatialLookup", m.SpatialLookupBuffer);
            renderShader.SetBuffer(1, "StartIndices", m.StartIndicesBuffer);
            renderShader.SetBuffer(1, "PDatas", m.PDataBuffer);
            renderShader.SetBuffer(1, "PTypes", m.PTypeBuffer);
            renderShader.SetBuffer(1, "Materials", m.MaterialBuffer);
        }

        if (m.RBDatas.Length != 0)
        {
            renderShader.SetBuffer(2, "RigidBodies", m.RBDataBuffer);
            renderShader.SetBuffer(2, "RBVectors", m.RBVectorBuffer);
            renderShader.SetBuffer(2, "Materials", m.MaterialBuffer);

            renderShader.SetBuffer(3, "RigidBodies", m.RBDataBuffer);
            renderShader.SetBuffer(3, "Materials", m.MaterialBuffer);
        }
    }

    public void SetRenderShaderTextures(ComputeShader renderShader)
    {
        renderShader.SetTexture(0, "Result", m.renderTexture);
        renderShader.SetTexture(0, "Background", m.backgroundTexture);

        renderShader.SetTexture(1, "Result", m.renderTexture);
        renderShader.SetTexture(1, "Caustics", m.causticsTexture);
        renderShader.SetTexture(1, "Background", m.backgroundTexture);
        renderShader.SetTexture(1, "Atlas", m.AtlasTexture);

        renderShader.SetTexture(2, "Result", m.renderTexture);
        renderShader.SetTexture(2, "Background", m.backgroundTexture);
        renderShader.SetTexture(2, "Atlas", m.AtlasTexture);

        renderShader.SetTexture(3, "Result", m.renderTexture);
        renderShader.SetTexture(3, "Atlas", m.AtlasTexture);

        renderShader.SetTexture(4, "Result", m.renderTexture);
        renderShader.SetTexture(4, "UITexture", m.uiTexture);
    }

    public void SetSortShaderBuffers(ComputeShader sortShader)
    {
        sortShader.SetBuffer(0, "SpatialLookup", m.SpatialLookupBuffer);

        sortShader.SetBuffer(0, "PDatas", m.PDataBuffer);
        sortShader.SetBuffer(0, "PTypes", m.PTypeBuffer);

        sortShader.SetBuffer(1, "SpatialLookup", m.SpatialLookupBuffer);

        sortShader.SetBuffer(1, "PDatas", m.PDataBuffer);
        sortShader.SetBuffer(1, "PTypes", m.PTypeBuffer);

        sortShader.SetBuffer(2, "StartIndices", m.StartIndicesBuffer);

        sortShader.SetBuffer(3, "SpatialLookup", m.SpatialLookupBuffer);
        sortShader.SetBuffer(3, "StartIndices", m.StartIndicesBuffer);
        sortShader.SetBuffer(3, "PTypes", m.PTypeBuffer);
        sortShader.SetBuffer(3, "PDatas", m.PDataBuffer);

        sortShader.SetBuffer(4, "SpatialLookup", m.SpatialLookupBuffer);
        sortShader.SetBuffer(4, "StartIndices", m.StartIndicesBuffer);
        sortShader.SetBuffer(4, "SpringCapacities", m.SpringCapacitiesBuffer);

        sortShader.SetBuffer(5, "SpringCapacities", m.SpringCapacitiesBuffer);

        sortShader.SetBuffer(6, "SpringCapacities", m.SpringCapacitiesBuffer);
        sortShader.SetBuffer(6, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        sortShader.SetBuffer(6, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        sortShader.SetBuffer(6, "SpringStartIndices_dbC", m.SpringStartIndicesBuffer_dbC);

        sortShader.SetBuffer(7, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        sortShader.SetBuffer(7, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        sortShader.SetBuffer(7, "SpringStartIndices_dbC", m.SpringStartIndicesBuffer_dbC);

        sortShader.SetBuffer(8, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        sortShader.SetBuffer(8, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        sortShader.SetBuffer(8, "SpringStartIndices_dbC", m.SpringStartIndicesBuffer_dbC);
    }

    public void UpdatePSimShaderVariables(ComputeShader pSimShader)
    {
        pSimShader.SetInt("MaxInfluenceRadiusSqr", m.MaxInfluenceRadiusSqr);
        pSimShader.SetFloat("InvMaxInfluenceRadius", m.InvMaxInfluenceRadius);
        pSimShader.SetVector("ChunksNum", new Vector2(m.ChunksNum.x, m.ChunksNum.y));
        pSimShader.SetVector("BoundaryDims", new Vector2(m.BoundaryDims.x, m.BoundaryDims.y));
        pSimShader.SetInt("ParticlesNum", m.ParticlesNum);
        pSimShader.SetInt("ParticleSpringsCombinedHalfLength", m.ParticleSpringsCombinedHalfLength);
        pSimShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        pSimShader.SetInt("SubTimeStepsPerFrame", m.SubTimeStepsPerFrame);
        pSimShader.SetFloat("LookAheadTime", m.LookAheadTime);
        pSimShader.SetFloat("StateThresholdPadding", m.StateThresholdPadding);
        pSimShader.SetFloat("FluidPadding", m.FluidPadding);
        pSimShader.SetFloat("MaxInteractionRadius", m.MaxInteractionRadius);
        pSimShader.SetFloat("InteractionAttractionPower", m.InteractionAttractionPower);
        pSimShader.SetFloat("InteractionFountainPower", m.InteractionFountainPower);
        pSimShader.SetFloat("InteractionTemperaturePower", m.InteractionTemperaturePower);
    }

    public void UpdateRenderShaderVariables(ComputeShader renderShader)
    {
        renderShader.SetFloat("VisualParticleRadii", m.VisualParticleRadii);
        renderShader.SetFloat("MetaballsThreshold", m.MetaballsThreshold);
        renderShader.SetFloat("MetaballsEdgeDensityWidth", m.MetaballsEdgeDensityWidth);
        renderShader.SetFloat("FluidEdgeWidth", m.FluidEdgeWidth);
        renderShader.SetFloat("RBEdgeWidth", m.RBEdgeWidth);
        renderShader.SetFloat("BackgroundUpScaleFactor", m.BackgroundUpScaleFactor);
        renderShader.SetVector("BackgroundBrightness", new Vector3(m.BackgroundBrightness.x, m.BackgroundBrightness.y, m.BackgroundBrightness.z));

        renderShader.SetInt("SpringRenderNumPeriods", m.SpringRenderNumPeriods);
        renderShader.SetFloat("SpringRenderWidth", m.SpringRenderWidth);
        renderShader.SetFloat("SpringRenderHalfMatWidth", m.SpringRenderMatWidth / 2.0f);
        renderShader.SetFloat("SpringRenderRodLength", Mathf.Max(m.SpringRenderRodLength, 0.01f));
        renderShader.SetFloat("TaperThresoldNormalised", m.TaperThresoldNormalised);

        renderShader.SetVector("Resolution", new Vector2(m.Resolution.x, m.Resolution.y));
        renderShader.SetVector("BoundsDims", new Vector2(m.BoundaryDims.x, m.BoundaryDims.y));
        renderShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        renderShader.SetFloat("InvMaxInfluenceRadius", m.InvMaxInfluenceRadius);
        renderShader.SetInt("MaxInfluenceRadiusSqr", m.MaxInfluenceRadiusSqr);
        renderShader.SetVector("ChunksNum", new Vector2(m.ChunksNum.x, m.ChunksNum.y));
        renderShader.SetInt("ParticlesNum", m.ParticlesNum);
        renderShader.SetInt("NumRigidBodies", m.RBDatas.Length);
        renderShader.SetInt("RBVectorsNum", m.RBVectors.Length);
    }

    public void UpdateSortShaderVariables(ComputeShader sortShader)
    {
        sortShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        sortShader.SetVector("ChunksNum", new Vector2(m.ChunksNum.x, m.ChunksNum.y));
        sortShader.SetInt("ChunksNumAll", m.ChunksNumAll);
        sortShader.SetInt("ChunkNumNextPow2", m.ChunksNumAllNextPow2);
        sortShader.SetInt("ParticlesNum", m.ParticlesNum);
        sortShader.SetInt("ParticlesNum_NextPow2", m.ParticlesNum_NextPow2);
    }

    // --- Ner RB shader ---

    public void SetNewRBSimShaderBuffers(ComputeShader rbSimShader)
    {
        rbSimShader.SetBuffer(0, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(0, "RBVectors", m.RBVectorBuffer);

        rbSimShader.SetBuffer(0, "SpatialLookup", m.SpatialLookupBuffer);
        rbSimShader.SetBuffer(0, "PTypes", m.PTypeBuffer);
        rbSimShader.SetBuffer(0, "PDatas", m.PDataBuffer);

        rbSimShader.SetBuffer(1, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(1, "RBVectors", m.RBVectorBuffer);

        rbSimShader.SetBuffer(2, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(2, "RBVectors", m.RBVectorBuffer);
        rbSimShader.SetBuffer(2, "RBAdjustments", m.RBAdjustmentBuffer);

        rbSimShader.SetBuffer(3, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(3, "RBAdjustments", m.RBAdjustmentBuffer);

        rbSimShader.SetBuffer(4, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(4, "RBVectors", m.RBVectorBuffer);

        rbSimShader.SetBuffer(5, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(5, "RBAdjustments", m.RBAdjustmentBuffer);
    }

    public void UpdateNewRBSimShaderVariables(ComputeShader rbSimShader)
    {
        rbSimShader.SetVector("BoundaryDims", new Vector2(m.BoundaryDims.x, m.BoundaryDims.y));
        rbSimShader.SetFloat("RigidBodyPadding", m.FluidPadding + m.RigidBodyPadding);

        rbSimShader.SetInt("NumRigidBodies", m.RBDatas.Length);
        rbSimShader.SetInt("NumVectors", m.RBVectors.Length);
        rbSimShader.SetInt("NumParticles", m.ParticlesNum);

        rbSimShader.SetFloat("RB_RBCollisionCorrectionFactor", m.RB_RBCollisionCorrectionFactor);
        rbSimShader.SetFloat("RB_RBCollisionSlop", m.RB_RBCollisionSlop);
        rbSimShader.SetBool("AllowLinkedRBCollisions", m.AllowLinkedRBCollisions);

        rbSimShader.SetFloat("RB_MaxInteractionRadius", m.RB_MaxInteractionRadius);
        rbSimShader.SetFloat("RB_InteractionAttractionPower", m.RB_InteractionAttractionPower);
    }
}