using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

/// <summary>
/// 不等式证书的一个分支：在 <paramref name="Guards"/> 成立时，<paramref name="Polynomial"/> ≥ 0。
/// <para>
/// 【Proposer/Verifier 分工】把"含绝对值/根式/分母"的命题拆成若干分支并清除分母、
/// 是**提案方的代数工作**（人/LLM/外部 CAS）；机器只做最后一步：
/// 对每个分支检查"守卫成立且多项式 &lt; 0"是否可满足——不可满足即该分支成立。
/// </para>
/// </summary>
public sealed record InequalityCase(
    IReadOnlyList<RelationStatement> Guards,
    ExactScalar Polynomial,
    string Description);

public sealed record InequalityCertificateResult(
    bool Verified,
    IReadOnlyList<string> FailedCases,
    string Explanation);

/// <summary>
/// 不等式证书验证器：逐分支把"多项式 ≥ 0"交给实代数神谕做反例检查。
/// 所有分支都无反而例 → 证书成立；任一分支出现反例 → 返回模型（可定位漏掉的分支）。
/// </summary>
public static class InequalityCertificateVerifier
{
    public static InequalityCertificateResult Verify(
        IAlgebraicOracle oracle,
        IReadOnlyList<string> variables,
        IReadOnlyList<InequalityCase> cases)
    {
        if (cases.Count == 0)
            return new(false, [], "证书为空：没有任何分支可验证");

        var failed = new List<string>();
        foreach (var @case in cases)
        {
            var constraints = new List<RelationStatement>(@case.Guards)
            {
                new(RelationKind.LessThan, @case.Polynomial, ExactScalar.FromInt(0))
            };

            var verdict = oracle.DecideExists(variables, constraints);
            switch (verdict.Kind)
            {
                case OracleVerdictKind.Unsat:
                    break;

                case OracleVerdictKind.Sat:
                    failed.Add($"[{@case.Description}] 存在反例：{verdict.Model}");
                    break;

                default:
                    failed.Add($"[{@case.Description}] 判定器无法闭合：{verdict.Reason ?? "unknown"}");
                    break;
            }
        }

        return failed.Count == 0
            ? new(true, [],
                $"{cases.Count} 个分支全部通过反例检查（不存在「守卫成立且多项式 < 0」的实点）")
            : new(false, failed,
                $"{failed.Count}/{cases.Count} 个分支未通过：{string.Join("；", failed)}");
    }
}
