# E：接回主线的后端边界与 Soundness 契约

> 目的：在把任何实验引擎接进 `ProofJudge` / `ISymbolicBackend` / `IAlgebraicOracle` 之前，
> 先定死"哪种命题交哪个后端、什么条件下 `Proved` 才算真、NDG 怎么处理"。
> 本文是接线前的**契约**，不是实现说明。

## 1. 引擎清单与脾气

| 引擎 | 本质 | 能证的 | 不能碰的 |
|---|---|---|---|
| `ExactScalar`（AngouriMath） | 单表达式化简 + 三态严通道 | 闭式恒等式（无自由变量）：√3·√3=3、121/256 等 | 全称命题、变量间关系、不等式（除已编码为恒等式） |
| `PolyExpr` + `UnifiedGeoSpace`（吴特征列） | 多项式等式系统上的 Ritt-Wu 消元 | 多项式等式蕴含（Pappus、垂心、配极、切线），generic 成立 | 根式、分母含变量、不等式/序、可约分支、超越 |
| `MPoly` + `GeoSpace`（ConstrainedGeometryExperiment） | 同一套吴算法的另一份实现 | 同上 | 同上（且与上面重复） |
| `ExactComplex`/`HComplex` | 复/Gauss 有理数上的 CP¹ 齐次点 | 反演、共圆/共线、旋转相位 | 不等式、一般实代数判定、主线实数点混用 |
| 主线 `IAlgebraicOracle`（Z3 nlsat） | ∃-QF 实闭域判定 | 多项式等式/不等式的存在性、反例查找；闭式 sqrt 经 s²=e, s≥0 正则化 | 一般三角/超越、变量在分母；量词交替（仅候选+反例） |
| 主线 `InequalityCertificate`/`ExtremumSolver` | 组合层：分支 + 神谕闭合 | 已拆好的不等式证书、1D 极值 | 未拆分支/未编码的原始命题 |
| 主线 `TheoremEngine` 规则链 | 结构化事实推理 | 已覆盖的 17 条规则构型 | 规则未覆盖的构型（需 Discovery/约束引擎补） |

## 2. 约束片段 × 引擎 能力矩阵

图例：✓ 可用；△ 有条件；✗ 不可用。

| 片段 | ExactScalar | Wu（PolyExpr） | HComplex | Z3 | 证书/极值层 |
|---|---|---|---|---|---|
| F1 闭式恒等式（无变量） | ✓ | ✓（当常数） | ✓ | ✓ | — |
| F2 线性关联（共线/共点/极线，带参数） | ✗ | ✓（generic） | ✗ | ✓（∃ 反例） | ✓（拆分支后） |
| F3 多项式等式定理（Pappus/垂心/切线） | ✗ | ✓（generic） | ✗ | ✓ | △ |
| F4 可约分支（乘积=0、并分支） | ✗ | ✗（只覆盖 generic 分支） | ✗ | ✓ | △（需拆分支） |
| F5 不等式/序（范围、严格 >、\|x\|>x_A） | ✗ | ✗ | ✗ | ✓ | ✓（拆分支后） |
| F6 根式/无理数未知量（√15 坐标） | △（闭式） | ✗ | ✗ | ✓（辅助变量 s²=e, s≥0；超出片段 Unknown） | △ |
| F7 复/共形（圆环点、反演、共圆） | ✗（现实现误判） | ✗ | ✓ | ✗ | ✗ |
| F8 分段曲线/定义域条件 | ✗ | ✗ | ✗ | △（需编码） | ✗（主线 PiecewiseConic） |
| F9 退化/NDG 分支 | ✗ | △（返回假设） | ✗ | ✓ | — |
| F10 超越（π、三角、对数） | △（AngouriMath） | ✗ | ✗ | ✗ | ✗ |

## 3. 状态与 Soundness 契约（最重要）

吴证明器的返回值有三个：`Proved` / `NotZero` / `Inconsistent`。**它们不能直接映射成主线的真/假。**

实测（`UnifiedGeoSpace`）：

```
约束 a*x−1=0，证 a*x−1=0        → Proved，NDG=[a]        ← 带初始式的 Proved
约束 u*v=0，证 u=0              → NotZero，NDG=[]         ← 可约分支只覆盖 generic
约束 1=0，证任意                 → Inconsistent
约束 {y−1=0, x*y−1=0}，证 x−1=0 → Proved，NDG=[]（此例初式可消）
```

**映射规则（接线必须按这张表）**

