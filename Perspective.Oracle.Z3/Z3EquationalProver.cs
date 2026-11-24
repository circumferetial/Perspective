using Perspective.Core.Reasoning;

namespace Perspective.Oracle.Z3;

/// <summary>
/// E 契约的参考实现：用 Z3（实闭域判定）做等式蕴含，验证
/// <see cref="IEquationalProver"/> 的状态映射与 soundness 约定。
/// <para>
/// 【能力】多项式等式（含无量词非线性）；hypotheses 只接受等式，goal 只接受等式。
/// 【语义】H ⇒ g=0 等价于 H ∧ g≠0 不可满足；H 本身不可满足 ⇒ InconsistentHypotheses。
/// 【定位】这是"参考后端"：证明不产生 NDG（Z3 直接判定），用于验证契约；
/// 吴特征列后端后续以同一接口接入并负责 NDG 条件。
/// </para>
/// </summary>
public sealed class Z3EquationalProver : IEquationalProver, IDisposable
{
    private readonly Z3Oracle _oracle;

    public Z3EquationalProver(uint timeoutMs = 5000) => _oracle = new Z3Oracle(timeoutMs);

    public string Name => "Z3（等式参考后端）";

    public EquationalProofResult ProveZero(
        IReadOnlyList<string> variables,
        IReadOnlyList<RelationStatement> hypotheses,
        RelationStatement goal)
    {
        if (goal.Kind != RelationKind.Equal)
            return EquationalProofResult.Unknown("E 后端只接受等式目标");
        if (hypotheses.Any(h => h.Kind != RelationKind.Equal))
            return EquationalProofResult.Unknown("E 后端只接受等式假设（不等式请走 IAlgebraicOracle/证书层）");

        var hypothesesSat = _oracle.DecideExists(variables, hypotheses);
        if (hypothesesSat.Kind == OracleVerdictKind.Unsat)
            return EquationalProofResult.InconsistentHypotheses("假设集无实解（空洞真，需显式标记）");
        if (hypothesesSat.Kind == OracleVerdictKind.Unknown)
            return EquationalProofResult.Unknown("假设集可满足性未判定：" + (hypothesesSat.Reason ?? "timeout"));

        var counterexample = new RelationStatement(
            RelationKind.NotEqual, goal.Left, goal.Right);
        var constraints = new List<RelationStatement>(hypotheses) { counterexample };
        var verdict = _oracle.DecideExists(variables, constraints);

        return verdict.Kind switch
        {
            OracleVerdictKind.Unsat => EquationalProofResult.Proved("H ∧ ¬goal 不可满足（无反例）"),
            OracleVerdictKind.Sat => EquationalProofResult.Unknown("存在反例模型：" + (verdict.Model ?? "?")),
            _ => EquationalProofResult.Unknown("判定未闭合：" + (verdict.Reason ?? "timeout"))
        };
    }

    public void Dispose() => _oracle.Dispose();
}
