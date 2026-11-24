using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// P3b：弦方向点的极线 ⇒ 调和共轭 ⇒ 中点（椭圆/双曲线/抛物线统一，不需要中心）。
/// </summary>
public class ConicChordHarmonicTests
{
    private static ExactScalar F(string s) => ExactScalar.FromFormula(s);
    private static ExactScalar I(int v) => ExactScalar.FromInt(v);

    private static FactBase Scene(Conic conic, HPoint a, HPoint b)
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Affine };
        var line = HLine.FromPoints(a, b);
        facts.AddConic("C", conic);
        facts.AddPoint("A", a);
        facts.AddPoint("B", b);
        facts.AddLine("L", line);
        facts.AddFact(new OnConicFact("A", "C"), "测试");
        facts.AddFact(new OnConicFact("B", "C"), "测试");
        facts.AddFact(new OnLineFact("A", "L"), "测试");
        facts.AddFact(new OnLineFact("B", "L"), "测试");
        return facts;
    }

    [Fact]
    public void Ellipse_ChordMidpoint_IsRecoveredThroughPolarHarmonic()
    {
        // 椭圆 x²/4 + y² = 1；弦端点 (2,0)、(0,1)；中点应为 (1, 1/2)
        var facts = Scene(Conic.Ellipse(F("2"), I(1)),
            HPoint.FromCartesian(2, 0), HPoint.FromCartesian(0, 1));

        Assert.Equal(1, ConicChordPolarHarmonicRule.Apply(facts));
        Assert.Equal(1, MidpointFromHarmonicInfinityRule.Apply(facts));

        var mid = Assert.Single(facts.Midpoints);
        var midPoint = facts.Points[mid.MidId];
        Assert.Equal(StrictResult.StrictlyTrue,
            PointTools.SameStrictly(midPoint, HPoint.FromCartesian(1, F("1/2"))));
    }

    [Fact]
    public void Parabola_ChordMidpoint_IsRecoveredWithoutCenter()
    {
        // 抛物线 y² = 4x；点 (t², 2t)：t=1 → (1,2)，t=2 → (4,4)；中点应为 (5/2, 3)
        var parabola = new Conic(new Matrix3x3(
            I(0), I(0), -I(2),
            I(0), I(1), I(0),
            -I(2), I(0), I(0)));
        var facts = Scene(parabola,
            HPoint.FromCartesian(1, 2), HPoint.FromCartesian(4, 4));

        Assert.Equal(1, ConicChordPolarHarmonicRule.Apply(facts));
        Assert.Equal(1, MidpointFromHarmonicInfinityRule.Apply(facts));

        var mid = Assert.Single(facts.Midpoints);
        var midPoint = facts.Points[mid.MidId];
        Assert.Equal(StrictResult.StrictlyTrue,
            PointTools.SameStrictly(midPoint, HPoint.FromCartesian(F("5/2"), 3)));
    }

    [Fact]
    public void DegenerateConic_DoesNotProduceHarmonic()
    {
        // xy = 0：退化；弦 x+y=1 与它交于 (0,1)、(1,0)，但退化曲线没有正常配极
        var degenerate = new Conic(new Matrix3x3(
            I(0), F("1/2"), I(0),
            F("1/2"), I(0), I(0),
            I(0), I(0), I(0)));
        var facts = Scene(degenerate,
            HPoint.FromCartesian(0, 1), HPoint.FromCartesian(1, 0));

        Assert.Equal(0, ConicChordPolarHarmonicRule.Apply(facts));
        Assert.Empty(facts.Harmonics);
    }
}
