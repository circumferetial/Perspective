using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>M3 第二批：用模板复现真题结论（定点 / 极值 / 分段求交）。</summary>
public class TemplateExamTests
{
    [Fact]
    public void FixedPointTemplate_Reproduces2022NationalB20()
    {
        var e = Conic.Ellipse(ExactScalar.FromFormula("sqrt(3)"), ExactScalar.FromInt(2));
        var a = HPoint.FromCartesian(0, -2);
        var b = HPoint.FromFormula("3/2", "-1");
        var p = HPoint.FromCartesian(1, -2);
        var ab = HLine.FromPoints(a, b);

        static HPoint Norm(HPoint q) => new(q.X / q.W, q.Y / q.W, 1);

        HLine BuildHN(ExactScalar t)
        {
            var m = Norm(e.OtherIntersection(a, HLine.FromPointAndSlope(a, t)));
            var n = Norm(e.OtherIntersection(m, HLine.FromPoints(p, m)));
            var horizontal = new HLine(0, 1, -m.Y);
            var tt = Norm(HLine.Intersect(horizontal, ab));
            var h = new HPoint(2 * tt.X - m.X, 2 * tt.Y - m.Y, 1);
            return HLine.FromPoints(h, n);
        }

        var board = new ProblemBoard();
        var answer = board.Solve(new FixedPointTemplate(), new FixedPointInput(BuildHN), "20(2)");

        Assert.Contains("定点", answer.Summary);
        Assert.Contains("SymbolicProved", string.Join("", answer.Steps ?? []));
    }

    [Fact]
    public void UnivariateExtremumTemplate_ReproducesZhejiang21()
    {
        // 距离² = 13 − 11s² − 2s，s ∈ [−1,1]，max = 144/11
        var s = ExactScalar.FromSymbol("s");
        var objective = ExactScalar.FromInt(13) - ExactScalar.FromInt(11) * s * s - ExactScalar.FromInt(2) * s;
        List<RelationStatement> domain =
        [
            new(RelationKind.GreaterOrEqual, s, ExactScalar.FromInt(-1)),
            new(RelationKind.LessOrEqual, s, ExactScalar.FromInt(1))
        ];

        var board = new ProblemBoard();
        var answer = board.Solve(
            new UnivariateExtremumTemplate(),
            new ExtremumInput("s", objective, domain, ExtremumKind.Maximum),
            "21(1)");

        Assert.NotNull(answer.Numeric);
        Assert.Equal(StrictResult.StrictlyTrue,
            (answer.Numeric!.Value - ExactScalar.FromFormula("144/11")).VerifyStrictZero());
        Assert.True(answer.Attained);
    }

    [Fact]
    public void PiecewiseLineTemplate_ReproducesShanghai20_3()
    {
        var b = ExactScalar.FromInt(3);
        var u = b * b;
        var xA = ExactScalar.Sqrt(ExactScalar.FromInt(8) * (u + ExactScalar.FromInt(2)) / (u + ExactScalar.FromInt(4)));

        var gamma1 = Conic.FromGeneralEquation(u, ExactScalar.FromInt(0), -ExactScalar.FromInt(4),
            ExactScalar.FromInt(0), ExactScalar.FromInt(0), -ExactScalar.FromInt(4) * u);
        var gamma2 = Conic.Circle(0, 0, ExactScalar.Sqrt(ExactScalar.FromInt(4) + u));
        var curve = new PiecewiseConic()
            .Add(gamma1, PieceCondition.Compare(RelationKind.GreaterOrEqual,
                ExactScalar.FromSymbol(PiecewiseConic.XSymbol).Abs(), xA))
            .Add(gamma2, PieceCondition.Compare(RelationKind.GreaterOrEqual,
                ExactScalar.FromSymbol(PiecewiseConic.XSymbol).Abs(), xA));
        var line = new HLine(b, ExactScalar.FromInt(2), -(u + ExactScalar.FromInt(4)));

        var board = new ProblemBoard();
        var answer = board.Solve(new PiecewiseLineTemplate(),
            new PiecewiseLineInput(curve, line), "20(3)");

        Assert.Contains("交点 2 个", answer.Summary);
    }
}
