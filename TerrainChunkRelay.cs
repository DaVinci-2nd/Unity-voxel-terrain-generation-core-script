// ============================================================================
// TerrainChunkRelay.cs — 体素区块操作转发器
//
// 功能：
//   1. 把球形修改请求转发给真实的体素区块实现
//   2. 优先走 IVoxelTerrainChunk 接口，兼容更通用的接入方式
//   3. 保留反射 fallback，继续兼容旧脚本
// ============================================================================

using System.Reflection;
using UnityEngine;

/// <summary>
/// 体素区块操作转发器。
/// Forwards terrain editing and synchronization calls to a chunk implementation.
/// </summary>
public class TerrainChunkRelay : MonoBehaviour
{
    // ================================================================
    // Inspector 配置
    // ================================================================

    [Header("Chunk Links / 区块链接")]
    [Tooltip("高精度源区块。当前区块作为低精度同步块时，从这里读取密度数据。\nHigh precision source chunk used when this relay acts as a synchronized proxy.")]
    public TerrainChunkRelay fullPrecisionSource;

    [Tooltip("需要跟随当前高精度区块同步的其他区块。\nOther relays that should synchronize after this chunk changes.")]
    public TerrainChunkRelay[] linkedChunks;

    // ================================================================
    // 运行时状态
    // ================================================================

    IVoxelTerrainChunk targetInterface;
    MonoBehaviour targetBehaviour;
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

    /// <summary>
    /// 更通用的高精度源区块别名。
    /// More generic alias for the high precision source chunk.
    /// </summary>
    public TerrainChunkRelay HighResolutionSourceChunk
    {
        get => fullPrecisionSource;
        set => fullPrecisionSource = value;
    }

    /// <summary>
    /// 更通用的同步目标区块别名。
    /// More generic alias for linked synchronization targets.
    /// </summary>
    public TerrainChunkRelay[] SynchronizedTargetChunks
    {
        get => linkedChunks;
        set => linkedChunks = value;
    }

    // ================================================================
    // 生命周期
    // ================================================================

    void Awake()
    {
        CacheTarget();
    }

    // ================================================================
    // 核心 API
    // ================================================================

    public bool ModifySphere(Vector3 worldCenter, float radius, float depth)
    {
        return ApplySubtractSphere(worldCenter, radius, depth);
    }

    /// <summary>
    /// 对当前命中的区块做球形减密度。
    /// Applies a subtract sphere in world space.
    /// </summary>
    public bool ApplySubtractSphere(Vector3 worldCenter, float radius, float depth)
    {
        if (fullPrecisionSource != null && fullPrecisionSource != this)
        {
            return fullPrecisionSource.ApplySubtractSphere(worldCenter, radius, depth);
        }

        if (!HasTarget() || !IsReady())
        {
            return false;
        }

        bool changed = targetInterface != null
            ? targetInterface.ApplyDensitySphere(worldCenter, radius, depth)
            : InvokeBool(applyMethod, targetBehaviour, worldCenter, radius, depth);

        if (!changed || linkedChunks == null)
        {
            return changed;
        }

        for (int i = 0; i < linkedChunks.Length; i++)
        {
            if (linkedChunks[i] != null)
            {
                linkedChunks[i].SynchronizeFromSource();
            }
        }

        return true;
    }

    /// <summary>
    /// 对当前命中的区块做球形加密度。
    /// Compatibility entry for spherical add operations.
    /// </summary>
    public bool AddSphere(Vector3 worldCenter, float radius, float height)
    {
        return ApplyAddSphere(worldCenter, radius, height);
    }

    /// <summary>
    /// 对当前命中的区块做球形加密度。
    /// Applies an add sphere in world space.
    /// </summary>
    public bool ApplyAddSphere(Vector3 worldCenter, float radius, float height)
    {
        if (fullPrecisionSource != null && fullPrecisionSource != this)
        {
            return fullPrecisionSource.ApplyAddSphere(worldCenter, radius, height);
        }

        if (!HasTarget() || !IsReady())
        {
            return false;
        }

        bool changed = targetInterface != null
            ? targetInterface.AddDensitySphere(worldCenter, radius, height)
            : InvokeBool(addMethod, targetBehaviour, worldCenter, radius, height);

        if (!changed || linkedChunks == null)
        {
            return changed;
        }

        for (int i = 0; i < linkedChunks.Length; i++)
        {
            if (linkedChunks[i] != null)
            {
                linkedChunks[i].SynchronizeFromSource();
            }
        }

        return true;
    }

    /// <summary>
    /// 用高精度源区块的密度场覆盖当前区块。
    /// Compatibility entry for synchronization from the source chunk.
    /// </summary>
    public bool SyncFromSource()
    {
        return SynchronizeFromSource();
    }