| 后端结果 | 主线语义 | 处理 |
|---|---|---|
| Wu `Proved` + `NDG=[]` | 可提升为 `StrictlyTrue`（编码忠实的前提下） | 进入 Derivation，来源标 `Wu` |
| Wu `Proved` + `NDG≠[]` | **条件证明** `ProvedUnderAssumptions` | NDG 必须作为假设列出；只有能由 `FactBase.AssumeNonZero`/神谕证成时才可提升为无条件 |
| Wu `NotZero` | **`Unknown`**（绝不是 `StrictlyFalse`） | 可尝试 Z3 反例；不得当成假 |
| Wu `Inconsistent` | 假设集不一致（空洞真） | 标 `Unverified/InvalidInput`，不得静默当证明 |
| Wu 抛异常/秩不降/超时 | `Unknown` | 必须内部捕获，不能冒泡 |
| Z3 `Unsat`（H∧¬C） | `StrictlyTrue`（h 编码忠实时） | 现有语义 |
| Z3 `Sat` | 反例 → `StrictlyFalse`（C 为等式且编码忠实时） | 现有语义 |
| Z3 `Unknown` | `Unknown` | 现状 |

**红线（接线时逐条检查）**

1. **不要把 `UnifiedGeoSpace.ProveZero` 塞进 `IAlgebraicOracle.DecideExists`**：方向不同——一个是"∃ 反例判定"，一个是"generic 等式蕴含"。接口分开。
2. `NotZero` 永远不得映射为 `StrictlyFalse`。
3. `Proved` 若带 NDG，类型上必须携带假设（返回 record 带 `NdgConditions`，不能只返回 bool）。
4. 可约约束（如 `u*v=0`）不做因式分解时只覆盖 generic 分支；需要全分支时必须先 factor（FLINT/SymPy）并逐分支证明。
5. Wu 片段外（根式/不等式/超越）必须拒绝或转 Z3/证书层，不得静默丢约束。
6. 编码必须忠实：`FactBase` 事实 → 多项式方程的过程中不得引入近似系数；`ExactScalar.FromSymbol` 的 probe 值只能作数值提示，不得参与精确判定。
7. 迭代/秩下降必须有上限，超限返回 `Unknown`。

## 4. NDG（非退化假设）契约

- Wu 每做一次伪除，会产生一个初始式 `lg ≠ 0`；这些就是 NDG。
- **NDG 不是"可以忽略的细节"，而是定理的前提**：`Proved` 只在这个开集上成立。
- 与主线已有设施对齐：`FactBase.AssumeNonZero` / NDG 事实应作为"可 discharge 的假设"；
  未 discharge 的 NDG 进入 `PowerAnswer`/`JudgmentReport` 的"前提"字段，供解释层展示。
- 对高考题式命题：退化情形（A=B、C=B、切线重合等）必须单独列出，不能靠 generic 证明吞掉。

## 5. 接口划分（建议架构）

```
IAlgebraicOracle          ∃-QF 实判定（Z3，现有）
    ↑
InequalityCertificate / ExtremumSolver / RealSets（现有组合层）

IEquationalProver         【新增】等式约束的 generic 蕴含（吴引擎）
    : Prove(hypotheses: Poly[], goal: Poly) -> EquationalProofResult
      { Status=Proved|ProvedUnderAssumptions|Unknown,
        Assumptions: Poly[],        // NDG
        Diagram/Chain: ... }        // 供解释层

ISymbolicBackend          化简/展开/因式分解/求解（AngouriMath/SymPy/FLINT，现有）
ComplexKernel             复/共形（ExactComplex/HComplex，暂不进 ProofJudge）
```

`ProofJudge` 的裁决顺序建议：
`TheoremEngine 规则链` → `IEquationalProver`（等式片段）→ `IAlgebraicOracle`（不等式/反例）→ `Unknown`。

## 6. 按命题类型的分派表（接主线的直接依据）

| 主线命题/事实 | 首选 | 备选/校验 |
|---|---|---|
| 共线/共点/极线/调和（带自由参数） | TheoremEngine 规则 | Wu（generic，NDG 校验） |
| 定点/定值（参数化构型） | Discovery + Wu | Z3 反例 |
| 切线/相切 | 规则 + 对偶二次型 | Wu |
| 面积/长度比例（仿射） | 规则 | ExactScalar 闭式 |
| 角度/垂直（度量） | 规则（Ω* 编码） | Z3/精确内积 |
| 不等式/范围/严格性 | Z3 + 证书层 | — |
| 根式/分段/定义域 | 主线 PiecewiseConic + Z3 | — |
| 复数/圆环点 | ComplexKernel（实验） | — |

## 7. E 的剩余待办（接 A/B 之前完成）

- **E1** 因式分解分支：可约约束先 factor，逐分支 `Proved`；否则只能 `Unknown`。
- **E2** 随机 soundness 测试：随机多项式系统，Wu `Proved`（NDG 无条件）必须与 Z3 全称判定一致。
- **E3** 编码忠实性测试：`FactBase → 多项式` 在五道真题上逐条对照。
- **E4** NDG 可 discharge 率：统计 `Proved` 中 NDG 能被 `AssumeNonZero`/Z3 消掉的比例；不能消掉的必须降级为条件证明。
- **E5** 复杂度护栏：伪除迭代上限、特征列规模上限、超时 → `Unknown`。
