using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

/// <summary>归约中引入的符号定义（如 t = a+b、p = b−a、s = (1+t²+pt)/t²）。</summary>
public sealed record SymbolDefinition(string Name, ExactScalar Expression);

/// <summary>
/// 【归约提案】把"题目原式"归约为"低维表达式"的提议，由人 / LLM / 未来的搜索生成，
/// 机器负责**验证**（Proposer/Verifier 分工，与外部构造注入同一哲学）。
/// <para>
/// 例（2023 新高考Ⅰ 22(2)）：原式 = 周长 P(a,b)，归约式 = 2(|p|+|s|)√(1+t²)，
/// 定义 t=a+b, p=b−a, s=(1+t²+pt)/t²，定义域 = 非退化条件（t≠0、p≠0 等）。
/// </para>
/// </summary>
public sealed record ReductionProposal(
    IReadOnlyList<string> BaseParameters,
    ExactScalar Objective,
    ExactScalar ReducedForm,
    IReadOnlyList<SymbolDefinition> Definitions,
    IReadOnlyList<RelationStatement> Domain,
    string ProposedBy);

public enum ReductionStatus
{
    /// <summary>符号恒等式严格成立。</summary>
    SymbolicProved,

    /// <summary>符号化不动，但高精度抽样一致（严格性弱一档，如实标注）。</summary>
    NumericallyVerified,

    /// <summary>验证失败（差异超阈值/样本不足/表达式无法求值）。</summary>
    Rejected
}

public sealed record ReductionResult(
    ReductionStatus Status,
    int SamplesUsed,
    double MaxDeviation,
    string Explanation);

/// <summary>
/// 归约提案的验证器：
/// ① 先尝试符号恒等（Objective − Reduced ≡ 0，走严通道）；
/// ② 失败则用随机有理样本做高精度核对（要求样本数足够且相对误差极小）；
/// ③ 都不通过 → Rejected，并说明样本偏差。
/// <para>
/// 【责任边界】`NumericallyVerified` 不是严格证明，只能作为"高置信候选"；
/// 对外输出若要求严格性，必须继续找符号证明或交给神谕闭合。
/// </para>
/// </summary>
public static class ReductionVerifier
{
    private const double RelativeTolerance = 1e-9;
    private const int MinSamples = 16;

    public static ReductionResult Verify(
        ReductionProposal proposal, int samples = 64, int seed = 20261123)
    {
        if (proposal.Objective.Exact is null || proposal.ReducedForm.Exact is null)
            return new(ReductionStatus.Rejected, 0, double.NaN, "归约涉及纯近似表达式，无法严格验证");

        // ① 符号恒等：把定义逐层代入归约式
        // 定义可能引用更早的定义，因此从后往前代入（s→(t,p)，再 p→(a,b)，再 t→(a,b)）
        var reducedExpanded = proposal.ReducedForm.Exact;
        foreach (var definition in proposal.Definitions.Reverse())
        {
            if (definition.Expression.Exact is null)
                return new(ReductionStatus.Rejected, 0, double.NaN,
                    $"定义 {definition.Name} 含纯近似表达式");
            reducedExpanded = reducedExpanded.Substitute(definition.Name, definition.Expression.Exact);
        }

        var difference = (proposal.Objective.Exact - reducedExpanded).Simplify();
        var differenceScalar = new ExactScalar(0, difference);
        if (differenceScalar.VerifyStrictZero() == StrictResult.StrictlyTrue)
        {
            return new(ReductionStatus.SymbolicProved, 0, 0,
                "符号恒等成立：Objective − Reduced ≡ 0（严通道）");
        }

        // ② 抽样核对
        var random = new Random(seed);
        int used = 0;
        double maxDeviation = 0;
        var failures = new List<string>();

        for (int attempt = 0; attempt < samples * 8 && used < samples; attempt++)
        {
            var environment = new Dictionary<string, ExactScalar>();
            foreach (var parameter in proposal.BaseParameters)
                environment[parameter] = ExactScalar.FromInt(random.Next(-3, 4));

            if (!SatisfiesDomain(proposal.Domain, environment)) continue;
            if (ContainsDegenerate(proposal, environment)) continue;

            var objectiveValue = Evaluate(proposal.Objective, environment);
            var reducedValue = Evaluate(proposal.ReducedForm, environment, proposal.Definitions);
            if (objectiveValue is null || reducedValue is null) continue;

            double scale = System.Math.Max(1.0, System.Math.Abs(objectiveValue.Value));
            double deviation = System.Math.Abs(objectiveValue.Value - reducedValue.Value) / scale;
            if (double.IsNaN(deviation) || double.IsInfinity(deviation))
            {
                failures.Add("样本求值出现 NaN/∞");
                continue;
            }

            used++;
            maxDeviation = System.Math.Max(maxDeviation, deviation);
        }

        if (used >= MinSamples && maxDeviation <= RelativeTolerance)
        {
            return new(ReductionStatus.NumericallyVerified, used, maxDeviation,
                $"符号化不动，但 {used} 组样本一致（最大相对偏差 {maxDeviation:E2}）；" +
                "注意：这是数值核对，不等于严格证明");
        }

        string reason = failures.Count > 0
            ? string.Join("；", failures)
            : $"样本不足（{used} 组）或偏差过大（{maxDeviation:E2}）";
        return new(ReductionStatus.Rejected, used, maxDeviation,
            $"归约未通过验证：{reason}");
    }

