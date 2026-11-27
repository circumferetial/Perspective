# Inversive 层（起步）

> 状态：实广义圆 + 一般反演 + 复 Möbius 作用已实现；共轴圆未开始。

## 已实现

`Perspective.Experiment/Inversive/GeneralizedCircle.cs`

```text
GeneralizedCircle<T> where T : IComputableField<T>
[A,B,C,D] 表示 A|z|²+Bz+Cz̄+D=0
```

- 存储/求值/正交/反演只需 `IComputableField<T>`；
- `Classify()` 额外要求 `IRealField<T>`（能判符号）；
- 反演只做域逆，不需要开方。

### 操作

- `FromCircle` / `FromLine` / `FromPoint`；
- `Classify()`：Circle / Line / Point / Imaginary / Empty；
- `Eval(Vec2<T>)`：实系数点值；
- `Center` / `RadiusSquared`；
- `InnerProduct` / `IsOrthogonal`；
- `InvertInUnitCircle()`：交换 A 与 D；
- `InvertInCircle(center, radiusSquared)`：平移 + 缩放 + 单位反演 + 回代。

### 复 Möbius

`Perspective.Experiment/Inversive/ComplexMobiusCircle.cs`

- `MobiusMap.Apply(GeneralizedCircle<ExactComplex>)`：
  \[
  H' = M^{-*} H M^{-1}
  \]
  作用在 Hermitian 矩阵 \(H=\begin{pmatrix}A&B\\C&D\end{pmatrix}\) 上。
- `AntiMobiusMap.Apply(GeneralizedCircle<ExactComplex>)`：先做 \(H\to H^T\)，再套同一公式。
- `EvalAt(HComplex)`：复广义圆在 CP¹ 点上的取值。

## 语义接口

`Perspective.Core.Math`

- `IAdditiveMagma/Monoid/Group`：`+`，再 `Zero`，再负元；
- `IMultiplicativeMagma/Monoid/Group`：`*`，再 `One`，再 `TryInverse`；
- `IExactRing`：加法群 + 乘法幺半群 + `FromLong`/`VerifyZero`/`IsUnit`；
- `IComputableField`：整环 + 乘法群；
- `IOrderedRing` / `IRealField` / `ISqrtable` / `IRealSqrtField` / `IApproxable`。

## 测试

- 圆/线/点圆识别；
- 正交圆：单位圆与圆心 \((2,0)\)、半径 \(\sqrt3\) 的圆；
- 单位反演：单位圆固定；直线 \(x=1\) 反演成 \(x^2+y^2-x=0\)；
- 一般反演：以 \((1,0)\) 为心、半径 \(1\)，直线 \(x=2\) 反演成以 \((1,0),(2,0)\) 为直径的圆；
- 复 Möbius：\(z\mapsto z+1\) 把单位圆变成 \(|w-1|=1\)；
- 反 Möbius 反演：直线 \(x=1\) 变成直径 \([0,1]\) 的圆。

## 未实现

- 共轴圆、根轴、圆链；
- 与 `HComplex` 点模型的完整桥接；
- 复广义圆的 `Classify`（复数无全序）。

## 边界

- `Classify` 只对 `IRealField` 可用；
- 不把 Inversive 塞进 `Subspace`；
- 下一步优先做 `AntiMobiusMap` 作用与共轴圆。
