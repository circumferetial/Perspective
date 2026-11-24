# 事实与定理总表（Fact & Theorem Catalog）

本页是**单一出处**：所有事实类型（Fact）与推理规则（Theorem）的清单、前提/结论、视角等级、
以及已知缺口。改代码时请同步本页。

## 0. 总原则

| 原则 | 说明 |
|---|---|
| 事实 append-only | 只增不改；id 绑定对象，值等价去重 |
| 类型即查询 | 每条事实登记进对应类型视图与 id 索引（`PointsOn`/`LinesThrough`/…） |
| 幂集事实规范化 | 共线/共点按**排序三元组**去重，按"对"索引第三元 |
| 辅助点隔离 | 规则生成的 `AUX:` 点不参与组合枚举（`structural: false`） |
| 视角门控 | 规则有 `RequiredLevel`；当前视角更"窄"（数值更小）时规则不激活 |
| 结构化证明 | 规则只记录 `Derivation(ruleId, fact, premises)`，文本由 `ProofNarrator` 渲染 |
| 黑箱隔离 | 外部判定器结论记为 `OracleStep`，只诚实转述，不伪造推导 |

## 1. 事实类型（当前 11 种）

| 事实 | 字段 | 含义 | 等级 | 检索/去重 |
|---|---|---|---|---|
| `OnLineFact` | PointId, LineId | 点在直线上 | — | 点→线、线→点索引 |
| `OnConicFact` | PointId, ConicId | 点在圆锥曲线上 | — | 曲线→点索引 |
| `TangentFact` | LineId, ConicId, TouchPointId | 直线切曲线于切点 | Projective | 线→曲线 |
| `PolarOfFact` | LineId, PoleId, ConicId | 直线是极点的极线 | Projective | 极点→线索引 |
| `CenterOfConicFact` | PointId, ConicId | 曲线中心 | Affine | 曲线→中心 |
| `MidpointFact` | MidId, AId, BId | 中点是线段中点 | Affine | 单 id |
| `HarmonicFact` | AId, BId, CId, DId | (A,B;C,D) = −1 | Projective | 类型视图 |
| `CollinearFact` | P1Id, P2Id, P3Id | 三点共线 | Projective | **排序三元组** + 对索引 |
| `ConcurrentFact` | L1Id, L2Id, L3Id | 三线共点 | Projective | **排序三元组** + 对索引 |
| `ConjugateDirectionsFact` | Line1Id, Line2Id, ConicId | 两方向关于二次部分共轭 | Affine | 类型视图 |
| `ConstantSlopeProductFact` | Line1Id, Line2Id, Value | 斜率积为定值 | Affine | 类型视图 |
| `ParallelFact` | Line1Id, Line2Id | 两线平行（不重合） | Affine | **排序二元组** + 双向索引 |
| `PerpendicularFact` | Line1Id, Line2Id | 两线垂直 | Similarity | **排序二元组** + 双向索引 |
| `AngleEqualFact` | A1, A2, B1, B2, Sign | 有向角 θ(A1,A2) ≡ Sign·θ(B1,B2) mod π | Similarity | 对内排序 + 对间排序 + Sign；按线对索引 |
| `AreaRatioFact` | A1,A2,A3, B1,B2,B3, Value | S(A1A2A3)/S(B1B2B3) = Value | Affine | 三角形顶点排序去重；按三角形索引 |
| `SegmentRatioFact` | A1,A2, B1,B2, Value | \|A1A2\|/\|B1B2\| = Value | Similarity | 端点排序去重；按线段索引 |
| `CrossRatioFact` | A,B,C,D, Value | (A,B;C,D) = Value | Projective | 4 元子群规范化（换对取倒数）；按点对索引 |
| `AlgebraicRelationFact` | LeftNorm, RightNorm, Kind | 任意"表达式 关系 表达式"（**终端专用**） | — | 规范化字符串去重；不参与组合枚举 |

**终端专用事实**：`AlgebraicRelationFact` 只用于表达"某表达式为定值/等于另一表达式"这类
长尾结论，不进入组合型规则的候选枚举（防爆炸）；新增其它终端事实时也应遵守这条。

## 2. 定理规则（当前 17 条）

