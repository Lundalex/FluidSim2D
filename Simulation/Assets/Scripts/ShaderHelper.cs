using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShaderHelper : MonoBehaviour
{
    public Main m;
    public void SetPSimShaderBuffers(ComputeShader pSimShader)
    {
        if (m.ParticlesNum != 0) {
            // Kernel PreCalculations
            pSimShader.SetBuffer(0, "PData", m.PDataBuffer);
            pSimShader.SetBuffer(0, "PTypes", m.PTypesBuffer);
        
            // Kernel PreCalculations
            pSimShader.SetBuffer(1, "SpatialLookup", m.SpatialLookupBuffer);
            pSimShader.SetBuffer(1, "StartIndices", m.StartIndicesBuffer);

            pSimShader.SetBuffer(1, "PData", m.PDataBuffer);
            pSimShader.SetBuffer(1, "PTypes", m.PTypesBuffer);

            pSimShader.SetBuffer(2, "PData", m.PDataBuffer);
            pSimShader.SetBuffer(2, "ChunkSizes", m.ChunkSizesBuffer);
            pSimShader.SetBuffer(2, "SpringCapacities", m.SpringCapacitiesBuffer);
            pSimShader.SetBuffer(2, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
            pSimShader.SetBuffer(2, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
            pSimShader.SetBuffer(2, "ParticleSpringsCombined", m.ParticleSpringsCombinedBuffer);
            
            // Kernel ParticleForces - 8/8 buffers
            pSimShader.SetBuffer(3, "SpatialLookup", m.SpatialLookupBuffer);
            pSimShader.SetBuffer(3, "StartIndices", m.StartIndicesBuffer);

            pSimShader.SetBuffer(3, "PData", m.PDataBuffer);
            pSimShader.SetBuffer(3, "PTypes", m.PTypesBuffer);

            pSimShader.SetBuffer(3, "SpringCapacities", m.SpringCapacitiesBuffer);
            pSimShader.SetBuffer(3, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
            pSimShader.SetBuffer(3, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
            pSimShader.SetBuffer(3, "ParticleSpringsCombined", m.ParticleSpringsCombinedBuffer);

            pSimShader.SetBuffer(4, "PData", m.PDataBuffer);
            pSimShader.SetBuffer(4, "PTypes", m.PTypesBuffer);
            pSimShader.SetBuffer(4, "SpringCapacities", m.SpringCapacitiesBuffer);

            pSimShader.SetBuffer(5, "PData", m.PDataBuffer);
            pSimShader.SetBuffer(5, "PTypes", m.PTypesBuffer);
            pSimShader.SetBuffer(5, "SortedStickyRequests", m.SortedStickyRequestsBuffer);
        }
    }

    public void SetRbSimShaderBuffers(ComputeShader rbSimShader)
    {
        if (m.RBodiesNum != 0)
        {
            rbSimShader.SetBuffer(0, "RBVector", m.RBVectorBuffer);
            rbSimShader.SetBuffer(0, "RBData", m.RBDataBuffer);

            rbSimShader.SetBuffer(1, "RBVector", m.RBVectorBuffer);
            rbSimShader.SetBuffer(1, "RBData", m.RBDataBuffer);
            rbSimShader.SetBuffer(1, "TraversedChunksAPPEND", m.TraversedChunks_AC_Buffer);

            // Maximum reached! (8)
            rbSimShader.SetBuffer(2, "PData", m.PDataBuffer);
            rbSimShader.SetBuffer(2, "PTypes", m.PTypesBuffer);
            rbSimShader.SetBuffer(2, "RBData", m.RBDataBuffer);
            rbSimShader.SetBuffer(2, "RBVector", m.RBVectorBuffer);
            rbSimShader.SetBuffer(2, "SpatialLookup", m.SpatialLookupBuffer);
            rbSimShader.SetBuffer(2, "StartIndices", m.StartIndicesBuffer);
            rbSimShader.SetBuffer(2, "TraversedChunksCONSUME", m.TraversedChunks_AC_Buffer);
            rbSimShader.SetBuffer(2, "StickynessReqsAPPEND", m.StickynessReqs_AC_Buffer);

            rbSimShader.SetBuffer(3, "RBData", m.RBDataBuffer);
            rbSimShader.SetBuffer(3, "RBVector", m.RBVectorBuffer);
        }
    }

    public void SetRenderShaderBuffers(ComputeShader renderShader)
    {
        if (m.ParticlesNum != 0) {
            renderShader.SetBuffer(0, "SpatialLookup", m.SpatialLookupBuffer);
            renderShader.SetBuffer(0, "StartIndices", m.StartIndicesBuffer);

            renderShader.SetBuffer(0, "PData", m.PDataBuffer);
            renderShader.SetBuffer(0, "PTypes", m.PTypesBuffer);
        }
        if (m.RBodiesNum != 0)
        {
            renderShader.SetBuffer(0, "RBData", m.RBDataBuffer);
            renderShader.SetBuffer(0, "RBVector", m.RBVectorBuffer);
        }
    }

    public void SetSortShaderBuffers(ComputeShader sortShader)
    {
        sortShader.SetBuffer(0, "SpatialLookup", m.SpatialLookupBuffer);

        sortShader.SetBuffer(0, "PData", m.PDataBuffer);
        sortShader.SetBuffer(0, "PTypes", m.PTypesBuffer);

        sortShader.SetBuffer(1, "SpatialLookup", m.SpatialLookupBuffer);

        sortShader.SetBuffer(1, "PData", m.PDataBuffer);
        sortShader.SetBuffer(1, "PTypes", m.PTypesBuffer);

        sortShader.SetBuffer(2, "StartIndices", m.StartIndicesBuffer);

        sortShader.SetBuffer(3, "SpatialLookup", m.SpatialLookupBuffer);
        sortShader.SetBuffer(3, "StartIndices", m.StartIndicesBuffer);
        sortShader.SetBuffer(3, "PTypes", m.PTypesBuffer);
        sortShader.SetBuffer(3, "PData", m.PDataBuffer);

        sortShader.SetBuffer(4, "SpatialLookup", m.SpatialLookupBuffer);
        sortShader.SetBuffer(4, "StartIndices", m.StartIndicesBuffer);
        sortShader.SetBuffer(4, "ChunkSizes", m.ChunkSizesBuffer);

        sortShader.SetBuffer(5, "ChunkSizes", m.ChunkSizesBuffer);
        sortShader.SetBuffer(5, "SpringCapacities", m.SpringCapacitiesBuffer);

        sortShader.SetBuffer(6, "SpringCapacities", m.SpringCapacitiesBuffer);
        sortShader.SetBuffer(6, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        sortShader.SetBuffer(6, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);

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

        marchingSquaresShader.SetBuffer(0, "PData", m.PDataBuffer);
        marchingSquaresShader.SetBuffer(0, "PTypes", m.PTypesBuffer);
        
        marchingSquaresShader.SetBuffer(1, "Vertices", m.VerticesBuffer);
        marchingSquaresShader.SetBuffer(1, "Triangles", m.TrianglesBuffer);
        marchingSquaresShader.SetBuffer(1, "Colors", m.ColorsBuffer);
        marchingSquaresShader.SetBuffer(1, "MSPoints", m.MSPointsBuffer);
    }

    public void UpdatePSimShaderVariables(ComputeShader pSimShader)
    {
        pSimShader.SetInt("MaxInfluenceRadiusSqr", m.MaxInfluenceRadiusSqr);
        pSimShader.SetFloat("InvMaxInfluenceRadius", m.InvMaxInfluenceRadius);
        pSimShader.SetInt("ChunkNumW", m.ChunkNumW);
        pSimShader.SetInt("ChunkNumH", m.ChunkNumH);
        pSimShader.SetInt("ChunkNum", m.ChunkNum);
        pSimShader.SetInt("IOOR", m.IOOR);
        pSimShader.SetInt("Width", m.Width);
        pSimShader.SetInt("Height", m.Height);
        pSimShader.SetInt("ParticlesNum", m.ParticlesNum);
        pSimShader.SetInt("ParticleSpringsCombinedHalfLength", m.ParticleSpringsCombinedHalfLength);
        pSimShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        pSimShader.SetInt("SpawnDims", m.SpawnDims);
        pSimShader.SetInt("TimeStepsPerRender", m.TimeStepsPerRender);
        pSimShader.SetFloat("LookAheadFactor", m.LookAheadFactor);
        pSimShader.SetFloat("BorderPadding", m.BorderPadding);
        pSimShader.SetFloat("MaxInteractionRadius", m.MaxInteractionRadius);
        pSimShader.SetFloat("InteractionAttractionPower", m.InteractionAttractionPower);
        pSimShader.SetFloat("InteractionFountainPower", m.InteractionFountainPower);
        pSimShader.SetInt("SpringSafety", m.SpringSafety);
        pSimShader.SetFloat("Plasticity", m.Plasticity);
        
        // Set math resources constants
    }

    public void UpdateRbSimShaderVariables(ComputeShader rbSimShader)
    {
        rbSimShader.SetInt("ChunkNumW", m.ChunkNumW);
        rbSimShader.SetInt("ChunkNumH", m.ChunkNumH);
        rbSimShader.SetInt("Width", m.Width);
        rbSimShader.SetInt("Height", m.Height);
        rbSimShader.SetInt("ParticlesNum", m.ParticlesNum);
        rbSimShader.SetInt("RBodiesNum", m.RBodiesNum);
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
        renderShader.SetInt("ChunkNumW", m.ChunkNumW);
        renderShader.SetInt("ChunkNumH", m.ChunkNumH);
        renderShader.SetInt("ParticlesNum", m.ParticlesNum);
        renderShader.SetInt("RBodiesNum", m.RBodiesNum);
        renderShader.SetInt("RBVectorNum", m.RBVectorNum);
        
    }

    public void UpdateSortShaderVariables(ComputeShader sortShader)
    {
        sortShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        sortShader.SetInt("ChunkNumW", m.ChunkNumW);
        sortShader.SetInt("ChunkNumH", m.ChunkNumH);
        sortShader.SetInt("ChunkNumNextPow2", m.ChunkNumNextPow2);
        sortShader.SetInt("ParticlesNum", m.ParticlesNum);
        sortShader.SetInt("IOOR", m.IOOR);
    }

    public void UpdateMarchingSquaresShaderVariables(ComputeShader marchingSquaresShader)
    {   
        marchingSquaresShader.SetInt("MarchW", m.MarchW);
        marchingSquaresShader.SetInt("MarchH", m.MarchH);
        marchingSquaresShader.SetFloat("MSResolution", m.MSResolution);
        marchingSquaresShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        marchingSquaresShader.SetInt("ChunkNumW", m.ChunkNumW);
        marchingSquaresShader.SetInt("ChunkNumH", m.ChunkNumH);
        marchingSquaresShader.SetInt("Width", m.Width);
        marchingSquaresShader.SetInt("Height", m.Height);
        marchingSquaresShader.SetInt("ParticlesNum", m.ParticlesNum);
        marchingSquaresShader.SetFloat("MSvalMin", m.MSvalMin);
        marchingSquaresShader.SetFloat("TriStorageLength", m.TriStorageLength);
    }
}