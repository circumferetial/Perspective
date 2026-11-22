# 命题形式化归约手册（Perspective）

本文件回答一个问题：**一道题从自然语言到可推形式，中间那一段怎么走？**
它是"记录只一处"的落点：目标模板、量词消去规则、能力边界都记在这里，代码注释只放指针。

## 1. 三层翻译

```
自然语言命题
   ├─ ① 场景登记：对象 → 坐标/矩阵 + 输入事实（FactBase）
   ├─ ② 命题形式化：语句 → Goal 模板（本文件第 2 节）
   └─ ③ 求解/证明：量词消去 + 严通道验证（本文件第 3 节）
```

①③ 已有实现；② 目前靠人工（或 AI 辅助）完成，**这就是与 Lean 形式化同性质的、无法完全自动化的一步**。
本手册的目标不是消灭这一步，而是让它**类型化、可报错、可叙述**。

## 2. 四个标准 Goal 模板

命名借用 FormalGeo 的 Goal 分类 + I2GATP 的 ndg 约定：

| 模板 | 字段 | 语义 | 现有实现 |
|---|---|---|---|
| `VerifyRelation(Predicate)` | 谓词 + 对象 id | 判定共线/共点/垂直/平行/相切/在曲线上等 | `ProofJudge` + `GeoClaim` |
| `SolveRange(Expr, Ineq, Var)` | 表达式 + 不等式方向 + 变元 | 求参数范围（如 Δ>0） | `InverseExamSolver.SolveSecantRange` |
| `FindLocus(Mover, Tracer, Domain)` | 主动点、追踪点、定义域约束 | 求轨迹方程（如中点 M(k) 的轨迹） | `DiscoveryEngine.DiscoverPointLocus` |
| `ExistsInvariant(ExistsVars, ForAllParams, Condition, NDG)` | 待求未知量 + 全称参数 + 条件 + 非退化条件 | **定点/定值模板**（如"存在 E 使 k_AE+k_BE 为定值"） | `SolveFixedPointsForSlopeSum` / `FindFixedDistanceCenter` |

**NDG 必须显式**：`ExcludedParamsNote`（例如"k = −2 时直线过 E，斜率无意义"）就是 ndg，
应作为 `ExistsInvariant` 的一等字段，而不是临时字符串。

## 3. 量词消去：三条归约

**核心原则：机器不量化无限集合，只量化有限个多项式的系数。**

### 3.1 ∀ 参数 → 一维恒等式（首选，对应"归约 A"）

对"动直线系 l(t) 恒过定点"：

1. 取两个合法参数值 t₁、t₂，求交点 p₀ = l(t₁) × l(t₂)（一次 3 维叉积，O(1)）；
2. 验证一维恒等式 `A(t)·x₀ + B(t)·y₀ + C(t)·w₀ ≡ 0`（走 `VerifyStrictZero`）。

优点：绕开"按 t 展开系数方程组"的麻烦；直线束 l₀ + t·l₁ 甚至可直接 p₀ = l₀ × l₁。
**注意**：对 sin/cos/√ 等非多项式系数，本归约只在符号引擎能化简时成立；三角恒等式可能返回
`NearZeroUnproven`，不得当成"已证"。

### 3.2 ∃ 未知量 → 多项式方程组 + 结式消元

"存在 E 使 f(E,k)/g(E,k) 为定值 λ" 等价于：`∃(E, λ): f(E,k) − λ·g(E,k) ≡ 0`。
把 E、λ 当未知量，按 k 的系数展开成多项式方程组，用 `MathS.Polynomials.Resultant` 消去一个变量，
再回代验证 + 严通道复核。**结式会产生增根，必须回代滤除**（已在 `SolveFixedPointsForSlopeSum` 实现）。

### 3.3 轨迹 → 插值拟合 + 代回验零（不用 Gröbner）

动点坐标可显式计算时：采 6~9 个精确点 → SVD/线性求解齐次方程组得候选圆锥曲线 →
把符号参数坐标代回，验证单变量恒等式。比多变元 Gröbner 消元便宜几个数量级。
**不适用**于：无法参数化的隐式轨迹、次数未知的复杂轨迹——那才需要真正的消元。

### 3.4 范围与不等式：用集合代数，不自己写

"机器像人一样理解范围" = 三件事：**表示**（区间并集）、**集合代数**（并/交/差）、
**精确成员判定**。这三件事不需要自己实现：AngouriMath 的 `Entity.Set` 提供
`Interval` / `FiniteSet` / `Union` / `Intersection` / `Complement` / `ElementInSet`，
`Perspective.Core.Math.RealSets` 只做薄封装，固定"全集 = ℝ"与"非退化参数要抠掉"两个约定。

| 人的动作 | 机器动作 |
|---|---|
| 多个约束同时成立 → 求交集 | `RealSets.Intersect(a, b)` |
| 分类讨论 → 多个范围求并 | `RealSets.Union(a, b)` |
| 排除斜率无定义/B 重合等参数 | `RealSets.SubtractPoints(range, k0, …)`（即 NDG） |
| 判断某个值是否在范围内 | `RealSets.Contains(range, value)`（三态：真/假/无法化简） |

不等式在三个不同位置，难度差三个数量级：

