﻿using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]

public class Chunk : MonoBehaviour
{
    #region Control Variables
    //General Info
    public WorldPos chunkCoords;
    public bool update;
    public bool rendered;

    //Measurements
    public static int chunkSize = 16;
    public static int chunkHeight = 64;

    //Components
    public Vector3 PosOffset { get { return new Vector3(chunkCoords.x * chunkSize, chunkCoords.y * chunkHeight, chunkCoords.z * chunkSize); } }
    public World world;
    public GameObject dot;

    //Storage
    bool pointsReady;
    static bool[,,] hits = new bool[chunkSize, chunkHeight, chunkSize];

    //Noise Parameters
    static float noiseScale = 0.12f;
    public static float threshold = 50f;
    public static float thresDropOff = 1f;
    
    //Other Options
    public bool meshRecalculate;
    #endregion

    #region Calculated Lists

    public static Vector3[] tetraPoints = { new Vector3(0, 0, 0), new Vector3(Mathf.Sqrt(3), 0, 1),
        new Vector3(0, 0, 2), new Vector3(Mathf.Sqrt(3), 0, -1),
        new Vector3(Mathf.Sqrt(3)-(2*Mathf.Sqrt(3) / 3), -2 * Mathf.Sqrt(1-Mathf.Sqrt(3)/3), 1),
        new Vector3(2 * Mathf.Sqrt(3) / 3, 2 * Mathf.Sqrt(1 - Mathf.Sqrt(3) / 3), 0) };

    public static Vector3[] hexPoints = {new Vector3(0,0,0), new Vector3(1,0,1), new Vector3(0,0,1), 
        new Vector3(1,0,0),new Vector3(1,-1,1), new Vector3(0,1,0)};

    static float f = 2f * Mathf.Sqrt(1 - (1 / Mathf.Sqrt(3)));
    static float g = (2f / 3f) * Mathf.Sqrt(3);
    static float h = Mathf.Sqrt(3);

    static Vector3[] p2H = { new Vector3(1f / h, ((-1f) * g) / (f * h), 0),
        new Vector3(0, 1f / f, 0), new Vector3(1f / (2f * h), ((-1f) * g) / (2f * f * h), 1f / 2f) };

    static Vector3[] h2P = { new Vector3(h, g, 0), new Vector3(0, f, 0), new Vector3(-1, 0, 2) };
    #endregion

    void Start()
    {
        GenerateMesh(new Vector3(chunkSize,chunkHeight,chunkSize));
        gameObject.name = "Chunk (" + chunkCoords.x + ", " + chunkCoords.y + ", " + chunkCoords.z + ")";
    }

    #region On Draw
    /// <summary>
    /// Method called when object is selected
    /// </summary>
    void OnDrawGizmosSelected ()
    {
        for(int i = 0; i < chunkSize; i++)
        {
            for (int j = 0; j < chunkHeight; j++)
            {
                for (int k = 0; k < chunkSize - 1; k++)
                {
                    if (hits[i, j, k])
                    {
                        Vector3 vert = HexToPos(new WorldPos(i, j, k));
                        Gizmos.color = Color.gray;
                        Gizmos.DrawSphere(vert, .2f);
                        if (world.show)
                        {
                            Vector3 dir = Procedural.Noise.noiseMethods[0][2](vert, noiseScale).derivative.normalized;
                            Gizmos.color = Color.yellow;
                            Gizmos.DrawRay(vert, dir);
                        }
                    }
                }
            }
        }
    }
    #endregion

    #region Face Construction
    /// <summary>
    /// Creates mesh
    /// </summary>
    /// <param name="wid">Width of points to be generated</param>
    void GenerateMesh(Vector3 size)
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector3> normals = new List<Vector3>();

