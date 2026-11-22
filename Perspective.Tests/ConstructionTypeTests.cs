using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// P0-1：外部构造类型扩展（直线/垂足/极线/切线）的验收。
/// </summary>
public class ConstructionTypeTests
{
    private static (FactBase Facts, Conic Conic, HPoint A, HPoint B) EllipseScene()
    {
        var conic = Conic.Ellipse(ExactScalar.FromInt(2), ExactScalar.FromFormula("sqrt(3)"));
        var a = HPoint.FromFormula("1", "3/2");
        var b = HPoint.FromCartesian(2, 0);
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddConic("C", conic);
        facts.AddPoint("A", a);
        facts.AddPoint("B", b);
        return (facts, conic, a, b);
    }

    [Fact]
    public void ProposedTangentLines_AreAccepted_AndEnableChordOfContact()
    {
        var (facts, conic, a, b) = EllipseScene();
        var t1 = conic.PolarLine(a);
        var t2 = conic.PolarLine(b);

        // 外部提出两条切线（并声称切点）
        var r1 = ConstructionEngine.ProposeLine(facts, "T1", t1,
            [new ClaimTangentAt("T1", "C", "A")], "user");
        var r2 = ConstructionEngine.ProposeLine(facts, "T2", t2,
            [new ClaimTangentAt("T2", "C", "B")], "user");
        Assert.True(r1.Accepted, string.Join("；", r1.FailedClaims));
        Assert.True(r2.Accepted, string.Join("；", r2.FailedClaims));

        // 外部提出交点 Q
        var q = HLine.Intersect(t1, t2);
        var rq = ConstructionEngine.ProposePoint(facts, "Q", q,
            [new ClaimPointOnLine("Q", "T1"), new ClaimPointOnLine("Q", "T2")], "user");
        Assert.True(rq.Accepted);

        new TheoremEngine().Register<ChordOfContactIsPolarRule>().RunToFixedPoint(facts);
        Assert.Contains(facts.Polars, p => p.PoleId == "Q" && p.ConicId == "C");
    }

    [Fact]
    public void ProposedParallelLine_IsVerified_AndWrongParallelRejected()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Affine };
        facts.AddLine("L1", new HLine(0, 1, 0));       // y = 0

        var good = ConstructionEngine.ProposeLine(facts, "L2", new HLine(0, 1, -2),
            [new ClaimLineParallel("L2", "L1")], "llm");
        Assert.True(good.Accepted);

        var bad = ConstructionEngine.ProposeLine(facts, "L3", new HLine(1, 1, 0),
            [new ClaimLineParallel("L3", "L1")], "llm");
        Assert.False(bad.Accepted);
        Assert.Single(bad.FailedClaims);
        Assert.DoesNotContain("L3", facts.Lines.Keys);
    }

    [Fact]
    public void ProposedPolarLine_IsVerified()
    {
        var (facts, conic, _, _) = EllipseScene();
        var pole = HPoint.FromCartesian(0, 5);
        var polar = conic.PolarLine(pole);

        facts.AddPoint("Q5", pole);

        var good = ConstructionEngine.ProposeLine(facts, "PolarQ", polar,
            [new ClaimLinePolarOf("PolarQ", "Q5", "C")], "user");
        Assert.True(good.Accepted);

        // 谎称一条无关直线是极线 → 必须拒绝
        var bad = ConstructionEngine.ProposeLine(facts, "Fake", new HLine(0, 1, -9),
            [new ClaimLinePolarOf("Fake", "Q5", "C")], "user");
        Assert.False(bad.Accepted);
        Assert.DoesNotContain("Fake", facts.Lines.Keys);
    }

    [Fact]
    public void ProposedFoot_IsVerified_AndRejectsNonFoot()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Similarity };
        facts.AddPoint("P", HPoint.FromCartesian(0, 7));
        facts.AddLine("L", new HLine(0, 1, 0));        // y = 0

        var good = ConstructionEngine.ProposePoint(facts, "H", HPoint.FromCartesian(0, 0),
            [new ClaimFoot("H", "P", "L")], "user");
        Assert.True(good.Accepted);
        Assert.Contains(facts.OnLines, f => f.PointId == "H" && f.LineId == "L");

        var bad = ConstructionEngine.ProposePoint(facts, "H2", HPoint.FromCartesian(3, 0),
            [new ClaimFoot("H2", "P", "L")], "user");
        Assert.False(bad.Accepted);
        Assert.DoesNotContain("H2", facts.Points.Keys);
    }

    [Fact]
    public void ProposedPerpendicularLine_IsVerified()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Similarity };
        facts.AddLine("L", new HLine(1, 2, -3));      // x + 2y - 3 = 0

        var good = ConstructionEngine.ProposeLine(facts, "N", new HLine(2, -1, 7),
            [new ClaimLinePerpendicular("N", "L")], "user");
        Assert.True(good.Accepted);

        var bad = ConstructionEngine.ProposeLine(facts, "N2", new HLine(2, 1, 0),
            [new ClaimLinePerpendicular("N2", "L")], "user");
        Assert.False(bad.Accepted);
    }
}
