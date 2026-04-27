// ============================================================================
// TerrainSphereModifier.cs — 球形体素编辑触发器
//
// 功能：
//   1. 在当前位置查找命中的体素区块
//   2. 对命中的区块批量执行球形减密度或加密度
//   3. 作为场景里的简单地形编辑入口
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 球形体素编辑触发器。
/// Finds nearby chunk relays and applies spherical terrain edits.
/// </summary>
public class TerrainSphereModifier : MonoBehaviour
{
    // ================================================================
    // Inspector 配置
    // ================================================================

    [Header("Operation Settings / 操作设置")]
    [Tooltip("球形操作半径，单位是世界坐标。\nWorld-space radius used for the edit operation.")]
    public float radius = 3f;

    [Tooltip("球形操作强度。减密度时表示挖掘深度，加密度时表示填充强度。\nEdit strength used by add and subtract operations.")]
    public float strength = 2f;

    [Tooltip("OverlapSphereNonAlloc 的缓存容量。默认值 64。\nPreallocated hit buffer size used by OverlapSphereNonAlloc.")]
    public int maxHits = 64;

    // ================================================================
    // 运行时状态
    // ================================================================

    Collider[] hits;
    readonly HashSet<TerrainChunkRelay> relays = new HashSet<TerrainChunkRelay>();

    /// <summary>
    /// 更通用的球形操作半径别名。
    /// More generic alias for the operation radius.
    /// </summary>
    public float OperationRadius
    {
        get => radius;
        set => radius = value;
    }

    /// <summary>
    /// 更通用的球形操作强度别名。
    /// More generic alias for the operation strength.
    /// </summary>
    public float OperationStrength
    {
        get => strength;
        set => strength = value;
    }

    /// <summary>
    /// 更通用的碰撞缓存容量别名。
    /// More generic alias for the overlap buffer size.
    /// </summary>
    public int OverlapBufferSize
    {
        get => maxHits;
        set => maxHits = value;
    }

    // ================================================================
    // 生命周期
    // ================================================================

    void Awake()
    {
        if (maxHits < 1)
        {
            maxHits = 1;
        }

        hits = new Collider[maxHits];
    }

    // ================================================================
    // 核心 API
    // ================================================================

    /// <summary>
    /// 在当前位置执行一次球形减密度。
    /// Compatibility entry for a subtract edit at the current transform position.
    /// </summary>
    public int ModifyNow()
    {
        return ApplySubtractAtCurrentPosition();
    }

    /// <summary>
    /// 在当前位置执行一次球形减密度。
    /// Applies a subtract edit at the current transform position.
    /// </summary>
    public int ApplySubtractAtCurrentPosition()
    {
        if (hits == null || hits.Length != maxHits)
        {
            hits = new Collider[maxHits];
        }

        relays.Clear();

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            hits,
            1 << 3,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            TerrainChunkRelay relay = hits[i].GetComponent<TerrainChunkRelay>();
            if (relay == null)
            {
                relay = hits[i].GetComponentInParent<TerrainChunkRelay>();
            }

            if (relay != null)
            {
                relays.Add(relay);
            }
        }

        int changedCount = 0;

        foreach (TerrainChunkRelay relay in relays)
        {
            if (relay != null && relay.ApplySubtractSphere(transform.position, radius, strength))
            {
                changedCount++;
            }
        }

        return changedCount;
    }

    /// <summary>
    /// 在当前位置执行一次球形加密度。
    /// Compatibility entry for an add edit at the current transform position.
    /// </summary>
    public int AddNow()
    {
        return ApplyAddAtCurrentPosition();
    }

    /// <summary>
    /// 在当前位置执行一次球形加密度。
    /// Applies an add edit at the current transform position.
    /// </summary>
    public int ApplyAddAtCurrentPosition()
    {
        if (hits == null || hits.Length != maxHits)
        {
            hits = new Collider[maxHits];
        }

        relays.Clear();

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            hits,
            1 << 3,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            TerrainChunkRelay relay = hits[i].GetComponent<TerrainChunkRelay>();
            if (relay == null)
            {
                relay = hits[i].GetComponentInParent<TerrainChunkRelay>();
            }

            if (relay != null)
            {
                relays.Add(relay);
            }
        }

        int changedCount = 0;

        foreach (TerrainChunkRelay relay in relays)
        {
            if (relay != null && relay.ApplyAddSphere(transform.position, radius, strength))
            {
                changedCount++;
            }
        }

        return changedCount;
    }
}
