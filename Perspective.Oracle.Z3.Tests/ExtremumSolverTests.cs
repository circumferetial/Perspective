using Perspective.Core.Math;
using Perspective.Core.Reasoning;
using Perspective.Oracle.Z3;

namespace Perspective.Oracle.Z3.Tests;

/// <summary>
/// M1-b：一维极值骨架（候选生成 + Z3 闭合）。
/// </summary>
public class ExtremumSolverTests
{
    private static readonly Z3Oracle Oracle = new(timeoutMs: 5000);

    private static RelationStatement Relation(RelationKind kind, ExactScalar left, ExactScalar right) =>
        new(kind, left, right);

    [Fact]
    public void Maximum_OfQuadraticOnInterval()
    {
        // f(x) = x(1−x) 在 [0,1] 上的最大值 = 1/4，在 x=1/2 取到
        var x = ExactScalar.FromSymbol("x");
        var objective = x * (ExactScalar.FromInt(1) - x);
        List<RelationStatement> domain =
        [
            Relation(RelationKind.GreaterOrEqual, x, ExactScalar.FromInt(0)),
            Relation(RelationKind.LessOrEqual, x, ExactScalar.FromInt(1))
        ];

        var report = ExtremumSolver.FindExtremum("x", objective, domain, ExtremumKind.Maximum, Oracle);

        Assert.NotNull(report);
        Assert.Equal(0.25, report!.Value.Approx, 12);
        Assert.True(report.Attained);
        Assert.NotNull(report.Closure);
        Assert.Equal(OracleVerdictKind.Unsat, report.Closure!.Kind);   // 不存在更大的点
    }

    [Fact]
    public void Minimum_OfQuadraticOnInterval()
    {
        var x = ExactScalar.FromSymbol("x");
        var objective = x * (ExactScalar.FromInt(1) - x);
        List<RelationStatement> domain =
        [
            Relation(RelationKind.GreaterOrEqual, x, ExactScalar.FromInt(0)),
            Relation(RelationKind.LessOrEqual, x, ExactScalar.FromInt(1))
        ];

        var report = ExtremumSolver.FindExtremum("x", objective, domain, ExtremumKind.Minimum, Oracle);

        Assert.NotNull(report);
        Assert.Equal(0.0, report!.Value.Approx, 12);   // 端点 x=0 或 1
        Assert.True(report.Attained);
        Assert.Equal(OracleVerdictKind.Unsat, report.Closure!.Kind);
    }

    /// <summary>
    /// 2022 浙江卷 21(ⅰ)：P(0,1) 到椭圆 x²/12+y²=1 上点的距离最大值。
    /// 令 s=sin t ∈ [−1,1]，距离² = 13 − 11s² − 2s，最大值 = 144/11（在 s = −1/11 取到）。
    /// </summary>
    [Fact]
    public void Question2022_Zhejiang_21_1_MaxDistanceSquared()
    {
        var s = ExactScalar.FromSymbol("s");
        var objective = ExactScalar.FromInt(13) - ExactScalar.FromInt(11) * s * s - ExactScalar.FromInt(2) * s;
        List<RelationStatement> domain =
        [
            Relation(RelationKind.GreaterOrEqual, s, ExactScalar.FromInt(-1)),
            Relation(RelationKind.LessOrEqual, s, ExactScalar.FromInt(1))
        ];

        var report = ExtremumSolver.FindExtremum("s", objective, domain, ExtremumKind.Maximum, Oracle);

        Assert.NotNull(report);
        Assert.Equal(144.0 / 11.0, report!.Value.Approx, 10);
        Assert.True(report.Attained);
        Assert.Equal(OracleVerdictKind.Unsat, report.Closure!.Kind);

        // 精确形式核对：144/11
        Assert.Equal(StrictResult.StrictlyTrue,
            (report.Value - ExactScalar.FromFormula("144/11")).VerifyStrictZero());

        // 最小值 = 0（s=1 即 P 自身），同样闭合
        var minimum = ExtremumSolver.FindExtremum("s", objective, domain, ExtremumKind.Minimum, Oracle);
        Assert.NotNull(minimum);
        Assert.Equal(0.0, minimum!.Value.Approx, 12);
        Assert.Equal(OracleVerdictKind.Unsat, minimum.Closure!.Kind);
    }

    [Fact]
    public void WithoutOracle_ReportIsHonestlyUnclosed()
    {
        var x = ExactScalar.FromSymbol("x");
        var objective = x * (ExactScalar.FromInt(1) - x);
        List<RelationStatement> domain =
        [
            Relation(RelationKind.GreaterOrEqual, x, ExactScalar.FromInt(0)),
            Relation(RelationKind.LessOrEqual, x, ExactScalar.FromInt(1))
        ];

        var report = ExtremumSolver.FindExtremum("x", objective, domain, ExtremumKind.Maximum, oracle: null);

        Assert.NotNull(report);
        Assert.Null(report!.Closure);
        Assert.Contains("未闭合", report.Explanation);
    }
}
