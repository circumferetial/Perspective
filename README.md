# Perspective

**严格、可解释的几何推理库**（.NET 11 / C#）：把"精确符号计算 + 定理规则链 + 外部判定器"粘合成一条
可验证、可叙述的证明链。

> 定位：**证明验证器 / 推演助手**，不是自动解题器。构造与降维可以由人/LLM 提案，机器负责严格验收。

## 能做什么

| 能力 | 说明 |
|---|---|
| 射影/仿射几何 | 极线极点、La Hire、切点弦、调和点列、Pascal、Brianchon、完全四点形等 17 条规则 |
| 圆锥曲线 | 分类、主轴、参数化、二次型限制到直线（无根号弦长） |
| 精确计算 | 双轨标量（double + AngouriMath 符号）、三态严通道、有理函数恒等式 |
| 反解/综合 | 割线范围、定点/定值（结式消元）、一元/有理目标极值 |
| 量词与约束 | 前束范式 AST、假设⇒结论（`CheckEntailment`）、不等式证书验证 |
| 外部引擎（胶水层） | Z3 nlsat（实代数判定）、SymPy（子进程，可选）、FLINT（规划中，C 直连） |
| 外部输入 | 构造注入（人/LLM 提构造，机器验收）、归约提案验证 |
| 可解释 | 结构化 `Derivation` + `ProofOracleStep` + 独立解释层 `ProofNarrator` |

## 已验证真题（回归测试内）

- 2023 新高考Ⅰ 22：轨迹方程 + 矩形周长 > 3√3（证书链）
- 2022 新高考Ⅰ 21：弦斜率 −1、面积 16√2/9
- 2022 浙江 21：最大距离 12/√11、|CD|min = 6√5
- 2022 全国乙 20：定点 (0,−2)（SymbolicProved）
- 2020 上海 20：b=2、cos∠F₁PF₂=11/16、OM·ON=b²+4 及范围 (6+2√5,+∞)

## 快速开始

```bash
dotnet build Perspective.slnx
dotnet test  Perspective.Tests                 # 主回归（无原生依赖）
dotnet test  Perspective.Oracle.Z3.Tests       # Z3 判定器（含原生库）
dotnet test  Perspective.Sympy.Tests           # SymPy 子进程（需 python + pip install sympy）
```

## 文档

- [架构与纪律](docs/architecture.md)
- [事实与定理总表](docs/facts-theorems.md)
- [形式化与能力边界](docs/formalization.md)
- [真题校准记录](docs/exams.md)
- [路线与待办](docs/backlog.md)
- [Gemini 咨询记录](docs/gemini-consultations.md)

## 许可证

**待定**（需要仓库所有者选择；注意 FLINT 为 LGPL，公开分发需保留声明与可替换动态库）。
在许可证确定前，请勿公开分发。

## 贡献

欢迎提交真题/构造/归约提案的示例；外部输入格式见 roadmap（M4）。
