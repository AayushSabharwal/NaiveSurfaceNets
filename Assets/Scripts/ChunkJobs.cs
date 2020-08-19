using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.PackageManager;
using UnityEngine;

[BurstCompile]
public struct PointValuesJob : IJobFor
{
    public NativeArray<float> Points;
    public float3 Offset;
    public int PointsPerAxis;
    public float SurfaceLevel;

    public void Execute(int index)
    {
        float3 point = Offset * (PointsPerAxis - 1) + Utility.IndexToChunkPoint(index, PointsPerAxis);

        Points[index] = Utility.DensityFunction(point) - SurfaceLevel;
    }
}

[BurstCompile]
public struct VoxelVertexJob : IJobFor
{
    [ReadOnly]
    public NativeArray<float> Points;
    [NativeDisableParallelForRestriction]
    public NativeArray<float3> VoxelPoints;
    [NativeDisableParallelForRestriction]
    public NativeArray<bool> HasVertex;
    [NativeDisableParallelForRestriction]
    public NativeArray<int3> Triangles;
    [NativeDisableParallelForRestriction]
    public NativeArray<float3> Normals;
    [ReadOnly]
    public float3 Offset;
    [ReadOnly]
    public int PointsPerAxis;

    private int3 _pointIndex3D;
    private int _voxelIndex;
    private int _cubeVertexMask;
    private int _edgeMask;
    private int VoxelsPerAxis => PointsPerAxis - 1;

    public void Execute(int index)
    {
        _pointIndex3D = Utility.IndexToChunkPoint(index, PointsPerAxis);
        if (_pointIndex3D.x >= VoxelsPerAxis ||
            _pointIndex3D.y >= VoxelsPerAxis ||
            _pointIndex3D.z >= VoxelsPerAxis) return;

        _voxelIndex = Utility.ChunkPointToOctreeIndex(VoxelPoints, _pointIndex3D, VoxelsPerAxis);
        HasVertex[_voxelIndex] = false;
        VoxelPoints[_voxelIndex] = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
        CalculateCubeIndex();
        if (_cubeVertexMask == 0 || _cubeVertexMask == 255) return;

        _edgeMask = LookupTables.EdgeTable[_cubeVertexMask];
        if (_edgeMask == 0) return;

        CalculateVoxelVertex();
        if (!HasVertex[_voxelIndex]) return;

        for (int i = 0; i < LookupTables.ForwardEdges.Length; i++)
        {
            if ((_edgeMask & (1 << LookupTables.ForwardEdges[i])) == 0)
            {
                Triangles[_voxelIndex * 6 + i * 2] = int3.zero;
                Triangles[_voxelIndex * 6 + i * 2 + 1] = int3.zero;
                continue;
            }

            int3 tri = int3.zero;
            int3 corner1 = _pointIndex3D + LookupTables.TriangleDirections[i, 0];
            int3 corner2 = _pointIndex3D + LookupTables.TriangleDirections[i, 1];
            int3 corner3 = _pointIndex3D + LookupTables.TriangleDirections[i, 2];
            if (Utility.ValidGridPoint(corner1, VoxelsPerAxis) &&
                Utility.ValidGridPoint(corner2, VoxelsPerAxis) &&
                Utility.ValidGridPoint(corner3, VoxelsPerAxis))
            {
                tri = new int3(Utility.ChunkPointToOctreeIndex(VoxelPoints, corner1, VoxelsPerAxis),
                               Utility.ChunkPointToOctreeIndex(VoxelPoints, corner2, VoxelsPerAxis),
                               Utility.ChunkPointToOctreeIndex(VoxelPoints, corner3, VoxelsPerAxis));
            }

            Triangles[_voxelIndex * 6 + i * 2] = tri;

            tri = int3.zero;
            corner1 = _pointIndex3D + LookupTables.TriangleDirections[i, 3];
            corner2 = _pointIndex3D + LookupTables.TriangleDirections[i, 4];
            corner3 = _pointIndex3D + LookupTables.TriangleDirections[i, 5];
            if (Utility.ValidGridPoint(corner1, VoxelsPerAxis) &&
                Utility.ValidGridPoint(corner2, VoxelsPerAxis) &&
                Utility.ValidGridPoint(corner3, VoxelsPerAxis))
            {
                tri = new int3(Utility.ChunkPointToOctreeIndex(VoxelPoints, corner1, VoxelsPerAxis),
                               Utility.ChunkPointToOctreeIndex(VoxelPoints, corner2, VoxelsPerAxis),
                               Utility.ChunkPointToOctreeIndex(VoxelPoints, corner3, VoxelsPerAxis));
            }

            Triangles[_voxelIndex * 6 + i * 2 + 1] = tri;
        }
    }