| # | 规则 | 前提 | 结论 | 等级 | 说明 |
|---|---|---|---|---|---|
| 1 | 切点弦极线 | 两条切线、Q 在两线上 | `PolarOfFact(切点弦, Q)` | Projective | 切点必须在曲线上 |
| 2 | La Hire 互反 | 两条极线、Q 在 P 的极线上 | `OnLineFact(P, Q的极线)` | Projective | 配极对称性 |
| 3 | 极点极线调和束 | 极线、割线、割线与极线交点、两曲线交点 | `HarmonicFact` | Projective | 严格回验交比 |
| 4 | 点差法中点斜率积 | 中心、中点、两点在曲线上 | `ConjugateDirectionsFact`（共轭，可搬运）/ `ConstantSlopeProductFact`（轴对齐坐标系显示性推论，**非**仿射不变量） | Affine（共轭）/ 固定轴向特例（斜率积） | 射影不保中点；斜率积仅在 M₀₁=0 的坐标系内成立，不做变换搬运 |
| 5 | 极线基本定理 | 点在曲线上 + 其极线 | `TangentFact` | Projective | 曲线上点的极线即切线 |
| 6 | 共轭直径平分弦 | 共轭方向、弦的两端在曲线上 | `MidpointFact` | Affine | |
| 7 | 同线三点共线 | 三点在同一直线上 | `CollinearFact` | Projective | 平凡但补链 |
| 8 | 同点三线共点 | 三线过同一点 | `ConcurrentFact` | Projective | 平凡但补链 |
| 9 | Pascal | 六点内接同一非退化曲线 | `CollinearFact`（对边交点） | Projective | 组合型，有成本闸 |
| 10 | 完全四点形调和性 | 四点为一般位置 | 6 条 `HarmonicFact` | Projective | 组合型，有成本闸 |
| 11 | Brianchon | 六条切线切同一非退化曲线 | `ConcurrentFact`（主对角线） | Projective | Pascal 对偶 |
| 12 | 平行传递 | 两条 `ParallelFact` | `ParallelFact` | Affine | 三线互异 + 严通道回验 |
| 13 | 垂直与平行相容 | `PerpendicularFact` + `ParallelFact` | `PerpendicularFact` | Similarity | 垂直非仿射不变量 |
| 14 | 平行线的对应角相等 | `ParallelFact` | `AngleEqualFact` | Similarity | θ(L₁,M) ≡ θ(L₂,M) |
| 15 | 角相等传递 | 两条 `AngleEqualFact`（共享线对） | `AngleEqualFact` | Similarity | 符号相乘：s₁·s₂ |
| 16 | 面积比传递 | 两条 `AreaRatioFact`（共享三角形） | `AreaRatioFact` | Affine | S₁/S₂·S₂/S₃ |
| 17 | 线段比传递 | 两条 `SegmentRatioFact`（共享线段） | `SegmentRatioFact` | Similarity | 平方形式回验（无根号） |

## 3. 已知缺口（按优先级）

> 说明：比例/角度/平行/垂直事实已补齐（P1-4）。当前主要缺口是"长尾定值组合"的
> 通用载体（已由 `AlgebraicRelationFact` 覆盖终端情形）与跨模型定理搬运。

### 3.1 度量/比例类事实（已全部补齐）

| 计划事实 | 含义 | 等级 | 注意 |
|---|---|---|---|


| `SegmentRatioFact` | \|AB\|/\|CD\| = r | Similarity | 长度比是相似不变量，不是仿射不变量 |
| `AreaRatioFact` | S(ABC)/S(DEF) = r | Affine | 面积比是仿射不变量（行列式比） |
| `CrossRatioFact` | (A,B;C,D) = r | Projective | `HarmonicFact` 是其 r=−1 特例；可考虑统一 |

配套规则（后续）：平行/垂直传递、角相等传递（含 mod π 代数）、相似三角形→比例、
面积比公式、交比链式传递。

### 3.2 能力边界（非事实缺口）

- 量词交替（覆盖 ∃∀/∀∃）只做了"候选 + 神谕闭合"的一维/有理情形；
- 构造搜索（辅助线）未做；
- 超越函数、实分支/弧段、一般不等式组未做。

## 4. 几何化简 → 代数闭合（纪律）

**先消元，再计算**（浙江 2022-21(ⅱ) 的教训）：

