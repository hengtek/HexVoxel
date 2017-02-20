﻿using UnityEditor;
using UnityEngine;

public class PointData : MonoBehaviour
{
    World world;
    Chunk chunk;

    void Start()
    {
        world = GameObject.Find("World").GetComponent<World>();
    }

    void OnDrawGizmosSelected()
    {
        if (Selection.activeGameObject != transform.gameObject)
            return;
        Gizmos.color = Color.green;
        Vector3 pos = transform.position;
        if (world.debugMode == DebugMode.Octahedron)
        {
            for (int i = 0; i < 6; i++)
            {
                for (int j = 5; j > i; j--)
                {
                    if (i % 2 == 1 || j != i + 1)
                        Gizmos.DrawLine(pos + GetTetra(i), pos + GetTetra(j));
                }
            }
        }
        world.GetChunk(pos).FaceBuilderCheck(world.GetChunk(pos).PosToHex(pos).ToVector3());
        WorldPos temp = world.GetChunk(pos).PosToHex(pos);
        //print(world.GetChunk(pos).HexToPos(temp) + ", " + temp.x + ", " + temp.y + ", " + temp.z);
    }

    Vector3 GetTetra(int index)
    {
        Vector3 vert = Chunk.tetraPoints[index];
        return vert;
    }
}