using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[ExecuteInEditMode]
public class FunctionSampler : MonoBehaviour
{
    [SerializeField]
    private float function;
    [SerializeField]
    private Vector3 normal;
    [SerializeField]
    private Vector3 tangent;
    [SerializeField]
    private Vector3Int point;
    [SerializeField]
    private int cellWidth;
    [SerializeField]
    private int index;
    private NativeArray<float3> arr;
    

    private void Update()
    {
        function = Utility.DensityFunction(transform.position);
        normal = Utility.DensityFunctionGradient(transform.position);
        tangent = -1f / (float3)normal;
        // if (arr.IsCreated)
        // {
        //     arr.Dispose();
        // }
        // index = Utility.ChunkPointToOctreeIndex(new int3(point.x, point.y, point.z), cellWidth);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + normal);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + tangent);
    }
}