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

## 路由原则

1. 快速精确路径；
2. **只要表达式能编码成 QF_NRA，就交给 Z3**；
3. 只有 Z3 返回 `UNKNOWN` / 超时 / 不可编码，才返回 `Unknown`；
4. `QuadScalar` 是快路径，不是准入门槛；
5. Z3 能判“零/存在”，不等于能直接构造精确点；构造仍需代数表示或根隔离。

## 未实现

- `QuadScalar`（单二次扩张）；
- `RealAlgebraic`（根隔离/Sturm）；
- FLINT/CGAL 胶水；
- Z3 fallback 在几何层的统一接线。

## 下一步

1. `QuadScalar`；
2. 选一个几何 fallback 点接 `TryProveZero`；
3. 再评估是否需要 `RealAlgebraic`。
