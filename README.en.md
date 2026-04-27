# Unity Voxel Terrain Core Script

> Note: the English text in this file was produced with translation software and lightly checked.
> If wording feels odd, use the Chinese README as the main reference.
> Used AI translation
> 中文文档见 [README.md](README.md)。

## File List

| File | Responsibility |
| --- | --- |
| `VolumePixelWorld.cs` | Builds the density field, generates the Marching Tetrahedra mesh, and exposes runtime edit and sync APIs |
| `TerrainChunkRelay.cs` | Forwards chunk operations, prefers `IVoxelTerrainChunk`, and keeps the old reflection fallback |
| `TerrainSphereModifier.cs` | Finds nearby chunks in the scene and applies spherical dig/fill operations |
| `VoxelTerrainInterfaces.cs` | Defines the chunk API, surface refresh callback, and destroy callback |

## What This Repo Includes

- A readable voxel chunk generation core for Unity.
- A density-field based terrain editing API.
- A basic high-detail / low-detail chunk sync flow.
- A generic interface layer extracted from project-specific dependencies.

## What This Repo Does Not Include

- A full Unity sample scene.
- Materials, textures, prefabs, or a world streaming system.
- A complete performance pass.

## Dependencies

- Unity runtime basics:
  - `MeshFilter`
  - `MeshRenderer`
  - `MeshCollider`
  - `Physics`
  - `Thread`
- Project-specific hard references were removed and replaced with:
  - `worldSeed`
  - `useClassicalTerrainCurve`
  - `shapeCacheRootPath`
  - `IVoxelSurfaceChangeReceiver`
  - `IVoxelChunkLifecycleReceiver`
- `TerrainSphereModifier` still queries `Layer 3`, so target chunk colliders must stay on `Layer 3` if you use it as-is.

## Quick Start

1. Create an empty GameObject, for example `VoxelChunk`.
2. Attach `VolumePixelWorld`.
3. Add `MeshRenderer` and assign a material. `VolumePixelWorld` only auto-adds `MeshFilter` and `MeshCollider`.
4. If you want a forwarding entry point, attach `TerrainChunkRelay` on the same GameObject.
5. If you want spherical runtime edits, create another GameObject, attach `TerrainSphereModifier`, and move it to the target position.
6. If you want chunk shape caching, fill `shapeCacheRootPath`. Leave it empty to disable caching.

## Scene Wiring

| Component | Required | Notes |
| --- | --- | --- |
| `VolumePixelWorld` | Yes | Core chunk script |
| `MeshRenderer` | Yes | Without it, the generated mesh will not be visible |
| `TerrainChunkRelay` | No | Needed for forwarding and LOD synchronization |
| `TerrainSphereModifier` | No | Needed for spherical runtime edits |
| `surfaceChangeReceiver` | No | Assign a `MonoBehaviour` that implements `IVoxelSurfaceChangeReceiver` |
| `chunkLifecycleReceiver` | No | Assign a `MonoBehaviour` that implements `IVoxelChunkLifecycleReceiver` |

## How To Use

### 1. Create a Basic Chunk

Tune these fields on `VolumePixelWorld`:

| Field | Default | Meaning |
| --- | --- | --- |
| `worldSeed` | `0` | Terrain random seed |
| `useClassicalTerrainCurve` | `false` | Enables the classical terrain curve |
| `mountainHeight` | `400` | Mountain height strength |
| `mountainWidth` | `0.002` | Mountain frequency |
| `landHeight` | `600` | Broad terrain height strength |
| `landWidth` | `0.0005` | Broad terrain frequency |
| `solidDepth` | `96` | Extra solid depth below the sampled surface |
| `airHeight` | `24` | Extra air height above the sampled surface |
| `isoLevel` | `0` | Iso-surface threshold |
| `worldLOD` | `1` | Density sampling step |
| `isLOD` | `false` | Marks the chunk as a synchronized low-detail chunk |

### 2. Dig at Runtime

```csharp
TerrainSphereModifier modifier = GetComponent<TerrainSphereModifier>();
int changedChunks = modifier.ApplySubtractAtCurrentPosition();
```

### 3. Fill at Runtime

```csharp
TerrainSphereModifier modifier = GetComponent<TerrainSphereModifier>();
int changedChunks = modifier.ApplyAddAtCurrentPosition();
```

### 4. Call the Chunk Interface Directly

```csharp
IVoxelTerrainChunk chunk = GetComponent<IVoxelTerrainChunk>();
chunk.ApplyDensitySphere(transform.position, 3f, 2f);
```

### 5. Hook Optional Receivers

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

## Public APIs

### `IVoxelTerrainChunk`

| Method | Purpose |
| --- | --- |
| `IsTerrainReady()` | Returns whether the chunk is ready for read/write operations |
| `ApplyDensitySphere(...)` | Spherical subtract, commonly used for digging |
| `AddDensitySphere(...)` | Spherical add, commonly used for filling |
| `GetDensitySamples()` | Returns the density array |
| `GetDensitySizeX/Y/Z()` | Returns the density field dimensions |
| `GetDensityStep()` | Returns the sampling step |
| `GetDensityOrigin()` | Returns the density origin |
| `SyncDensityFromSource(...)` | Synchronizes this chunk from an external density field |

### `TerrainChunkRelay`

| Method | Purpose |
| --- | --- |
| `ModifySphere(...)` | Legacy name for spherical subtract |
| `ApplySubtractSphere(...)` | New alias for spherical subtract |
| `AddSphere(...)` | Legacy name for spherical add |
| `ApplyAddSphere(...)` | New alias for spherical add |
| `SyncFromSource()` | Legacy synchronization name |
| `SynchronizeFromSource()` | New synchronization alias |

