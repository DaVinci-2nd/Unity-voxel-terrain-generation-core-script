# Unity Voxel Terrain Core Script

> 这是一套可单独接入的 Unity 体素地形系统脚本。游戏原型为unity6 LTS (DX12)
> 包含区块生成、球形挖填、区块同步和可选缓存。
> Eng docx见 [README.en.md](README.en.md)。

## 文件清单

| 文件 | 职责 |
| --- | --- |
| `VolumePixelWorld.cs` | 生成密度场、构建 Marching Tetrahedra 网格、提供运行时挖填和同步接口 |
| `TerrainChunkRelay.cs` | 统一转发区块操作，优先走 `IVoxelTerrainChunk`，整理的代码中保留旧反射兼容 |
| `TerrainSphereModifier.cs` | 在场景里查找命中的区块，然后执行球形挖掘或填充 |
| `VoxelTerrainInterfaces.cs` | 定义区块接口、表面刷新回调接口、销毁回调接口 |

## 依赖清单

- Unity 运行时基础组件：
  - `MeshFilter`
  - `MeshRenderer`
  - `MeshCollider`
  - `Physics`
  - `Thread`
- 代码里的原项目硬依赖已去掉，改成了下面这些通用入口：
  - `worldSeed`
  - `useClassicalTerrainCurve`
  - `shapeCacheRootPath`
  - `IVoxelSurfaceChangeReceiver`
  - `IVoxelChunkLifecycleReceiver`
- `TerrainSphereModifier` 现在仍然固定查询 `Layer 3`。如果你直接用它，区块碰撞体要放在 `Layer 3`。

## 快速上手

1. 在 Unity 里创建一个空物体，比如 `VoxelChunk`。
2. 挂上 `VolumePixelWorld`。
3. 再给这个物体补一个 `MeshRenderer`，并指定材质。`VolumePixelWorld` 只会自动补 `MeshFilter` 和 `MeshCollider`。
4. 如果你要统一转发操作，再挂一个 `TerrainChunkRelay` 到同一个物体上。
5. 如果你要在场景里做球形编辑，再创建一个物体挂 `TerrainSphereModifier`，把它移动到想挖或想填的位置。
6. 如果你要缓存区块形状，就给 `shapeCacheRootPath` 填一个目录路径；留空就是不启用缓存。

## 场景接线

| 组件 | 必填 | 说明 |
| --- | --- | --- |
| `VolumePixelWorld` | 是 | 区块核心脚本 |
| `MeshRenderer` | 是 | 不补这个组件的话，网格生成了也看不见 |
| `TerrainChunkRelay` | 否 | 需要统一转发、LOD 同步时再挂 |
| `TerrainSphereModifier` | 否 | 需要运行时球形挖填时再挂 |
| `surfaceChangeReceiver` | 否 | 挂实现了 `IVoxelSurfaceChangeReceiver` 的 `MonoBehaviour` |
| `chunkLifecycleReceiver` | 否 | 挂实现了 `IVoxelChunkLifecycleReceiver` 的 `MonoBehaviour` |

## 如何使用

### 1. 生成基础区块

- 把 `VolumePixelWorld` 挂到一个空物体上。
- 按需要调整这几个字段：

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `worldSeed` | `0` | 地形随机种子 |
| `useClassicalTerrainCurve` | `false` | 是否启用经典地形曲线 |
| `mountainHeight` | `400` | 山体高度强度 |
| `mountainWidth` | `0.002` | 山体频率 |
| `landHeight` | `600` | 大地形起伏强度 |
| `landWidth` | `0.0005` | 大地形起伏频率 |
| `solidDepth` | `96` | 地表以下补出的实心深度 |
| `airHeight` | `24` | 地表以上保留的空气高度 |
| `isoLevel` | `0` | 等值面阈值 |
| `worldLOD` | `1` | 密度采样步长 |
| `isLOD` | `false` | 是否作为低精度同步块 |

### 2. 运行时球形挖坑

```csharp
TerrainSphereModifier modifier = GetComponent<TerrainSphereModifier>();
int changedChunks = modifier.ApplySubtractAtCurrentPosition();
```

### 3. 运行时球形填充