    private int CubeCorner(int index)
    {
        switch (index)
        {
            case 0:
                return Utility.ChunkPointToIndex(_pointIndex3D, PointsPerAxis);
            case 1:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(0, 0, 1), PointsPerAxis);
            case 2:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(1, 0, 1), PointsPerAxis);
            case 3:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(1, 0, 0), PointsPerAxis);
            case 4:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(0, 1, 0), PointsPerAxis);
            case 5:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(0, 1, 1), PointsPerAxis);
            case 6:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(1, 1, 1), PointsPerAxis);
            case 7:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(1, 1, 0), PointsPerAxis);
            default: throw new IndexOutOfRangeException($"Cube corner index {index} out of range");
        }
    }

    private void CalculateCubeIndex()
    {
        _cubeVertexMask = 0;
        if (Points[CubeCorner(0)] < 0) _cubeVertexMask |= 1;

        if (Points[CubeCorner(1)] < 0) _cubeVertexMask |= 2;

        if (Points[CubeCorner(2)] < 0) _cubeVertexMask |= 4;

        if (Points[CubeCorner(3)] < 0) _cubeVertexMask |= 8;

        if (Points[CubeCorner(4)] < 0) _cubeVertexMask |= 16;

        if (Points[CubeCorner(5)] < 0) _cubeVertexMask |= 32;

        if (Points[CubeCorner(6)] < 0) _cubeVertexMask |= 64;

        if (Points[CubeCorner(7)] < 0) _cubeVertexMask |= 128;
    }

    private void CalculateVoxelVertex()
    {
        float3 numerator = new float3(0f, 0f, 0f);
        float denominator = 0f;
        for (int i = 0; i < 12; i++)
        {
            if ((_edgeMask & (1 << i)) != 0)
            {
                float3 intPoint = Interpolate(Points[CubeCorner(LookupTables.EdgeIndexToCorners[i, 0])],
                                              Points[CubeCorner(LookupTables.EdgeIndexToCorners[i, 1])],
                                              Utility
                                                  .IndexToChunkPoint(CubeCorner(LookupTables.EdgeIndexToCorners[i, 0]),
                                                                     PointsPerAxis),
                                              Utility
                                                  .IndexToChunkPoint(CubeCorner(LookupTables.EdgeIndexToCorners[i, 1]),
                                                                     PointsPerAxis)
                                             );
                float density = math.abs(Utility.DensityFunction(intPoint + Offset));
                denominator += density;
                numerator += intPoint * density;
            }
        }

        if (!Mathf.Approximately(denominator, 0f))
        {
            float3 point = numerator / denominator;
            VoxelPoints[_voxelIndex] = point;
            Normals[_voxelIndex] = Utility.DensityFunctionGradient(point + Offset);
            HasVertex[_voxelIndex] = true;
        }
    }

    private float3 Interpolate(float density1, float density2, float3 point1, float3 point2)
    {
        return point1 + (point2 - point1) * (0 - density1) / (density2 - density1);
    }
}

