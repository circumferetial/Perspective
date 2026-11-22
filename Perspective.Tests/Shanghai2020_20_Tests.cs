using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// M2：2020 上海卷 20（双曲线 + 圆 + 分段曲线 Γ）。
/// 原文核实要点：Γ = {Γ1 与 Γ2 上 |x|&gt;x_A 的部分}；
/// (2) 求 cos∠F₁PF₂；(3) 直线过 S(0,(b²+4)/2) 且斜率 −b/2，与 Γ 交于 M、N，求 OM·ON 及范围。
/// </summary>
public class Shanghai2020_20_Tests
{
    private static ExactScalar B2(ExactScalar b) => b * b;

    /// <summary>x_A² = 8(b²+2)/(b²+4)。</summary>
    private static ExactScalar XASquared(ExactScalar b)
    {
        var u = B2(b);
        return ExactScalar.FromInt(8) * (u + ExactScalar.FromInt(2)) / (u + ExactScalar.FromInt(4));
    }

    private static Conic Gamma1(ExactScalar b)
    {
        // x²/4 − y²/b² = 1  ⇔  b²x² − 4y² − 4b² = 0
        var u = B2(b);
        return Conic.FromGeneralEquation(u, ExactScalar.FromInt(0), -ExactScalar.FromInt(4),
            ExactScalar.FromInt(0), ExactScalar.FromInt(0), -ExactScalar.FromInt(4) * u);
    }

    private static Conic Gamma2(ExactScalar b) =>
        Conic.Circle(0, 0, ExactScalar.Sqrt(ExactScalar.FromInt(4) + B2(b)));

    private static HLine LineL(ExactScalar b)
    {
        // y = −(b/2)x + (b²+4)/2  ⇔  b·x + 2y − (b²+4) = 0
        var u = B2(b);
        return new HLine(b, ExactScalar.FromInt(2), -(u + ExactScalar.FromInt(4)));
    }

    // ---------- (1) x_A = √6 ⇒ b = 2 ----------
    [Fact]
    public void Question1_XAEqualsSqrt6_ImpliesBEquals2()
    {
        var b = ExactScalar.FromInt(2);
        var xA2 = XASquared(b);

        // 代入 b=2：x_A² = 8·6/8 = 6
        Assert.Equal(StrictResult.StrictlyTrue,
            (xA2 - ExactScalar.FromInt(6)).VerifyStrictZero());
    }

    // ---------- (2) b=√5，|PF₁|=8 ⇒ cos∠F₁PF₂ = 11/16 ----------
    [Fact]
    public void Question2_CosineOfAngle_IsElevenSixteenths()
    {
        var b = ExactScalar.FromFormula("sqrt(5)");
        var xA = ExactScalar.Sqrt(XASquared(b));       // 2√14/3
        var p = HPoint.FromCartesian(
            ExactScalar.FromInt(4),
            ExactScalar.FromFormula("sqrt(15)"));

        // P 属于 Γ1 段：x > x_A
        Assert.True(p.X.Approx > xA.Approx);
        Assert.Equal(StrictResult.StrictlyTrue, Gamma1(b).ContainsStrictly(p));
        Assert.Equal(StrictResult.StrictlyFalse, Gamma2(b).ContainsStrictly(p));

        // F₁=(−3,0)、F₂=(3,0)
        var f1 = HPoint.FromCartesian(-3, 0);
        var f2 = HPoint.FromCartesian(3, 0);

        // |PF₁| = 8（严格）
        var pf1Squared = Metric.DistanceSquared(p, f1);
        Assert.Equal(StrictResult.StrictlyTrue,
            (pf1Squared - ExactScalar.FromInt(64)).VerifyStrictZero());

        // cos∠F₁PF₂ = 11/16
        var cosine = Metric.CosineAngle(Metric.Vector(p, f1), Metric.Vector(p, f2));
        Assert.NotNull(cosine);
        Assert.Equal(StrictResult.StrictlyTrue,
            (cosine!.Value - ExactScalar.FromFormula("11/16")).VerifyStrictZero());
        Assert.Equal(11.0 / 16.0, cosine.Value.Approx, 9);
    }