```csharp
TerrainSphereModifier modifier = GetComponent<TerrainSphereModifier>();
int changedChunks = modifier.ApplyAddAtCurrentPosition();
```

### 4. 手动调用区块接口

```csharp
IVoxelTerrainChunk chunk = GetComponent<IVoxelTerrainChunk>();
chunk.ApplyDensitySphere(transform.position, 3f, 2f);
```

### 5. 接入可选回调

```csharp
using UnityEngine;

public class ExampleSurfaceReceiver : MonoBehaviour, IVoxelSurfaceChangeReceiver
{
    public void OnVoxelSurfaceChanged()
    {
        Debug.Log("[ExampleSurfaceReceiver] Surface changed.");
    }
}
```

```csharp
using UnityEngine;

public class ExampleLifecycleReceiver : MonoBehaviour, IVoxelChunkLifecycleReceiver
{
    public void OnVoxelChunkDestroyed()
    {
        Debug.Log("[ExampleLifecycleReceiver] Chunk destroyed.");
    }
}
```

## 公开接口

### `IVoxelTerrainChunk`

| 方法 | 作用 |
| --- | --- |
| `IsTerrainReady()` | 判断区块是否可以安全读写 |
| `ApplyDensitySphere(...)` | 球形减密度，常用于挖坑 |
| `AddDensitySphere(...)` | 球形加密度，常用于填充 |
| `GetDensitySamples()` | 返回密度数组 |
| `GetDensitySizeX/Y/Z()` | 返回密度场尺寸 |
| `GetDensityStep()` | 返回采样步长 |
| `GetDensityOrigin()` | 返回密度原点 |
| `SyncDensityFromSource(...)` | 用外部密度场同步当前区块 |

### `TerrainChunkRelay`

| 方法 | 作用 |
| --- | --- |
| `ModifySphere(...)` | 旧名字，兼容球形减密度 |
| `ApplySubtractSphere(...)` | 新名字，球形减密度 |
| `AddSphere(...)` | 旧名字，兼容球形加密度 |
| `ApplyAddSphere(...)` | 新名字，球形加密度 |
| `SyncFromSource()` | 旧名字，兼容同步 |
| `SynchronizeFromSource()` | 新名字，同步当前区块 |

### `TerrainSphereModifier`

| 方法 | 作用 |
| --- | --- |
| `ModifyNow()` | 旧名字，兼容当前位置球形挖掘 |
| `ApplySubtractAtCurrentPosition()` | 新名字，当前位置球形挖掘 |
| `AddNow()` | 旧名字，兼容当前位置球形填充 |
| `ApplyAddAtCurrentPosition()` | 新名字，当前位置球形填充 |

## 技术原理

### 1. 密度场怎么生成

- 应该是脚本先按 `(x, z)` 采样多层 `PerlinNoise`，得到每一列的地表高度。
- 然后把地表高度扩成 `(x, y, z)` 三维密度场。
- 当前实现的核心写法比较直接：

```csharp
densityField[index] = surface - worldY;
```

- 大于等于 `isoLevel` 的点看成实心，小于 `isoLevel` 的点看成空气。

### 2. Marching Tetrahedra 怎么转网格

- 每个体素立方体会被拆成 6 个四面体。
- 每个四面体检查 6 条边有没有穿过等值面。
- 交点数量是 3 时生成 1 个三角形，交点数量是 4 时生成 2 个三角形。
- 关键入口在：
  - `BuildMeshFromDensity()`
  - `PolygoniseCube()`
  - `PolygoniseTetra()`

关键代码块：

```csharp
PolygoniseTetra(p0, p5, p1, p6, v0, v5, v1, v6, vertexList, triangleList);
PolygoniseTetra(p0, p1, p2, p6, v0, v1, v2, v6, vertexList, triangleList);
PolygoniseTetra(p0, p2, p3, p6, v0, v2, v3, v6, vertexList, triangleList);
```

### 3. 球形挖掘 / 填充怎么改密度

- 脚本会把世界坐标球心转到区块本地坐标。
- 再算出受影响的密度点范围。
- 在球体内部按一个平滑衰减去加密度或减密度。

关键代码块：

