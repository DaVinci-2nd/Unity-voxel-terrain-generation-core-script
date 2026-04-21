using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

//体素世界生成器
public class VolumePixelWorld : MonoBehaviour
{
    int xSize;
    int zSize;
    int ySize;
    public float mountainHeight = 400f;
    public float mountainWidth = 0.002f;
    public float landHeight = 600f;
    public float landWidth = 0.0005f;
    public float solidDepth = 96f;
    public float airHeight = 24f;
    public float isoLevel = 0f;
    Dictionary<VertexKey, int> vertexCache;
    const float vertexSnapScale = 10000f;
    const float isoEpsilon = 0.00001f;

    Mesh mesh;
    int[] triangles;
    Vector3[] vertices;
    float[] densityField;
    float[] surfaceHeights;
    float baseMinY;
    private float y;
    public int worldLOD = 1;
    public bool isLOD;
    int R;
    Vector3 position;
    private bool classical;
    MeshFilter meshFilter;
    MeshCollider meshCollider;
    bool meshReady;
    string cachedMainBlockFolderPath;
    string cachedBlockFilePath;
    bool hasCachedBlockFolder;
    bool blockShapeDirty;
    public GrassRenderer grassRenderer;

    void Start()
    {
        CacheBlockFolder();

        classical = SetWorld.worldClassical;
        xSize = 50 / worldLOD;
        zSize = 50 / worldLOD;
        mesh = new Mesh();
        mesh.MarkDynamic();

        if (!TryGetComponent(out meshFilter))
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }
        meshFilter.sharedMesh = mesh;

        Random.InitState(SetWorld.worldSeed);
        R = Random.Range(-5000, 5000);
        position = transform.position;

