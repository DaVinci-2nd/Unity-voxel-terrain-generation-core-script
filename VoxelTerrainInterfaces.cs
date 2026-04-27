// ============================================================================
// VoxelTerrainInterfaces.cs — 体素区块通用接口
//
// 功能：
//   1. 约定体素区块最小可用的对外能力
//   2. 约定网格刷新后的可选回调
//   3. 约定区块销毁时的可选回调
// ============================================================================

using UnityEngine;

/// <summary>
/// 体素区块通用接口。
/// Generic contract for a voxel terrain chunk.
/// </summary>
public interface IVoxelTerrainChunk
{
    /// <summary>
    /// 判断区块是否已经生成完成，且可以安全读写密度数据。
    /// Returns true when the chunk mesh and density data are ready to use.
    /// </summary>
    bool IsTerrainReady();

    /// <summary>
    /// 在世界空间中做一次球形减密度操作，常用于挖坑。
    /// Applies a spherical subtract operation in world space.
    /// </summary>
    bool ApplyDensitySphere(Vector3 worldCenter, float radius, float strength);

    /// <summary>
    /// 在世界空间中做一次球形加密度操作，常用于填土。
    /// Applies a spherical add operation in world space.
    /// </summary>
    bool AddDensitySphere(Vector3 worldCenter, float radius, float strength);

    /// <summary>
    /// 读取当前区块的密度采样数组。
    /// Returns the current density sample array.
    /// </summary>
    float[] GetDensitySamples();

    /// <summary>
    /// 返回 X 方向的密度网格尺寸。
    /// Returns the density grid size on the X axis.
    /// </summary>
    int GetDensitySizeX();

    /// <summary>
    /// 返回 Y 方向的密度网格尺寸。
    /// Returns the density grid size on the Y axis.
    /// </summary>
    int GetDensitySizeY();

    /// <summary>
    /// 返回 Z 方向的密度网格尺寸。
    /// Returns the density grid size on the Z axis.
    /// </summary>
    int GetDensitySizeZ();

    /// <summary>
    /// 返回密度采样步长，单位是本地区块坐标。
    /// Returns the sampling step used by the density field.
    /// </summary>
    int GetDensityStep();

    /// <summary>
    /// 返回密度场在本地区块空间中的原点。
    /// Returns the density origin in local chunk space.
    /// </summary>
    Vector3 GetDensityOrigin();

    /// <summary>
    /// 用另一份密度场数据同步当前区块。
    /// Synchronizes this chunk from another density source.
    /// </summary>
    bool SyncDensityFromSource(float[] sourceDensity, int sourceX, int sourceY, int sourceZ, int sourceStep, Vector3 sourceOrigin);
}

/// <summary>
/// 表面刷新回调接口。
/// Optional callback invoked after the chunk surface changes.
/// </summary>
public interface IVoxelSurfaceChangeReceiver
{
    /// <summary>
    /// 在体素表面重建完成后触发。
    /// Called after the voxel surface has been rebuilt.
    /// </summary>
    void OnVoxelSurfaceChanged();
}

/// <summary>
/// 区块销毁回调接口。
/// Optional callback invoked before the chunk component is destroyed.
/// </summary>
public interface IVoxelChunkLifecycleReceiver
{
    /// <summary>
    /// 在区块销毁阶段触发。
    /// Called during chunk destruction.
    /// </summary>
    void OnVoxelChunkDestroyed();
}
