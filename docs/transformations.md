# 2D 群变换清单

> 状态：**没有统一群接口**；每个变换按自己的载体实现。  
> 这不是遗漏，而是“少机器”的结果：载体、域、无穷远和可逆性都不同。

## 清单

| 变换 | 作用对象 | 复合 | 逆 | 当前缺口 |
|---|---|---|---|---|
| `LinearMap3<T>` | 3×3 射影线性映射 | `Multiply` | 无 | 缺 `Inverse`；需行列式为**单位** |
| `UnitPhase` | CP¹ / 旋转 | `operator *` | `Conjugate()` | 逆存在但未命名 `Inverse` |
| `UnitTranslation<T>` | 平移 | `Compose` / `operator *` | `Inverse` | 完整 |
| `UnitBoost` | 双曲 boost（x 轴） | `operator *` | `Inverse` | 只沿 x 轴；任意方向需先旋转 |
| `MobiusMap` | PGL(2,ℂ) 作用 CP¹ | `Compose` | `Inverse` | 有限/∞ 由 `HComplex` 处理 |
| `AntiMobiusMap` | 反莫比乌斯 | `Compose` | `Inverse` | 与 Möbius 的复合规则已实现 |
| `ConicFrame` | 标准抛物线参数 t | 无 | 无 | 缺 `Compose` / `Inverse`；只支持 `y²=xz` |

`AffineLens` 是仿射图，`CayleyKleinMetric` 是度量/极性；它们不是群元素。

## 为什么没有统一

1. **载体不同**：3×3 矩阵、2×2 复矩阵、1D 相位/平移/boost、参数 Möbius。
2. **可逆性不同**：`LinearMap3` 需要环上的单位，`ConicFrame` 需要分母非零。
3. **无穷远不同**：`MobiusMap` 用 `HComplex` 的 ∞；`ConicFrame` 目前把 ∞ 作为失败。
4. **C# 无法强制群公理**：统一接口只能表达签名，不能表达结合律/逆律。

## 何时补

- 需要把 `ConicFrame` 从“归一化一把尺”升级为可复合/可逆的变换时：补 `Compose` / `Inverse`；
- 需要真正对矩阵求逆时：只在 `IComputableField` 上补 `LinearMap3.Inverse`；
- 需要任意方向双曲 boost 时：组合 `UnitPhase × UnitBoost × UnitPhase.Conjugate`，不要改 `UnitBoost`；
- 需要统一抽象时：先等两个以上变换有相同的 `Apply/Compose/Inverse` 语义，再抽薄接口。

## 结论

当前是**“按需长出来的部分群作用”**，不是完整群库。  
先记录，不为整齐而补全。
