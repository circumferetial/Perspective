# Subspace 目录分层

> 原则：**通用代数骨架**与**具体几何表示**分开；文件夹只做组织，命名空间保持 `Perspective.Experiment.Subspace`。

## Core：通用骨架

`Perspective.Experiment/Subspace/Core/`

- `AlgebraCore.cs`
  - `Primal3<T>` / `Dual3<T>` / `LinearMap3<T>` / `BilinearForm3<T>`
  - `SubspaceAlgebra`
  - `AffineLens<T>` / `CayleyKleinMetric<T>`
- `Result.cs`
  - `Result<T,E>` / `GeometryError`

特点：只依赖 `IExactRing<T>`，不依赖 `ExactScalar` 或 `Approx`。

## Exact：ExactScalar 专用

`Perspective.Experiment/Subspace/Exact/`

- `ExactGeometryOps.cs`：除法、`Sqrt`、`Approx`、`CrossRatio`、`ConicIntersection` 等。
- `ConicKind.cs`：实仿射 conic 分类。
- `ConicFrame.cs`：标准抛物线参数 \(t\) 的 Möbius 归一化。

特点：依赖 `ExactScalar`，可以做除法、开方和数值守卫。

## Representations：三种表示列

`Perspective.Experiment/Subspace/Representations/`

- `ComplexGeometry.cs`：`ExactComplex`、`UnitPhase`、`HComplex`。
- `ParabolicGeometry.cs`：`DualNumber<T>`、`UnitTranslation<T>`。
- `HyperbolicGeometry.cs`：`SplitComplex<T>`、`UnitBoost`。

特点：不同的 `IExactRing` 实现；不改变 Core 的泛型骨架。

## Symbolic：多项式与值域

`Perspective.Experiment/Subspace/Symbolic/`

- `PolyGeoSpace.cs`
  - `PolyExpr`
  - `UnifiedGeoSpace`（吴方法）
  - `RationalRange` / `RationalRangeApprox`
  - `IntervalRange`

特点：符号多项式、特征列、区间值域；比 Core 更“计算”，比 Exact 更“符号”。

## Benchmarks / Demos

- `Benchmarks/`：`ProblemSet5`、高中 11 项、射影 7 项、P-FIX。
- `Demos/`：可执行演示；只引用前面的层。

## 边界

- 新几何表示（广义圆、Möbius 4 向量、3D）不放进 `Subspace`。
- 通用代数扩展进 `Core`；`ExactScalar` 专用进 `Exact`；新的数系进 `Representations`。