| 位置 | 形式 | 状态 |
|---|---|---|
| 作为目标（求参数范围） | 解一元多项式不等式 | ✅ 交给 `Entity.Solve` |
| 作为 ∀ 的定义域限制 | $\forall k\in D: F(k)=0$ | ✅ 多项式恒等式自动对任意子集成立；只需另验 $D$ 内交点存在 |
| 作为轨迹的弧段 | $\exists k\in D:\ (x,y)=(x(k),y(k))$ 的像 | ❌ 需要半代数投影（CAD） |

端点可以是无理代数数（如 $1\pm\sqrt2$），保持精确符号；成员判定若集合表达式无法化简为布尔，
返回 `NoSymbolicData`，**绝不猜**。

## 3.5 库优先政策（不自己写，也不轻易说"不支持"）

| 层级 | 用什么 | 状态 |
|---|---|---|
| Tier 1 | AngouriMath：一元不等式求解、集合代数、符号化简 | ✅ 已用（`RealSets`） |
| Tier 2 | **Z3（`Microsoft.Z3` NuGet，底层即 C API）**：非线性实算术判定（nlsat） | ✅ 已验证可用（见下），待接入为"代数判定神谕" |
| Tier 2b | SymPy（外部进程）：多变量不等式化简 | ⏳ 本机有 Python 3.14、未装 SymPy；Z3 覆盖后可暂不接 |
| Tier 3 | 实代数 QE（QEPCAD / RAGLib） | ❌ 不接；真需要时外部进程 |

**Z3 实测**（探针验证）：
- `x²−2x−1=0 ∧ x>1`（即 x=1+√2）下判 `x>2` → SATISFIABLE；
- `k²−4<0 ∧ k<0` 下判 `k=−1` → SAT、`k=1` → UNSAT；
- `0<1+√2` → SAT、`3<1+√2` → UNSAT。
结论：Z3 能精确判定代数数比较与不等式组可满足性，**弥补 AngouriMath 对嵌套根式
`ElementInSet` 化简不可靠的短板**。但 Z3 只给判定、不产出人类可读区间，因此定位是
**判定神谕**（当 `RealSets.Contains` 返回 `NoSymbolicData` 时兜底），不替代 `RealSets` 的表示。

接入方式（待实施）：独立可选项目 `Perspective.Oracle.Z3`（Core 不引入原生依赖）+
`IAlgebraicOracle` 接口；仅在需要时引用。

原则：**能库则库**；只有"现成库确实不支持且必须做"才标"不支持"，并且要在本文件登记原因。

## 4. 明确的能力边界（不做的部分）

| 类型 | 状态 | 原因 |
|---|---|---|
| 等式型关系（共线/共点/相切/在曲线上…） | ✅ | 已有事实类型 + 严通道 |
| 定点 / 定值（∃∀ 等式型） | ✅ | 结式消元 + 一维恒等 |
| 参数范围（简单不等式） | ⚠️ 有限 | 用 `Entity.Solve` 解一元不等式；一般不等式组/最值需要区间算术或 CAD，未做 |
| 轨迹的弧段/定义域约束 | ❌ | 需要半代数 QE / CAD（例：k<0 时中点轨迹只是一段弧） |
| 实分支/方向约束（如"交右支"、钝角判定） | ❌ | 复数域上的结式无法区分分支与方向 |
| 超越函数（sin/cos/指对） | ⚠️ 有限 | 需先做有理化代换；符号化简能力不足时只能报 `NearZeroUnproven` |
| 构造性存在（"作辅助线使…"） | ❌ | 属于综合法/构造搜索，不在代数归约范围内 |

**覆盖率的诚实估计**（来源：与 Gemini 的讨论，待独立验证）：
高中解析几何的等式/轨迹/定点定值核心部分约 80%~90%；
若只允许系数匹配 + 简单结式、不做完整特征列/Gröbner，复杂竞赛平几约 30%~50%，深层嵌套会次数爆炸；
几何不等式与有向选枝类问题为 0%。

## 5. 与现有代码的映射

| 归约 | 代码位置 |
|---|---|
| ∀ → 一维恒等式 | `DiscoveryEngine.DiscoverFixedPoint`、`DiscoverSlopeRelations` |
| ∃ → 结式 + 回代 | `InverseExamSolver.SolveFixedPointsForSlopeSum` |
| 轨迹 → SVD + 代回 | `DiscoveryEngine.DiscoverPointLocus`、`FindFixedDistanceCenter` |
| 范围 → 不等式求解 | `InverseExamSolver.SolveSecantRange` |
| 关系验证 | `ProofJudge` + `TheoremEngine.Prove`（目标导向反向链） |
| NDG | `FixedSlopePointResult.ExcludedParamsNote`（待升格为显式字段） |

## 6. 待办

1. 把四个 Goal 模板落成类型（`Goal` 记录族 + `ProblemSpec`），让②显式化、可报错；
2. NDG 从字符串升格为字段，并在结论里输出；
3. 范围题若要做扎实，需要引入区间算术或实代数 QE——不在当前范围，先记录；
4. 题库格式可借鉴 FormalGeo CDL / I2GATP XML：前提、结论、ndg 三段式，与本文件第 2 节对齐。
