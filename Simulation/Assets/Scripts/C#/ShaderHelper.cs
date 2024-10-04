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

            pSimShader.SetBuffer(6, "PDatas", m.PDataBuffer);
            pSimShader.SetBuffer(6, "PTypes", m.PTypeBuffer);
            pSimShader.SetBuffer(6, "SortedStickyRequests", m.SortedStickyRequestsBuffer);
        }
    }

    public void SetRbSimShaderBuffers(ComputeShader rbSimShader)
    {
        if (m.RBDatas.Length != 0)
        {
            rbSimShader.SetBuffer(0, "RBVectors", m.RBVectorBuffer);
            rbSimShader.SetBuffer(0, "RBDatas", m.RBDataBuffer);

            rbSimShader.SetBuffer(1, "RBVectors", m.RBVectorBuffer);
            rbSimShader.SetBuffer(1, "RBDatas", m.RBDataBuffer);
            rbSimShader.SetBuffer(1, "TraversedChunksAPPEND", m.TraversedChunks_AC_Buffer);

            // Maximum reached! (8)
            rbSimShader.SetBuffer(2, "PDatas", m.PDataBuffer);
            rbSimShader.SetBuffer(2, "PTypes", m.PTypeBuffer);
            rbSimShader.SetBuffer(2, "RBDatas", m.RBDataBuffer);
            rbSimShader.SetBuffer(2, "RBVectors", m.RBVectorBuffer);
            rbSimShader.SetBuffer(2, "SpatialLookup", m.SpatialLookupBuffer);
            rbSimShader.SetBuffer(2, "StartIndices", m.StartIndicesBuffer);
            rbSimShader.SetBuffer(2, "TraversedChunksCONSUME", m.TraversedChunks_AC_Buffer);
            rbSimShader.SetBuffer(2, "StickynessReqsAPPEND", m.StickynessReqs_AC_Buffer);

            rbSimShader.SetBuffer(3, "RBDatas", m.RBDataBuffer);
            rbSimShader.SetBuffer(3, "RBVectors", m.RBVectorBuffer);
        }
    }

    public void SetRenderShaderBuffers(ComputeShader renderShader)
    {
        if (m.ParticlesNum != 0) {
            renderShader.SetBuffer(0, "SpatialLookup", m.SpatialLookupBuffer);
            renderShader.SetBuffer(0, "StartIndices", m.StartIndicesBuffer);

            renderShader.SetBuffer(0, "PDatas", m.PDataBuffer);
            renderShader.SetBuffer(0, "PTypes", m.PTypeBuffer);
        }
        if (m.RBDatas.Length != 0)
        {
            renderShader.SetBuffer(0, "RBDatas", m.RBDataBuffer);
            renderShader.SetBuffer(0, "RBVectors", m.RBVectorBuffer);
        }
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

        sortShader.SetBuffer(9, "StickynessReqsCONSUME", m.StickynessReqs_AC_Buffer);
        sortShader.SetBuffer(9, "SortedStickyRequests", m.SortedStickyRequestsBuffer);

        sortShader.SetBuffer(10, "SortedStickyRequests", m.SortedStickyRequestsBuffer);
    }

    public void SetMarchingSquaresShaderBuffers(ComputeShader marchingSquaresShader)
    {
        marchingSquaresShader.SetBuffer(0, "MSPoints", m.MSPointsBuffer);
        marchingSquaresShader.SetBuffer(0, "SpatialLookup", m.SpatialLookupBuffer);
        marchingSquaresShader.SetBuffer(0, "StartIndices", m.StartIndicesBuffer);

        marchingSquaresShader.SetBuffer(0, "PDatas", m.PDataBuffer);
        marchingSquaresShader.SetBuffer(0, "PTypes", m.PTypeBuffer);
        
        marchingSquaresShader.SetBuffer(1, "Vertices", m.VerticesBuffer);
        marchingSquaresShader.SetBuffer(1, "Triangles", m.TrianglesBuffer);
        marchingSquaresShader.SetBuffer(1, "Colors", m.ColorsBuffer);
        marchingSquaresShader.SetBuffer(1, "MSPoints", m.MSPointsBuffer);
    }

    public void UpdatePSimShaderVariables(ComputeShader pSimShader)
    {
        pSimShader.SetInt("MaxInfluenceRadiusSqr", m.MaxInfluenceRadiusSqr);
        pSimShader.SetFloat("InvMaxInfluenceRadius", m.InvMaxInfluenceRadius);
        pSimShader.SetVector("ChunksNum", new Vector2(m.ChunksNum.x, m.ChunksNum.y));
        pSimShader.SetInt("Width", m.Width);
        pSimShader.SetInt("Height", m.Height);
        pSimShader.SetInt("ParticlesNum", m.ParticlesNum);
        pSimShader.SetInt("ParticleSpringsCombinedHalfLength", m.ParticleSpringsCombinedHalfLength);
        pSimShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        pSimShader.SetInt("SpawnDims", m.SpawnDims);
        pSimShader.SetInt("TimeStepsPerRender", m.TimeStepsPerRender);
        pSimShader.SetFloat("LookAheadFactor", m.LookAheadFactor);
        pSimShader.SetFloat("StateThresholdPadding", m.StateThresholdPadding);
        pSimShader.SetFloat("BorderPadding", m.BorderPadding);
        pSimShader.SetFloat("MaxInteractionRadius", m.MaxInteractionRadius);
        pSimShader.SetFloat("InteractionAttractionPower", m.InteractionAttractionPower);
        pSimShader.SetFloat("InteractionFountainPower", m.InteractionFountainPower);
        pSimShader.SetFloat("InteractionTemperaturePower", m.InteractionTemperaturePower);
    }

    public void UpdateRbSimShaderVariables(ComputeShader rbSimShader)
    {
        rbSimShader.SetVector("ChunksNum", new Vector2(m.ChunksNum.x, m.ChunksNum.y));
        rbSimShader.SetInt("Width", m.Width);
        rbSimShader.SetInt("Height", m.Height);
        rbSimShader.SetInt("ParticlesNum", m.ParticlesNum);
        rbSimShader.SetInt("RBodiesNum", m.RBDatas.Length);
        rbSimShader.SetInt("RBVectorsNum", m.RBVectors.Length);
        rbSimShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        rbSimShader.SetInt("MaxChunkSearchSafety", m.MaxChunkSearchSafety);

        rbSimShader.SetFloat("Damping", m.Damping);
        rbSimShader.SetFloat("Gravity", m.Gravity);
        rbSimShader.SetFloat("RbElasticity", m.RbElasticity);
        rbSimShader.SetFloat("BorderPadding", m.BorderPadding);
    }

    public void UpdateRenderShaderVariables(ComputeShader renderShader)
    {
        renderShader.SetFloat("VisualParticleRadii", m.VisualParticleRadii);
        renderShader.SetFloat("RBRenderThickness", m.RBRenderThickness);
        renderShader.SetInt("ResolutionX", m.ResolutionX);
        renderShader.SetInt("ResolutionY", m.ResolutionY);
        renderShader.SetInt("Width", m.Width);
        renderShader.SetInt("Height", m.Height);
        renderShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        renderShader.SetVector("ChunksNum", new Vector2(m.ChunksNum.x, m.ChunksNum.y));
        renderShader.SetInt("ParticlesNum", m.ParticlesNum);
        renderShader.SetInt("RBodiesNum", m.RBDatas.Length);
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

    public void UpdateMarchingSquaresShaderVariables(ComputeShader marchingSquaresShader)
    {   
        marchingSquaresShader.SetInt("MarchW", m.MarchW);
        marchingSquaresShader.SetInt("MarchH", m.MarchH);
        marchingSquaresShader.SetFloat("MSResolution", m.MSResolution);
        marchingSquaresShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        marchingSquaresShader.SetVector("ChunksNum", new Vector2(m.ChunksNum.x, m.ChunksNum.y));
        marchingSquaresShader.SetInt("Width", m.Width);
        marchingSquaresShader.SetInt("Height", m.Height);
        marchingSquaresShader.SetInt("ParticlesNum", m.ParticlesNum);
        marchingSquaresShader.SetFloat("MSvalMin", m.MSvalMin);
        marchingSquaresShader.SetFloat("TriStorageLength", m.TriStorageLength);
    }
}