    // ---------- (3) 直线与圆相切于 M=(b,2)，OM·ON = b²+4 ----------
    [Fact]
    public void Question3_LineTangentAtM_AndDotProductIdentity()
    {
        var b = ExactScalar.FromSymbol("b");
        var u = B2(b);
        var line = LineL(b);
        var circle = Gamma2(b);

        // M=(b,2) 在直线上、在圆上
        var m = HPoint.FromCartesian(b, ExactScalar.FromInt(2));
        Assert.Equal(StrictResult.StrictlyTrue, line.ContainsStrictly(m));
        Assert.Equal(StrictResult.StrictlyTrue, circle.ContainsStrictly(m));

        // N = 直线与 Γ1 的交点（x = (b⁴+12b²+16)/(2b(b²+4))）
        var n = HPoint.FromCartesian(
            (u * u + ExactScalar.FromInt(12) * u + ExactScalar.FromInt(16)) /
            (ExactScalar.FromInt(2) * b * (u + ExactScalar.FromInt(4))),
            (u * u / ExactScalar.FromInt(4) + u + ExactScalar.FromInt(4)) / (u + ExactScalar.FromInt(4)));
        Assert.Equal(StrictResult.StrictlyTrue, line.ContainsStrictly(n));
        Assert.Equal(StrictResult.StrictlyTrue, Gamma1(b).ContainsStrictly(n));

        // OM·ON = b²+4（恒等式）
        var dot = m.X * n.X + m.Y * n.Y;
        Assert.Equal(StrictResult.StrictlyTrue,
            (dot - (u + ExactScalar.FromInt(4))).VerifyStrictZero());
    }

    // ---------- (3) 两交点存在性：b=3 时 M、N 都在 Γ 上 ----------
    [Fact]
    public void Question3_TwoIntersections_ForB3()
    {
        var b = ExactScalar.FromInt(3);
        var xA = ExactScalar.Sqrt(XASquared(b));

        var gamma = new PiecewiseConic()
            .Add(Gamma1(b), PieceCondition.Compare(RelationKind.GreaterOrEqual,
                ExactScalar.FromSymbol(PiecewiseConic.XSymbol).Abs(), xA))
            .Add(Gamma2(b), PieceCondition.Compare(RelationKind.GreaterOrEqual,
                ExactScalar.FromSymbol(PiecewiseConic.XSymbol).Abs(), xA));

        var points = gamma.IntersectLine(LineL(b));

        Assert.Equal(2, points.Count);   // M=(3,2) 与 N
        var m = HPoint.FromCartesian(3, 2);
        Assert.Contains(points, p => PointTools.SameStrictly(p, m) == StrictResult.StrictlyTrue);
    }

    // ---------- (3) 范围：b² > 2+2√5 ⇒ OM·ON > 6+2√5（下确界不可达） ----------
    [Fact]
    public void Question3_Range_IsOpenLowerBound()
    {
        var b = ExactScalar.FromSymbol("b");
        var u = B2(b);

        // y_A² − 4 = b² − 8(b²+2)/(b²+4) − 4 = (b⁴ − 4b² − 16)/(b² + 4)
        var yASquaredMinus4 = u - XASquared(b);   // y_A² − 4
        var expected = (u * u - ExactScalar.FromInt(4) * u - ExactScalar.FromInt(16)) / (u + ExactScalar.FromInt(4));
        Assert.Equal(StrictResult.StrictlyTrue, (yASquaredMinus4 - expected).VerifyStrictZero());

        // 边界 b² = 2+2√5：y_A=2（不可取，因为要求 y_A>2）⇒ b²+4=6+2√5 不可达
        var boundaryU = ExactScalar.FromInt(2) + ExactScalar.FromInt(2) * ExactScalar.FromFormula("sqrt(5)");
        var boundaryYA = boundaryU - XASquared(ExactScalar.Sqrt(boundaryU));   // y_A² − 4
        Assert.Equal(StrictResult.StrictlyTrue, boundaryYA.VerifyStrictZero());   // 恰为 0

        // b²>2+2√5 时 OM·ON=b²+4>6+2√5
        var dotMinusBound = (u + ExactScalar.FromInt(4)) - (ExactScalar.FromInt(6) + ExactScalar.FromInt(2) * ExactScalar.FromFormula("sqrt(5)"));
        var condition = u - boundaryU;
        Assert.Equal(StrictResult.StrictlyTrue,
            (dotMinusBound - condition).VerifyStrictZero());   // 两者差为 0：同正同负
    }
}
