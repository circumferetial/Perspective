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
4. **只增不改**：`FactBase` append-only，规则重复执行由 `AddFact` 去重保证幂等。

## 5. 事实模型

- 事实用 `record` 以 ID 绑定，天然值等价去重；
- 唯一写入入口 `FactBase.AddFact`，同时维护按类型视图与按 id 索引；
- 规则用声明式查询表达前提（`LinesThrough`、`PointsOn`、`HasOnConic` 等），
  不手写全表扫描；
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