[BurstCompile]
public struct SeamsJob : IJob
{
    [ReadOnly]
    public NativeArray<float3> XChunk;
    // [ReadOnly]
    // public NativeArray<bool> XChunkHasKey;
    // [ReadOnly]
    // public NativeArray<float3> YChunk;
    // [ReadOnly]
    // public NativeArray<bool> YChunkHasKey;
    // [ReadOnly]
    // public NativeArray<float3> ZChunk;
    // [ReadOnly]
    // public NativeArray<bool> ZChunkHasKey;
    // [ReadOnly]
    // public NativeArray<float3> XYChunk;
    // [ReadOnly]
    // public NativeArray<bool> XYChunkHasKey;
    // [ReadOnly]
    // public NativeArray<float3> XZChunk;
    // [ReadOnly]
    // public NativeArray<bool> XZChunkHasKey;
    // [ReadOnly]
    // public NativeArray<float3> YZChunk;
    // [ReadOnly]
    // public NativeArray<bool> YZChunkHasKey;
    // [ReadOnly]
    // public NativeArray<float3> XYZChunk;
    // [ReadOnly]
    // public NativeArray<bool> XYZChunkHasKey;

    [NativeDisableParallelForRestriction]
    public NativeArray<float3> NewVertices;
    [NativeDisableParallelForRestriction]
    public NativeArray<int3> Triangles;
    [ReadOnly]
    public NativeArray<float3> VoxelPoints;
    [ReadOnly]
    public NativeArray<float> Points;
    [ReadOnly]
    public NativeArray<bool> HasVertex;
    [ReadOnly]
    public int PointsPerAxis;

    private int _cubeVertexMask;
    private int _edgeMask;
    private int3 _pointIndex3D;
    private int _voxelIndex;
    private int _newVerticesIndex;

    private int VoxelsPerAxis => PointsPerAxis - 1;

    public void Execute()
    {
        _newVerticesIndex = 0;
        //X
        int3 extra = int3.zero;
        extra.x = 16;
        for (int i = 0; i < VoxelsPerAxis - 1; i++)
        {
            for (int j = 0; j < VoxelsPerAxis - 1; j++)
            {
                _pointIndex3D = new int3(VoxelsPerAxis - 1, i, j);
                _voxelIndex = Utility.ChunkPointToOctreeIndex(VoxelPoints, _pointIndex3D, VoxelsPerAxis);
                if (!HasVertex[_voxelIndex]) continue;

                CalculateCubeIndex();
                _edgeMask = LookupTables.EdgeTable[_cubeVertexMask];
                MakeTriangles(XChunk, extra);
            }
        }
    }

