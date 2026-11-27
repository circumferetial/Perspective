# Inversive 层（起步）

> 状态：最小实广义圆已实现；复数 Möbius/共轴圆未开始。

## 已实现

`Perspective.Experiment/Inversive/GeneralizedCircle.cs`

```text
GeneralizedCircle<T> where T : IComputableField<T>
[A,B,C,D] 表示 A(x²+y²)+Bx+Cy+D=0
```

- 存储/求值/正交/反演只需 `IComputableField<T>`；
- `Classify()` 额外要求 `IRealField<T>`（能判符号）；
- 反演只做域逆，不需要开方。

## 语义接口

`Perspective.Core.Math`

- `IComputableField<T>`：域逆；
- `IOrderedRing<T>`：三态符号；
- `IRealField<T>`：`IComputableField + IOrderedRing`；
- `ISqrtable<T>`：开方；
- `IRealSqrtField<T>`：实域 + 开方；
- `IApproxable<T>`：近似值，仅守卫。

组合接口只组合语义，不新增约定。

## 测试

- 圆/线/点圆识别；
- 正交圆：单位圆与圆心 \((2,0)\)、半径 \(\sqrt3\) 的圆；
- 单位反演：单位圆固定；直线 \(x=1\) 反演成 \(x^2+y^2-x=0\)；
- 一般反演：以 \((1,0)\) 为心、半径 \(1\)，直线 \(x=2\) 反演成以 \((1,0),(2,0)\) 为直径的圆。

## 未实现

- 复系数广义圆；
- `MobiusMap` / `AntiMobiusMap` 对 4 向量的作用；
- 共轴圆、根轴、圆链；
- 与 `HComplex` 的点模型桥接。

## 边界

- 目前只做**实**广义圆，不做复系数；
- 不把 Inversive 塞进 `Subspace`；
- 下一步优先做 `MobiusMap` 作用（需要复系数表示）。
