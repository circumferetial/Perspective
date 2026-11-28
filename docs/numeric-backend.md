# 数值后端：分层与信任边界

## 运行时

### BigRational

`Perspective.Core/Math/BigRational.cs`

- `Num/Den`，`Den>0`，gcd 归一化；
- `+ - * /`、`CompareTo`；
- `VerifyZero`、`TryInverse`、`TrySign`、`TrySqrt`；
- `TrySqrt` 只接受有理平方数，其余返回 `StrictlyFalse`（Q 内无根）。

### QuadScalar

`Perspective.Core/Math/QuadScalar.cs`

- `a + b√d`，单个二次扩张；
- `FromSqrt(BigRational)` 做平方因子分解；
- 精确零/符号/逆；
- `TrySqrt` 只对**有理元件**（`b=0`）构造根；`b≠0` 返回 `Unknown`；
- 不同 `d` 的混合会抛 `NotSupportedException`，表示超出片段。

### Z3 RCF

`Perspective.Oracle.Z3/Z3Rcf.cs`

- 直接 `[LibraryImport("libz3")]` 调 `Z3_rcf_*`；
- 精确构造/比较实代数数；
- `sqrt` 用 `Z3_rcf_mk_roots`（`x²-a` 取正根）；
- 已验证 `sqrt(1+sqrt2)`、`x²-(1+sqrt2)=0`、符号比较；
- 依赖原生 `libz3`（MIT），不引用托管 `Microsoft.Z3.dll`。

### ConicPencil（起步）

`Perspective.Experiment.Z3/ConicPencil.cs`

- 计算 `det(Q1+λQ2)` 的升序系数；
- 用 Z3 RCF 找退化成员 λ；
- 已验证：构造已知退化成员 λ=1 的 pencil；
- **尚未**做退化 conic 的直线分解与交点构造。

### Z3 oracle

`Perspective.Oracle.Z3/Z3Oracle.cs`

- `TryZero(ExactScalar)`：闭式零判定；
- `TryProveZero(ExactScalar, vars)`：参数化恒等式；
- `sqrt(e)` 正则化为 `s²=e, s≥0`；
- `UNSAT → StrictlyTrue`，`SAT → StrictlyFalse`，其余 `Unknown`。

## 信任边界

- `StrictlyTrue` 来自 Z3 `UNSAT` 时，**依赖 Z3 实现**；
- `Unknown` / 超时 / 不可编译节点，绝不升格；
- `FromApprox` 没有精确基因，不能进入证明路径；
- AngouriMath 只做显示/解析，不做最终判定。

## 路由原则

1. 快速精确路径；
2. **只要表达式能编码成 QF_NRA，就交给 Z3**；
3. 只有 Z3 返回 `UNKNOWN` / 超时 / 不可编码，才返回 `Unknown`；
4. `QuadScalar` 是快路径，不是准入门槛；
5. Z3 能判“零/存在”，不等于能直接构造精确点；构造仍需代数表示或根隔离。

## 明确暂缓

### 纯 C# BFMSS / CORE::Expr 复刻

- 估计不是 500~800 行可 production 的规模；
- 难点是表达式 DAG、根分离界、精度下界、多根号；
- 只在“禁止原生依赖”或“Z3 RCF 性能不可接受”时再评估。

### 纯 C# Sturm / Resultant / RealAlgebraic

- 1000~1500 行是乐观估计；
- 结式、Sturm、隔离区间、退化情况都很重；
- 当前没有必须自研的触发条件。

### 多根号塔 / RadicalTower

- 深度 2 可做，但收益有限；
- 先让 `QuadScalar` + Z3 RCF 覆盖；
- 不因“未来可能有”先造通用塔。

## 当前优先级

1. **收口 soundness**：`ExactReal` 边界、Z3 RCF adapter、禁止 Approx 进入证明；
2. **Conic pencil**：用“直线 + conic”表示 conic–conic 交点，避免展开四次根；
3. **真实算法测试**：测 `GenericConicLine`/`ConicPencil`，不测手算恒等式；
4. 数值类型只在有明确题目需要时扩展。

## 下一步

1. 做 `ExactReal` 抽象，把 `BigRational`/`QuadScalar`/`Z3Rcf` 统一；
2. 用 `Z3Rcf` 跑通一个多根号交点；
3. 设计 `ConicPencil`，不展开四次根；
4. 区间过滤必须是严格外扩，不能用 `double` 四舍五入冒充。
