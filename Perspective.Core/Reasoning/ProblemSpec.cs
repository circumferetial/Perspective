using AngouriMath;
using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

/// <summary>问题陈述的量词前缀形状。</summary>
public enum ProblemShape
{
    /// <summary>无量词：常量陈述。</summary>
    QuantifierFree,

    /// <summary>纯全称 ∀*：应作为恒等式验证。</summary>
    Universal,

    /// <summary>纯存在 ∃*：应作为方程/不等式求根。</summary>
    Existential,

    /// <summary>量词交替（∀∃ / ∃∀ / 更复杂）：需要 QE，当前不支持。</summary>
    Mixed
}

/// <summary>问题求解报告（三值：True/False/Unknown，绝不静默猜测）。</summary>
public sealed record ProblemReport(
    ProblemShape Shape,
    TruthValue Truth,
    string Explanation,
    Entity.Set? Solutions = null,
    string? UnsupportedReason = null);

/// <summary>
/// 问题陈述的求解器分派：**先看量词形状，再选求解策略**。
/// <para>
/// 支持：
/// <list type="bullet">
/// <item>∀* 无量词矩阵 → 恒等式验证（<see cref="ExactScalar.VerifyStrictZero"/>）；</item>
/// <item>∃*（单变量、单原子关系）→ 方程/不等式求根；</item>
/// <item>无量词 → 常量比较。</item>
/// </list>
/// 不支持：量词交替、量词出现在逻辑连接内部（需先化前束范式）、多变量存在量词
/// （需要结式消元或多变量 QE）。
/// </para>
/// </summary>
public static class ProblemSpec
{
    /// <summary>求解一个量词陈述。</summary>
    public static ProblemReport Evaluate(Statement statement)
    {
        var (shape, prefix, matrix) = Classify(statement);
        if (prefix is null)
            return new ProblemReport(ProblemShape.Mixed, TruthValue.Unknown,
                "无法化为前束范式：量词出现在逻辑连接内部", UnsupportedReason: "需先做前束范式变换");

        return shape switch
        {
            ProblemShape.QuantifierFree => EvaluateQuantifierFree(matrix),
            ProblemShape.Universal => EvaluateUniversal(prefix, matrix),
            ProblemShape.Existential => EvaluateExistential(prefix, matrix),
            _ => new ProblemReport(ProblemShape.Mixed, TruthValue.Unknown,
                "量词交替（∀∃ / ∃∀）需要量词消去", UnsupportedReason: "当前只支持纯 ∀* 或纯 ∃*"),
        };
    }

    /// <summary>
    /// 线性化量词前缀：把根部的量词链抽成 (kind, vars) 列表。
    /// 若量词出现在逻辑连接内部（无法直接前束化），返回 prefix = null。
    /// </summary>
    private static (ProblemShape Shape, List<(QuantifierKind Kind, string Var)>? Prefix, Statement Matrix)
        Classify(Statement statement)
    {
        var prefix = new List<(QuantifierKind, string)>();
        var current = statement;

        while (current is QuantifiedStatement q)
        {
            foreach (var v in q.Variables) prefix.Add((q.Kind, v));
            current = q.Body;
        }

        // 矩阵里若还有量词（例如 And(∀..., ...)），当前不做前束化
        if (ContainsQuantifier(current))
            return (ProblemShape.Mixed, null, current);

        bool hasForAll = prefix.Any(p => p.Item1 == QuantifierKind.ForAll);
        bool hasExists = prefix.Any(p => p.Item1 == QuantifierKind.Exists);

        var shape = (hasForAll, hasExists) switch
        {
            (false, false) => ProblemShape.QuantifierFree,
            (true, false) => ProblemShape.Universal,
            (false, true) => ProblemShape.Existential,
            _ => ProblemShape.Mixed
        };

        return (shape, prefix, current);
    }

    private static bool ContainsQuantifier(Statement statement) => statement switch
    {
        QuantifiedStatement => true,
        JunctionStatement j => j.Items.Any(ContainsQuantifier),
        NotStatement n => ContainsQuantifier(n.Body),
        ImplicationStatement i => i.Premises.Any(ContainsQuantifier) || ContainsQuantifier(i.Conclusion),
        _ => false
    };

    // ---------- 无量词：常量比较 ----------
    private static ProblemReport EvaluateQuantifierFree(Statement matrix)
    {
        var truth = EvaluateMatrix(matrix);
        string text = truth switch
        {
            TruthValue.True => "量词-free 陈述为真",
            TruthValue.False => "量词-free 陈述为假",
            _ => "无法判定（含符号量）"
        };
        return new ProblemReport(ProblemShape.QuantifierFree, truth, text);
    }

