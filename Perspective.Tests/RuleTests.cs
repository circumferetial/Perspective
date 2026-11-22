using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>命题 1–8 的正例与"伪造标签"反例。</summary>
public class RuleTests
{
    // ---------- 命题 2：La Hire ----------
    [Fact]
    public void LaHire_PositiveAndForgedLabelNegative()
    {
        var circle = Conic.FromGeneralEquation(1, 0, 1, -4, 0, 0);
        var p = HPoint.FromCartesian(0, 0);
        var q = HPoint.FromCartesian(0, 3);

        // 正例
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddConic("C", circle);
        facts.AddPoint("P", p);
        facts.AddPoint("Q", q);
        facts.AddLine("LP", circle.PolarLine(p));
        facts.AddLine("LQ", circle.PolarLine(q));
        facts.AddFact(new PolarOfFact("LP", "P", "C"), "t");
        facts.AddFact(new PolarOfFact("LQ", "Q", "C"), "t");
        facts.AddFact(new OnLineFact("Q", "LP"), "t");
        new TheoremEngine().Register<PolePolarReciprocityRule>().RunToFixedPoint(facts);
        Assert.Contains(new OnLineFact("P", "LQ"), facts.Facts);

        // 反例：LP 伪造成 y=0（不是 P 的极线）
        var bad = new FactBase { CurrentLevel = TransformLevel.Projective };
        bad.AddConic("C", circle);
        bad.AddPoint("P", p);
        bad.AddPoint("Q", q);
        bad.AddLine("LBOGUS", new HLine(0, 1, 0));
        bad.AddLine("LQ", circle.PolarLine(q));
        bad.AddFact(new PolarOfFact("LBOGUS", "P", "C"), "bogus");
        bad.AddFact(new PolarOfFact("LQ", "Q", "C"), "t");
        bad.AddFact(new OnLineFact("Q", "LBOGUS"), "bogus");
        new TheoremEngine().Register<PolePolarReciprocityRule>().RunToFixedPoint(bad);
        Assert.DoesNotContain(new OnLineFact("P", "LQ"), bad.Facts);
    }

    // ---------- 命题 5：极线基本定理 ----------
    [Fact]
    public void PolarFundamental_PositiveAndNegative()
    {
        var circle = Scenes.Circle4;

        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        var onCurve = HPoint.FromCartesian(0, 2);
        facts.AddConic("C", circle);
        facts.AddPoint("P", onCurve);
        facts.AddLine("L", circle.PolarLine(onCurve));
        facts.AddFact(new OnConicFact("P", "C"), "t");
        facts.AddFact(new PolarOfFact("L", "P", "C"), "t");
        new TheoremEngine().Register<PolarFundamentalRule>().RunToFixedPoint(facts);
        Assert.Contains(new TangentFact("L", "C", "P"), facts.Facts);

        // 反例：点不在曲线上
        var off = new FactBase { CurrentLevel = TransformLevel.Projective };
        var outside = HPoint.FromCartesian(0, 3);
        off.AddConic("C", circle);
        off.AddPoint("P", outside);
        off.AddLine("L", circle.PolarLine(outside));
        off.AddFact(new PolarOfFact("L", "P", "C"), "t");
        new TheoremEngine().Register<PolarFundamentalRule>().RunToFixedPoint(off);
        Assert.DoesNotContain(new TangentFact("L", "C", "P"), off.Facts);
    }

    // ---------- 命题 6：共轭直径平分弦 ----------
    [Fact]
    public void ConjugateDiameterBisectsChord_PositiveAndForgedMidpoint()
    {
        var circle = Scenes.Circle4;
        var a = HPoint.FromFormula("1", "sqrt(3)");
        var b = HPoint.FromFormula("1", "-sqrt(3)");

        FactBase Build(HPoint m)
        {
            var f = new FactBase { CurrentLevel = TransformLevel.Affine };
            f.AddConic("C", circle);
            f.AddPoint("A", a);
            f.AddPoint("B", b);
            f.AddPoint("M", m);
            f.AddLine("AB", HLine.FromPoints(a, b));
            f.AddLine("OM", HLine.FromPoints(HPoint.FromCartesian(0, 0), m));
            f.AddFact(new OnConicFact("A", "C"), "t");
            f.AddFact(new OnConicFact("B", "C"), "t");
            f.AddFact(new OnLineFact("A", "AB"), "t");
            f.AddFact(new OnLineFact("B", "AB"), "t");
            f.AddFact(new OnLineFact("M", "AB"), "t");
            f.AddFact(new ConjugateDirectionsFact("OM", "AB", "C"), "t");
            return f;
        }

        var ok = Build(HPoint.FromCartesian(1, 0));
        new TheoremEngine().Register<ConjugateDiameterBisectsChordRule>().RunToFixedPoint(ok);
        Assert.Contains(new MidpointFact("M", "A", "B"), ok.Facts);

        var bad = Build(HPoint.FromFormula("1", "1/2"));
        new TheoremEngine().Register<ConjugateDiameterBisectsChordRule>().RunToFixedPoint(bad);
        Assert.DoesNotContain(new MidpointFact("M", "A", "B"), bad.Facts);
    }