    private static bool ContainsDegenerate(
        ReductionProposal proposal, Dictionary<string, ExactScalar> environment)
    {
        try
        {
            foreach (var definition in proposal.Definitions)
            {
                if (definition.Expression.Exact is null) continue;
                var substituted = definition.Expression.Exact;
                foreach (var (name, value) in environment)
                    if (value.Exact is not null)
                        substituted = substituted.Substitute(name, value.Exact);

                if (substituted.EvaluableNumerical &&
                    !double.IsFinite((double)substituted.EvalNumerical()))
                    return true;
            }
        }
        catch (Exception)
        {
            return true;
        }
        return false;
    }

    private static bool SatisfiesDomain(
        IReadOnlyList<RelationStatement> domain, Dictionary<string, ExactScalar> environment)
    {
        foreach (var relation in domain)
        {
            var left = Evaluate(relation.Left, environment);
            var right = Evaluate(relation.Right, environment);
            if (left is null || right is null) return false;

            double difference = left.Value - right.Value;
            bool ok = relation.Kind switch
            {
                RelationKind.Equal => System.Math.Abs(difference) < 1e-9,
                RelationKind.NotEqual => System.Math.Abs(difference) > 1e-9,
                RelationKind.LessThan => difference < -1e-9,
                RelationKind.GreaterThan => difference > 1e-9,
                RelationKind.LessOrEqual => difference <= 1e-9,
                RelationKind.GreaterOrEqual => difference >= -1e-9,
                _ => false
            };
            if (!ok) return false;
        }
        return true;
    }

    private static double? Evaluate(
        ExactScalar expression, Dictionary<string, ExactScalar> environment,
        IReadOnlyList<SymbolDefinition>? definitions = null)
    {
        try
        {
            var substituted = expression.Exact;
            if (substituted is null) return null;

            if (definitions is not null)
                foreach (var definition in definitions.Reverse())
                    if (definition.Expression.Exact is not null)
                        substituted = substituted.Substitute(definition.Name, definition.Expression.Exact);

            foreach (var (name, value) in environment)
                if (value.Exact is not null)
                    substituted = substituted.Substitute(name, value.Exact);

            substituted = substituted.Simplify();
            if (!substituted.EvaluableNumerical) return null;
            double valueResult = (double)substituted.EvalNumerical();
            return double.IsFinite(valueResult) ? valueResult : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