        Thread threadBuild = new Thread(Build);
        threadBuild.Start();
        StartCoroutine(WaitForThreadCompletion(threadBuild));
    }

    void CacheBlockFolder()
    {
        hasCachedBlockFolder = false;
        cachedMainBlockFolderPath = null;
        cachedBlockFilePath = null;

        if (isLOD)
        {
            return;
        }

        string saveFolderPath = DataSaver.currentSaveFolderPath;
        if (string.IsNullOrEmpty(saveFolderPath))
        {
            return;
        }

        cachedMainBlockFolderPath = Path.Combine(saveFolderPath, "Blocks", "Main");
        Directory.CreateDirectory(cachedMainBlockFolderPath);

        cachedBlockFilePath = Path.Combine(cachedMainBlockFolderPath, GetCurrentBlockFileName());

        hasCachedBlockFolder = true;
    }

    string GetCurrentBlockFileName()
    {
        int blockX = Mathf.FloorToInt(transform.position.x / 50f);
        int blockZ = Mathf.FloorToInt(transform.position.z / 50f);
        return blockX + "_" + blockZ;
    }

    bool TryLoadBlockShapeData()
    {
        if (isLOD || !hasCachedBlockFolder || string.IsNullOrEmpty(cachedBlockFilePath))
        {
            return false;
        }

        if (!File.Exists(cachedBlockFilePath))
        {
            return false;
        }

        try
        {
            using (FileStream stream = new FileStream(cachedBlockFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int savedX = reader.ReadInt32();
                int savedY = reader.ReadInt32();
                int savedZ = reader.ReadInt32();
                float savedBaseMinY = reader.ReadSingle();

                if (savedX != xSize || savedZ != zSize || savedY < 1)
                {
                    return false;
                }

                int width = savedX + 1;
                int height = savedY + 1;
                int depth = savedZ + 1;
                int densityLength = width * height * depth;

                float[] loadedDensity = new float[densityLength];

                for (int i = 0; i < densityLength; i++)
                {
                    loadedDensity[i] = reader.ReadSingle();
                }

                ySize = savedY;
                baseMinY = savedBaseMinY;
                densityField = loadedDensity;
                blockShapeDirty = false;
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    void SaveBlockShapeData()
    {
        if (isLOD || !hasCachedBlockFolder || string.IsNullOrEmpty(cachedBlockFilePath))
        {
            return;
        }

        if (!blockShapeDirty)
        {
            return;
        }

        if (densityField == null || densityField.Length == 0)
        {
            return;
        }

        try
        {
            using (FileStream stream = new FileStream(cachedBlockFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(xSize);
                writer.Write(ySize);
                writer.Write(zSize);
                writer.Write(baseMinY);

                for (int i = 0; i < densityField.Length; i++)
                {
                    writer.Write(densityField[i]);
                }

                blockShapeDirty = false;
            }
        }
        catch
        {
        }
    }

    private IEnumerator WaitForThreadCompletion(Thread thread)
    {
        while (thread.IsAlive)
        {
            yield return null;
        }

        ApplyMeshData();
        meshReady = true;
    }

    void Build()
    {
        if (TryLoadBlockShapeData())
        {
            BuildMeshFromDensity();
            return;
        }

        surfaceHeights = new float[(xSize + 1) * (zSize + 1)];

        float minSurface = float.MaxValue;
        float maxSurface = float.MinValue;

        for (int i = 0, z = 0; z <= zSize; z++)
        {
            for (int x = 0; x <= xSize; x++)
            {
                y = (Mathf.PerlinNoise((position.x + x * worldLOD + R) * mountainWidth, (position.z + z * worldLOD - R) * mountainWidth) - 0.5f) * mountainHeight
                    + (Mathf.PerlinNoise((position.x + x * worldLOD + R) * 1.7f * mountainWidth, (position.z + z * worldLOD - R) * 1.7f * mountainWidth) - 0.5f) * 0.7f * mountainHeight * ((Mathf.PerlinNoise((position.x + x * worldLOD + R) * mountainWidth, (position.z + z * worldLOD - R) * mountainWidth) - 0.5f) + (Mathf.PerlinNoise((position.x + x * worldLOD - R) * landWidth, (position.z + z * worldLOD + R) * landWidth) - 0.5f))
                    + (Mathf.PerlinNoise((position.x + x * worldLOD + R) * 4f * mountainWidth, (position.z + z * worldLOD - R) * 4f * mountainWidth) - 0.5f) * 0.4f * mountainHeight * ((Mathf.PerlinNoise((position.x + x * worldLOD + R) * mountainWidth, (position.z + z * worldLOD - R) * mountainWidth) - 0.5f) + (Mathf.PerlinNoise((position.x + x * worldLOD - R) * landWidth, (position.z + z * worldLOD + R) * landWidth) - 0.5f))
                    + (Mathf.PerlinNoise((position.x + x * worldLOD + R) * 16f * mountainWidth, (position.z + z * worldLOD - R) * 16f * mountainWidth) - 0.5f) * 0.2f * mountainHeight * ((Mathf.PerlinNoise((position.x + x * worldLOD + R) * mountainWidth, (position.z + z * worldLOD - R) * mountainWidth) - 0.5f) + (Mathf.PerlinNoise((position.x + x * worldLOD - R) * landWidth, (position.z + z * worldLOD + R) * landWidth) - 0.5f))
                    + (Mathf.PerlinNoise((position.x + x * worldLOD + R) * 180f * mountainWidth, (position.z + z * worldLOD - R) * 180f * mountainWidth) - 0.5f) * 0.02f * mountainHeight * ((Mathf.PerlinNoise((position.x + x * worldLOD + R) * mountainWidth, (position.z + z * worldLOD - R) * mountainWidth) - 0.5f) + (Mathf.PerlinNoise((position.x + x * worldLOD - R) * landWidth, (position.z + z * worldLOD + R) * landWidth) - 0.5f))
                    + (Mathf.PerlinNoise((position.x + x * worldLOD - R) * landWidth, (position.z + z * worldLOD + R) * landWidth) - 0.5f) * landHeight;

                if (!classical)
                    y = ((Mathf.Cos(y * 0.015f) / -2f + 0.5f) + (Mathf.Cos(y * 0.032f) / -2f + 0.5f) + 0.3f) / 2 * y;
                else
                    y = y * Mathf.Abs(Mathf.Atan(y / 10f) / (Mathf.PI / 2f));

                surfaceHeights[i] = y;

                if (y < minSurface)
                {
                    minSurface = y;
                }

                if (y > maxSurface)
                {
                    maxSurface = y;
                }

                i++;
            }
        }

        baseMinY = minSurface - solidDepth;
        float maxY = maxSurface + airHeight;
        ySize = Mathf.Max(1, Mathf.CeilToInt((maxY - baseMinY) / worldLOD));

        BuildDensityField();
        BuildMeshFromDensity();
    }

    void BuildDensityField()
    {
        int width = xSize + 1;
        int height = ySize + 1;
        int slice = width * height;

        densityField = new float[width * height * (zSize + 1)];

        float step = worldLOD;

        for (int z = 0; z <= zSize; z++)
        {
            int zOffset = z * slice;
            int surfaceRow = z * width;

            for (int x = 0; x <= xSize; x++)
            {
                float surface = surfaceHeights[surfaceRow + x];
                int index = zOffset + x;
                float worldY = baseMinY;

                for (int yIndex = 0; yIndex <= ySize; yIndex++)
                {
                    densityField[index] = surface - worldY;
                    index += width;
                    worldY += step;
                }
            }
        }
    }

    struct VertexKey
    {
        public int x;
        public int y;
        public int z;

        public VertexKey(Vector3 v)
        {
            x = Mathf.RoundToInt(v.x * vertexSnapScale);
            y = Mathf.RoundToInt(v.y * vertexSnapScale);
            z = Mathf.RoundToInt(v.z * vertexSnapScale);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + z;
                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is VertexKey))
            {
                return false;
            }

            VertexKey other = (VertexKey)obj;
            return x == other.x && y == other.y && z == other.z;
        }
    }

    void BuildMeshFromDensity()
    {
        int cubeCount = xSize * ySize * zSize;

        List<Vector3> vertexList = new List<Vector3>(Mathf.Max(1024, cubeCount / 8));
        List<int> triangleList = new List<int>(Mathf.Max(2048, cubeCount / 2));
        vertexCache = new Dictionary<VertexKey, int>(Mathf.Max(1024, cubeCount / 8));

        for (int z = 0; z < zSize; z++)
        {
            for (int yIndex = 0; yIndex < ySize; yIndex++)
            {
                for (int x = 0; x < xSize; x++)
                {
                    PolygoniseCube(x, yIndex, z, vertexList, triangleList);
                }
            }
        }

        vertices = vertexList.ToArray();
        triangles = triangleList.ToArray();

        vertexList.Clear();
        triangleList.Clear();
        vertexCache.Clear();

        vertexList = null;
        triangleList = null;
        vertexCache = null;

        surfaceHeights = null;
    }

    void PolygoniseCube(int x, int yIndex, int z, List<Vector3> vertexList, List<int> triangleList)
    {
        int width = xSize + 1;
        int height = ySize + 1;
        int slice = width * height;

        int i000 = z * slice + yIndex * width + x;
        int i100 = i000 + 1;
        int i010 = i000 + width;
        int i110 = i010 + 1;
        int i001 = i000 + slice;
        int i101 = i001 + 1;
        int i011 = i001 + width;
        int i111 = i011 + 1;

        float v0 = densityField[i000];
        float v1 = densityField[i100];
        float v2 = densityField[i101];
        float v3 = densityField[i001];
        float v4 = densityField[i010];
        float v5 = densityField[i110];
        float v6 = densityField[i111];
        float v7 = densityField[i011];

        float wx = x * worldLOD;
        float wy = baseMinY + yIndex * worldLOD;
        float wz = z * worldLOD;
        float step = worldLOD;

        Vector3 p0 = new Vector3(wx, wy, wz);
        Vector3 p1 = new Vector3(wx + step, wy, wz);
        Vector3 p2 = new Vector3(wx + step, wy, wz + step);
        Vector3 p3 = new Vector3(wx, wy, wz + step);
        Vector3 p4 = new Vector3(wx, wy + step, wz);
        Vector3 p5 = new Vector3(wx + step, wy + step, wz);
        Vector3 p6 = new Vector3(wx + step, wy + step, wz + step);
        Vector3 p7 = new Vector3(wx, wy + step, wz + step);

        PolygoniseTetra(p0, p5, p1, p6, v0, v5, v1, v6, vertexList, triangleList);
        PolygoniseTetra(p0, p1, p2, p6, v0, v1, v2, v6, vertexList, triangleList);
        PolygoniseTetra(p0, p2, p3, p6, v0, v2, v3, v6, vertexList, triangleList);
        PolygoniseTetra(p0, p3, p7, p6, v0, v3, v7, v6, vertexList, triangleList);
        PolygoniseTetra(p0, p7, p4, p6, v0, v7, v4, v6, vertexList, triangleList);
        PolygoniseTetra(p0, p4, p5, p6, v0, v4, v5, v6, vertexList, triangleList);
    }

    void PolygoniseTetra(
    Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
    float d0, float d1, float d2, float d3,
    List<Vector3> vertexList, List<int> triangleList)
    {
        Vector3 c0 = Vector3.zero, c1 = Vector3.zero, c2 = Vector3.zero, c3 = Vector3.zero;
        int crossCount = 0;

        TryAddCrossPoint(ref c0, ref c1, ref c2, ref c3, ref crossCount, p0, p1, d0, d1);
        TryAddCrossPoint(ref c0, ref c1, ref c2, ref c3, ref crossCount, p0, p2, d0, d2);
        TryAddCrossPoint(ref c0, ref c1, ref c2, ref c3, ref crossCount, p0, p3, d0, d3);
        TryAddCrossPoint(ref c0, ref c1, ref c2, ref c3, ref crossCount, p1, p2, d1, d2);
        TryAddCrossPoint(ref c0, ref c1, ref c2, ref c3, ref crossCount, p1, p3, d1, d3);
        TryAddCrossPoint(ref c0, ref c1, ref c2, ref c3, ref crossCount, p2, p3, d2, d3);

        if (crossCount < 3 || crossCount > 4)
        {
            return;
        }

        Vector3 solidCenter = Vector3.zero;
        Vector3 airCenter = Vector3.zero;
        int solidCount = 0;
        int airCount = 0;

        if (d0 >= isoLevel) { solidCenter += p0; solidCount++; } else { airCenter += p0; airCount++; }
        if (d1 >= isoLevel) { solidCenter += p1; solidCount++; } else { airCenter += p1; airCount++; }
        if (d2 >= isoLevel) { solidCenter += p2; solidCount++; } else { airCenter += p2; airCount++; }
        if (d3 >= isoLevel) { solidCenter += p3; solidCount++; } else { airCenter += p3; airCount++; }

        Vector3 desiredNormal;
        if (solidCount > 0 && airCount > 0)
        {
            solidCenter /= solidCount;
            airCenter /= airCount;
            desiredNormal = (airCenter - solidCenter).normalized;
        }
        else
        {
            desiredNormal = Vector3.up;
        }

        if (crossCount == 3)
        {
            Sort3(ref c0, ref c1, ref c2, desiredNormal);
            AddOrientedTriangle(c0, c1, c2, desiredNormal, vertexList, triangleList);
        }
        else
        {
            Sort4(ref c0, ref c1, ref c2, ref c3, desiredNormal);
            AddOrientedTriangle(c0, c1, c2, desiredNormal, vertexList, triangleList);
            AddOrientedTriangle(c0, c2, c3, desiredNormal, vertexList, triangleList);
        }
    }

    void TryAddCrossPoint(
    ref Vector3 c0, ref Vector3 c1, ref Vector3 c2, ref Vector3 c3, ref int count,
    Vector3 a, Vector3 b, float da, float db)
    {
        bool insideA = da >= isoLevel;
        bool insideB = db >= isoLevel;

        if (insideA == insideB)
        {
            return;
        }

        Vector3 p = InterpolateIso(a, b, da, db);

        if (count > 0 && (c0 - p).sqrMagnitude <= isoEpsilon * isoEpsilon) return;
        if (count > 1 && (c1 - p).sqrMagnitude <= isoEpsilon * isoEpsilon) return;
        if (count > 2 && (c2 - p).sqrMagnitude <= isoEpsilon * isoEpsilon) return;
        if (count > 3 && (c3 - p).sqrMagnitude <= isoEpsilon * isoEpsilon) return;

        if (count == 0) c0 = p;
        else if (count == 1) c1 = p;
        else if (count == 2) c2 = p;
        else if (count == 3) c3 = p;

        count++;
    }

    float GetAngle(Vector3 p, Vector3 center, Vector3 axisX, Vector3 axisY)
    {
        Vector3 d = p - center;
        return Mathf.Atan2(Vector3.Dot(d, axisY), Vector3.Dot(d, axisX));
    }

    void Swap(ref Vector3 a, ref Vector3 b)
    {
        Vector3 t = a;
        a = b;
        b = t;
    }

    void Sort3(ref Vector3 a, ref Vector3 b, ref Vector3 c, Vector3 normal)
    {
        Vector3 center = (a + b + c) / 3f;

        Vector3 axisX = Vector3.Cross(normal, Vector3.up);
        if (axisX.sqrMagnitude < 0.000001f)
        {
            axisX = Vector3.Cross(normal, Vector3.right);
        }
        axisX.Normalize();

        Vector3 axisY = Vector3.Cross(normal, axisX).normalized;

        float aa = GetAngle(a, center, axisX, axisY);
        float ab = GetAngle(b, center, axisX, axisY);
        float ac = GetAngle(c, center, axisX, axisY);

        if (aa > ab) { Swap(ref a, ref b); float t = aa; aa = ab; ab = t; }
        if (ab > ac) { Swap(ref b, ref c); float t = ab; ab = ac; ac = t; }
        if (aa > ab) { Swap(ref a, ref b); }
    }

    void Sort4(ref Vector3 a, ref Vector3 b, ref Vector3 c, ref Vector3 d, Vector3 normal)
    {
        Vector3 center = (a + b + c + d) * 0.25f;

        Vector3 axisX = Vector3.Cross(normal, Vector3.up);
        if (axisX.sqrMagnitude < 0.000001f)
        {
            axisX = Vector3.Cross(normal, Vector3.right);
        }
        axisX.Normalize();

        Vector3 axisY = Vector3.Cross(normal, axisX).normalized;

        float aa = GetAngle(a, center, axisX, axisY);
        float ab = GetAngle(b, center, axisX, axisY);
        float ac = GetAngle(c, center, axisX, axisY);
        float ad = GetAngle(d, center, axisX, axisY);

        if (aa > ab) { Swap(ref a, ref b); float t = aa; aa = ab; ab = t; }
        if (ac > ad) { Swap(ref c, ref d); float t = ac; ac = ad; ad = t; }
        if (aa > ac) { Swap(ref a, ref c); float t = aa; aa = ac; ac = t; Swap(ref b, ref d); t = ab; ab = ad; ad = t; }
        if (ab > ad) { Swap(ref b, ref d); float t = ab; ab = ad; ad = t; }
        if (ab > ac) { Swap(ref b, ref c); }
    }

    Vector3 InterpolateIso(Vector3 a, Vector3 b, float da, float db)
    {
        float delta = db - da;

        if (Mathf.Abs(delta) < isoEpsilon)
        {
            return (a + b) * 0.5f;
        }

        float t = (isoLevel - da) / delta;
        t = Mathf.Clamp01(t);
        return a + (b - a) * t;
    }

    void AddUniqueCrossPoint(List<Vector3> cross, Vector3 point)
    {
        for (int i = 0; i < cross.Count; i++)
        {
            if ((cross[i] - point).sqrMagnitude <= isoEpsilon * isoEpsilon)
            {
                return;
            }
        }

        cross.Add(point);
    }

    void SortPolygonVertices(List<Vector3> verts, Vector3 normal)
    {
        if (verts == null || verts.Count < 3)
        {
            return;
        }

        Vector3 center = Vector3.zero;
        for (int i = 0; i < verts.Count; i++)
        {
            center += verts[i];
        }
        center /= verts.Count;

        Vector3 axisX = Vector3.Cross(normal, Vector3.up);
        if (axisX.sqrMagnitude < 0.000001f)
        {
            axisX = Vector3.Cross(normal, Vector3.right);
        }
        axisX.Normalize();

        Vector3 axisY = Vector3.Cross(normal, axisX).normalized;

        verts.Sort((a, b) =>
        {
            Vector3 da = a - center;
            Vector3 db = b - center;

            float angleA = Mathf.Atan2(Vector3.Dot(da, axisY), Vector3.Dot(da, axisX));
            float angleB = Mathf.Atan2(Vector3.Dot(db, axisY), Vector3.Dot(db, axisX));

            return angleA.CompareTo(angleB);
        });
    }

    void AddOrientedTriangle(
    Vector3 a, Vector3 b, Vector3 c,
    Vector3 desiredNormal,
    List<Vector3> vertexList, List<int> triangleList)
    {
        Vector3 faceNormal = Vector3.Cross(b - a, c - a);

        if (faceNormal.sqrMagnitude <= 0.00000001f)
        {
            return;
        }

        if (Vector3.Dot(faceNormal, desiredNormal) < 0f)
        {
            int ia = GetOrAddVertex(a, vertexList);
            int ib = GetOrAddVertex(c, vertexList);
            int ic = GetOrAddVertex(b, vertexList);

            if (ia == ib || ia == ic || ib == ic)
            {
                return;
            }

            triangleList.Add(ia);
            triangleList.Add(ib);
            triangleList.Add(ic);
        }
        else
        {
            int ia = GetOrAddVertex(a, vertexList);
            int ib = GetOrAddVertex(b, vertexList);
            int ic = GetOrAddVertex(c, vertexList);

            if (ia == ib || ia == ic || ib == ic)
            {
                return;
            }

            triangleList.Add(ia);
            triangleList.Add(ib);
            triangleList.Add(ic);
        }
    }

    int GetOrAddVertex(Vector3 v, List<Vector3> vertexList)
    {
        VertexKey key = new VertexKey(v);

        if (vertexCache.TryGetValue(key, out int index))
        {
            return index;
        }

        index = vertexList.Count;
        vertexList.Add(v);
        vertexCache.Add(key, index);
        return index;
    }

    int GetDensityIndex(int x, int yIndex, int z)
    {
        return (z * (ySize + 1) + yIndex) * (xSize + 1) + x;
    }

    Vector3 GetDensityPoint(int x, int yIndex, int z)
    {
        return new Vector3(x * worldLOD, baseMinY + yIndex * worldLOD, z * worldLOD);
    }

    void ApplyMeshData()
    {
        if (mesh == null || vertices == null || triangles == null)
        {
            return;
        }

        mesh.Clear();
        mesh.indexFormat = vertices.Length > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();

        if (!TryGetComponent(out meshFilter))
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        meshFilter.sharedMesh = mesh;

        if (!isLOD)
        {
            AddMeshColliderAndSetMesh();
        }
    }

    //制作碰撞器
    public void AddMeshColliderAndSetMesh()
    {
        if (!TryGetComponent(out meshCollider))
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        if (!TryGetComponent(out meshFilter))
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    public bool IsTerrainReady()
    {
        return meshReady && mesh != null && densityField != null && vertices != null && triangles != null;
    }

    public bool ApplyDensitySphere(Vector3 worldCenter, float radius, float strength)
    {
        if (!IsTerrainReady() || radius <= 0f || Mathf.Approximately(strength, 0f))
        {
            return false;
        }

        Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
        float sqrRadius = radius * radius;
        bool changed = false;

        float invStep = 1f / worldLOD;

        int minX = Mathf.Clamp(Mathf.FloorToInt((localCenter.x - radius) * invStep), 0, xSize);
        int maxX = Mathf.Clamp(Mathf.CeilToInt((localCenter.x + radius) * invStep), 0, xSize);

        int minY = Mathf.Clamp(Mathf.FloorToInt((localCenter.y - radius - baseMinY) * invStep), 0, ySize);
        int maxY = Mathf.Clamp(Mathf.CeilToInt((localCenter.y + radius - baseMinY) * invStep), 0, ySize);

        int minZ = Mathf.Clamp(Mathf.FloorToInt((localCenter.z - radius) * invStep), 0, zSize);
        int maxZ = Mathf.Clamp(Mathf.CeilToInt((localCenter.z + radius) * invStep), 0, zSize);

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int yIndex = minY; yIndex <= maxY; yIndex++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 point = GetDensityPoint(x, yIndex, z);
                    float sqrDistance = (point - localCenter).sqrMagnitude;

                    if (sqrDistance > sqrRadius)
                    {
                        continue;
                    }

                    float distance01 = Mathf.Sqrt(sqrDistance) / radius;
                    float falloff = 1f - distance01;
                    densityField[GetDensityIndex(x, yIndex, z)] -= strength * falloff * falloff;
                    changed = true;
                }
            }
        }

        if (!changed)
        {
            return false;
        }

        blockShapeDirty = true;
        BuildMeshFromDensity();
        ApplyMeshData();
        RefreshGrassIfNeeded();
        return true;
    }

    public bool AddDensitySphere(Vector3 worldCenter, float radius, float strength)
    {
        if (!IsTerrainReady() || radius <= 0f || Mathf.Approximately(strength, 0f))
        {
            return false;
        }

        Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
        float sqrRadius = radius * radius;
        bool changed = false;

        float invStep = 1f / worldLOD;

        int minX = Mathf.Clamp(Mathf.FloorToInt((localCenter.x - radius) * invStep), 0, xSize);
        int maxX = Mathf.Clamp(Mathf.CeilToInt((localCenter.x + radius) * invStep), 0, xSize);

        int minY = Mathf.Clamp(Mathf.FloorToInt((localCenter.y - radius - baseMinY) * invStep), 0, ySize);
        int maxY = Mathf.Clamp(Mathf.CeilToInt((localCenter.y + radius - baseMinY) * invStep), 0, ySize);

        int minZ = Mathf.Clamp(Mathf.FloorToInt((localCenter.z - radius) * invStep), 0, zSize);
        int maxZ = Mathf.Clamp(Mathf.CeilToInt((localCenter.z + radius) * invStep), 0, zSize);

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int yIndex = minY; yIndex <= maxY; yIndex++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 point = GetDensityPoint(x, yIndex, z);
                    float sqrDistance = (point - localCenter).sqrMagnitude;

                    if (sqrDistance > sqrRadius)
                    {
                        continue;
                    }

                    float distance01 = Mathf.Sqrt(sqrDistance) / radius;
                    float falloff = 1f - distance01;
                    densityField[GetDensityIndex(x, yIndex, z)] += strength * falloff * falloff;
                    changed = true;
                }
            }
        }

        if (!changed)
        {
            return false;
        }

        blockShapeDirty = true;
        BuildMeshFromDensity();
        ApplyMeshData();
        RefreshGrassIfNeeded();
        return true;
    }

    public float[] GetDensitySamples()
    {
        return densityField;
    }

    public int GetDensitySizeX()
    {
        return xSize;
    }

    public int GetDensitySizeY()
    {
        return ySize;
    }

    public int GetDensitySizeZ()
    {
        return zSize;
    }

    public int GetDensityStep()
    {
        return worldLOD;
    }

    public Vector3 GetDensityOrigin()
    {
        return new Vector3(0f, baseMinY, 0f);
    }

    public bool SyncDensityFromSource(float[] sourceDensity, int sourceX, int sourceY, int sourceZ, int sourceStep, Vector3 sourceOrigin)
    {
        if (!IsTerrainReady() || sourceDensity == null || sourceDensity.Length == 0 || sourceStep <= 0)
        {
            return false;
        }

        bool changed = false;
        int sourceWidth = sourceX + 1;
        int sourceHeight = sourceY + 1;

        for (int z = 0; z <= zSize; z++)
        {
            for (int yIndex = 0; yIndex <= ySize; yIndex++)
            {
                for (int x = 0; x <= xSize; x++)
                {
                    Vector3 p = GetDensityPoint(x, yIndex, z);

                    int sx = Mathf.Clamp(Mathf.RoundToInt((p.x - sourceOrigin.x) / sourceStep), 0, sourceX);
                    int sy = Mathf.Clamp(Mathf.RoundToInt((p.y - sourceOrigin.y) / sourceStep), 0, sourceY);
                    int sz = Mathf.Clamp(Mathf.RoundToInt((p.z - sourceOrigin.z) / sourceStep), 0, sourceZ);

                    int sourceIndex = (sz * sourceHeight + sy) * sourceWidth + sx;
                    int targetIndex = GetDensityIndex(x, yIndex, z);

                    float value = sourceDensity[sourceIndex];

                    if (!Mathf.Approximately(densityField[targetIndex], value))
                    {
                        densityField[targetIndex] = value;
                        changed = true;
                    }
                }
            }
        }

        if (!changed)
        {
            return false;
        }

        blockShapeDirty = true;
        BuildMeshFromDensity();
        ApplyMeshData();
        return true;
    }

    void RefreshGrassIfNeeded()
    {
        if (grassRenderer == null || isLOD)
        {
            return;
        }
        else
        {
            grassRenderer.RebuildGrassDistribution();
        }
    }

    /*
    private void OnDrawGizmos()
    {
        for(int i = 0; i < vertices.Length; i++)
        {
            Gizmos.DrawSphere(vertices[i], .1f);
        }
    }
    */


    void OnDestroy()
    {
        SaveBlockShapeData();
        WorldBuilder.ClearChunkGC();
    }
}