    /// <summary>
    /// 用高精度源区块的密度场覆盖当前区块。
    /// Synchronizes this relay from its high precision source.
    /// </summary>
    public bool SynchronizeFromSource()
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

        return targetInterface != null
            ? targetInterface.SyncDensityFromSource(sourceDensity, sourceXSize, sourceYSize, sourceZSize, sourceStep, sourceOrigin)
            : InvokeBool(syncMethod, targetBehaviour, sourceDensity, sourceXSize, sourceYSize, sourceZSize, sourceStep, sourceOrigin);
    }

    /// <summary>
    /// 判断目标区块是否已经完成生成。
    /// Returns true when the proxied chunk is ready.
    /// </summary>
    public bool IsReady()
    {
        if (!HasTarget())
        {
            return false;
        }

        return targetInterface != null
            ? targetInterface.IsTerrainReady()
            : InvokeBool(readyMethod, targetBehaviour);
    }

    /// <summary>
    /// 读取代理区块的密度数组。
    /// Returns the proxied chunk density samples.
    /// </summary>
    public float[] GetDensity()
    {
        if (!HasTarget())
        {
            return null;
        }

        return targetInterface != null
            ? targetInterface.GetDensitySamples()
            : densityMethod.Invoke(targetBehaviour, null) as float[];
    }

    /// <summary>
    /// 读取代理区块的 X 尺寸。
    /// Returns the density size on the X axis.
    /// </summary>
    public int GetXSize()
    {
        if (!HasTarget())
        {
            return 0;
        }

        return targetInterface != null
            ? targetInterface.GetDensitySizeX()
            : InvokeInt(xSizeMethod, targetBehaviour);
    }

    /// <summary>
    /// 读取代理区块的 Y 尺寸。
    /// Returns the density size on the Y axis.
    /// </summary>
    public int GetYSize()
    {
        if (!HasTarget())
        {
            return 0;
        }

        return targetInterface != null
            ? targetInterface.GetDensitySizeY()
            : InvokeInt(ySizeMethod, targetBehaviour);
    }

    /// <summary>
    /// 读取代理区块的 Z 尺寸。
    /// Returns the density size on the Z axis.
    /// </summary>
    public int GetZSize()
    {
        if (!HasTarget())
        {
            return 0;
        }

        return targetInterface != null
            ? targetInterface.GetDensitySizeZ()
            : InvokeInt(zSizeMethod, targetBehaviour);
    }

    /// <summary>
    /// 读取代理区块的采样步长。
    /// Returns the density step used by the proxied chunk.
    /// </summary>
    public int GetStep()
    {
        if (!HasTarget())
        {
            return 0;
        }

        return targetInterface != null
            ? targetInterface.GetDensityStep()
            : InvokeInt(stepMethod, targetBehaviour);
    }

    /// <summary>
    /// 读取代理区块的密度原点。
    /// Returns the density origin used by the proxied chunk.
    /// </summary>
    public Vector3 GetOrigin()
    {
        if (!HasTarget())
        {
            return Vector3.zero;
        }

        return targetInterface != null
            ? targetInterface.GetDensityOrigin()
            : InvokeVector3(originMethod, targetBehaviour);
    }

    // ================================================================
    // 内部辅助
    // ================================================================

    // 优先找通用接口实现，找不到再退回旧的反射方式。
    void CacheTarget()
    {
        targetInterface = null;
        targetBehaviour = null;
        readyMethod = null;
        applyMethod = null;
        addMethod = null;
        syncMethod = null;
        densityMethod = null;
        xSizeMethod = null;
        ySizeMethod = null;
        zSizeMethod = null;
        stepMethod = null;
        originMethod = null;

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            if (behaviour is IVoxelTerrainChunk chunk)
            {
                targetInterface = chunk;
                targetBehaviour = behaviour;
                return;
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
                targetBehaviour = behaviour;
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
        if (targetInterface != null || targetBehaviour != null)
        {
            return true;
        }

        CacheTarget();
        return targetInterface != null || targetBehaviour != null;
    }

    bool InvokeBool(MethodInfo method, MonoBehaviour behaviour, params object[] args)
    {
        if (method == null || behaviour == null)
        {
            return false;
        }

        object result = method.Invoke(behaviour, args);
        return result is bool value && value;
    }

    int InvokeInt(MethodInfo method, MonoBehaviour behaviour)
    {
        if (method == null || behaviour == null)
        {
            return 0;
        }

        object result = method.Invoke(behaviour, null);
        return result is int value ? value : 0;
    }

    Vector3 InvokeVector3(MethodInfo method, MonoBehaviour behaviour)
    {
        if (method == null || behaviour == null)
        {
            return Vector3.zero;
        }

        object result = method.Invoke(behaviour, null);
        return result is Vector3 value ? value : Vector3.zero;
    }
}
