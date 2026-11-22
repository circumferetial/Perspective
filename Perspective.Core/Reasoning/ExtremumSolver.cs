using AngouriMath;
using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

public enum ExtremumKind
{
    Minimum,
    Maximum
}

/// <summary>一维极值/确界结果（值精确；是否可达单独标注）。</summary>
public sealed record ExtremumReport(
    ExtremumKind Kind,
    ExactScalar Value,
    bool Attained,
    IReadOnlyList<ExactScalar> Candidates,
    OracleVerdict? Closure,
    string Explanation);

/// <summary>
/// 一维极值求解骨架：**候选生成 → 外部神谕闭合**。
/// <para>
/// 【为什么这样分层】极值问题形如 `∃m (∀t∈D. f(t) ≥ m ∧ ∃t∈D. f(t)=m)`，是量词交替；
/// 通用 QE 代价高，但一维情形可以：
/// ① 生成候选（端点、驻点）；② 选最优候选 m*；③ 让神谕检查 `∃t∈D. f(t) &lt; m*`
///    —— UNSAT 即证明 m* 是下界，SAT 则给出反例（帮助发现漏掉的候选/约束）。
/// </para>
/// <para>
/// 【责任边界】候选生成是启发式（可能漏候选）；**确定性由外部判定器闭合保证**。
/// 若神谕返回 Unknown，则报告不得声称已证明。
/// </para>
/// </summary>
public static class ExtremumSolver
{
    /// <summary>求解一元目标函数在定义域约束下的极值（或严格确界）。</summary>
    public static ExtremumReport? FindExtremum(
        string variableName,
        ExactScalar objective,
        IReadOnlyList<RelationStatement> domain,
        ExtremumKind kind,
        IAlgebraicOracle? oracle = null)
    {
        if (objective.Exact is null) return null;

        Entity.Variable variable = variableName;
        var candidates = new List<ExactScalar>();
        var notes = new List<string>();

        // ① 端点候选：从定义域关系里抽取常数端
        foreach (var relation in domain)
        {
            if (relation.Right.Exact is Entity.Number && relation.Left.Exact is not Entity.Number)
                candidates.Add(relation.Right);
            if (relation.Left.Exact is Entity.Number && relation.Right.Exact is not Entity.Number)
                candidates.Add(relation.Left);
        }

        // ② 驻点候选：f'(t) = 0 的实根
        try
        {
            var derivative = objective.Exact.Differentiate(variable).Simplify();
            var roots = MathS.Equality(derivative, 0).Solve(variable);
            if (roots is Entity.Set.FiniteSet finite)
            {
                foreach (var root in finite)
                {
                    if (root.EvalNumerical() is Entity.Number.Complex complex && !complex.ImaginaryPart.IsZero)
                        continue;
                    candidates.Add(ToScalar(root.Simplify()));
                }
            }
        }
        catch (Exception ex)
        {
            notes.Add("驻点求解失败：" + ex.GetType().Name);
        }

        if (candidates.Count == 0) return null;

        // ③ 只保留满足定义域的候选，并按目标值排序
        var feasible = candidates
            .Where(c => SatisfiesDomain(domain, variableName, c))
            .Select(c => (Candidate: c, Value: Evaluate(objective, variableName, c)))
            .Where(x => x.Value is not null)
            .Select(x => (x.Candidate, Value: x.Value!.Value))
            .ToList();
        if (feasible.Count == 0) return null;

        var best = kind == ExtremumKind.Maximum
            ? feasible.OrderByDescending(x => x.Value.Approx).First()
            : feasible.OrderBy(x => x.Value.Approx).First();

        // ④ 可达性：有候选严格取到该值即视为可达
        bool attained = feasible.Any(x => (x.Value - best.Value).VerifyStrictZero() == StrictResult.StrictlyTrue);
        if (!attained)
        {
            // 也可能有一个候选与最优值在数值上相同但符号未证相等；保守标注不可达
            attained = feasible.Any(x => System.Math.Abs(x.Value.Approx - best.Value.Approx) < 1e-12
                && SatisfiesDomain(domain, variableName, x.Candidate));
        }

        // ⑤ 神谕闭合：检查是否存在"更优"的点
        OracleVerdict? closure = null;
        string closureText = "未启用外部判定器（仅候选启发式，未闭合）";
        if (oracle is not null)
        {
            var opposing = kind == ExtremumKind.Maximum
                ? new RelationStatement(RelationKind.GreaterThan, objective, best.Value)
                : new RelationStatement(RelationKind.LessThan, objective, best.Value);

            var constraints = new List<RelationStatement>(domain) { opposing };
            closure = oracle.DecideExists([variableName], constraints);
            closureText = closure.Kind switch
            {
                OracleVerdictKind.Unsat => kind == ExtremumKind.Maximum
                    ? "外部判定器确认不存在更大的点，故该值为全局上界"
                    : "外部判定器确认不存在更小的点，故该值为全局下界",
                OracleVerdictKind.Sat => "外部判定器找到更优的反例（候选生成可能漏解）：" + closure.Model,
                _ => "外部判定器无法判定：" + (closure.Reason ?? "unknown")
            };
        }

        string attainedText = attained ? "可取到" : "不可取到（严格确界/端点开）";
        return new ExtremumReport(
            kind,
            best.Value,
            attained,
            feasible.Select(x => x.Candidate).ToList(),
            closure,
            $"{kind}候选「{best.Value}」{attainedText}；{closureText}"
            + (notes.Count > 0 ? "；" + string.Join("；", notes) : ""));
    }

    private static bool SatisfiesDomain(
        IReadOnlyList<RelationStatement> domain, string variableName, ExactScalar candidate)
    {
        foreach (var relation in domain)
        {
            var left = Evaluate(relation.Left, variableName, candidate);
            var right = Evaluate(relation.Right, variableName, candidate);
            if (left is null || right is null) return false;

            var difference = left.Value - right.Value;
            bool ok = relation.Kind switch
            {
                RelationKind.Equal => difference.VerifyStrictZero() == StrictResult.StrictlyTrue,
                RelationKind.NotEqual => difference.VerifyStrictZero() != StrictResult.StrictlyTrue,
                RelationKind.LessThan => difference.Approx < -1e-9,
                RelationKind.GreaterThan => difference.Approx > 1e-9,
                RelationKind.LessOrEqual => difference.Approx <= 1e-9,
                RelationKind.GreaterOrEqual => difference.Approx >= -1e-9,
                _ => false
            };
            if (!ok) return false;
        }
        return true;
    }

    private static ExactScalar? Evaluate(ExactScalar expression, string variableName, ExactScalar value)
    {
        if (expression.Exact is null || value.Exact is null) return null;
        try
        {
            var substituted = expression.Exact.Substitute(variableName, value.Exact).Simplify();
            double approx = substituted.EvaluableNumerical ? (double)substituted.EvalNumerical() : double.NaN;
            return new ExactScalar(approx, substituted);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ExactScalar ToScalar(Entity entity)
    {
        try
        {
            double approx = entity.EvaluableNumerical ? (double)entity.EvalNumerical() : double.NaN;
            return new ExactScalar(approx, entity);
        }
        catch (Exception)
        {
            return new ExactScalar(double.NaN, entity);
        }
    }
}
