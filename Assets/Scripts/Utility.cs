using System;
using System.Collections;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class Utility
{
    private const float Epsilon = 1e-4f;
    private static readonly float3 UnitX = new float3(1f, 0f, 0f);
    private static readonly float3 UnitY = new float3(0f, 1f, 0f);
    private static readonly float3 UnitZ = new float3(0f, 0f, 1f);
    private static readonly float3 NullFloat3 = new float3(float.MaxValue, float.MaxValue, float.MaxValue);

    public static int3 IndexToChunkPoint(int index, int pointsPerAxis)
    {
        return new int3(index / (pointsPerAxis * pointsPerAxis),
                        index % (pointsPerAxis * pointsPerAxis) /
                        pointsPerAxis, index % pointsPerAxis);
    }

    public static int ChunkPointToIndex(int3 chunkPoint, int pointsPerAxis)
    {
        return chunkPoint.x * pointsPerAxis * pointsPerAxis + chunkPoint.y * pointsPerAxis + chunkPoint.z;
    }

    public static bool ValidGridPoint(int3 point, int pointsPerAxis)
    {
        return point.x >= 0 && point.y >= 0 && point.z >= 0 && point.x < pointsPerAxis && point.y < pointsPerAxis &&
               point.z < pointsPerAxis;
    }

    public static float DensityFunction(float3 point)
    {
        // return noise.cnoise(point);
        return point.x * point.x * point.x - point.y * point.y + point.z * point.z;
    }

    public static float3 DensityFunctionGradient(float3 point)
    {
        float dfx = DensityFunction(point + Epsilon * UnitX) - DensityFunction(point - Epsilon * UnitX);
        float dfy = DensityFunction(point + Epsilon * UnitY) - DensityFunction(point - Epsilon * UnitY);
        float dfz = DensityFunction(point + Epsilon * UnitZ) - DensityFunction(point - Epsilon * UnitZ);
        return new float3(dfx, dfy, dfz) / (2 * Epsilon);
    }

    public static int ChunkPointToOctreeIndex(NativeArray<float3> octree, int3 point, int cellWidth)
    {
        if (point.x < 0 || point.y < 0 || point.z < 0 ||
            point.x >= cellWidth || point.y >= cellWidth || point.z >= cellWidth)
            throw new ArgumentException($"Utility/ChunkPointToOctreeIndex: point {point} out of range");
        int depth = Mathf.CeilToInt(math.log2(cellWidth*cellWidth*cellWidth) / 3) + 1;
        int3 cellCenter = new int3(cellWidth / 2, cellWidth / 2, cellWidth / 2);
        int currentDepth = 0;
        int index = 0;
        while (currentDepth+1 < depth)
        {
            int increment = 0;
            if (point.x >= cellCenter.x)
            {
                cellCenter.x += cellWidth >> (currentDepth + 2);
                increment |= 1;
            }
            else
                cellCenter.x -= cellWidth >> (currentDepth + 2);

            if (point.y >= cellCenter.y)
            {
                cellCenter.y += cellWidth >> (currentDepth + 2);
                increment |= 2;
            }
            else
                cellCenter.y -= cellWidth >> (currentDepth + 2);

            if (point.z >= cellCenter.z)
            {
                cellCenter.z += cellWidth >> (currentDepth + 2);
                increment |= 4;
            }
            else
                cellCenter.z -= cellWidth >> (currentDepth + 2);

            if ((index * 8 + increment+1) < octree.Length && (octree[index * 8 + increment + 1] != NullFloat3).or())
            {
                index = index * 8 + increment + 1;
                currentDepth++;
            }
            else
            {
                break;
            }
        }

        return index; 
               // + (utility.ceilPow8(cellWidth*cellWidth*cellWidth) - 1) / 7;
    }
}