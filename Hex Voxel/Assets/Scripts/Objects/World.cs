﻿using System.Collections.Generic;
using UnityEngine;

public enum DebugMode { None, Octahedron, Gradient }
public enum PointMode { None, Gradient, All}

public class World : MonoBehaviour
{
    public bool pointLoc;
    public bool show;
    public float size;
    public GameObject chunk;

    public DebugMode debugMode = DebugMode.None;
    public PointMode pointMode;

    static float f = 2f * Mathf.Sqrt(1 - (1 / Mathf.Sqrt(3)));
    static float g = (2f / 3f) * Mathf.Sqrt(3);
    static float h = Mathf.Sqrt(3);

    static Vector3[] p2H = { new Vector3(1f / h, ((-1f) * g) / (f * h), 0),
        new Vector3(0, 1f / f, 0), new Vector3(1f / (2f * h), ((-1f) * g) / (2f * f * h), 1f / 2f) };

    static Vector3[] h2P = { new Vector3(h, g, 0), new Vector3(0, f, 0), new Vector3(-1, 0, 2) };

    public Dictionary<WorldPos, Chunk> chunks = new Dictionary<WorldPos, Chunk>();

    // Use this for initialization
    void Start()
    {
        
    }

    void Update()
    {
        
        if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.O))
            debugMode = debugMode != DebugMode.Octahedron ? DebugMode.Octahedron : DebugMode.None;
        if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.G))
            debugMode = debugMode != DebugMode.Gradient ? DebugMode.Gradient : DebugMode.None;
    }

    /// <summary>
    /// Instantiates a new Chunk and sets it up
    /// </summary>
    /// <param name="pos">Chunk Coords of the Chunk to be created</param>
    public void CreateChunk(WorldPos pos)
    {
        if (chunks.ContainsKey(pos))//This is kind of cheating this should have already been checked by some were getting through
            return;
        GameObject newChunk = Instantiate(chunk, new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0)) as GameObject;
        Chunk chunkScript = newChunk.GetComponent<Chunk>();
        chunkScript.chunkCoords = pos;
        chunkScript.world = GetComponent<World>();
        chunks.Add(chunkScript.chunkCoords, chunkScript);
    }

    /// <summary>
    /// Find the Chunk at a current position
    /// </summary>
    /// <param name="pos">True Position of test point</param>
    /// <returns>Chunk script</returns>
    public Chunk GetChunk(Vector3 pos)
    {
        WorldPos chunkCoord = PosToChunk(pos);
        Chunk output;
        if (chunks.TryGetValue(chunkCoord, out output))
            return output;
        else
            return null;
    }

    /// <summary>
    /// Eliminates a Chunk at given Chunk Coordiantes
    /// </summary>
    /// <param name="chunkCoord">Chunk Coordinates</param>
    public void DestroyChunk(WorldPos chunkCoord)
    {
        Chunk targetChunk = GetChunk(ChunkToPos(chunkCoord));
        if (targetChunk == null)
            print(targetChunk.chunkCoords);
        Destroy(targetChunk.gameObject);
        chunks.Remove(chunkCoord);
    }

    public Vector3 HexToPos(Vector3 point)
    {
        Vector3 output;
        output.x = h2P[0].x * point.x + h2P[0].y * point.y + h2P[0].z * point.z;
        output.y = h2P[1].x * point.x + h2P[1].y * point.y + h2P[1].z * point.z;
        output.z = h2P[2].x * point.x + h2P[2].y * point.y + h2P[2].z * point.z;
        return output;
    }

    public Vector3 PosToHex(Vector3 point)
    {
        Vector3 output;
        output.x = p2H[0].x * point.x + p2H[0].y * point.y + p2H[0].z * point.z;
        output.y = p2H[1].x * point.x + p2H[1].y * point.y + p2H[1].z * point.z;
        output.z = p2H[2].x * point.x + p2H[2].y * point.y + p2H[2].z * point.z;
        return output;
    }

    public WorldPos PosToChunk(Vector3 point)
    {
        Vector3 hex = PosToHex(point);
        Vector3 output;
        output.x = Mathf.FloorToInt(hex.x / Chunk.chunkSize);
        output.y = Mathf.FloorToInt(hex.y / Chunk.chunkHeight);
        output.z = Mathf.FloorToInt(hex.z / Chunk.chunkSize);
        return output.ToWorldPos();
    }

    public Vector3 ChunkToPos(WorldPos chunkCoord)
    {
        Vector3 output;
        output.x = chunkCoord.x * Chunk.chunkSize;
        output.y = chunkCoord.y * Chunk.chunkHeight;
        output.z = chunkCoord.z * Chunk.chunkSize;
        output += Vector3.one;
        return HexToPos(output);
    }
}
