using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    private NativeArray<float> _pointValues;
    private NativeArray<float3> _voxelVertices;
    private NativeArray<bool> _hasVertex;
    private NativeArray<int3> _triangles;
    private NativeArray<float3> _normals;
    private NativeArray<float3> _newVertices;
    private int _chunkSize;
    private float _surfaceLevel;
    private Vector3Int _currentIndex;
    private JobHandle _pointsJob;
    private JobHandle _voxelJob;
    private JobHandle _seamsJob;
    private JobHandle _dependencyJob;
    private bool _isProcessingPoints;
    private bool _isProcessingVoxels;
    private bool _isProcessingSeams;
    private bool _isAboveSurfaceLevel;
    private MeshFilter _filter;
    private Mesh _mesh;
    private List<Vector3> _meshVertices;
    private List<int> _meshTriangles;
    private List<Vector3> _meshNormals;

    private int PointsPerAxis => _chunkSize + 1;
    private int VoxelsPerAxis => PointsPerAxis - 1;
    private int NumPoints => PointsPerAxis * PointsPerAxis * PointsPerAxis;
    private int NumVoxels => VoxelsPerAxis * VoxelsPerAxis * VoxelsPerAxis;
    // private int OctreeDepth => Mathf.CeilToInt(math.log2(NumVoxels) / 3) + 1;
    private int OctreeLength => ((utility.ceilPow8(NumVoxels) << 3) - 1) / 7;
    private int OctreeOffset => (utility.ceilPow8(NumVoxels) - 1) / 7;

    public void Initialize(int chunkSize, float surfaceLevel)
    {
        _chunkSize = chunkSize;
        _surfaceLevel = surfaceLevel;
        _currentIndex = Vector3Int.FloorToInt(transform.position) / chunkSize;
        _dependencyJob = new JobHandle();
        _filter = gameObject.GetComponent<MeshFilter>();
        _mesh = new Mesh();
        _filter.sharedMesh = _mesh;
        _meshVertices = new List<Vector3>();
        _meshTriangles = new List<int>();
        _meshNormals = new List<Vector3>();

        if (_pointValues.IsCreated) _pointValues.Dispose();

        if (_voxelVertices.IsCreated) _voxelVertices.Dispose();

        if (_hasVertex.IsCreated) _hasVertex.Dispose();

        if (_triangles.IsCreated) _triangles.Dispose();

        if (_normals.IsCreated) _normals.Dispose();
        _pointValues = new NativeArray<float>(NumPoints, Allocator.Persistent,
                                              NativeArrayOptions.UninitializedMemory);
        _voxelVertices = new NativeArray<float3>(OctreeLength, Allocator.Persistent);
        _hasVertex = new NativeArray<bool>(OctreeLength, Allocator.Persistent,
                                           NativeArrayOptions.UninitializedMemory);
        _triangles = new NativeArray<int3>(OctreeLength * 6, Allocator.Persistent,
                                           NativeArrayOptions.UninitializedMemory);
        _normals = new NativeArray<float3>(OctreeLength, Allocator.Persistent,
                                           NativeArrayOptions.UninitializedMemory);
        _pointsJob = new PointValuesJob
                     {
                         Offset = new float3(_currentIndex.x, _currentIndex.y, _currentIndex.z),
                         Points = _pointValues,
                         PointsPerAxis = PointsPerAxis,
                         SurfaceLevel = _surfaceLevel
                     }.Schedule(_pointValues.Length, _dependencyJob);
        _isProcessingPoints = true;
        _isProcessingVoxels = false;
        _isProcessingSeams = false;
        _isAboveSurfaceLevel = true;
    }

    private void Update()
    {
        if (_isProcessingPoints && _pointsJob.IsCompleted)
        {
            _pointsJob.Complete();
            _dependencyJob.Complete();
            _dependencyJob = new JobHandle();
            if (_pointValues.Max() < 0f)
            {
                _isProcessingVoxels = false;
                _isAboveSurfaceLevel = false;
                NaiveSurfaceNets.Instance.ChunkDone();
            }
            else
            {
                _voxelJob = new VoxelVertexJob
                            {
                                Points = _pointValues,
                                PointsPerAxis = PointsPerAxis,
                                VoxelPoints = _voxelVertices,
                                HasVertex = _hasVertex,
                                Triangles = _triangles,
                                Normals = _normals,
                                Offset = transform.position,
                            }.Schedule(_pointValues.Length, _dependencyJob);
                _isProcessingPoints = false;
                _isProcessingVoxels = true;
            }
        }
        else if (!_isProcessingPoints && _isProcessingVoxels && _voxelJob.IsCompleted)
        {
            _voxelJob.Complete();
            _dependencyJob.Complete();
            _mesh.Clear();
            _meshVertices.Clear();
            _meshTriangles.Clear();
            _meshNormals.Clear();

            for (int i = OctreeOffset; i < _voxelVertices.Length; i++)
            {
                if (_hasVertex[i])
                {
                    _meshVertices.Add(_voxelVertices[i]);
                    _meshNormals.Add(_normals[i]);
                    for (int j = 0; j < 6; j++)
                    {
                        if ((_triangles[i*6+j] != int3.zero).or())
                        {
                            _meshTriangles.Add(_triangles[i * 6 + j].x - OctreeOffset);
                            _meshTriangles.Add(_triangles[i * 6 + j].y - OctreeOffset);
                            _meshTriangles.Add(_triangles[i * 6 + j].z - OctreeOffset);
                        }
                    }
                }
                else
                {
                    _meshVertices.Add(Vector3.zero);
                    _meshNormals.Add(Vector3.one);
                }
            }

            _mesh.vertices = _meshVertices.ToArray();
            _mesh.triangles = _meshTriangles.ToArray();
            _mesh.normals = _meshNormals.ToArray();
            _isProcessingVoxels = false;
        }
    }

    private void OnDisable()
    {
        _pointsJob.Complete();
        _voxelJob.Complete();
        _dependencyJob.Complete();
        _pointValues.Dispose();
        _voxelVertices.Dispose();
        _hasVertex.Dispose();
        _normals.Dispose();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(_currentIndex * _chunkSize + Vector3.one * _chunkSize * 0.5f, Vector3.one * _chunkSize);
    }
}