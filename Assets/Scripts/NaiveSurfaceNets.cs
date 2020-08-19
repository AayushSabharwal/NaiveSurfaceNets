using System;
using Unity.Mathematics;
using UnityEngine;

public class NaiveSurfaceNets : MonoBehaviour
{
    [SerializeField]
    private Vector3Int viewDistance = Vector3Int.one;
    [SerializeField]
    private int chunkSize = 16;
    [SerializeField]
    private float surfaceLevel;
    [SerializeField]
    private GameObject chunk;

    private int _totalChunks;
    private int _finishedChunks;
    private bool _doneSeams;

    public static NaiveSurfaceNets Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (math.ceilpow2(chunkSize) != chunkSize)
        {
            Debug.LogError("Chunk Size needs to be power of 2");
        }

        _totalChunks = 0;
        _finishedChunks = 0;
        for (int i = -viewDistance.x; i <= viewDistance.x; i++)
        {
            for (int j = -viewDistance.y; j <= viewDistance.y; j++)
            {
                for (int k = -viewDistance.z; k <= viewDistance.z; k++)
                {
                    _totalChunks++;
                    Chunk c = Instantiate(chunk, new Vector3(i, j, k) * chunkSize, Quaternion.identity)
                        .GetComponent<Chunk>();
                    c.Initialize(chunkSize, surfaceLevel);
                }
            }
        }
    }

    public void ChunkDone()
    {
        _finishedChunks++;
    }
}