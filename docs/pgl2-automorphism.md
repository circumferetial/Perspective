# PGL(2) 自同构：现状与边界

> 状态：设计记录，**完整引擎未实现**。当前只有 `ConicFrame` 这一把“归一化尺”。

## 1. 它是什么

\[
PGL(2)=\left\{\begin{pmatrix}a&b\\c&d\end{pmatrix}:ad-bc\ne0\right\}\Big/\sim
\]

两个矩阵相差一个非零常数视为同一变换。它作用在射影直线 \(\mathbb P^1\)：

\[
t\mapsto \frac{at+b}{ct+d}
\]

即 Möbius 变换，性质：

- 保持交比与调和关系；
- 在 \(\mathbb P^1\) 上三重传递，可把任意三个互异点送到 \(0,1,\infty\)；
- 对参数化的圆锥曲线 \(\Gamma\cong\mathbb P^1\)，它给出 conic 的自同构。

## 2. 当前实现

`Perspective.Experiment/Subspace/ConicFrame.cs`：

```csharp
ConicFrame.FromThreeParameters(t0, t1, t2)
ConicFrame.ApplyParameter(t)
ConicFrame.ApplyPoint(P(t))       // 只支持标准抛物线 y²=xz
```

它只做一件事：把三个参数送到 \(0,1,\infty\)。

对应基准：`PR-3F`（ConicFrame 归一化后交比仍为 \(-1\)）。

## 3. 完整引擎会多什么

- 2×2 矩阵的 `Compose` / `Inverse`；
- \(\mathbb P^1\) 的 \(\infty\) 作为普通点参与运算；
- 把 conic 上的点、ambient 直线、`BilinearForm3` 一起变换；
- 自动验证入射、交比、调和关系在变换下不变；
- 对任意 conic（不只标准抛物线）建立参数化与 frame。

## 4. 为什么现在不做

- 当前 PR-1S/2S/3S 只需要“WLOG 归一化”，一把 `ConicFrame` 足够；
- 完整引擎是新一层“群作用”，不是 2D conic benchmark 的必需品；
- 先记录边界，避免以后忘记它存在；
- 等出现以下任一需求再实现：
  1. 需要把**任意 conic 的任意点/线/二次曲线**一起变换；
  2. 需要 `Compose`/`Inverse` 组成变换群；
  3. 需要把 PR 证明从“归一化后”升级为“从任意四点出发”。

## 5. 决策

**不为“怕忘”而实现群作用引擎；先写这张记录。**

需要时再按第 4 节的门槛开工。