### `TerrainSphereModifier`

| Method | Purpose |
| --- | --- |
| `ModifyNow()` | Legacy name for subtracting at the current position |
| `ApplySubtractAtCurrentPosition()` | New alias for subtracting at the current position |
| `AddNow()` | Legacy name for adding at the current position |
| `ApplyAddAtCurrentPosition()` | New alias for adding at the current position |

## Technical Notes

### 1. Density Field Generation

- The script samples layered `PerlinNoise` values on `(x, z)` to get a surface height per column.
- Then it expands that height field into a full `(x, y, z)` density field.
- The core line is:

```csharp
densityField[index] = surface - worldY;
```

- Samples greater than or equal to `isoLevel` are treated as solid; samples below `isoLevel` are treated as air.

### 2. Marching Tetrahedra Mesh Extraction

- Each voxel cube is split into 6 tetrahedra.
- Each tetrahedron checks which of its 6 edges crosses the iso-surface.
- 3 intersection points produce 1 triangle; 4 intersection points produce 2 triangles.
- Main entry points:
  - `BuildMeshFromDensity()`
  - `PolygoniseCube()`
  - `PolygoniseTetra()`

Key code block:

```csharp
PolygoniseTetra(p0, p5, p1, p6, v0, v5, v1, v6, vertexList, triangleList);
PolygoniseTetra(p0, p1, p2, p6, v0, v1, v2, v6, vertexList, triangleList);
PolygoniseTetra(p0, p2, p3, p6, v0, v2, v3, v6, vertexList, triangleList);
```

### 3. Runtime Dig / Fill

- The script converts the world-space sphere center to local chunk space.
- Then it finds the affected density sample range.
- Inside the sphere, it applies a smooth falloff and adds or subtracts density.

Key code block:

```csharp
float distance01 = Mathf.Sqrt(sqrDistance) / radius;
float falloff = 1f - distance01;
densityField[GetDensityIndex(x, yIndex, z)] -= strength * falloff * falloff;
```

### 4. High-Detail / Low-Detail Chunk Sync

- `TerrainChunkRelay` reads density data from the high-detail source chunk.
- The target chunk samples the nearest values using its own grid step.
- After that, it rebuilds the mesh.

Key code block:

```csharp
int sx = Mathf.Clamp(Mathf.RoundToInt((p.x - sourceOrigin.x) / sourceStep), 0, sourceX);
int sy = Mathf.Clamp(Mathf.RoundToInt((p.y - sourceOrigin.y) / sourceStep), 0, sourceY);
int sz = Mathf.Clamp(Mathf.RoundToInt((p.z - sourceOrigin.z) / sourceStep), 0, sourceZ);
```

## Key Method Guide

| Method | Purpose |
| --- | --- |
| `Build()` | Main build flow: cache first, otherwise sample heights |
| `BuildDensityField()` | Expands the 2D height field into a 3D density field |
| `BuildMeshFromDensity()` | Scans all voxel cells and produces vertices and triangles |
| `PolygoniseCube()` | Splits a cube into tetrahedra |
| `PolygoniseTetra()` | Extracts the iso-surface from one tetrahedron |
| `ApplyDensitySphere()` | Runtime dig operation |
| `AddDensitySphere()` | Runtime fill operation |
| `SyncDensityFromSource()` | Synchronizes one chunk from another density source |

## Performance Issues

- `BuildMeshFromDensity()` scans the whole chunk volume from the start, and each cube is further split into 6 tetrahedra for polygon generation. Once the chunk resolution goes up, this becomes the main CPU hotspot.
- `ApplyMeshData()` rewrites the full `vertices` and `triangles` arrays every time, then runs `mesh.RecalculateBounds()`, `mesh.RecalculateNormals()`, and `mesh.RecalculateTangents()`. These are full-chunk recalculations, not partial updates.
- `AddMeshColliderAndSetMesh()` clears `meshCollider.sharedMesh` and assigns it again. That forces a full collider refresh, which becomes expensive when terrain edits happen often at runtime.
- `ApplyDensitySphere()`, `AddDensitySphere()`, and `SyncDensityFromSource()` all call `BuildMeshFromDensity()` and `ApplyMeshData()` right after changing data. In practice, each terrain edit triggers a full chunk rebuild.
- `SyncDensityFromSource()` remaps every density sample in the target chunk from the source data. When many LOD chunks are present, this step can scale the synchronization cost up quickly.
- `TerrainChunkRelay` also walks through `linkedChunks` and synchronizes them one by one after a source chunk changes. If many linked chunks exist, one edit can cascade into multiple chunk rebuilds.

## Known Limits

- This repo only contains the core scripts, not a full demo project.
- `TerrainSphereModifier` still hardcodes `Layer 3`.
- `VolumePixelWorld` auto-adds `MeshFilter` and `MeshCollider`, but not `MeshRenderer`.
- Initial generation uses a background thread, but runtime edits still rebuild the full chunk mesh and collider.
- The cache format is a simple binary dump without a version field.

## Validation Checklist

- `VolumePixelWorld` is attached to the chunk GameObject.
- `MeshRenderer` exists and has a material assigned.
- If `TerrainChunkRelay` is used, it is on the same GameObject as `VolumePixelWorld`.
- If `TerrainSphereModifier` is used, target chunk colliders are on `Layer 3`.
- If caching is enabled, `shapeCacheRootPath` points to a writable folder.

## Author

DaVinci-2nd
https://github.com/DaVinci-2nd
https://space.bilibili.com/432070384

