using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Oracle.Z3.Tests;

/// <summary>
/// 2023 新高考Ⅰ 22(2) 的证集构件：归约提案 + 内层最小化证书 + 外层严格性证书。
/// 供端到端回归与各分项测试复用。
/// </summary>
internal static class Exam2023_22_2
{
    public static ReductionProposal BuildReductionProposal()
    {
        var a = ExactScalar.FromSymbol("a");
        var b = ExactScalar.FromSymbol("b");
        var one = ExactScalar.FromInt(1);
        var quarter = ExactScalar.FromFormula("1/4");

        var t = a + b;
        var p = b - a;
        var s = (one + t * t + p * t) / (t * t);

        var pointA = HPoint.FromCartesian(a, a * a + quarter);
        var pointB = HPoint.FromCartesian(b, b * b + quarter);
        var pointC = HPoint.FromCartesian(b - s * t, b * b + quarter + s);

        var objective = ExactScalar.FromInt(2) *
            (Metric.Distance(pointA, pointB) + Metric.Distance(pointB, pointC));
        var reduced = ExactScalar.FromInt(2) * (p.Abs() + s.Abs()) * ExactScalar.Sqrt(one + t * t);

        return new ReductionProposal(
            ["a", "b"], objective, reduced,
            [new("t", t), new("p", p), new("s", s)],
            [
                new RelationStatement(RelationKind.NotEqual, t, ExactScalar.FromInt(0)),
                new RelationStatement(RelationKind.NotEqual, p, ExactScalar.FromInt(0))
            ],
            "user");
    }

    /// <summary>内层最小化：t≥1 时 t²(|p|+|s|) ≥ 1+t²（四个符号分支）。</summary>
    public static IReadOnlyList<InequalityCase> BuildInnerMinimizationCases()
    {
        var t = ExactScalar.FromSymbol("t");
        var p = ExactScalar.FromSymbol("p");
        var one = ExactScalar.FromInt(1);
        var zero = ExactScalar.FromInt(0);
        var t2 = t * t;
        var core = one + t2 + p * t;
        var cTerm = one + t2;

        var cases = new List<InequalityCase>();
        foreach (var (pSign, coreSign) in new[] { (+1, +1), (+1, -1), (-1, +1), (-1, -1) })
        {
            var a = t2 * p * ExactScalar.FromInt(pSign) + core * ExactScalar.FromInt(coreSign);
            var guards = new List<RelationStatement>
            {
                new(RelationKind.GreaterOrEqual, t, one),
                new(RelationKind.NotEqual, t, zero),
                pSign > 0
                    ? new RelationStatement(RelationKind.GreaterOrEqual, p, zero)
                    : new RelationStatement(RelationKind.LessThan, p, zero),
                coreSign > 0
                    ? new RelationStatement(RelationKind.GreaterOrEqual, core, zero)
                    : new RelationStatement(RelationKind.LessThan, core, zero)
            };
            cases.Add(new InequalityCase(guards, a - cTerm, $"inner p{(pSign > 0 ? "≥0" : "<0")}, s{(coreSign > 0 ? "≥0" : "<0")}"));
        }
        return cases;
    }

    /// <summary>
    /// 外层严格性：4A²(1+t²) − 27t⁴ &gt; 0（A=t²(|p|+|s|)），
    /// 非退化守卫：|t|≥1 时 p≠0，|t|≤1 时 core≠0（C=B）。
    /// </summary>
    public static IReadOnlyList<InequalityCase> BuildOuterStrictnessCases()
    {
        var t = ExactScalar.FromSymbol("t");
        var p = ExactScalar.FromSymbol("p");
        var one = ExactScalar.FromInt(1);
        var zero = ExactScalar.FromInt(0);
        var t2 = t * t;
        var t4 = t2 * t2;
        var core = one + t2 + p * t;

        var regions = new (string Name, RelationStatement[] Guards, RelationStatement Degenerate)[]
        {
            ("t≥1", [new(RelationKind.GreaterOrEqual, t, one)], new(RelationKind.NotEqual, p, zero)),
            ("t≤-1", [new(RelationKind.LessOrEqual, t, -one)], new(RelationKind.NotEqual, p, zero)),
            ("0<t≤1", [new(RelationKind.GreaterThan, t, zero), new(RelationKind.LessOrEqual, t, one)],
                new(RelationKind.NotEqual, core, zero)),
            ("-1≤t<0", [new(RelationKind.LessThan, t, zero), new(RelationKind.GreaterOrEqual, t, -one)],
                new(RelationKind.NotEqual, core, zero))
        };

        var cases = new List<InequalityCase>();
        foreach (var region in regions)
        foreach (var (pSign, coreSign) in new[] { (+1, +1), (+1, -1), (-1, +1), (-1, -1) })
        {
            var a = t2 * p * ExactScalar.FromInt(pSign) + core * ExactScalar.FromInt(coreSign);
            var polynomial = ExactScalar.FromInt(4) * a * a * (one + t2) - ExactScalar.FromInt(27) * t4;

            var guards = new List<RelationStatement>(region.Guards) { region.Degenerate };
            guards.Add(pSign > 0
                ? new RelationStatement(RelationKind.GreaterOrEqual, p, zero)
                : new RelationStatement(RelationKind.LessThan, p, zero));
            guards.Add(coreSign > 0
                ? new RelationStatement(RelationKind.GreaterOrEqual, core, zero)
                : new RelationStatement(RelationKind.LessThan, core, zero));

            cases.Add(new InequalityCase(guards, polynomial,
                $"outer {region.Name}, p{(pSign > 0 ? "≥0" : "<0")}, s{(coreSign > 0 ? "≥0" : "<0")}",
                Strict: true));
        }
        return cases;
    }
}
