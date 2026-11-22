# 待办清单（Backlog）

**本页是唯一待办出处**：任务、优先级、验收标准都记在这里；完成一项就勾掉并写提交号。
路线决策与 Gemini 咨询记录见 [gemini-consultations.md](gemini-consultations.md)（只放决策，不放任务）。

## P0（下一步就做）

### 1. ~~外部构造类型扩展（点 → 线/曲线/垂足/极线）~~ ✅ 已完成（`P0-1`，见提交记录）

- **动机**：目前 `ConstructionEngine.ProposePoint` 只支持点构造；用户/LLM 的惊艳构造常是
  "作一条线""作垂足""取极点"。
- **内容**：
  - `ProposeLine(facts, id, line, claims)`：claims 支持 `线过两点`、`与已知线平行/垂直`、
    `是某点的极线`、`切某曲线于某点`；
  - `ProposeFoot`：垂足（验证 `H ∈ L` 且 `PH ⟂ L`）；
  - `ProposePole`：线/点的极点（验证 `PolarOfFact`）；
  - 构造产物统一登记，`Derivation.RuleId = external-construction` + 来源 Note。
- **验收标准**：`ConstructionEngineTests` 覆盖各类型正/反例；构造被拒时不写事实。
- **依赖**：需要先补 `PerpendicularFact`/`ParallelFact`（见 P1-4）才能完整表达垂足/平行。
- **风险**：claims 种类膨胀 → 先用最小集合（过两点、垂足、极线、切线）。

### 2. JSON 提案格式 + CLI 验收报告

- **动机**：让外部 LLM/用户以文件形式提构造，不依赖 C# API。
- **内容**：
  - 定义 `proposal.schema.json`：对象（点/线/曲线）+ 坐标/引用 + claims + 来源；
  - CLI：`perspective propose <file.json>` → 输出 `accepted/failed`、逐条验证结果、
    随后尝试证明目标（可选）；
  - 验收报告同时给出"构造有效但规则链不足"的明确信号。
- **验收标准**：示例 JSON（切点弦场景）从文件跑通；错误提案给出精确失败项。
- **依赖**：P0-1。
- **风险**：坐标序列化要保留精确基因（不能只存 double）；可先支持"由构造算子生成"
  （如 `intersection L1 L2`），再支持显式坐标。

### 3. 2023 新高考Ⅰ 22(2) 通用化（多参数最值最小实现）

- **动机**：探针已证明降维可行；需要把"固定一参数、对另一参数求最小、退化分支分析"
  抽成可复用能力。
- **内容**：
  - `ExtremumSolver` 多参数最小实现：`FindExtremumWithInnerVariable`（外层变量 t，
    内层变量 p 先最小化）；
  - 退化分支记录：内层最优落在 `p=0`（A=B）或 `s=0`（C=B）时标记"严格确界/不可达"；
  - 基准回归：Q22(2) 自动输出"周长 > 3√3"（或明确卡点）。
- **验收标准**：新测试复现探针结论；`ProofNarrator` 能叙述"内层最优在退化边界"。
- **风险**：通用化过度设计 → 先只支持"两层、内层可显式求解"的形状。

## P1（能力补齐）

### 4. 比例/角度/平行/垂直事实（相似/仿射层）——✅ 全部完成

- **动机**：纯几何链缺相似/角度/比例载体（详见 facts-theorems.md §3.1）。
- **内容**：`ParallelFact`(Affine)、`PerpendicularFact`(Similarity)、
  `AngleEqualFact`(有向角 mod π)、`AreaRatioFact`(Affine)、`SegmentRatioFact`(Similarity)、
  `CrossRatioFact`(Projective，统一 HarmonicFact)；
  配套传递规则（平行/垂直/角相等/比例链）。
- **验收标准**：正反例测试；`ProofJudge` 支持垂直/平行命题（当前为缺口）。
- **风险**：有向角 mod π 的规范化必须统一，否则链式传递出错。