```csharp
float distance01 = Mathf.Sqrt(sqrDistance) / radius;
float falloff = 1f - distance01;
densityField[GetDensityIndex(x, yIndex, z)] -= strength * falloff * falloff;
```

### 4. 高低精度区块怎么同步

- `TerrainChunkRelay` 会先从高精度源区块拿到密度数据。
- 低精度区块按自己的采样点，到源密度场里取最近的值。
- 改完后重新构网格。

关键代码块：

```csharp
int sx = Mathf.Clamp(Mathf.RoundToInt((p.x - sourceOrigin.x) / sourceStep), 0, sourceX);
int sy = Mathf.Clamp(Mathf.RoundToInt((p.y - sourceOrigin.y) / sourceStep), 0, sourceY);
int sz = Mathf.Clamp(Mathf.RoundToInt((p.z - sourceOrigin.z) / sourceStep), 0, sourceZ);
```

## 关键代码块说明

| 方法 | 作用 |
| --- | --- |
| `Build()` | 区块主构建流程，先尝试缓存，再采样高度 |
| `BuildDensityField()` | 把二维高度图扩成三维密度场 |
| `BuildMeshFromDensity()` | 扫描所有体素单元并生成顶点和三角形 |
| `PolygoniseCube()` | 把立方体拆成四面体 |
| `PolygoniseTetra()` | 从四面体里提取等值面 |
| `ApplyDensitySphere()` | 球形挖掘 |
| `AddDensitySphere()` | 球形填充 |
| `SyncDensityFromSource()` | 用源区块同步当前区块 |

## 性能问题

- `BuildMeshFromDensity()` 会从头扫描整个区块体素，并且每个立方体都会继续拆成 6 个四面体去跑一遍组面流程。区块分辨率一上来，这一段就是最重的 CPU 开销。
- `ApplyMeshData()` 每次都会重新写入整份 `vertices` 和 `triangles`，然后再跑 `mesh.RecalculateBounds()`、`mesh.RecalculateNormals()`、`mesh.RecalculateTangents()`。这几步都属于整块重算，不是局部更新。
- `AddMeshColliderAndSetMesh()` 会把 `meshCollider.sharedMesh` 先清空再重新赋值。这会让碰撞网格整块刷新，运行时频繁改地形时开销会比较明显。
- `ApplyDensitySphere()`、`AddDensitySphere()`、`SyncDensityFromSource()` 这 3 个入口在数据改完以后，都会直接调用 `BuildMeshFromDensity()` 和 `ApplyMeshData()`。也就是每次修改一次地形，都会触发一次整块重建。
- `SyncDensityFromSource()` 会把目标区块的每一个密度点都重新映射一遍源区块数据。LOD 区块一多时，这一步会把同步成本继续放大。
- `TerrainChunkRelay` 在高精度源区块改动后，还会继续遍历 `linkedChunks` 逐个同步。连锁同步一多时，单次编辑可能带出多块一起重建。

## 已知限制

- 为游戏内源代码直接上传，包含单个区块的生成代码、地形同步器和地形修改器，仅供参考使用，直接塞进去会报错（并没有摘除我项目中的其它对接），需要修改一下。
- `TerrainSphereModifier` 仍然写死 `Layer 3` 查询，自行改动
- `VolumePixelWorld` 会自动补 `MeshFilter` 和 `MeshCollider`，不会自动补 `MeshRenderer`。
- 初次构建放在线程里，运行时挖填还是整块重建网格和碰撞体。
- 因此是暴力硬刷新，请自行使用多线程工程优化性能漏洞
- 缓存文件格式是简单二进制，没有版本号，会有隐患

## 验证

- 场景物体上已经挂了 `VolumePixelWorld`。
- 场景物体上已经挂了 `MeshRenderer` 并指定材质。
- 如果用了 `TerrainChunkRelay`，它和 `VolumePixelWorld` 在同一个物体上。
- 如果用了 `TerrainSphereModifier`，目标区块碰撞体在 `Layer 3`。
- 如果启用了缓存，`shapeCacheRootPath` 指向一个可写目录。

## 作者

DaVinci-2nd
https://github.com/DaVinci-2nd
https://space.bilibili.com/432070384
<!-- 留空，仓库所有者可自行填写其他个人资料 -->
