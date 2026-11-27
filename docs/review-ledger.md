# 代码审查台账

> 状态定义：`pending` 未审；`reviewed` 已审但未修；`fixed` 已修并回归；`deferred` 明确暂缓。
>
> 每个模块至少一轮数学审 + 一轮架构/API 审；发现进本表，不在多处重复记录。

## 总览

| 批次 | 模块 | 文件 | 状态 | 主要发现 |
|---|---|---|---|---|
| B0 | Inversive + 新代数接口 | `Inversive/*`、`ExactScalar.cs` 接口段 | **reviewed** | 实/复语义混用；Sign/TrySqrt 用 Approx 冒充证明；EvalAt 无穷远；Classify 全平面 |
| B1 | Core 射影骨架 | `Core/AlgebraCore.cs`、`Core/Result.cs` | pending | — |
| B2 | Exact 几何 | `Exact/ExactGeometryOps.cs`、`ConicFeatures.cs`、`ConicFrame.cs` | pending | — |
| B3 | Symbolic/值域 | `Symbolic/PolyGeoSpace.cs` | pending | — |
| B4 | 三列表示 | `Representations/ComplexGeometry.cs`、`ParabolicGeometry.cs`、`HyperbolicGeometry.cs` | pending | — |
| B5 | Benchmarks | `Benchmarks/*` | pending | — |
| B6 | Demos/Program | `Demos/*`、`Program.cs` | pending | — |
| B7 | Tests | `Perspective.Experiment.Tests/*` | pending | — |

## B0 已确认问题

- Critical：`GeneralizedCircle<T>` 实/复语义混用；
- Critical：`IOrderedRing.Sign` / `ISqrtable.TrySqrt` 用 `Approx` 冒充证明；
- High：`EvalAt` 无穷远值；`Classify` 全平面；`InvertInCircle` 负半径；
- 分歧裁定：
  - `InvertInUnitCircle` 保持 `[D,B,C,A]`，不交换 B/C；
  - Anti-Möbius 用 `H^T`，不用 `JHJ`。

## 审查规则

1. Reviewer 只出问题清单，不改代码；
2. 每条带 file:line、反例/公式、最小修法、severity；
3. 有分歧时由本表裁定并记录；
4. 修完补回归测试，状态改 `fixed`；
5. 不把“没发现问题”当作“审过”；必须至少覆盖正常路径 + 退化/失败路径。
