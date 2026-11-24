# A：17 条规则的最小结构层与前提漏洞审计（核验版）

> 来源：用户 + Gemini 的初稿；本文件由本地 AI 用判据探针 + 代码阅读核验。
> 所有"实测"均可在 `%TEMP%` 探针复现（未改仓库代码）。
> 结论标记：✅ 确认 / ⚠️ 部分确认 / ❌ 推翻。

## 1. 核验结果总表

| 规则 | 初稿主张 | 核验 | 证据 |
|---|---|---|---|
| #1 切点弦极线 | 缺 det(C)≠0 守卫 | ⚠️ 部分：**不会污染事实库**，但缺显式守卫 | `ChordOfContactIsPolarRule` 用 `conic.CheckLineRelation(...)==Tangent`（TheoremEngine.cs:181），退化曲线返回 `DegenerateConic` 直接跳过；det 守卫是被间接覆盖的 |
| #2 La Hire | 维持 Projective | ✅ | 严通道回验两条极线，无等级问题 |
| #3 极点极线调和 | 维持 Projective | ✅ | 回验 HarmonicFact |
| **#4 点差法** | `ConstantSlopeProductFact` 标 Affine 是错的，连 Similarity 都不保持 | ✅ 确认（数值） | 见 §2；原始 $k_{OM}k_{AB}=-0.25=-M_{00}/M_{11}$；**旋转 30° 后** $M_{01}\neq0$，$k'_1k'_2\approx0.0909$，$-M'_{00}/M'_{11}\approx-0.5385$；**剪切后** $k_1k_2\approx-0.333$，$-M_{00}/M_{11}=-0.2$ |
| #5 极线基本定理 | 缺 det(C)≠0 守卫；奇异点极线退化为零向量 | ✅ 确认（可污染） | 退化二次型 $xy=0$、极点=奇点 $(0,0,1)$：`PolarLine` = 零直线；`IsPolarLine(conic,O,零直线)=StrictlyTrue`；`PolarFundamentalRule.Apply` 后事实库新增 **TangentFact(零直线)**，而 `ConicTools.IsTangent` 对该事实判 `StrictlyFalse`。`AddFactCore` 不校验结论（FactBase.cs:478），所以污染成立 |
| #6 共轭直径平分弦 | 可由 #2+#3+中点桥收编 | ✅ 数学上可行（见 §4） | 当前实现独立可跑，等级 Affine 正确 |
| #7 同线三点共线 | 只靠 id 不同，未查坐标重合 | ✅ 确认 | `PointTools.Collinear(p,p,q)=StrictlyTrue`（重合点恒共线）；规则 `CollinearFromCommonLineRule` 只用 id 去重（TheoremEngine.cs:991-1003） |
| #8 同点三线共点 | 维持 Projective | ✅ | 已检查三线互异（TheoremEngine.cs:1055） |
| #9 Pascal | 维持 Projective | ✅ | 六点严通道回验 |
| #10 完全四点形调和 | 维持 Projective | ✅ | 调和回验 |
| #11 Brianchon | 维持 Projective | ✅ | 切线+共点回验 |
| #12 平行传递 | 可由 #8 + l∞ 收编 | ✅ 数学上可行（见 §4） | 需要"方向无穷远点"规范化 |
| #13 垂直平行相容 | 维持 Similarity | ✅ | 垂直依赖 Ω* |
| #14 平行线对应角相等 | `AreAngleEqual` 对退化直线恒真 | ✅ 确认 | `AreAngleEqual((l∞,x=0),(y=0,y=0),+1)=StrictlyTrue`；`AreAngleEqual((x=0,y=0),(l∞,x=0),+1)=StrictlyTrue`；零直线同样为真（Invariants.cs:131） |
| #15 角相等传递 | 同上，另加 90° 符号歧义 | ⚠️ 部分：退化直线问题确认；90° 符号歧义未测（建议补） | 与 #14 同一底层 |
| #16 面积比传递 | Affine 正确 | ✅ 未发现新问题 | 面积比是仿射不变量 |
| #17 线段比传递 | 一般线段比仿射不保持；平行前提可救 | ✅ 确认（数值） | 一般位置比 0.5 → 剪切后 0.3536；**平行线段**比 0.5 → 剪切后仍 0.5。另：`IsSegmentRatio` 接受**负值**（ratio=-1/2 判 `StrictlyTrue`），无穷远点会抛 `DivideByZeroException`（Invariants.cs:111） |

## 2. #4 的准确结论（比初稿更细）

- **数学**：$k_{OM}\cdot k_{AB}$ 的常值形式只在**坐标轴对齐**（二次型 $M_{01}=0$）时等于 $-M_{00}/M_{11}$；
  这与"Affine 不变量"是两回事——仿射剪切/旋转都会让 $M_{01}\neq0$，该恒等式不再成立。
- **代码现状**：`ConicMidpointSlopeRule` 只在 `m.M01 == 0` 时才写 `ConstantSlopeProductFact`（TheoremEngine.cs:423-438），
  且写入前用当前坐标数值回验（:429-430）。因此**不会凭空写出假事实**；
  但事实的"等级=Affine"与文档暗示的"仿射不变量"是**错误语义**，且该事实在坐标变换后不可搬运
  （`SceneTransfer.Holds` 默认丢弃它，这是对的行为）。
