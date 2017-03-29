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
    public bool rendered = true;
    public bool cornersReady;
    bool uniform;

    //Measurements
    public static int chunkSize = 8;
    public static int chunkHeight = 8;

    //Components
    public Vector3 HexOffset { get { return new Vector3(chunkCoords.x * chunkSize, chunkCoords.y * chunkHeight, chunkCoords.z * chunkSize); } }
    public Vector3 PosOffset { get { return world.HexToPos(new Vector3(chunkCoords.x * chunkSize, chunkCoords.y * chunkHeight, chunkCoords.z * chunkSize)); } }
    MeshFilter filter;
    MeshCollider coll;
    Mesh mesh;

    List<Vector3> verts = new List<Vector3>();
    List<int> tris = new List<int>();
    List<Vector3> normals = new List<Vector3>();
    List<Vector3> vertTemp = new List<Vector3>();
    List<int> vertFail = new List<int>();
    List<int> vertSuccess = new List<int>();

    public World world;
    public GameObject dot;

    //Storage
    bool pointsReady;
    bool[,,] vertexes = new bool[chunkSize, chunkHeight, chunkSize];
    Chunk[] neighbors = new Chunk[6];

    //Corners for Interpolation
    public float[,,] corners = new float[2, 2, 2];
    public bool[,,] cornerInitialized = new bool[2, 2, 2];

    //Noise Parameters
    public static float noiseScale = 0.01f;
    public static float threshold = 0f;
    public static float thresDropOff = .25f;
    
    //Other Options
    public bool meshRecalculate;
    #endregion

    #region Calculated Lists

    public static Vector3[] tetraPoints = { new Vector3(0, 0, 0), new Vector3(Mathf.Sqrt(3), 0, 1),
        new Vector3(0, 0, 2), new Vector3(Mathf.Sqrt(3), 0, -1),
        new Vector3(Mathf.Sqrt(3)-(2*Mathf.Sqrt(3) / 3), -2 * Mathf.Sqrt(1-Mathf.Sqrt(3)/3), 1),
        new Vector3(2 * Mathf.Sqrt(3) / 3, 2 * Mathf.Sqrt(1 - Mathf.Sqrt(3) / 3), 0) };

    public static Vector3[] hexPoints = {new Vector3(0,0,0), new Vector3(1,0,1), new Vector3(0,0,1),
        new Vector3(1,0,0),new Vector3(1,-1,1), new Vector3(0,1,0), new Vector3(-1,1,0), new Vector3(0,1,1)};

    public static float sqrt3 = Mathf.Sqrt(3);

    public static Vector3[] neighborChunkCoords = { new Vector3(-chunkSize * World.h, 0, chunkSize),
        new Vector3(chunkSize * World.h, 0, -chunkSize),
        new Vector3(-chunkHeight * World.g, -chunkHeight * World.f, 0),
        new Vector3(chunkHeight * World.g, chunkHeight * World.f, 0),
        new Vector3(0, 0, -2 * chunkSize), new Vector3(0, 0, 2 * chunkSize) };

    public static int[][] neighborChunkCorners = { new int[]{ 0,1,2,3}, new int[]{ 4,5,6,7}, new int[] {0,1,4,5},
        new int[] {2,3,6,7}, new int[] {0,2,4,6}, new int[] {1,3,5,7}};
    #endregion

    #region Start
    void Start()
    {
        filter = gameObject.GetComponent<MeshFilter>();
        coll = gameObject.GetComponent<MeshCollider>();
        mesh = new Mesh();
        StartGeneration();
    }

    public void StartGeneration()
    {
        gameObject.name = "Chunk (" + chunkCoords.x + ", " + chunkCoords.y + ", " + chunkCoords.z + ")";
        ResetValues();
        FindNeighbors();
        FindCorners();
        if (!uniform)
            GenerateMesh(new Vector3(chunkSize, chunkHeight, chunkSize));

        mesh.RecalculateBounds();
        if (meshRecalculate) { mesh.RecalculateNormals(); }
        filter.mesh = mesh;
        coll.sharedMesh = mesh;
    }

    void ResetValues()
    {
        uniform = false;
        corners = new float[2, 2, 2];
        cornerInitialized = new bool[2, 2, 2];
        verts.Clear();
        tris.Clear();
        normals.Clear();
        mesh.Clear();
        vertexes = new bool[chunkSize, chunkHeight, chunkSize];
    }