    // ---------- 纯全称：恒等式验证 ----------
    private static ProblemReport EvaluateUniversal(
        List<(QuantifierKind Kind, string Var)> prefix, Statement matrix)
    {
        var truth = EvaluateMatrix(matrix);
        string vars = string.Join(", ", prefix.Select(p => p.Var));
        string text = truth switch
        {
            TruthValue.True => $"对所有 {vars} 恒成立（严通道验证）",
            TruthValue.False => $"并非对所有 {vars} 成立（已找到反例或恒等不成立）",
            _ => $"对 {vars} 的恒等性无法判定"
        };
        return new ProblemReport(ProblemShape.Universal, truth, text);
    }

    // ---------- 纯存在：方程 / 不等式求根 ----------
    private static ProblemReport EvaluateExistential(
        List<(QuantifierKind Kind, string Var)> prefix, Statement matrix)
    {
        if (prefix.Count != 1)
            return new ProblemReport(ProblemShape.Existential, TruthValue.Unknown,
                "多变量存在量词需要结式消元", UnsupportedReason: "当前仅支持单变量 ∃");

        if (matrix is not RelationStatement relation)
            return new ProblemReport(ProblemShape.Existential, TruthValue.Unknown,
                "存在量词下的矩阵必须是单个原子关系", UnsupportedReason: "逻辑连接的 ∃ 未支持");

        if (relation.Left.Exact is null || relation.Right.Exact is null)
            return new ProblemReport(ProblemShape.Existential, TruthValue.Unknown,
                "表达式缺少符号精确数据", UnsupportedReason: "存在纯近似输入");

        Entity.Variable variable = prefix[0].Var;
        Entity statement = relation.Kind switch
        {
            RelationKind.Equal => MathS.Equality(relation.Left.Exact, relation.Right.Exact),
            RelationKind.LessThan => MathS.LessThan(relation.Left.Exact, relation.Right.Exact),
            RelationKind.GreaterThan => MathS.GreaterThan(relation.Left.Exact, relation.Right.Exact),
            _ => throw new NotSupportedException($"存在量词下的关系 {relation.Kind} 暂不支持")
        };

        Entity.Set solutions;
        try
        {
            solutions = statement.Solve(variable);
        }
        catch (Exception ex)
        {
            return new ProblemReport(ProblemShape.Existential, TruthValue.Unknown,
                "求解器异常：" + ex.GetType().Name, UnsupportedReason: ex.Message);
        }

        // 方程求解器在复数域上工作：必须过滤掉非实解（如 x²+1=0 的 ±i）
        bool empty;
        if (solutions is Entity.Set.FiniteSet finiteSet)
        {
            var realRoots = finiteSet
                .Where(z => z.EvalNumerical() is Entity.Number.Complex c && c.ImaginaryPart.IsZero)
                .Select(z => z.Simplify())
                .ToArray();
            empty = realRoots.Length == 0;
            if (!empty) solutions = MathS.Sets.Finite(realRoots);
        }
        else
        {
            empty = false;   // 区间等非有限集合：视为非空
        }

        return new ProblemReport(
            ProblemShape.Existential,
            empty ? TruthValue.False : TruthValue.True,
            empty ? $"不存在实数 {prefix[0].Var} 满足条件" : $"存在实数 {prefix[0].Var} 满足条件（解集 {RealSets.Describe(solutions)}）",
            Solutions: solutions);
    }

    // ---------- 矩阵求值（三值逻辑） ----------
    private static TruthValue EvaluateMatrix(Statement matrix) => matrix switch
    {
        RelationStatement relation => EvaluateRelation(relation),
        JunctionStatement junction => junction.IsAnd
            ? KleeneAnd(junction.Items.Select(EvaluateMatrix))
            : KleeneOr(junction.Items.Select(EvaluateMatrix)),
        NotStatement not => Negate(EvaluateMatrix(not.Body)),
        ImplicationStatement implication => EvaluateImplication(implication),
        QuantifiedStatement => TruthValue.Unknown,
        _ => TruthValue.Unknown
    };

    private static TruthValue EvaluateImplication(ImplicationStatement implication)
    {
        var premises = implication.Premises.Select(EvaluateMatrix).ToList();
        var conclusion = EvaluateMatrix(implication.Conclusion);

        if (premises.Any(p => p == TruthValue.False)) return TruthValue.True;      // 前提不成立 => 蕴含为真
        if (conclusion == TruthValue.True) return TruthValue.True;
        if (premises.All(p => p == TruthValue.True)) return conclusion;            // 前提全真 => 取决于结论
        return TruthValue.Unknown;
    }

