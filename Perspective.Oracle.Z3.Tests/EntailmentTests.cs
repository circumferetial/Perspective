using Perspective.Core.Math;
using Perspective.Core.Reasoning;
using Perspective.Oracle.Z3;

namespace Perspective.Oracle.Z3.Tests;

/// <summary>
/// 【题目现场约束】假设 ⇒ 结论的判定：题目给的新等式/不等式不需要进"事实库"，
/// 它们就是问题实例的约束（关系对象），交给判定器闭合。
/// </summary>
public class EntailmentTests
{
    private static readonly Z3Oracle Oracle = new(timeoutMs: 5000);

    private static RelationStatement Relation(RelationKind kind, ExactScalar left, ExactScalar right) =>
        new(kind, left, right);

    [Fact]
    public void NovelInequalities_ImplyConclusion()
    {
        // 已知 k>0 且 k²<2，求证 k³<3（库中没有这条"定理"，只是约束+结论）
        var k = ExactScalar.FromSymbol("k");
        List<RelationStatement> hypotheses =
        [
            Relation(RelationKind.GreaterThan, k, ExactScalar.FromInt(0)),
            Relation(RelationKind.LessThan, k * k, ExactScalar.FromInt(2))
        ];
        var conclusion = Relation(RelationKind.LessThan, k * k * k, ExactScalar.FromInt(3));

        var report = ProblemSpec.CheckEntailment(Oracle, ["k"], hypotheses, conclusion);

        Assert.Equal(TruthValue.True, report.Truth);
        Assert.Equal(OracleVerdictKind.Unsat, report.Verdict.Kind);
    }

    [Fact]
    public void NovelInequalities_DoNotImplyFalseConclusion()
    {
        // 同样的 k>0 且 k²<2 推不出 k<1（k=1.2 是反例）
        var k = ExactScalar.FromSymbol("k");
        List<RelationStatement> hypotheses =
        [
            Relation(RelationKind.GreaterThan, k, ExactScalar.FromInt(0)),
            Relation(RelationKind.LessThan, k * k, ExactScalar.FromInt(2))
        ];
        var conclusion = Relation(RelationKind.LessThan, k, ExactScalar.FromInt(1));

        var report = ProblemSpec.CheckEntailment(Oracle, ["k"], hypotheses, conclusion);

        Assert.Equal(TruthValue.False, report.Truth);
        Assert.Equal(OracleVerdictKind.Sat, report.Verdict.Kind);
        Assert.False(string.IsNullOrEmpty(report.Verdict.Model));
    }

    [Fact]
    public void GeometricNovelConstraint_ImplyingBound()
    {
        // 单位圆上第一象限的点：x²+y²=1 ∧ x>0 ∧ y>0 ⇒ x+y>1
        var x = ExactScalar.FromSymbol("x");
        var y = ExactScalar.FromSymbol("y");
        List<RelationStatement> hypotheses =
        [
            Relation(RelationKind.Equal, x * x + y * y, ExactScalar.FromInt(1)),
            Relation(RelationKind.GreaterThan, x, ExactScalar.FromInt(0)),
            Relation(RelationKind.GreaterThan, y, ExactScalar.FromInt(0))
        ];
        var conclusion = Relation(RelationKind.GreaterThan, x + y, ExactScalar.FromInt(1));

        var report = ProblemSpec.CheckEntailment(Oracle, ["x", "y"], hypotheses, conclusion);

        Assert.Equal(TruthValue.True, report.Truth);
    }

    [Fact]
    public void GeometricNovelConstraint_UpperBound()
    {
        // 同上 ⇒ x+y<2（最大值 √2 < 2）
        var x = ExactScalar.FromSymbol("x");
        var y = ExactScalar.FromSymbol("y");
        List<RelationStatement> hypotheses =
        [
            Relation(RelationKind.Equal, x * x + y * y, ExactScalar.FromInt(1)),
            Relation(RelationKind.GreaterThan, x, ExactScalar.FromInt(0)),
            Relation(RelationKind.GreaterThan, y, ExactScalar.FromInt(0))
        ];
        var conclusion = Relation(RelationKind.LessThan, x + y, ExactScalar.FromInt(2));

        var report = ProblemSpec.CheckEntailment(Oracle, ["x", "y"], hypotheses, conclusion);

        Assert.Equal(TruthValue.True, report.Truth);
    }

    [Fact]
    public void NonPolynomialConstraint_IsReportedUnknown()
    {
        var x = ExactScalar.FromSymbol("x");
        List<RelationStatement> hypotheses =
        [
            Relation(RelationKind.Equal, ExactScalar.Sin(x), ExactScalar.FromInt(0))
        ];
        var conclusion = Relation(RelationKind.LessThan, x, ExactScalar.FromInt(4));

        var report = ProblemSpec.CheckEntailment(Oracle, ["x"], hypotheses, conclusion);

        Assert.Equal(TruthValue.Unknown, report.Truth);
    }

    [Fact]
    public void ImpliesStatement_IsClassifiedButNeedsOracle()
    {
        // 语法层：∀k (k>0 ∧ k²<2 → k³<3) 可以写成量词树（Horn 形）
        var k = ExactScalar.FromSymbol("k");
        var statement = Statement.ForAll(["k"],
            Statement.Implies(
                [
                    Relation(RelationKind.GreaterThan, k, ExactScalar.FromInt(0)),
                    Relation(RelationKind.LessThan, k * k, ExactScalar.FromInt(2))
                ],
                Relation(RelationKind.LessThan, k * k * k, ExactScalar.FromInt(3))));

        // 纯符号引擎无法判定"假设下的蕴含"（它只能判恒等式），如实返回 Unknown
        var syntactic = ProblemSpec.Evaluate(statement);
        Assert.Equal(ProblemShape.Universal, syntactic.Shape);
        Assert.Equal(TruthValue.Unknown, syntactic.Truth);

        // 交给判定器闭合后为真
        var closed = ProblemSpec.CheckEntailment(Oracle, ["k"],
            [
                Relation(RelationKind.GreaterThan, k, ExactScalar.FromInt(0)),
                Relation(RelationKind.LessThan, k * k, ExactScalar.FromInt(2))
            ],
            Relation(RelationKind.LessThan, k * k * k, ExactScalar.FromInt(3)));

        Assert.Equal(TruthValue.True, closed.Truth);
    }
}
