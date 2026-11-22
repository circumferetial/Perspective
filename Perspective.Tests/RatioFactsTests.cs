using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// P1-4（第三批）：面积比（仿射）、线段比（相似）、交比（射影）事实与传递规则。
/// 场景点位与目标比值严格对应，避免"假设的边长/面积"与实际坐标不符。
/// </summary>
public class RatioFactsTests
{
    private static FactBase Scene()
    {
        // 面积比是仿射不变量、线段比是相似不变量；取 Similarity 视角让两类规则都能激活
        var facts = new FactBase { CurrentLevel = TransformLevel.Similarity };

        // 三个三角形，面积两倍分别为 4、2、1
        // T1: A(0,0) B(2,0) C(0,2)  => 4
        facts.AddPoint("A", HPoint.FromCartesian(0, 0));
        facts.AddPoint("B", HPoint.FromCartesian(2, 0));
        facts.AddPoint("C", HPoint.FromCartesian(0, 2));
        // T2: D(0,0) E(2,0) F(0,1)  => 2
        facts.AddPoint("D", HPoint.FromCartesian(0, 0));
        facts.AddPoint("E", HPoint.FromCartesian(2, 0));
        facts.AddPoint("F", HPoint.FromCartesian(0, 1));
        // T3: G(0,0) H(1,0) I(0,1)  => 1
        facts.AddPoint("G", HPoint.FromCartesian(0, 0));
        facts.AddPoint("H", HPoint.FromCartesian(1, 0));
        facts.AddPoint("I", HPoint.FromCartesian(0, 1));

        // 线段：|S1S2|=2、|S3S4|=1、|S5S6|=0.5
        facts.AddPoint("S1", HPoint.FromCartesian(0, 0));
        facts.AddPoint("S2", HPoint.FromCartesian(2, 0));
        facts.AddPoint("S3", HPoint.FromCartesian(0, 0));
        facts.AddPoint("S4", HPoint.FromCartesian(0, 1));
        facts.AddPoint("S5", HPoint.FromCartesian(0, 0));
        facts.AddPoint("S6", HPoint.FromCartesian(0, ExactScalar.FromFormula("1/2")));

        // 共线四点：(P0,P1;P2,P3) = 4/3
        facts.AddPoint("P0", HPoint.FromCartesian(0, 0));
        facts.AddPoint("P1", HPoint.FromCartesian(1, 0));
        facts.AddPoint("P2", HPoint.FromCartesian(2, 0));
        facts.AddPoint("P3", HPoint.FromCartesian(3, 0));
        return facts;
    }

    [Fact]
    public void AreaRatioFact_IsCanonicalized()
    {
        var a = AreaRatioFact.Create("A", "B", "C", "D", "E", "F", ExactScalar.FromInt(2));
        var b = AreaRatioFact.Create("C", "A", "B", "F", "D", "E", ExactScalar.FromInt(2));
        Assert.Equal(a, b);
    }

    [Fact]
    public void AreaRatio_StrictCheck()
    {
        var facts = Scene();
        // S(ABC)/S(DEF) = 4/2 = 2
        Assert.Equal(StrictResult.StrictlyTrue, GeometryAudit.IsAreaRatio(
            facts.Points["A"], facts.Points["B"], facts.Points["C"],
            facts.Points["D"], facts.Points["E"], facts.Points["F"],
            ExactScalar.FromInt(2)));

        Assert.NotEqual(StrictResult.StrictlyTrue, GeometryAudit.IsAreaRatio(
            facts.Points["A"], facts.Points["B"], facts.Points["C"],
            facts.Points["D"], facts.Points["E"], facts.Points["F"],
            ExactScalar.FromInt(3)));
    }

    [Fact]
    public void AreaRatioTransitivity_DerivesComposedRatio()
    {
        var facts = Scene();
        // S(ABC)/S(DEF) = 2，S(DEF)/S(GHI) = 2 ⇒ S(ABC)/S(GHI) = 4
        facts.AddFact(AreaRatioFact.Create("A", "B", "C", "D", "E", "F", ExactScalar.FromInt(2)), "初始");
        facts.AddFact(AreaRatioFact.Create("D", "E", "F", "G", "H", "I", ExactScalar.FromInt(2)), "初始");

        new TheoremEngine().Register<AreaRatioTransitivityRule>().RunToFixedPoint(facts);

        Assert.Contains(facts.AreaRatios, f =>
            f.A1 == "A" && f.A2 == "B" && f.A3 == "C" &&
            f.B1 == "G" && f.B2 == "H" && f.B3 == "I" &&
            (f.Value - ExactScalar.FromInt(4)).VerifyStrictZero() == StrictResult.StrictlyTrue);
    }

    [Fact]
    public void SegmentRatio_StrictCheck()
    {
        var facts = Scene();
        // |S1S2|/|S3S4| = 2/1 = 2
        Assert.Equal(StrictResult.StrictlyTrue, GeometryAudit.IsSegmentRatio(
            facts.Points["S1"], facts.Points["S2"],
            facts.Points["S3"], facts.Points["S4"],
            ExactScalar.FromInt(2)));

        Assert.NotEqual(StrictResult.StrictlyTrue, GeometryAudit.IsSegmentRatio(
            facts.Points["S1"], facts.Points["S2"],
            facts.Points["S3"], facts.Points["S4"],
            ExactScalar.FromInt(3)));
    }

    [Fact]
    public void SegmentRatioTransitivity_DerivesComposedRatio()
    {
        var facts = Scene();
        // |S1S2|/|S3S4| = 2，|S3S4|/|S5S6| = 2 ⇒ |S1S2|/|S5S6| = 4
        facts.AddFact(SegmentRatioFact.Create("S1", "S2", "S3", "S4", ExactScalar.FromInt(2)), "初始");
        facts.AddFact(SegmentRatioFact.Create("S3", "S4", "S5", "S6", ExactScalar.FromInt(2)), "初始");

        new TheoremEngine().Register<SegmentRatioTransitivityRule>().RunToFixedPoint(facts);

        Assert.Contains(facts.SegmentRatios, f =>
            f.A1 == "S1" && f.A2 == "S2" &&
            f.B1 == "S5" && f.B2 == "S6" &&
            (f.Value - ExactScalar.FromInt(4)).VerifyStrictZero() == StrictResult.StrictlyTrue);
    }

    [Fact]
    public void CrossRatio_StrictCheckAndCanonicalization()
    {
        var facts = Scene();
        // (P0,P1;P2,P3) = (2/1)/(3/2) = 4/3
        var value = ExactScalar.FromFormula("4/3");
        Assert.Equal(StrictResult.StrictlyTrue, GeometryAudit.IsCrossRatio(
            facts.Points["P0"], facts.Points["P1"], facts.Points["P2"], facts.Points["P3"], value));

        // 同时交换两对：交比不变
        Assert.Equal(
            CrossRatioFact.Create("P0", "P1", "P2", "P3", value),
            CrossRatioFact.Create("P1", "P0", "P3", "P2", value));

        // 只交换一对：取倒数（4/3 → 3/4）
        var reciprocal = CrossRatioFact.Create("P1", "P0", "P2", "P3", value);
        Assert.Equal(StrictResult.StrictlyTrue,
            (reciprocal.Value - ExactScalar.FromFormula("3/4")).VerifyStrictZero());
    }
}