    private static TruthValue EvaluateRelation(RelationStatement relation)
    {
        var diff = relation.Left - relation.Right;

        switch (relation.Kind)
        {
            case RelationKind.Equal:
                return diff.VerifyStrictZero() switch
                {
                    StrictResult.StrictlyTrue => TruthValue.True,
                    StrictResult.StrictlyFalse => TruthValue.False,
                    _ => TruthValue.Unknown
                };

            case RelationKind.NotEqual:
                // ∀x (L ≠ R)：仅在 L−R 恒为非零常量时可判定
                if (diff.Exact is Entity.Number && System.Math.Abs(diff.Approx) > 1e-9) return TruthValue.True;
                if (diff.VerifyStrictZero() == StrictResult.StrictlyTrue) return TruthValue.False;
                return TruthValue.Unknown;

            default:
                // 不等式：仅在两侧都是数值常量时可判定
                if (relation.Left.Exact is not Entity.Number || relation.Right.Exact is not Entity.Number)
                    return TruthValue.Unknown;

                double l = relation.Left.Approx, r = relation.Right.Approx;
                return relation.Kind switch
                {
                    RelationKind.LessThan => l < r - 1e-9 ? TruthValue.True : (l > r + 1e-9 ? TruthValue.False : TruthValue.Unknown),
                    RelationKind.GreaterThan => l > r + 1e-9 ? TruthValue.True : (l < r - 1e-9 ? TruthValue.False : TruthValue.Unknown),
                    RelationKind.LessOrEqual => l <= r + 1e-9 ? TruthValue.True : TruthValue.False,
                    RelationKind.GreaterOrEqual => l >= r - 1e-9 ? TruthValue.True : TruthValue.False,
                    _ => TruthValue.Unknown
                };
        }
    }

    private static TruthValue KleeneAnd(IEnumerable<TruthValue> values)
    {
        bool anyFalse = false, anyUnknown = false;
        foreach (var v in values)
        {
            if (v == TruthValue.False) anyFalse = true;
            else if (v == TruthValue.Unknown) anyUnknown = true;
        }
        if (anyFalse) return TruthValue.False;
        return anyUnknown ? TruthValue.Unknown : TruthValue.True;
    }

    private static TruthValue KleeneOr(IEnumerable<TruthValue> values)
    {
        bool anyTrue = false, anyUnknown = false;
        foreach (var v in values)
        {
            if (v == TruthValue.True) anyTrue = true;
            else if (v == TruthValue.Unknown) anyUnknown = true;
        }
        if (anyTrue) return TruthValue.True;
        return anyUnknown ? TruthValue.Unknown : TruthValue.False;
    }

    private static TruthValue Negate(TruthValue value) => value switch
    {
        TruthValue.True => TruthValue.False,
        TruthValue.False => TruthValue.True,
        _ => TruthValue.Unknown
    };

    /// <summary>
    /// 【题目现场约束】检查"给定假设下结论是否成立"：`H₁ ∧ … ∧ Hₙ ⇒ C`。
    /// <para>
    /// 归结为反正例检查：`∃vars (H ∧ ¬C)` 不可满足 ⇒ 结论成立。
    /// 假设与结论都是**关系对象（表达式 + 关系符号）**，而不是 predicate/delegate——
    /// 因此它们可以被代数地代入、消元、交给判定器。
    /// </para>
    /// </summary>
    public static EntailmentReport CheckEntailment(
        IAlgebraicOracle oracle,
        IReadOnlyList<string> variables,
        IReadOnlyList<RelationStatement> hypotheses,
        RelationStatement conclusion)
    {
        var verdict = oracle.DecideUniversal(variables, hypotheses, conclusion);
        var truth = verdict.Kind switch
        {
            OracleVerdictKind.Unsat => TruthValue.True,
            OracleVerdictKind.Sat => TruthValue.False,
            _ => TruthValue.Unknown
        };
        string explanation = verdict.Kind switch
        {
            OracleVerdictKind.Unsat => "在给定约束下未找到反例，结论成立",
            OracleVerdictKind.Sat => "找到反例（约束满足但结论不成立）：" + verdict.Model,
            _ => "无法判定：" + (verdict.Reason ?? "unknown")
        };
        return new EntailmentReport(truth, verdict, explanation);
    }
}

/// <summary>假设 ⇒ 结论的判定报告（三值 + 判定器原始结论）。</summary>
public sealed record EntailmentReport(TruthValue Truth, OracleVerdict Verdict, string Explanation);
