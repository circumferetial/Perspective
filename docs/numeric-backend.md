# 数值后端：分层与信任边界

## 运行时

### BigRational

`Perspective.Core/Math/BigRational.cs`

- `Num/Den`，`Den>0`，gcd 归一化；
- `+ - * /`、`CompareTo`；
- `VerifyZero`、`TryInverse`、`TrySign`、`TrySqrt`；
- `TrySqrt` 只接受有理平方数，其余返回 `StrictlyFalse`（Q 内无根）。

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

## 未实现

- `QuadScalar`（单二次扩张）；
- `RealAlgebraic`（根隔离/Sturm）；
- FLINT/CGAL 胶水；
- Z3 fallback 在几何层的统一接线。

## 下一步

1. `QuadScalar`；
2. 选一个几何 fallback 点接 `TryProveZero`；
3. 再评估是否需要 `RealAlgebraic`。
