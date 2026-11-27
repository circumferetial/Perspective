# Subspace v1.0 范围（候选）

> 状态：**已封口（v1.0-subspace）**。  
> 原则：1.0 只承诺“目前涉及到的部分做完”，不把下一层塞进来。

## 1.0 包含

### 表示与契约

- `Core/`：`Primal3` / `Dual3` / `LinearMap3` / `BilinearForm3` / `SubspaceAlgebra` / `AffineLens` / `CayleyKleinMetric` / `Result`。
- `Exact/`：`ExactScalar` 专用 `ExactGeometryOps`、`ConicKind`、`ConicFrame`、`ConicFeatures`。
- `Representations/`：`ExactComplex` / `DualNumber` / `SplitComplex` 三列。
- `Symbolic/`：`PolyExpr`、`UnifiedGeoSpace`、`RationalRange`、`IntervalRange`。

### 基准

- 高中圆锥曲线：11 项；
- 标准基准：7 项；
- 抛物线定点：P-FIX；
- 射影：PR-1/2/3、PR-1S/2S/3S、PR-1F/2F/3F；
- 覆盖洞：COV-1（焦点/准线）、COV-2（切线/极线）、COV-3（圆交圆）。

### conic 特征

- 轴对齐椭圆/双曲线/圆：中心、焦点、离心率；
- 轴对齐抛物线：顶点（作为 `Center`）、焦点、离心率 \(e=1\)；
- 椭圆/双曲线/抛物线准线；圆无有限准线。

## 1.0 不包含

- 广义圆 / Möbius 4 维表示（下一层 `Inversive`）；
- 3D / 线几何；
- 高次曲线 / Hilbert 度量；
- 旋转 conic 的焦点/准线；
- 完整 PGL(2) 群作用引擎；
- `LinearMap3.Inverse`；
- `RationalRange` v2 定义域/极点；
- 产品 UI / 题库自动建模。

## 已知限制

1. `IntervalRange` 只支持多项式、导数次数 ≤2、有限端点；HS-C 的轨迹范围/挖点仍有人工步骤。
2. PR-1S/2S/3S 是**归一化后的符号证明**；PR-1F/2F/3F 用 `ConicFrame` 验证具体任意配置，但还没有把任意点符号证明完全接线。
3. `ConicFeatures` 只处理 `Q01=0` 的轴对齐 conic；旋转 conic 返回失败。
4. 群变换是“按需部分群作用”，不是统一群接口；见 `docs/transformations.md`。
5. PGL(2) 完整引擎未实现；边界见 `docs/pgl2-automorphism.md`。
6. 一般 conic–conic 求交未实现；当前只有“直线 ∩ conic”和“圆 ∩ 圆”。

## 退出标准

满足以下即可打可选快照 tag（例如 `v1.0-subspace`）：

- 74 个测试通过；
- 11 项高中基准 + 7 项标准基准 + 9 项射影基准通过；
- `ConicFeatures` 覆盖轴对齐三类 conic 的焦点/离心率/准线；
- 上述“不包含”在文档中明确。

Tag 只是学习快照，不改变代码行为。
