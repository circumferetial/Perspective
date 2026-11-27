# Inversive 层（起步）

> 状态：最小实广义圆已实现；复数 Möbius/共轴圆未开始。

## 已实现

`Perspective.Experiment/Inversive/GeneralizedCircle.cs`

```text
[A,B,C,D] 表示 A(x²+y²)+Bx+Cy+D=0
```

- `FromCircle(center, radiusSquared)`
- `FromLine(b,c,d)`
- `FromPoint(point)`（点圆）
- `Kind`：Circle / Line / Point / Imaginary / Empty
- `Eval(point)`
- `Center()` / `RadiusSquared()`
- `InnerProduct` / `IsOrthogonal`
- `InvertInUnitCircle()`：交换 A 与 D

## 测试

- 圆/线/点圆识别；
- 正交圆：单位圆与圆心 \((2,0)\)、半径 \(\sqrt3\) 的圆；
- 单位反演：单位圆固定；直线 \(x=1\) 反演成 \(x^2+y^2-x=0\)。

## 未实现

- 一般圆反演（任意中心/半径）；
- 复系数广义圆；
- `MobiusMap` / `AntiMobiusMap` 对 4 向量的作用；
- 共轴圆、根轴、圆链；
- 与 `HComplex` 的点模型桥接。

## 边界

- 目前只做**实**广义圆，不做复系数；
- 不把 Inversive 塞进 `Subspace`；
- 下一步优先做一般圆反演与 `MobiusMap` 作用。
