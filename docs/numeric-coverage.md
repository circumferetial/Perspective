# 数字层能覆盖多少题

> 这里统计的是**数字表示能力**，不是完整题解管线。
> 当前 benchmark 仍主要走 `ExactScalar`；BigRational/QuadScalar 先做并行验证。

## 能力矩阵

| 问题类型 | BigRational | QuadScalar | Z3 |
|---|---|---|---|
| 有理点/线/极点/极点极线 | ✓ | ✓ | ✓ |
| 有理系数 conic 分类/入射 | ✓ | ✓ | ✓ |
| 有理直线 ∩ 有理 conic（判别式平方） | ✓ | ✓ | ✓ |
| 有理直线 ∩ conic（判别式非平方） | — | ✓（单根号） | ✓ |
| 圆 ∩ 直线 | — | ✓（单根号） | ✓ |
| 椭圆/双曲线焦点（单根号） | — | ✓ | ✓ |
| 准线（单根号） | — | ✓ | ✓ |
| 两独立根号混合 | ✗ | ✗（显式拒绝） | 判定 ✓ / 构造 ✗ |
| 一般 conic–conic 求交（四次） | ✗ | ✗ | 判定 ✓ / 构造 ✗ |
| 角度/三角函数 | ✗ | ✗ | 一般 ✗ |
| 有理函数范围/极点 | 数值 ✓ | 数值 ✓ | 判定部分 |

## 对现有题目的估计

### 高中 11 项

从数字表示看：

- HS-1/2/3/4/6/7/8/A/C：有理数即可；
- HS-5：面积含 \(\sqrt3\)，单根号；
- HS-B：面积上界 \(2\sqrt2\)，单根号。

所以：

```text
11/11 的数值表示可覆盖
```

但这不等于 11/11 已用新后端重写；目前它们仍主要在 `ExactScalar` 上。

### 射影 9 项

PR-1/2/3、S 系列、F 系列都使用有理坐标/有理参数：

```text
9/9 可用 BigRational 表示
```

### 三个“缺口题”

- A 旋转 conic：结果含 \(\sqrt5\)，单根号可表示；但 `ConicFeatures` 还不支持旋转；
- B 一般 conic–conic：四次根，当前不能构造；
- C 有理函数范围/极点：数值可表示，范围算法仍缺。

所以：

```text
A：数字可表示，几何功能未接
B：数字层不足
C：算法层不足
```

## 数字层算术 sanity（不是算法移植）

以下测试只验证**数字类型运算**与手推公式，不调用完整几何算法：

- **HS-1 有理实例**：\(l:y=x-\frac35\)，用 BigRational + Vieta 验证垂直条件为 0；
- **HS-3 有理实例**：焦点弦端点 \(y_1=1,y_2=-4\)，验证 \(1/|FM|+1/|FN|=1\)；
- **HS-5 单根号实例**：切线 \(y=2x+1\) 截渐近线，面积² 用 QuadScalar 精确算到 3；
- **HS-7 有理实例**：\(m=1\) 时重心轨迹点 \((4/9,-2/9)\)，验证 \(3x^2+6y^2=2x\)；
- **HS-8 单根号实例**：\(t=2/3\) 时 \(x=2/3\) 与椭圆交于 \(y=\pm\sqrt{8/3}\)，QuadScalar 验证 \(k_{BN}=2k_{AM}\)。

对应测试：

```text
NumericProblemTests
```

## 真正调用算法的测试

- `GenericConicLineTests`：
  - `BigRational`：\(y^2=4x\) 与 \(x=1\) 的实际直线∩conic；
  - `QuadScalar`：单位圆与 \(y=1/2\) 的实际交点；
  - `BigRational` 非平方判别式 → `Unsupported`，不抛异常。
- `AlgorithmPipelineTests.HS3_ConicIntersection_FocusChord`：
  调用 `ConicIntersection.Intersect` 求 \(y^2=4x\) 与 \(x=1\) 的交点，再算焦点距离倒数和。
- `BigRationalGeometryTests` / `QuadScalarGeometryTests`：
  调用 `Join` / `Meet` / `Det` / `Eval` 等几何原语。

所以当前准确说法是：

```text
数字算术 sanity：5 项
调用几何原语：BigRational/QuadScalar 若干
完整题目的算法管线：HS-3（ExactScalar）
GenericConicLine 算法：BigRational/QuadScalar 两条真实路径
```

## 结论

- **有理几何**：BigRational 已经够用；
- **单根号几何**：QuadScalar 够用；
- **多根号/四次交点**：需要 Z3 判定或 `RealAlgebraic` 构造；
- **三角/超越**：当前不接。

一句话：

> **数字层能覆盖“有理 + 单根号”的绝大多数高中圆锥曲线，但还不能自动构造四次交点。**