    private int CubeCorner(int index)
    {
        switch (index)
        {
            case 0:
                return Utility.ChunkPointToIndex(_pointIndex3D, PointsPerAxis);
            case 1:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(0, 0, 1), PointsPerAxis);
            case 2:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(1, 0, 1), PointsPerAxis);
            case 3:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(1, 0, 0), PointsPerAxis);
            case 4:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(0, 1, 0), PointsPerAxis);
            case 5:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(0, 1, 1), PointsPerAxis);
            case 6:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(1, 1, 1), PointsPerAxis);
            case 7:
                return Utility.ChunkPointToIndex(_pointIndex3D + new int3(1, 1, 0), PointsPerAxis);
            default: throw new IndexOutOfRangeException($"Cube corner index {index} out of range");
        }
    }

    private void CalculateCubeIndex()
    {
        _cubeVertexMask = 0;
        if (Points[CubeCorner(0)] < 0) _cubeVertexMask |= 1;

        if (Points[CubeCorner(1)] < 0) _cubeVertexMask |= 2;

        if (Points[CubeCorner(2)] < 0) _cubeVertexMask |= 4;

        if (Points[CubeCorner(3)] < 0) _cubeVertexMask |= 8;

        if (Points[CubeCorner(4)] < 0) _cubeVertexMask |= 16;

        if (Points[CubeCorner(5)] < 0) _cubeVertexMask |= 32;

        if (Points[CubeCorner(6)] < 0) _cubeVertexMask |= 64;

        if (Points[CubeCorner(7)] < 0) _cubeVertexMask |= 128;
    }

    private void MakeTriangles(NativeArray<float3> relevantOctree, int3 extra)
    {
        for (int i = 0; i < LookupTables.ForwardEdges.Length; i++)
        {
            if ((_edgeMask & (1 << LookupTables.ForwardEdges[i])) == 0)
            {
                Triangles[_voxelIndex * 6 + i * 2] = int3.zero;
                Triangles[_voxelIndex * 6 + i * 2 + 1] = int3.zero;
                continue;
            }

            int3 tri = int3.zero;
            int3 corner1 = _pointIndex3D + LookupTables.TriangleDirections[i, 0];
            int3 corner2 = _pointIndex3D + LookupTables.TriangleDirections[i, 1];
            int3 corner3 = _pointIndex3D + LookupTables.TriangleDirections[i, 2];
            tri.x = Utility.ChunkPointToOctreeIndex(VoxelPoints, corner1, VoxelsPerAxis);
            if (!Utility.ValidGridPoint(corner2, VoxelsPerAxis))
            {
                corner2 -= extra;
                NewVertices[_newVerticesIndex] = relevantOctree[Utility.ChunkPointToOctreeIndex(relevantOctree, corner2, VoxelsPerAxis)];
                
            }
            else
            {
                NewVertices[_newVerticesIndex] =
                    VoxelPoints[Utility.ChunkPointToOctreeIndex(VoxelPoints, corner2, VoxelsPerAxis)];
            }
            tri.y = _newVerticesIndex;
            _newVerticesIndex++;
            
            if (!Utility.ValidGridPoint(corner3, VoxelsPerAxis))
            {
                corner3 -= extra;
                NewVertices[_newVerticesIndex] = relevantOctree[Utility.ChunkPointToOctreeIndex(relevantOctree, corner3, VoxelsPerAxis)];
            }
            else
            {
                NewVertices[_newVerticesIndex] =
                    VoxelPoints[Utility.ChunkPointToOctreeIndex(VoxelPoints, corner3, VoxelsPerAxis)];
            }
            tri.z = _newVerticesIndex;
            _newVerticesIndex++;
            Triangles[_voxelIndex * 6 + i * 2] = tri;

            tri = int3.zero;
            corner1 = _pointIndex3D + LookupTables.TriangleDirections[i, 3];
            corner2 = _pointIndex3D + LookupTables.TriangleDirections[i, 4];
            corner3 = _pointIndex3D + LookupTables.TriangleDirections[i, 5];
            tri.x = Utility.ChunkPointToOctreeIndex(VoxelPoints, corner1, VoxelsPerAxis);
            if (!Utility.ValidGridPoint(corner2, VoxelsPerAxis))
            {
                corner2 -= extra;
                NewVertices[_newVerticesIndex] = relevantOctree[Utility.ChunkPointToOctreeIndex(relevantOctree, corner2, VoxelsPerAxis)];
                
            }
            else
            {
                NewVertices[_newVerticesIndex] =
                    VoxelPoints[Utility.ChunkPointToOctreeIndex(VoxelPoints, corner2, VoxelsPerAxis)];
            }
            tri.y = _newVerticesIndex;
            _newVerticesIndex++;
            
            if (!Utility.ValidGridPoint(corner3, VoxelsPerAxis))
            {
                corner3 -= extra;
                NewVertices[_newVerticesIndex] = relevantOctree[Utility.ChunkPointToOctreeIndex(relevantOctree, corner3, VoxelsPerAxis)];
            }
            else
            {
                NewVertices[_newVerticesIndex] =
                    VoxelPoints[Utility.ChunkPointToOctreeIndex(VoxelPoints, corner3, VoxelsPerAxis)];
            }
            tri.z = _newVerticesIndex;
            _newVerticesIndex++;

            Triangles[_voxelIndex * 6 + i * 2 + 1] = tri;
        }
    }
}