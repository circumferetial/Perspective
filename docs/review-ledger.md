# 代码审查台账

> 状态定义：`pending` 未审；`reviewed` 已审但未修；`fixed` 已修并回归；`deferred` 明确暂缓。
>
> 每个模块至少一轮数学审 + 一轮架构/API 审；发现进本表，不在多处重复记录。

## 总览

| 批次 | 模块 | 文件 | 状态 | 主要发现 |
|---|---|---|---|---|
| B0 | Inversive + 新代数接口 | `Inversive/*`、`ExactScalar.cs` 接口段 | **reviewed** | 实/复语义混用；Sign/TrySqrt 用 Approx 冒充证明；EvalAt 无穷远；Classify 全平面 |
| B1 | Core 射影骨架 | `Core/AlgebraCore.cs`、`Core/Result.cs` | **reviewed** | SameSubspace 零因子误判；FromFourPoints 未要求单位行列式；OtherIntersection 守卫不可靠；特征 2 假设 |
| B2 | Exact 几何 | `Exact/ExactGeometryOps.cs`、`ConicFeatures.cs`、`ConicFrame.cs` | **reviewed** | Classify/Foci 用 Approx 判符号/零；巨尺度椭圆抛异常；CentralRadiusSquared 漏 2Q01；欧氏 lens 尺度守卫失效 |
| B3 | Symbolic/值域 | `Symbolic/PolyGeoSpace.cs` | **reviewed** | ProveZero 忽略 ndg；PolyExpr 近似零；值域/证书用 Approx 丢根；区间空集未处理 |
| B4 | 三列表示 | `Representations/ComplexGeometry.cs`、`ParabolicGeometry.cs`、`HyperbolicGeometry.cs` | **reviewed** | ExactComplex 除法/可逆守卫用 Approx；HComplex 齐次缩放误判；UnitPhase/Boost 不变量未强制 |
| B5 | Benchmarks | `Benchmarks/*` | **reviewed** | HS-3 预期命题错误；`--benchmark` FAIL 不设退出码；F 系列不验证帧；手工步骤包装成 PASS；P-FIX 忽略 ndg |
| B6 | Demos/Program | `Demos/*`、`Program.cs` | **reviewed** | `--benchmark` FAIL 仍退 0；Demo 打印未解包 Result；覆盖 18 项基准无测试 |
| B7 | Tests | `Perspective.Experiment.Tests/*` | **reviewed** | 两个 Critical 无守护测试；部分测试固化 Approx 判零；多处只测 IsOk |

## B0 已确认问题

- Critical：`GeneralizedCircle<T>` 实/复语义混用；
- Critical：`IOrderedRing.Sign` / `ISqrtable.TrySqrt` 用 `Approx` 冒充证明；
- High：`EvalAt` 无穷远值；`Classify` 全平面；`InvertInCircle` 负半径；
- 分歧裁定：
  - `InvertInUnitCircle` 保持 `[D,B,C,A]`，不交换 B/C；
  - Anti-Möbius 用 `H^T`，不用 `JHJ`。

## B1 已确认问题

- Critical：`SameSubspace` 在零因子环上把非单位比例误判为同一点；
- High：`FromFourPoints` 未要求单位行列式；`OtherIntersection` 用 `IsZeroFast` 漏判零结果；
- High：多个公式默认 \(2\neq0\)，但 `IExactRing` 允许特征 2；
- Medium：`LinearMap3.Multiply(Dual3)` 是裸矩阵乘法，不是直线作用。

## B3 已确认问题

- Critical：`UnifiedGeoSpace.ProveZero` 忽略非退化初式 `ndg`，可能错误返回 `Proved`；
- Critical：`PolyExpr` 把 `FromApprox(1e-12)` 当严格零，`VerifyZero` 违反 sound 契约；
- Critical：`RationalRange` / `IntervalRange` / `Certify` 用 `Approx` 丢系数、丢根、签发错误证书；
- High：`IntervalRange` 空开区间/无穷端点未处理；`RationalRangeApprox` 未检查收敛。

## B2 已确认问题

- Critical：`Sign` 用 Approx 判零/符号，数值抛物线被判成椭圆；
- Critical：`Foci` 对巨大精确椭圆抛 `ArgumentOutOfRangeException`；
- High：`Foci/Directrices` 对未证零/符号系数返回 NaN/塌缩焦点；
- High：`CentralRadiusSquared` 漏 `2Q01m`；欧氏 lens 的 `l∞/Ω*` 未按尺度归一。

## B3 已确认问题

- Critical：`UnifiedGeoSpace.ProveZero` 忽略非退化初式 `ndg`，可能错误返回 `Proved`；
- Critical：`PolyExpr` 把 `FromApprox(1e-12)` 当严格零，`VerifyZero` 违反 sound 契约；
- Critical：`RationalRange` / `IntervalRange` / `Certify` 用 `Approx` 丢系数、丢根、签发错误证书；
- High：`IntervalRange` 空开区间/无穷端点未处理；`RationalRangeApprox` 未检查收敛。

## B4 已确认问题

- Critical：`ExactComplex` 除法用 `IsZeroWithFallback(treatUnprovenAsZero:true)`，非零符号/近零会抛 `DivideByZeroException`；
- Critical：`HComplex.AreConcyclicOrCollinear` 在齐次放大时把真零判成 `StrictlyFalse`；
- High：`HComplex` 零向量/无穷远守卫对近似零失效；`MobiusMap.IsInvertible` 用裸 `VerifyZero`，奇异近似矩阵可通过；
- High：`UnitPhase`/`UnitBoost` 位置构造器不强制单位不变量。

## B6 已确认问题

- High：`--benchmark` 全部 `BenchmarkResult` 丢弃，FAIL 仍退出 0；
- High：Demo 直接把 `Result<...>` 插值打印，输出类型名；
- High：`--subspace` 无通过/失败统计与异常隔离；
- High：`dotnet test` 只守护 1/5 benchmark 套件，18 项基准检查无测试。

## B7 已确认问题

- Critical：`Sign/TrySqrt` 与 `GeneralizedCircle` 实/复混用没有守护测试；
- Critical：`IsZeroVector_HandlesMixedSymbolicAndNumericComponents` 固化了“Approx 近零=零”的错误；
- Medium：多处只测 `IsOk`/`NotEmpty`，不验值/端点/第三态；
- Medium：6 个测试逐字复制 benchmark 函数体，不构成独立 oracle。

## B5 已确认问题

- Critical：HS-3 的 Expected 命题错误；代码实际证明的是另一条恒等式；
- Critical：`--benchmark` 丢弃所有结果，FAIL 仍退出 0；24/31 项不在测试里；
- High：PR-1F/2F/3F 只把 frame 作用到点后复验定理，不验证 frame 是否真的把 U,V 送到 0,1；
- High：HS-C/HS-4/HS-8/HS-A/HS-B 的定义域、挖点、反向命题多为文案，不在 PASS 判据；
- High：P-FIX 的 `ProveZero` 带非空 ndg，却被当作无条件证明。

## 审查规则

1. Reviewer 只出问题清单，不改代码；
2. 每条带 file:line、反例/公式、最小修法、severity；
3. 有分歧时由本表裁定并记录；
4. 修完补回归测试，状态改 `fixed`；
5. 不把“没发现问题”当作“审过”；必须至少覆盖正常路径 + 退化/失败路径。
