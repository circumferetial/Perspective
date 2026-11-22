# 交接快照（Handoff）

> 用途：人脑上下文/会话切换时的恢复入口。**当前状态以本页 + backlog 为准。**

## 一分钟恢复

```bash
cd D:\Projects\01-active\Perspective
dotnet build Perspective.slnx
dotnet test  Perspective.Tests              # 132 通过（无原生依赖）
dotnet test  Perspective.Oracle.Z3.Tests    # 28 通过（需 Z3）
dotnet test  Perspective.Sympy.Tests        # 6 通过（需 python + sympy）
```

## 已完成里程碑

| 里程碑 | 内容 | 提交 |
|---|---|---|
| M0 | 基础：ExactScalar/Erlangen/FactBase/17 规则/解释层/测试工程 | 多个早期提交 |
| M1 | Z3 神谕、归约提案、不等式证书、2023 Ⅰ 22 端到端 | `1a4285e`…`a1f51a0` |
| M2 | 分段曲线 PiecewiseConic、角度工具、2020 上海 20 三问 | `a46d9c9` |
| M3 | 题板 ProblemBoard + 7 题型模板（多问依赖） | `35b19d5`、`ccad955` |
| M5 | 视角门控定理搬运 SceneTransfer | 本次 |
| M6（部分） | README / .gitignore / GitHub Actions CI / 本交接页 | 本次 |

## 关键决策（详情见对应文档）

- **分工**：重计算交给外部强库（AngouriMath/Z3/SymPy/未来 FLINT），本项目做契约、验证、可解释
  （architecture §4i）。
- **Proposer/Verifier**：构造与归约可由人/LLM 提案，机器只做严格验收（architecture §4g/§4h）。
- **证书链**：量词交替不做通用 QE，用"归约 + 内层最小化 + 外层严格性"三段证书（§4h）。
- **视角门控**：任何搬运/使用都受 Erlangen 等级约束；定理搬运逐条严通道回验（§4k）。
- **单一出处**：任务在 `backlog.md`，决策在 `gemini-consultations.md`，事实/规则在 `facts-theorems.md`。

## 立即可以接的下一步

| 优先级 | 任务 | 说明 |
|---|---|---|
| 1 | **M6 剩余**：许可证（需仓库所有者拍板）、DD+AR 伴侣、构造搜索、Lean 导出 | 先定许可证再公开 |
| 2 | **M1-b0b**：FLINT C 直连（`flint-23.dll` 导出已验证） | 替代 SymPy 的多项式重活；注意 LGPL |
| 3 | **M4**（用户暂缓）：JSON 提案 + CLI、MCP | 等格式稳定后再做 |
| 4 | **M5 剩余**：Union-Find 查询期规范化、标准形缓存 | 性能/结论相似 |

## 已知限制（诚实清单）

- 量词交替：不做通用 QE，只支持"候选 + 反例检查"与证书链；
- 超越函数、实分支/弧段、一般不等式组：不保证；
- 构造搜索：本质仍依赖外部提案；
- 搬运：中心/共轭方向/斜率积/代数关系事实暂不搬运（直接丢弃并在报告中可见）；
- SymPy 后端每次调用启动 Python 进程（慢但不影响正确性）。

## 测试与工程纪律

- 数学公式先符号验证再写码；测试必须含反例；
- 原生依赖项目单独拆测试工程（Oracle/SymPy），主回归保持秒级；
- 改文档时遵守"单一出处"：决策/任务/事实分页存放，别处只放指针。
