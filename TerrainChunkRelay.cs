using System.Reflection;
using UnityEngine;

public class TerrainChunkRelay : MonoBehaviour
{
    public TerrainChunkRelay fullPrecisionSource;
    public TerrainChunkRelay[] linkedChunks;

    MonoBehaviour target;
    MethodInfo readyMethod;
    MethodInfo applyMethod;
    MethodInfo addMethod;
    MethodInfo syncMethod;
    MethodInfo densityMethod;
    MethodInfo xSizeMethod;
    MethodInfo ySizeMethod;
    MethodInfo zSizeMethod;
    MethodInfo stepMethod;
    MethodInfo originMethod;

    void Awake()
    {
        CacheTarget();
    }

    void CacheTarget()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            System.Type type = behaviour.GetType();

            MethodInfo ready = type.GetMethod("IsTerrainReady", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo apply = type.GetMethod("ApplyDensitySphere", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo add = type.GetMethod("AddDensitySphere", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo sync = type.GetMethod("SyncDensityFromSource", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo density = type.GetMethod("GetDensitySamples", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo xs = type.GetMethod("GetDensitySizeX", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo ys = type.GetMethod("GetDensitySizeY", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo zs = type.GetMethod("GetDensitySizeZ", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo step = type.GetMethod("GetDensityStep", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo origin = type.GetMethod("GetDensityOrigin", BindingFlags.Instance | BindingFlags.Public);

            if (ready != null && apply != null && add != null && sync != null && density != null && xs != null && ys != null && zs != null && step != null && origin != null)
            {
                target = behaviour;
                readyMethod = ready;
                applyMethod = apply;
                addMethod = add;
                syncMethod = sync;
                densityMethod = density;
                xSizeMethod = xs;
                ySizeMethod = ys;
                zSizeMethod = zs;
                stepMethod = step;
                originMethod = origin;
                return;
            }
        }
    }

    bool HasTarget()
    {
        if (target != null)
        {
            return true;
        }

        CacheTarget();
        return target != null;
    }

    public bool ModifySphere(Vector3 worldCenter, float radius, float depth)
    {
        if (fullPrecisionSource != null && fullPrecisionSource != this)
        {
            return fullPrecisionSource.ModifySphere(worldCenter, radius, depth);
        }

        if (!HasTarget() || !IsReady())
        {
            return false;
        }

        object result = applyMethod.Invoke(target, new object[] { worldCenter, radius, depth });
        bool changed = result is bool value && value;

        if (!changed || linkedChunks == null)
        {
            return changed;
        }

        for (int i = 0; i < linkedChunks.Length; i++)
        {
            if (linkedChunks[i] != null)
            {
                linkedChunks[i].SyncFromSource();
            }
        }

        return true;
    }

    public bool AddSphere(Vector3 worldCenter, float radius, float height)
    {
        if (fullPrecisionSource != null && fullPrecisionSource != this)
        {
            return fullPrecisionSource.AddSphere(worldCenter, radius, height);
        }

        if (!HasTarget() || !IsReady())
        {
            return false;
        }

        object result = addMethod.Invoke(target, new object[] { worldCenter, radius, height });
        bool changed = result is bool value && value;

        if (!changed || linkedChunks == null)
        {
            return changed;
        }

        for (int i = 0; i < linkedChunks.Length; i++)
        {
            if (linkedChunks[i] != null)
            {
                linkedChunks[i].SyncFromSource();
            }
        }

        return true;
    }

    public bool SyncFromSource()
    {
        if (fullPrecisionSource == null || fullPrecisionSource == this)
        {
            return false;
        }

        if (!HasTarget() || !fullPrecisionSource.IsReady())
        {
            return false;
        }

        float[] sourceDensity = fullPrecisionSource.GetDensity();
        int sourceXSize = fullPrecisionSource.GetXSize();
        int sourceYSize = fullPrecisionSource.GetYSize();
        int sourceZSize = fullPrecisionSource.GetZSize();
        int sourceStep = fullPrecisionSource.GetStep();
        Vector3 sourceOrigin = fullPrecisionSource.GetOrigin();

        if (sourceDensity == null || sourceDensity.Length == 0)
        {
            return false;
        }

        object result = syncMethod.Invoke(target, new object[] { sourceDensity, sourceXSize, sourceYSize, sourceZSize, sourceStep, sourceOrigin });
        return result is bool value && value;
    }

    public bool IsReady()
    {
        if (!HasTarget())
        {
            return false;
        }

        object result = readyMethod.Invoke(target, null);
        return result is bool value && value;
    }

    public float[] GetDensity()
    {
        if (!HasTarget())
        {
            return null;
        }

        return densityMethod.Invoke(target, null) as float[];
    }

    public int GetXSize()
    {
        if (!HasTarget())
        {
            return 0;
        }

        object result = xSizeMethod.Invoke(target, null);
        return result is int value ? value : 0;
    }

    public int GetYSize()
    {
        if (!HasTarget())
        {
            return 0;
        }

        object result = ySizeMethod.Invoke(target, null);
        return result is int value ? value : 0;
    }

    public int GetZSize()
    {
        if (!HasTarget())
        {
            return 0;
        }

        object result = zSizeMethod.Invoke(target, null);
        return result is int value ? value : 0;
    }

    public int GetStep()
    {
        if (!HasTarget())
        {
            return 0;
        }

        object result = stepMethod.Invoke(target, null);
        return result is int value ? value : 0;
    }

    public Vector3 GetOrigin()
    {
        if (!HasTarget())
        {
            return Vector3.zero;
        }

        object result = originMethod.Invoke(target, null);
        return result is Vector3 value ? value : Vector3.zero;
    }
}