### 5. Union-Find 查询期规范化

- **动机**：减少组合规则冗余匹配（Gemini 建议，KM-SCR 思想）。
- **内容**：点/线等价类（仅"已严格证明相等"时可合并）；查询时归一到代表元；
  **不改写**既有 Derivation 前提 id；共线/共点**不能**用 UF（它们是超图）。
- **验收标准**：等价测试；规则匹配次数下降（可测）。

### 6. 视角门控的定理搬运 + 标准形缓存

- **动机**：跨模型"结论相似"。
- **内容**：`Scene.Transform(Matrix3x3)` 搬运对象/事实/证明记录；
  标准形缓存 key = 曲线标准形 + 锚定元素归一化后的不变量（交比/离心率等）；
  门控：链中含 Similarity/Isometry 规则时禁止仿射搬运。
- **验收标准**：圆上的定理搬到椭圆；越级搬运被门控阻止。

### 7. 3 类高考专用辅助构造

- **动机**：高考解析几何的"辅助构造"其实只有 3 类（Gemini 判断，与我们经验一致）。
- **内容**：原点平移齐次化、补全对称点、极线/准线垂足。
- **验收标准**：在 1~2 道真题上验证构造能简化计算；与 `ConstructionEngine` 串联。

### 4b. ~~代数关系事实（长尾"定值组合"的兜底）~~ ✅ 已完成

- **动机**：题目里会出现"某组合相加为定值、再除以某系数仍为定值"这类无穷变体，
  不可能为每个组合定义事实类型。
- **内容**：`AlgebraicRelationFact(Left, Right, RelationKind)`：以表达式为数据，
  规范化（展开/合并）后去重；仅作**终端结论**，默认不参与组合枚举（防爆炸）。
- **验收标准**：能表达并验证任意"表达式 = 常数/另一表达式"；与 `CheckEntailment` 接口一致。

## P2（中长期）

### 8. DD+AR 伴侣

有向角方程（mod π）与线段比线性方程组 + 高斯消元，饱和度与 Horn 规则交替；
回写 FactBase 触发下一轮规则。

### 9. Guided checkpoints（拆分恒等式）

把大恒等式拆成"中间检查点"的规则编写规范/工具（利用现有 Derivation DAG，不新增结构）。

### 10. 构造搜索（HAGeo 式）

极简构造算子集 + 数值巧合度打分（`Func<AuxCandidate, DiagramState, double>` 注入）
+ 1~2 步预算。

### 11. MCP server（可选，见下）

把引擎能力暴露为 MCP 工具：`propose_construction` / `verify_claim` / `prove` /
`solve_extremum` / `check_entailment`。等 P0-2 的 JSON 提案格式稳定后再包一层。

### 12. Lean / Proof IR 导出（长期）

Proof IR 稳定后，SemanticStep 编译成 Lean 骨架，OracleStep 对接 `polyrith`/`nlinarith`。

## 已完成（近期）

| 项 | 提交 |
|---|---|
| Z3 神谕（IAlgebraicOracle + OracleStep + 测试隔离） | `1a4285e` |
| 一维/有理目标极值（含浙江 21 两问） | `1d05a8c`、`02f30d3` |
| 题目现场约束（Implies + CheckEntailment） | `a6187ec` |
| Gemini 两轮咨询记录 | `51653da` |
| 2023 新高考Ⅰ 22(2) 降维探针 | `51653da` |
| 外部构造注入（Proposer/Verifier） | `4bd731c` |
| 构造类型扩展（线/垂足/极线/切线，P0-1） | `231a934` |
| 平行/垂直事实 + 传递规则（P1-4 第一批） | `9ac2edc` |
| 有向角相等事实 + 传递规则（P1-4 第二批） | `3675de3` |
| 面积比/线段比/交比事实 + 传递规则（P1-4 第三批） | `7707e5e` |
| 代数关系事实 AlgebraicRelationFact（P1-4b） | 本批 |
