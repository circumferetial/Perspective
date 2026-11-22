using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>命题 9–11：Pascal、完全四点形调和、Brianchon。</summary>
public class ProjectiveTheoremTests
{
    private static void AssertStrictlyCollinear(FactBase facts, CollinearFact f) =>
        Assert.Equal(StrictResult.StrictlyTrue,
            PointTools.Collinear(facts.Points[f.P1Id], facts.Points[f.P2Id], facts.Points[f.P3Id]));

    [Fact]
    public void Pascal_ProducesStrictlyCollinearFact()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddConic("C", Scenes.UnitCircle);
        foreach (var (id, p) in Scenes.SixConicPoints)
        {
            facts.AddPoint(id, p);
            facts.AddFact(new OnConicFact(id, "C"), "t");
        }

        var engine = new TheoremEngine().Register<PascalRule>();
        engine.RunToFixedPoint(facts);

        Assert.Single(facts.Collinears);
        AssertStrictlyCollinear(facts, facts.Collinears[0]);

        // 幂等
        engine.RunToFixedPoint(facts);
        Assert.Single(facts.Collinears);
    }

    [Fact]
    public void Pascal_RejectsOffConicPointAndTooFewPoints()
    {
        // 一个点不在圆上，但伪造 OnConic 标签 -> 严通道应把它过滤掉，剩 5 点，不触发
        var bad = new FactBase { CurrentLevel = TransformLevel.Projective };
        bad.AddConic("C", Scenes.UnitCircle);
        foreach (var (id, p) in Scenes.SixConicPoints.Take(5))
        {
            bad.AddPoint(id, p);
            bad.AddFact(new OnConicFact(id, "C"), "t");
        }
        bad.AddPoint("X", HPoint.FromCartesian(0, 2));   // 不在单位圆上
        bad.AddFact(new OnConicFact("X", "C"), "bogus");
        new TheoremEngine().Register<PascalRule>().RunToFixedPoint(bad);
        Assert.Empty(bad.Collinears);
    }

    [Fact]
    public void CompleteQuadrangle_ProducesSixStrictHarmonicFacts()
    {
        var facts = Scenes.QuadrangleScene();
        var engine = new TheoremEngine().Register<CompleteQuadrangleHarmonicRule>();
        engine.RunToFixedPoint(facts);

        Assert.Equal(6, facts.Harmonics.Count);
        foreach (var h in facts.Harmonics)
            Assert.Equal(StrictResult.StrictlyTrue, GeometryAudit.IsHarmonic(
                facts.Points[h.AId], facts.Points[h.BId], facts.Points[h.CId], facts.Points[h.DId]));

        // AB 边：对角点 P=(4,0)，交点 S=(4/7,0)
        var ab = facts.Harmonics.First(h => h.AId == "A" && h.BId == "B" && h.CId.Contains("CQ[AB|CD]"));
        var g = facts.Points[ab.CId].ToCartesianApprox()!.Value;
        var s = facts.Points[ab.DId].ToCartesianApprox()!.Value;
        Assert.True(System.Math.Abs(g.X - 4) < 1e-9 && System.Math.Abs(g.Y) < 1e-9);
        Assert.True(System.Math.Abs(s.X - 4.0 / 7) < 1e-9 && System.Math.Abs(s.Y) < 1e-9);

        // 幂等
        engine.RunToFixedPoint(facts);
        Assert.Equal(6, facts.Harmonics.Count);
    }

    [Fact]
    public void CompleteQuadrangle_RejectsThreeCollinearPoints()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddPoint("A", HPoint.FromCartesian(0, 0));
        facts.AddPoint("B", HPoint.FromCartesian(1, 0));
        facts.AddPoint("C", HPoint.FromCartesian(2, 0));   // A,B,C 共线
        facts.AddPoint("D", HPoint.FromCartesian(0, 2));
        new TheoremEngine().Register<CompleteQuadrangleHarmonicRule>().RunToFixedPoint(facts);
        Assert.Empty(facts.Harmonics);
    }

    [Fact]
    public void Brianchon_ProducesStrictlyConcurrentDiagonals()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddConic("C", Scenes.UnitCircle);
        int i = 1;
        foreach (var (touchId, touch, line) in Scenes.SixTangents)
        {
            facts.AddPoint(touchId, touch);
            facts.AddLine("T" + i, line);
            facts.AddFact(new TangentFact("T" + i, "C", touchId), "t");
            i++;
        }

        var engine = new TheoremEngine().Register<BrianchonRule>();
        engine.RunToFixedPoint(facts);

        Assert.Single(facts.Concurrents);
        var f = facts.Concurrents[0];
        var l1 = facts.Lines[f.L1Id];
        var l2 = facts.Lines[f.L2Id];
        var l3 = facts.Lines[f.L3Id];
        var x = HLine.Intersect(l1, l2);
        var y = HLine.Intersect(l1, l3);
        Assert.Equal(StrictResult.StrictlyTrue, PointTools.SameStrictly(x, y));

        // 幂等
        engine.RunToFixedPoint(facts);
        Assert.Single(facts.Concurrents);
    }

    [Fact]
    public void Brianchon_RejectsForgedTangent()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddConic("C", Scenes.UnitCircle);
        int i = 1;
        foreach (var (touchId, touch, line) in Scenes.SixTangents)
        {
            facts.AddPoint(touchId, touch);
            // 最后一条替换成过圆心的割线，但保留伪造 TangentFact
            var actual = i == 6 ? new HLine(1, 0, 0) : line;
            facts.AddLine("T" + i, actual);
            facts.AddFact(new TangentFact("T" + i, "C", touchId), "bogus");
            i++;
        }
        new TheoremEngine().Register<BrianchonRule>().RunToFixedPoint(facts);
        Assert.Empty(facts.Concurrents);
    }
}