    // ---------- 命题 7：同线三点共线 ----------
    [Fact]
    public void CollinearFromCommonLine_PositiveNegativeAndPermutationDedup()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        var a = HPoint.FromCartesian(0, 0);
        var b = HPoint.FromCartesian(1, 1);
        var c = HPoint.FromCartesian(2, 2);
        facts.AddPoint("A", a);
        facts.AddPoint("B", b);
        facts.AddPoint("C", c);
        facts.AddLine("L", HLine.FromPoints(a, b));
        facts.AddFact(new OnLineFact("A", "L"), "t");
        facts.AddFact(new OnLineFact("B", "L"), "t");
        facts.AddFact(new OnLineFact("C", "L"), "t");

        new TheoremEngine().Register<CollinearFromCommonLineRule>().RunToFixedPoint(facts);
        Assert.Contains(CollinearFact.Create("A", "B", "C"), facts.Facts);
        Assert.Contains("C", facts.ThirdPointsCollinearWith("A", "B"));

        // 排列去重
        Assert.False(facts.AddFact(new CollinearFact("C", "A", "B"), "perm"));

        // 反例：标签说 C 在 L 上，但坐标不共线
        var bad = new FactBase { CurrentLevel = TransformLevel.Projective };
        bad.AddPoint("A", a);
        bad.AddPoint("B", b);
        bad.AddPoint("C", HPoint.FromCartesian(2, 3));
        bad.AddLine("L", HLine.FromPoints(a, b));
        foreach (var id in new[] { "A", "B", "C" }) bad.AddFact(new OnLineFact(id, "L"), "bogus");
        new TheoremEngine().Register<CollinearFromCommonLineRule>().RunToFixedPoint(bad);
        Assert.Empty(bad.Collinears);
    }

    // ---------- 命题 8：同点三线共点 ----------
    [Fact]
    public void ConcurrentFromCommonPoint_PositiveNegativeAndDegenerateGuard()
    {
        var o = HPoint.FromCartesian(0, 0);

        FactBase Build(params HLine[] lines)
        {
            var f = new FactBase { CurrentLevel = TransformLevel.Projective };
            f.AddPoint("O", o);
            for (int i = 0; i < lines.Length; i++)
            {
                string id = "L" + (i + 1);
                f.AddLine(id, lines[i]);
                f.AddFact(new OnLineFact("O", id), "t");
            }
            return f;
        }

        var ok = Build(new HLine(0, 1, 0), new HLine(1, 0, 0), new HLine(1, -1, 0));
        new TheoremEngine().Register<ConcurrentFromCommonPointRule>().RunToFixedPoint(ok);
        Assert.Contains(ConcurrentFact.Create("L1", "L2", "L3"), ok.Concurrents);

        // 反例：第三条线实际不过 O
        var bad = Build(new HLine(0, 1, 0), new HLine(1, 0, 0), new HLine(1, -1, 1));
        new TheoremEngine().Register<ConcurrentFromCommonPointRule>().RunToFixedPoint(bad);
        Assert.Empty(bad.Concurrents);

        // 退化守卫：两条"线"其实是同一条
        var dup = Build(new HLine(0, 1, 0), new HLine(0, 2, 0), new HLine(1, 0, 0));
        new TheoremEngine().Register<ConcurrentFromCommonPointRule>().RunToFixedPoint(dup);
        Assert.Empty(dup.Concurrents);
    }
}