1. **几何化简**：用射影/仿射不变量（线对、极点极线、交比、中点、共轭方向）把构形压成对称量；
2. **降维无根号**：`Conic.RestrictToLine` / `IntersectionChordLengthSquared` 用韦达定理替代显式开方；
3. **代数闭合**：`ExtremumSolver`（一维/有理目标）+ `IAlgebraicOracle`（Z3 判可满足性）。

反例：显式求 A、B 的根号坐标再算交点，表达式复杂度 3511；换线对后 19。

## 5. 长尾结论的容纳方式（分层事实模型）

**核心认识**：结论看起来无穷，但**关系符号是有限的**（在线上、在圆上、平行、垂直、
角相等、比相等、面积比、共圆、相切、共线、共点、交比……）。偏门定理绝大多数是
"已知关系的**新组合**"，不是"新的关系种类"。所以真正要开放的是**规则**，不是事实类型。

### 5.1 四层事实（按使用方式分流）

| 层 | 形式 | 什么时候用 | 是否参与组合枚举 |
|---|---|---|---|
| L1 类型化事实 | 当前 11 种 C# record | 高频、需要索引、被大量规则当前提 | **是** |
| L2 泛化事实 `GenericFact` | `(schemaId, args[])` + 注册表 | 需要索引/组合，但不是热点 | 否（默认） |
| L3 代数关系事实 `AlgebraicRelationFact` | `(expr, =/≠/</>)` | 任何"偏门"结论的万能兜底（半代数几何结论都是坐标多项式关系） | 否 |
| L4 按需声明 `OnDemandClaim` | 名字 + 验证器 | 只作**终点结论**、不入库 | 否 |

**决策树**（新增一条结论时问自己）：

1. 它会被大量规则当**前提**、或需要枚举组合吗？→ **L1 类型化事实**
2. 需要索引、但组合少？→ **L2 GenericFact + schema**
3. 只作为**终点**被证明/证伪？→ **L4 OnDemandClaim**（不入库，避免污染事实库）
4. 表达很怪、一次性、无法归类？→ **L3 代数关系事实**（万能兜底）

### 5.2 泛化事实的 schema（让"数据"仍可被机器理解）

`RelationSchema` 注册表，每条声明：

| 属性 | 作用 |
|---|---|
| `Id` / 显示名 | 稳定键（与 `RuleIds` 同理） |
| `Arity` / 参数类型（点/线/曲线） | 做类型检查与变量绑定 |
| `RequiredLevel` | 视角门控（与 L1 一致） |
| 对称性（如 `Collinear` 是 S₃、`Concurrent` 是 S₃、`Parallel` 是 S₂） | 自动规范化去重 |
| `IndexKey` 规则 | 决定进哪个索引 |
| 解释模板 | 槽位化文案，供 `ProofNarrator` 渲染 |

**关键**：schema 是**描述**（可读、可组合、可生成解释）；
具体"怎么验"是**语义**（验证器/神谕），两者分离。
这就是"用 delegate 不好描述"的解法——delegate 只负责判真假，不负责表达知识。

### 5.3 规则层同样分两类

| 类型 | 形式 | 例子 |
|---|---|---|
| **规则模式**（data） | Horn 形：`前提模式 ∧ … → 结论模式`，变量统一绑定 | 平行/垂直/角相等的传递链 |
| **构造规则**（code） | 需要矩阵构造/消元的定理应用 | Pascal、Brianchon、切点弦（当前 11 条） |

两类规则的输入输出都用 schema 描述，解释都由"模板 + 前提槽"生成，
因此**加规则不需要新增事实类型**，也不需要写自然语言。

### 5.4 防爆炸

只有 L1 参与组合枚举；L2/L3/L4 默认**不进入**候选池。
否则"长尾"会污染组合规则（Pascal/四点形）的候选集，重演 208 条事实的成本问题。

## 6. 落地顺序（建议）

1. **L4 按需声明**（最小）：先让 `ProofJudge` 能接受"任意关系谓词 + 严格验证器 + 模板"，
   解决"偏门结论能问、能判"；
2. **L2 GenericFact + schema 注册表**：平行/垂直/角相等/比例等批量关系走 schema；
3. **L3 代数关系事实**：作为万能兜底（`ExactScalar` 表达式 + 关系 + 渲染）；
4. **规则模式（data-driven Horn）**：把传递/链式规则数据化，减少 C# 代码。