#endregion

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
                    if (vertexes[i, j, k])
                    {
                        Vector3 vert = HexToPos(new WorldPos(i, j, k));
                        Gizmos.color = Color.gray;
                        Gizmos.DrawSphere(vert, .2f);
                        if (world.showNormals)
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
    void FindNeighbors()
    {
        for (int i = 0; i < 6; i++)
        {
            try { neighbors[i] = world.GetChunk(PosOffset + neighborChunkCoords[i]); }
            catch { }
        }
    }

    void FindCorners()
    {
        for (int i = 0; i < 6; i++)
        {
            bool neighborExists = false;
            try { neighborExists = neighbors[i].cornersReady; }
            catch { neighborExists = false; }
            if (neighborExists)
            {
                for (int j = 0; j < 4; j++)
                {
                    int cornerIndex = neighborChunkCorners[i][j];
                    int x = cornerIndex >= 4 ? 1 : 0;
                    int y = cornerIndex / 2 % 2 == 1 ? 1 : 0;
                    int z = cornerIndex % 2 == 1 ? 1 : 0;
                    corners[x, y, z] = neighbors[i].corners[i < 2 ? (x == 1 ? 0 : 1) : x, (i == 2 || i == 3) ? (y == 1 ? 0 : 1) : y, i > 3 ? (z == 1 ? 0 : 1) : z];
                    cornerInitialized[x, y, z] = true;
                }
            }
        }
        for (int i = 0; i < 8; i++)
        {
            int x = i < 4 ? 1 : 0;
            int y = i / 2 % 2 == 1 ? 1 : 0;
            int z = i % 2 == 1 ? 1 : 0;
            if (!cornerInitialized[x, y, z])
                corners[x, y, z] = GetNoise(HexOffset + new Vector3(chunkSize * x, chunkHeight * y, chunkSize * z));
        }
        cornersReady = true;
        CheckUniformity();
    }
    
    /// <summary>
    /// Creates mesh
    /// </summary>
    /// <param name="wid">Width of points to be generated</param>
    void GenerateMesh(Vector3 size)
    {
        for (int i = 0; i < size.x; i++)
        {
            for (int j = 0; j < size.y; j++)
            {
                for (int k = 0; k < size.z; k++)
                {
                    Vector3 center = new Vector3(i, j, k);
                    Vector3 shiftedCenter = center + HexOffset;
                    if (GradientCheck(shiftedCenter))
                    {
                        vertexes[i,j,k] = true;
                        if(world.pointMode == PointMode.Gradient) { CreatePoint(center); }
                    }
                    if (world.pointMode == PointMode.All) { CreatePoint(center); }
                }
            }
        }
        for (int i = 0; i < size.x; i++)
        {
            for (int j = 0; j < size.y; j++)
            {
                for (int k = 0; k < size.z; k++)
                {
                    Build(new WorldPos(i,j,k));
                }
            }
        }

        //Mesh Procedure
        List<Vector3> posVerts = new List<Vector3>();
        foreach (Vector3 hex in verts)
        {
            Vector3 offset = Vector3.zero;
            Vector3 smooth = Vector3.zero;
            if (world.offsetLand)
                offset = .5f * GetNormal(HexToPos(hex.ToWorldPos())*50) + 2 * GetNormal(HexToPos(hex.ToWorldPos()) * 3);
            if(world.smoothLand)
            {
                Vector3 point = hex + HexOffset;
                Vector3 norm = Procedural.Noise.noiseMethods[0][2](point, noiseScale).derivative * 20 + new Vector3(0, thresDropOff, 0);
                norm = norm.normalized * sqrt3/2;
                float A = GetNoise(PosToHex(HexToPos(point.ToWorldPos()) + norm));
                float B = GetNoise(PosToHex(HexToPos(point.ToWorldPos()) - norm));
                float T = 0;
                smooth = norm.normalized * ((A + B) / 2 - T) / ((A - B)/2) * -sqrt3 / 2;
            }
            posVerts.Add(HexToPos(new WorldPos(Mathf.RoundToInt(hex.x), Mathf.RoundToInt(hex.y), Mathf.RoundToInt(hex.z))) + offset + smooth);
        }
        mesh.SetVertices(posVerts);
        mesh.SetTriangles(tris, 0);
        mesh.SetNormals(normals);
    }

    void Build(WorldPos center)
    {
        vertTemp = new List<Vector3>();
        vertSuccess = new List<int>();
        vertFail = new List<int>();
        GetHitList(center);
        bool[] hitList = new bool[6];
        foreach (int success in vertSuccess)
            hitList[success] = true;

        int[] tempTriArray;
        Vector3[] tempVertArray;

        tempTriArray = World.triLookup[World.boolArrayToInt(hitList)];
        tempVertArray = World.vertLookup[World.boolArrayToInt(hitList)];

        if (tempTriArray == null)
            tempTriArray = new int[0];
        if (tempVertArray == null)
            tempVertArray = new Vector3[0];

        List<int> tempTempTri = new List<int>();
        foreach (int tri in tempTriArray)
            tempTempTri.Add(tri + verts.Count);

        List<Vector3> temptempVert = new List<Vector3>();
        foreach (Vector3 vert in tempVertArray)
            temptempVert.Add(vert + center.ToVector3());

        tris.AddRange(tempTempTri);
        verts.AddRange(temptempVert);

        BuildThirdSlant(center);
    }

    void GetHitList(WorldPos center)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector3 vert = (center + hexPoints[i].ToWorldPos()).ToVector3();
            if (CheckHit(vert))
            {
                vertTemp.Add(vert);
                vertSuccess.Add(i);
            }
            else
                vertFail.Add(i);
        }
    }

    void BuildThirdSlant(WorldPos center)
    {
        if (CheckHit(center.ToVector3()) && CheckHit(center.ToVector3() + hexPoints[1]) && 
            CheckHit(center.ToVector3() - hexPoints[3] + hexPoints[5]) && 
            CheckHit(center.ToVector3() + hexPoints[2] + hexPoints[5]) && World.thirdDiagonalActive)
        {
            int vertCount = verts.Count;
            vertTemp.Clear();
            vertTemp.Add(center.ToVector3());
            vertTemp.Add(center.ToVector3() - hexPoints[3] + hexPoints[5]);
            vertTemp.Add(center.ToVector3() + hexPoints[2] + hexPoints[5]);
            vertTemp.Add(center.ToVector3());
            vertTemp.Add(center.ToVector3() + hexPoints[2] + hexPoints[5]);
            vertTemp.Add(center.ToVector3() + hexPoints[1]);
            for (int i = 0; i < 6; i++)
            {
                verts.Add(vertTemp[i]);
                tris.Add(vertCount + i);
            }
            for (int i = 0; i < 6; i++)
            {
                verts.Add(vertTemp[5 - i]);
                tris.Add(vertCount + 6 + i);
            }
        }
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
    /// <param name="point">Hex point to check</param>
    /// <returns>Boolean</returns>
    public bool GradientCheck(Vector3 point)
    {
        //float x = point.x;
        //float y = point.y;
        //float z = point.z;
        Vector3 gradient = Procedural.Noise.noiseMethods[0][2](point, noiseScale).derivative*20 + new Vector3(0, thresDropOff, 0);
        //gradient -= new Vector3(2 * x * 4 * Mathf.Pow(Mathf.Pow(x,2)+ Mathf.Pow(z, 2),3), 0, 2 * z * 4 * Mathf.Pow(Mathf.Pow(x, 2) + Mathf.Pow(z, 2), 3)) * Mathf.Pow(10,-12);
        gradient = gradient.normalized * sqrt3/2;
        WorldPos hexPos = new WorldPos(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), Mathf.RoundToInt(point.z));
        if (!Land(PosToHex(HexToPos(hexPos) + gradient)) && Land(PosToHex(HexToPos(hexPos) - gradient)))
            return true;
        return false;
    }

    public bool Land(Vector3 point)
    {
        return GetInterp(point)<threshold;
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

    public float GetInterp(Vector3 pos)
    {
        Vector3 low = HexOffset;
        Vector3 high = HexOffset + new Vector3(chunkSize, chunkHeight, chunkSize);

        float xD = (pos.x - low.x) / (high.x - low.x);
        float yD = (pos.y - low.y) / (high.y - low.y);
        float zD = (pos.z - low.z) / (high.z - low.z);

        float c00 = corners[0, 0, 0] * (1 - xD) + corners[1, 0, 0] * xD;
        float c01 = corners[0, 0, 1] * (1 - xD) + corners[1, 0, 1] * xD;
        float c10 = corners[0, 1, 0] * (1 - xD) + corners[1, 1, 0] * xD;
        float c11 = corners[0, 1, 1] * (1 - xD) + corners[1, 1, 1] * xD;

        float c0 = c00 * (1 - yD) + c10 * yD;
        float c1 = c01 * (1 - yD) + c11 * yD;

        float c = c0 * (1 - zD) + c1 * zD;
        return c;
    }

    public static float GetNoise(Vector3 pos)
    {
        float noiseVal = Procedural.Noise.noiseMethods[0][2](pos, noiseScale).value * 20 + pos.y * thresDropOff;
        return noiseVal;
    }

    public static Vector3 GetNormal(Vector3 pos)
    {
        return Procedural.Noise.noiseMethods[0][2](pos, noiseScale).derivative.normalized;
    }

    /// <summary>
    /// Finds if this point is in the checks array
    /// </summary>
    /// <param name="point">hex point to check</param>
    /// <returns>Boolean</returns>
    public bool CheckHit(Vector3 point)
    {
        bool output;
        if (point.x < chunkSize && point.x > -1 && point.y < chunkHeight && point.y > -1 && point.z < chunkSize && point.z > -1)
            output = vertexes[(int)(point.x + .5f), (int)(point.y + .5f), (int)(point.z + .5f)];
        else
            output = world.CheckHit(HexToPos(point.ToWorldPos()));
        return output;
    }

    void CheckUniformity()
    {
        bool allLow = true;
        bool allHigh = true;
        foreach (var corner in corners)
        {
            if (corner - threshold < 1)
                allHigh = false;
            if (corner - threshold > -1)
                allLow = false;
        }
        if (allHigh || allLow)
            uniform = true;
    }
    #endregion

    #region Conversions
    /// <summary>
    /// Converts from World Position to Hex Coordinates
    /// </summary>
    /// <param name="point">World Position</param>
    /// <returns>Hex Coordinate</returns>
    public Vector3 PosToHex (Vector3 point)
    {
        point.x -= PosOffset.x;
        point.y -= PosOffset.y;
        point.z -= PosOffset.z;
        return world.PosToHex(point);
    }

    /// <summary>
    /// Converts from Hex Coordinate to World Position
    /// </summary>
    /// <param name="point">Hex Coordinate</param>
    /// <returns>World Position</returns>
    public Vector3 HexToPos (WorldPos point)
    {
        Vector3 output = new Vector3();
        output = world.HexToPos(point.ToVector3());
        output.x += PosOffset.x;
        output.y += PosOffset.y;
        output.z += PosOffset.z;
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
        for (int i = 0; i < 6; i++)
        {
            Vector3 vert = center + hexPoints[i];
            print(vert + "(Check Point) " + i + ", " + CheckHit(vert));
            if (CheckHit(vert))
            {
                vertTemp.Add(vert);
                vertSuccess.Add(i);
            }
            else
                vertFail.Add(i);
        }
        print(vertSuccess.Count);
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
