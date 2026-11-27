using Perspective.Core.Math;
using Perspective.Core.Reasoning;
using Perspective.Oracle.Z3;

namespace Perspective.Oracle.Z3.Tests;

/// <summary>
/// M1-a：Z3 实代数判定器（∃-QF 闭合）与 OracleStep 记录。
/// 说明：本套件单独成项目——引用 Z3 原生库会显著拖慢测试进程启动，
/// 不能让它污染 Perspective.Tests 的快速回归。
/// </summary>
public class Z3OracleTests
{
    private static readonly Z3Oracle Oracle = new(timeoutMs: 5000);
    private static readonly string[] VariableX = ["x"];

    private static RelationStatement Relation(RelationKind kind, ExactScalar left, ExactScalar right) =>
        new(kind, left, right);

    [Fact]
    public void ExistsWithSolution_IsSat()
    {
        var t = ExactScalar.FromSymbol("t");
        var verdict = Oracle.DecideExists(["t"],
        [
            Relation(RelationKind.LessThan, t * t, ExactScalar.FromInt(2)),
            Relation(RelationKind.GreaterThan, t, ExactScalar.FromInt(1))
        ]);

        Assert.Equal(OracleVerdictKind.Sat, verdict.Kind);
        Assert.False(string.IsNullOrEmpty(verdict.Model));
    }

    [Fact]
    public void ExistsWithoutSolution_IsUnsat()
    {
        var t = ExactScalar.FromSymbol("t");
        var verdict = Oracle.DecideExists(["t"],
        [
            Relation(RelationKind.LessThan, t * t, ExactScalar.FromInt(0))
        ]);

        Assert.Equal(OracleVerdictKind.Unsat, verdict.Kind);
    }

    [Fact]
    public void UniversalWithoutCounterexample_IsProved()
    {
        var k = ExactScalar.FromSymbol("k");
        var verdict = Oracle.DecideUniversal(
            ["k"], [],
            Relation(RelationKind.GreaterThan, k * k + 1, ExactScalar.FromInt(0)));

        Assert.Equal(OracleVerdictKind.Unsat, verdict.Kind);   // 无反正例 => ∀ 成立
    }

    [Fact]
    public void UniversalWithCounterexample_IsRefuted()
    {
        var k = ExactScalar.FromSymbol("k");
        var verdict = Oracle.DecideUniversal(
            ["k"], [],
            Relation(RelationKind.LessThan, k * k, ExactScalar.FromInt(4)));

        Assert.Equal(OracleVerdictKind.Sat, verdict.Kind);     // 找到反正例
    }

    [Fact]
    public void TryZero_ClosesRationalIdentities()
    {
        Assert.Equal(StrictVerdict.StrictlyTrue,
            Oracle.TryZero(ExactScalar.FromInt(0)));
        Assert.Equal(StrictVerdict.StrictlyFalse,
            Oracle.TryZero(ExactScalar.FromInt(1)));

        var identity = ExactScalar.FromInt(1) / ExactScalar.FromInt(3)
                       + ExactScalar.FromInt(1) / ExactScalar.FromInt(6)
                       - ExactScalar.FromInt(1) / ExactScalar.FromInt(2);
        Assert.Equal(StrictVerdict.StrictlyTrue, Oracle.TryZero(identity));

        Assert.Equal(StrictVerdict.Unknown,
            Oracle.TryZero(ExactScalar.FromApprox(1e-12)));
    }

    [Fact]
    public void TryZero_HandlesSquareRoots()
    {
        var sqrt2 = ExactScalar.Sqrt(ExactScalar.FromInt(2));
        Assert.Equal(StrictVerdict.StrictlyTrue,
            Oracle.TryZero(sqrt2 * sqrt2 - ExactScalar.FromInt(2)));

        var sqrt3 = ExactScalar.Sqrt(ExactScalar.FromInt(3));
        var sqrt6 = ExactScalar.Sqrt(ExactScalar.FromInt(6));
        var rhs = ExactScalar.Sqrt(ExactScalar.FromInt(5)
                                   + ExactScalar.FromInt(2) * sqrt6);
        Assert.Equal(StrictVerdict.StrictlyTrue,
            Oracle.TryZero(sqrt2 + sqrt3 - rhs));

        Assert.Equal(StrictVerdict.Unknown,
            Oracle.TryZero(sqrt2 - ExactScalar.FromApprox(1.4142135623730951)));
    }

    [Fact]
    public void RationalCoefficients_AreExact()
    {
        // 3x + 1 = 0 有精确解 x = -1/3（若用 double 近似会引入误差）
        var x = ExactScalar.FromSymbol("x");
        var verdict = Oracle.DecideExists(VariableX,
        [
            Relation(RelationKind.Equal, ExactScalar.FromInt(3) * x + ExactScalar.FromInt(1), ExactScalar.FromInt(0))
        ]);

        Assert.Equal(OracleVerdictKind.Sat, verdict.Kind);
    }

    [Fact]
    public void UnsupportedNode_IsReportedAsUnknown()
    {
        var x = ExactScalar.FromSymbol("x");
        var verdict = Oracle.DecideExists(VariableX,
        [
            Relation(RelationKind.Equal, ExactScalar.Sin(x), ExactScalar.FromInt(0))
        ]);

        Assert.Equal(OracleVerdictKind.Unknown, verdict.Kind);
        Assert.False(string.IsNullOrEmpty(verdict.Reason));
    }

    [Fact]
    public void OracleStep_IsRenderedHonestly()
    {
        var step = new OracleStep(
            Claim: "∀k, k²+1>0",
            Constraints: [],
            Solver: "Z3.nlsat",
            Verdict: OracleVerdictKind.Unsat);

        string text = ProofNarrator.RenderOracle(step);
        Assert.Contains("不存在反例", text);
        Assert.Contains("Z3.nlsat", text);
    }
}