- **建议**：不做"降级到 Isometry"这种补丁；直接**删掉 `ConstantSlopeProductFact` 作为定理结论**，
  只保留 `ConjugateDirectionsFact`（射影/仿射可搬运的共轭关系），
  斜率积仅作为"当前坐标系下的展示性推论"，带显式 `M01=0` 守卫，不参与规则链。

## 3. 前提守卫清单（可直接落成 patch + 测试）

| # | 位置 | 需要的守卫 | 现状 | 严重度 |
|---|---|---|---|---|
| G1 | `PolarFundamentalRule.Apply`（TheoremEngine.cs:456-488） | ① `conic.Matrix.Determinant() ≠ 0`；② 极线非零向量；③ 建议直接复用 `CheckLineRelation(line)==Tangent` | 三样都没有 | **高**（可写入假 TangentFact，已实测） |
| G2 | `CollinearFromCommonLineRule.Apply`（TheoremEngine.cs:985-1013） | 三点**坐标两两互异**（`PointTools.SameStrictly != StrictlyTrue`） | 只有 id 互异 | 中 |
| G3 | `GeometryAudit.AreAngleEqual`（Invariants.cs:131） | 任一线方向向量为零（$A=B=0$，含 $l_\infty$ 与零直线）时返回 `StrictlyFalse`/`NoSymbolicData` | 恒真 | **高**（角定理全面误判） |
| G4 | `GeometryAudit.IsSegmentRatio`（Invariants.cs:111） | ① `value ≥ 0`；② 四点有限（`!IsAtInfinity`）；③ 分母线段非退化 | 只有③ | 中 |
| G5 | `PolarLine`/`IsPolarLine` 调用方 | 退化曲线时应显式拒绝（与 G1 同源） | 无 | 高（同 G1） |
| G6 | #17 若将来加"仿射级线段比"事实 | 必须加 `AB ∥ CD` 前提；一般位置只能挂 Similarity | 现规则为传递，等级 Similarity，正确 | 记录性 |

另建议补测试（现有 132 个之外）：
- `AreAngleEqual` 退化线三连（$l_\infty$、零线、$A=B=0$）都应 `StrictlyFalse`；
- `Collinear(p,p,q)` 必须 `StrictlyFalse`（坐标重合视为退化）；
- `IsSegmentRatio(..., -1/2)` 必须 `StrictlyFalse`；无穷远点不得抛异常；
- `PolarFundamentalRule` 在退化二次型 + 奇点极线下**不得**产生 TangentFact；
- 剪切/旋转下 `ConstantSlopeProductFact` 不再被规则生成（或该事实类型删除）。

## 4. 精简方案评估（#4 共轭部分 / #6 / #12 ← l∞）

初稿方向正确，且与 `Subspace/GeometryLens` 的结构层一致。需要新增/修改：

1. **$l_\infty$ 入库**：作为一条特殊直线事实（或 `FactBase` 的结构字段），并允许其上的无穷远点作为普通点登记。
2. **方向点规范化**：每条直线的"方向无穷远点" $D_\infty = L \cap l_\infty$ 需按方向去重（同一方向共享一个点 id），否则 #8 无法把"平行"识别为"共点"。
3. **#12 的替代**：平行 $\iff$ $L_1,L_2,L_3$ 同过同一 $D_\infty$ ⇒ 规则 #8 直接给出 `ConcurrentFact`；
   再加一条桥接"三线共点于 $l_\infty$ ⇒ 两两平行"（替代传递规则，且对 $n$ 条平行线通用）。
4. **#6 的替代**：对圆锥曲线，$OM = C\cdot D_\infty$（$D_\infty$ 是弦方向）就是共轭直径；
   弦 $AB$ 与 $OM$ 交于 $M$；由配极/调和：$(A,B;M,D_\infty)=-1$（$D_\infty\in l_\infty$ 时该调和共轭即中点）。
   于是 #6 = #3（调和束）+ **中点桥**（"调和共轭于无穷远点 ⇒ 中点"），不需要单独证共轭直径。
5. **抛物线**：上述 $OM = C\cdot D_\infty$ 仍然成立（极线一般不过中心，但给出直径 $y=2a/m$），
   所以合并后**抛物线自动纳入**，不需要额外分支。
6. **#4 共轭部分**：直接由 3 得到 `ConjugateDirectionsFact`；`ConstantSlopeProductFact` 删除。

**风险/代价**：$l_\infty$ 不应作为全局假设，而是"用到时才惰性登记"（否则所有场景被强制成仿射）；
"中点桥"是中点定义的第二种来源，需与现有 `IsMidpoint` 交叉验证（两套定义必须一致）；
调和→中点需要处理 $A,B$ 与 $M$ 重合的退化情形。

## 5. 建议落地顺序

1. **P0（纯补守卫，不改变能力）**：G1/G3/G2/G4 + 对应回归测试（预计 1 批）；
2. **P1（清理 #4 语义）**：删除 `ConstantSlopeProductFact` 结论或降为展示性推论（带 $M_{01}=0$ 守卫）；
3. **P2（l∞ 基础设施）**：$l_\infty$、方向点规范化、`ConcurrentFact→ParallelFact` 桥；用 #12 做首个验证案例；
4. **P3（合并 #6）**：中点桥 + 调和束，覆盖椭圆/双曲/抛物线；对照现有 `ConjugateDiameterBisectsChordRule` 的测试。