        for (int i = 0; i < size.x; i++)
        {
            for (int j = 0; j < size.y; j++)
            {
                for (int k = 0; k < size.z; k++)
                {
                    Vector3 center = new Vector3(i, j, k);
                    Vector3 shiftedCenter = center + PosOffset*2;
                    if (Land(shiftedCenter) && GradientCheck(shiftedCenter))
                    {
                        hits[i, j, k] = true;
                        CreatePoint(center);
                    }
                }
            }
        }
        for (int i = 0; i < size.x; i++)
        {
            for (int j = 0; j < size.y; j++)
            {
                for (int k = 0; k < size.z; k++)
                {
                    Vector3 center = new Vector3(i, j, k);
                    FaceBuilder.Build(center, GetComponent<Chunk>(), ref verts, ref tris, ref normals);
                }
            }
        }
        //Mesh Procedure
        MeshFilter filter = gameObject.GetComponent<MeshFilter>();
        MeshCollider collider = gameObject.GetComponent<MeshCollider>();
        Mesh mesh = new Mesh();
        mesh.Clear();
        List<Vector3> posVerts = new List<Vector3>();
        foreach (Vector3 hex in verts)
            posVerts.Add(HexToPos(new WorldPos(Mathf.RoundToInt(hex.x), Mathf.RoundToInt(hex.y), Mathf.RoundToInt(hex.z))));
        mesh.vertices = posVerts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.normals = normals.ToArray();
        mesh.RecalculateBounds();
        filter.mesh = mesh;
        collider.sharedMesh = mesh;
        if (meshRecalculate) { filter.mesh.RecalculateNormals(); }
    }
    #endregion

    #region Update Mechanisms
    public bool UpdateChunk()
    {
        rendered = true;
        return true;
    }
    #endregion

    #region Checks
    /// <summary>
    /// Checks if a point is on the edge of a surface using IVT and gradients
    /// </summary>
    /// <param name="point">Point to check</param>
    /// <returns>Boolean</returns>
    bool GradientCheck(Vector3 point)
    {
        Vector3 normal = Procedural.Noise.noiseMethods[0][2](point, noiseScale).derivative.normalized;
        //normal += new Vector3(0, thresDropOff, 0);
        normal = normal.normalized * 3f;
        if (GetNoise(point + normal, noiseScale) > threshold - point.y * thresDropOff && GetNoise(point - normal, noiseScale) < threshold - point.y * thresDropOff)
            return true;
        return false;
    }

    /// <summary>
    /// Checks if the point is within a solid
    /// </summary>
    /// <param name="point">Point to check</param>
    /// <returns>Boolean</returns>
    bool Land(Vector3 point)
    {
        //print(GetNoise(point, noiseScale));
        return GetNoise(point, noiseScale) < threshold - point.y * thresDropOff;
    }

    /// <summary>
    /// Checks if a triangle faces the same direction as the noise
    /// </summary>
    /// <param name="center">Point to check</param>
    /// <param name="normal">Normal to check</param>
    /// <returns>Boolean</returns>
    public bool TriNormCheck(Vector3 center, Vector3 normal)
    {
        return 90 > Vector3.Angle(Procedural.Noise.noiseMethods[0][2](center, noiseScale).derivative, normal);
    }

    /// <summary>
    /// Returns value of 3D noise at a point
    /// </summary>
    /// <param name="pos">Point to check</param>
    /// <param name="scale">Size of the waves</param>
    /// <returns>Value of the noise</returns>
    public static float GetNoise(Vector3 pos, float scale)
    {
        return Procedural.Noise.noiseMethods[0][2](pos, scale).value * 20 + 10;
    }

    public static Vector3 GetNormal(Vector3 pos)
    {
        return Procedural.Noise.noiseMethods[0][2](pos, noiseScale).derivative.normalized;
    }

    /// <summary>
    /// Finds if this point is in the checks array
    /// </summary>
    /// <param name="point">Point to check</param>
    /// <returns>Boolean</returns>
    public bool CheckHit(Vector3 point)
    {
        bool output;
        try { output = hits[Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), Mathf.RoundToInt(point.z)]; }
        catch { output = false;}
        return output;
    }
    #endregion

    #region Conversions
    /// <summary>
    /// Converts from World Position to Hex Coordinates
    /// </summary>
    /// <param name="point">World Position</param>
    /// <returns>Hex Coordinate</returns>
    public WorldPos PosToHex (Vector3 point)
    {
        WorldPos output = new WorldPos();
        point.x -= PosOffset.x*2;
        point.y -= PosOffset.y*2;
        point.z -= PosOffset.z*2;
        output.x = Mathf.RoundToInt(p2H[0].x * point.x + p2H[0].y * point.y + p2H[0].z * point.z);
        output.y = Mathf.RoundToInt(p2H[1].x * point.x + p2H[1].y * point.y + p2H[1].z * point.z);
        output.z = Mathf.RoundToInt(p2H[2].x * point.x + p2H[2].y * point.y + p2H[2].z * point.z);
        return output;
    }

    /// <summary>
    /// Converts from Hex Coordinate to World Position
    /// </summary>
    /// <param name="point">Hex Coordinate</param>
    /// <returns>World Position</returns>
    public Vector3 HexToPos (WorldPos point)
    {
        Vector3 output = new Vector3();
        output.x = h2P[0].x * point.x + h2P[0].y * point.y + h2P[0].z * point.z;
        output.y = h2P[1].x * point.x + h2P[1].y * point.y + h2P[1].z * point.z;
        output.z = h2P[2].x * point.x + h2P[2].y * point.y + h2P[2].z * point.z;
        output.x += PosOffset.x*2;
        output.y += PosOffset.y*2;
        output.z += PosOffset.z*2;
        return output;
    }
    #endregion

    #region Debug
    void CreatePoint(Vector3 location)
    {
        WorldPos posLoc = new WorldPos(Mathf.RoundToInt(location.x), Mathf.RoundToInt(location.y), Mathf.RoundToInt(location.z));
        Vector3 warpedLocation = HexToPos(posLoc);
        if (world.pointLoc)
        {
            GameObject copy = Instantiate(dot, warpedLocation, new Quaternion(0, 0, 0, 0)) as GameObject;
            copy.transform.parent = gameObject.transform;
        }
    }

    public void FaceBuilderCheck(Vector3 center)
    {
        List<Vector3> vertTemp = new List<Vector3>();
        List<int> vertFail = new List<int>();
        List<int> vertSuccess = new List<int>();
        print(vertSuccess.Count);
        for (int i = 0; i < 6; i++)
        {
            Vector3 vert = center + hexPoints[i];
            print(vert + "(Check Point) " + i);
            if (CheckHit(vert))
            {
                vertTemp.Add(vert);
                vertSuccess.Add(i);
            }
            else
                vertFail.Add(i);
        }
        switch (vertTemp.Count)
        {
            case 6:
                print(center + "= " + "Octahedron");
                break;

            case 5:
                print(center + "= " + "Rectangular Prism");
                break;

            case 4:
                if (vertFail[0] == 4 && vertFail[1] == 5)
                    print(center + "= " + "Horizontal Square");
                else if (vertFail[0] == 2 && vertFail[1] == 3)
                    print(center + "= " + "Vertical Square");
                else if (vertFail[0] == 0 && vertFail[1] == 1)
                    print(center + "= " + "Vertical Square 2");
                else if (vertSuccess[2] == 4 && vertSuccess[3] == 5)
                    print(center + "= " + "Corner Tetrahedron");
                else if (vertFail[0] == 0 || vertFail[0] == 1)
                    print(center + "= " + "New Tetrahedron");
                else
                    print(center + "= " + "Tetrahedron");
                break;

            case 3:
                print(center + "= " + "Triangle");
                break;

            default:
                break;
        }
        if (CheckHit(center) && CheckHit(center + hexPoints[1]) && CheckHit(center - hexPoints[3] + hexPoints[5]) && CheckHit(center + hexPoints[2] + hexPoints[5]))
            print(center + "= " + "Third Diagonal");
    }
    #endregion
}