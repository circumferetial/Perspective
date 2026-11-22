# Perspective 架构纪律（单一出处）

本文件是项目**架构决策与纪律**的唯一记录点；代码注释只放指针，不重复正文。
推导细节留在代码注释里；本文件只记"为什么这么定"。

## 1. 表示层：矩阵管几何本体，坐标卡只管 I/O

- **射影平面上的几何对象一律用齐次坐标 + 矩阵表示**：点/线/二次曲线 = 齐次向量 / 3×3 矩阵。
  射影变换、极线、退化、交比都是矩阵运算，无需额外机制。
- **极坐标等非线性坐标卡只出现在输入/输出边界**（`PolarCoordinates`），
  转成齐次点后立刻丢弃角度。原因：二次曲线在 (r, θ) 下含根号与三角函数，不再是多项式；
  角度多值、原点处角度未定义，不适合进入推理内环。
- 标准位置构造函数（`Conic.Ellipse/Hyperbola/Parabola/Circle`）只给标准形；
  平移/旋转/伸缩一律用 `Conic.Transformed(Matrix3x3)` 组合，
  **不为每种变换组合写重载**（N 种变换是 N! 种重载，而一个矩阵入口覆盖全部可逆变换）。

## 2. 层级纪律：任何结论都要标"在哪个变换群下有意义"

- 变换分类：`PerspectiveTransform.Classify` → `TransformLevel`
  （Isometry < Similarity < Affine < Projective < Singular）。
- 曲线分类：`ConicTools.Classify` → `ConicClassification.MeaningfulAt`
  （椭圆/双曲/抛物 = Affine；圆 = Similarity；退化 = Projective）。
- 规则守卫：`TheoremEngine` 用 `facts.CurrentLevel > rule.RequiredLevel` 屏蔽越界定理。
- 原则：**坐标公式是某个坐标系下的表示，只有群不变量才是几何事实**。
  写新定理前先问"它在哪个群下不变"，再决定能否用坐标公式。

## 3. 严格性纪律：快通道负责筛，严通道负责结论

- 每个数同时带 double 近似（快）与 AngouriMath 精确表达式（严）。
- 纯近似数据（拖拽、数值拟合）**不得冒充严格结论**：
  `ConicClassification.IsExact = false`、`ProofStatus` 分级、`StrictResult` 四级。
- 证明类判定必须先走廉价的 O(1) 事实查询，再走昂贵的符号化简
  （代价递增排序；实测顺序错误会带来约 1000 倍耗时差）。

## 4. 定理规则契约

每条规则必须：
1. **前提清单**：在 XML 注释里写 前提/结论/推导/等级 四段；
2. **前提回验**：不信任 FactBase 里的标签，用严通道复核坐标级前提
   （如 `ConicTools.IsPolarLine`、`GeometryAudit.IsMidpoint`、`Conic.ContainsStrictly`）；
3. **结论落库前验证**：结论本身也要过严通道，失败即跳过（宁缺毋假报）；
4. **只增不改**：`FactBase` append-only，规则重复执行由 `AddFact` 去重保证幂等；
5. **声明 `ConclusionTypes` 与 `PremiseTypes`**：这是目标导向推演的元数据，
   必须如实反映规则输出与依赖的事实类型。

## 4b. 推演策略：BFS 与目标导向并存

- `RunToFixedPoint`：数据驱动 BFS，所有规则对所有事实跑到不动点。完备但会产生大量
  无关事实（实例场景实测：11 条规则全开推出 250 条事实）。
- `Prove(facts, targetFactType, ...)`：**类型级反向链**。从待证结论的事实类型出发，
  反向闭包出"可能产出该类型"的规则子集，只激活这些规则。同一场景实测：只需激活
  3/11 条规则、产生 21 条事实即可证出同一结论。
  反向闭包按类型取超集，**只会多激活、不会漏规则**，结论仍由规则内部严通道保证，
  因此是完备性的保守近似，不是启发式剪枝。
- 待证命题没有对应事实类型时（垂直/平行），跳过推演直接走代数预言机。
- 未采纳的备选：**反证通道**（把待证命题当假设注入，允许未验证前提参与推导，
  若某条规则的结论严格验证失败则判定矛盾）。它只能证伪、且依赖"假设→可检验后果"
  的规则链，链条断了就既不矛盾也证明不了，因此不适合作为主证明器。

## 4c. 解释层与计算层分离

- **计算结构不携带自然语言**：规则只记录 `Derivation(RuleId, Fact, Premises[])`，
  不拼字符串；`FactBase.DerivationLog` 是结构化证明记录。
- **解释文本集中在 `ProofNarrator`**：按 `RuleIds.*` 模板渲染。改文案、换语言、
  出 LaTeX 都只改这一个文件。
- `RuleIds` 常量表是规则与解释层的稳定键，避免两边各写魔法字符串而漂移。
- 写入 API 分离：初始条件/辅助连线用 `AddFact(fact, note)`；规则导出用
  `AddDerivedFact(fact, ruleId, params premises)`。**刻意不共用同一个名字**——
  零前提时 params 重载会与 note 重载产生歧义，导致规则记录被误当成初始条件。
- 迁移状态：11 条规则**全部迁移**（规则代码中不再出现自然语言）。

## 5. 事实模型

- 事实用 `record` 以 ID 绑定，天然值等价去重；
- 唯一写入入口 `FactBase.AddFact`，同时维护按类型视图与按 id 索引；
- 规则用声明式查询表达前提（`LinesThrough`、`PointsOn`、`HasOnConic` 等），
  不手写全表扫描；
- **规则自动生成的辅助点必须加 `AUX:` 前缀**（如 Pascal 的交点、四点形的对角点）。
  这类点只用于让事实可引用，**不得参与"顶点/直线枚举"**——否则下一轮规则会把它
  当成新的结构性对象，导致组合爆炸与错误构造。同理，辅助点的 id 必须包含生成它的
  完整上下文（例如四点形的四个顶点），禁止只按一条边命名，否则不同构形会撞 id；
- **幂集事实**（`CollinearFact` 三点共线、`ConcurrentFact` 三线共点）无法按单个 id
  索引：去重靠**排序后的三元组规范键**（`Create` + `AddFact` 内规范化），检索靠
  **按对索引第三元**（`ThirdPointsCollinearWith`、`ThirdLinesConcurrentWith`）。
  新增同类事实时必须同时补"规范化 + 对索引"，否则会出现排列重复或检索退化；
- 待证命题（`GeoClaim`）**不入 Facts**，防止循环论证；
  `ProofJudge` 先跑定理链，未命中再用代数预言机，并标注 `TrueByAlgebraOnly`
  仅表示"该坐标实现为真"，不等于"从前提可推"。

## 6. 加新定理/新事实类型的检查清单

1. 结论能否用**已有事实类型**表达？能用就不加新类型（成本最低）。
2. 若必须加类型：先确认查询方式（幂集事实如 Collinear/Concurrent 无法按 id 索引，
   会放大扫描），再在 `AddFact` 里补索引与类型视图。
3. 规则的前提是否都能严通道回验？不能回验的前提要显式注明"trusted"。
4. 是否有退化情形（重合点、无穷远点、相切、除零）？在注释里写明处理方式。
5. 补一个正例 + 一个反例（伪造标签）验证，再提交。
