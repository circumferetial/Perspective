using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>解释层：计算结构不含自然语言，文本由 ProofNarrator 统一渲染。</summary>
public class ExplanationTests
{
    // ---------- La Hire：结构化记录 ----------
    [Fact]
    public void LaHire_ProducesStructuredDerivationWithoutProse()
    {
        var circle = Conic.FromGeneralEquation(1, 0, 1, -4, 0, 0);
        var p = HPoint.FromCartesian(0, 0);
        var q = HPoint.FromCartesian(0, 3);

        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddConic("C", circle);
        facts.AddPoint("P", p);
        facts.AddPoint("Q", q);
        facts.AddLine("LP", circle.PolarLine(p));
        facts.AddLine("LQ", circle.PolarLine(q));
        facts.AddFact(new PolarOfFact("LP", "P", "C"), "初始");
        facts.AddFact(new PolarOfFact("LQ", "Q", "C"), "初始");
        facts.AddFact(new OnLineFact("Q", "LP"), "初始");

        new TheoremEngine().Register<PolePolarReciprocityRule>().RunToFixedPoint(facts);

        var conclusion = new OnLineFact("P", "LQ");
        var d = facts.DerivationLog.Single(x => x.Fact == conclusion);

        Assert.Equal(RuleIds.PolePolarReciprocity, d.RuleId);
        Assert.Null(d.Note);                                   // 规则不再拼自然语言
        Assert.Equal(2, d.Premises.OfType<PolarOfFact>().Count());
        Assert.Contains(d.Premises, x => x is OnLineFact ol && ol.PointId == "Q");

        string text = ProofNarrator.Render(facts, d);
        Assert.Contains("La Hire", text);
    }

    // ---------- 同线三点共线 ----------
    [Fact]
    public void Collinear_RendersCommonLineExplanation()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        var a = HPoint.FromCartesian(0, 0);
        var b = HPoint.FromCartesian(1, 1);
        var c = HPoint.FromCartesian(2, 2);
        facts.AddPoint("A", a);
        facts.AddPoint("B", b);
        facts.AddPoint("C", c);
        facts.AddLine("L", HLine.FromPoints(a, b));
        foreach (var id in new[] { "A", "B", "C" }) facts.AddFact(new OnLineFact(id, "L"), "初始");

        new TheoremEngine().Register<CollinearFromCommonLineRule>().RunToFixedPoint(facts);

        var d = facts.DerivationLog.Single(x => x.RuleId == RuleIds.CollinearFromCommonLine);
        string text = ProofNarrator.Render(facts, d);
        Assert.Contains("三点共线", text);
        Assert.Contains("L", text);
    }

    // ---------- 切点弦极线 ----------
    [Fact]
    public void ChordOfContact_RendersTangentExplanation()
    {
        var facts = Scenes.DemoScene();
        new TheoremEngine().Register<ChordOfContactIsPolarRule>().RunToFixedPoint(facts);

        var d = facts.DerivationLog.FirstOrDefault(x => x.RuleId == RuleIds.ChordOfContactIsPolar);
        Assert.NotNull(d);
        Assert.Null(d!.Note);
        Assert.True(d.Premises.OfType<TangentFact>().Count() >= 2);

        string text = ProofNarrator.Render(facts, d);
        Assert.Contains("切点弦", text);
        Assert.Contains("极线", text);
    }

    // ---------- Pascal ----------
    [Fact]
    public void Pascal_RendersSixPointHexagon()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddConic("C", Scenes.UnitCircle);
        foreach (var (id, p) in Scenes.SixConicPoints)
        {
            facts.AddPoint(id, p);
            facts.AddFact(new OnConicFact(id, "C"), "初始");
        }

        new TheoremEngine().Register<PascalRule>().RunToFixedPoint(facts);

        var d = facts.DerivationLog.Single(x => x.RuleId == RuleIds.Pascal);
        Assert.Equal(6, d.Premises.OfType<OnConicFact>().Count());

        string text = ProofNarrator.Render(facts, d);
        Assert.Contains("Pascal", text);
        Assert.Contains("内接", text);
    }

    // ---------- 极线基本定理（第二步迁移） ----------
    [Fact]
    public void PolarFundamental_NowUsesStructuredDerivation()
    {
        var circle = Scenes.Circle4;
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        var p = HPoint.FromCartesian(0, 2);
        facts.AddConic("C", circle);
        facts.AddPoint("P", p);
        facts.AddLine("L", circle.PolarLine(p));
        facts.AddFact(new OnConicFact("P", "C"), "初始");
        facts.AddFact(new PolarOfFact("L", "P", "C"), "初始");

        new TheoremEngine().Register<PolarFundamentalRule>().RunToFixedPoint(facts);

        var d = facts.DerivationLog.Single(x => x.Fact is TangentFact);
        Assert.Equal(RuleIds.PolarFundamental, d.RuleId);
        Assert.Null(d.Note);
        Assert.Contains("切线", ProofNarrator.Render(facts, d));
    }

    // ---------- 点差法：两种结论共用同一规则 id ----------
    [Fact]
    public void MidpointSlope_RendersConjugateAndSlopeProduct()
    {
        var ellipse = Scenes.Ellipse4x3;
        var a = HPoint.FromFormula("2", "0");
        var b = HPoint.FromFormula("0", "sqrt(3)");
        var m = HPoint.FromFormula("1", "sqrt(3)/2");

        var facts = new FactBase { CurrentLevel = TransformLevel.Affine };
        facts.AddConic("E", ellipse);
        facts.AddPoint("O", HPoint.FromCartesian(0, 0));
        facts.AddPoint("A", a);
        facts.AddPoint("B", b);
        facts.AddPoint("M", m);
        facts.AddFact(new CenterOfConicFact("O", "E"), "初始");
        facts.AddFact(new OnConicFact("A", "E"), "初始");
        facts.AddFact(new OnConicFact("B", "E"), "初始");
        facts.AddFact(new MidpointFact("M", "A", "B"), "初始");

        // 共轭方向由 P3 规则产出（弦方向点的极线），旧 #4 只保留轴对齐斜率积
        facts.AddLine("AB", HLine.FromPoints(a, b));
        facts.AddFact(new OnLineFact("A", "AB"), "初始");
        facts.AddFact(new OnLineFact("B", "AB"), "初始");

        new TheoremEngine()
            .Register<ConicMidpointSlopeRule>()
            .Register<ConicChordPolarConjugateRule>()
            .RunToFixedPoint(facts);

        var conj = facts.DerivationLog.Single(x =>
            x.RuleId == RuleIds.ConicChordPolarConjugate && x.Fact is ConjugateDirectionsFact);
        Assert.Null(conj.Note);
        Assert.Contains("共轭", ProofNarrator.Render(facts, conj));

        var slope = facts.DerivationLog.Single(x => x.RuleId == RuleIds.ConicMidpointSlope && x.Fact is ConstantSlopeProductFact);
        Assert.Null(slope.Note);
        Assert.Contains("斜率", ProofNarrator.Render(facts, slope));
    }

    // ---------- 三线共点 ----------
    [Fact]
    public void Concurrent_RendersCommonPoint()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddPoint("O", HPoint.FromCartesian(0, 0));
        facts.AddLine("L1", new HLine(0, 1, 0));
        facts.AddLine("L2", new HLine(1, 0, 0));
        facts.AddLine("L3", new HLine(1, -1, 0));
        foreach (var id in new[] { "L1", "L2", "L3" }) facts.AddFact(new OnLineFact("O", id), "初始");

        new TheoremEngine().Register<ConcurrentFromCommonPointRule>().RunToFixedPoint(facts);

        var d = facts.DerivationLog.Single(x => x.RuleId == RuleIds.ConcurrentFromCommonPoint);
        Assert.Null(d.Note);
        Assert.Contains("共点", ProofNarrator.Render(facts, d));
    }

    // ---------- 完全四点形 ----------
    [Fact]
    public void Quadrangle_RendersHarmonicProperty()
    {
        var facts = Scenes.QuadrangleScene();
        new TheoremEngine().Register<CompleteQuadrangleHarmonicRule>().RunToFixedPoint(facts);

        var d = facts.DerivationLog.First(x => x.RuleId == RuleIds.CompleteQuadrangleHarmonic);
        Assert.Null(d.Note);
        Assert.Contains("调和", ProofNarrator.Render(facts, d));
    }

    // ---------- Brianchon ----------
    [Fact]
    public void Brianchon_RendersExscribedHexagon()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddConic("C", Scenes.UnitCircle);
        int i = 1;
        foreach (var (touchId, touch, line) in Scenes.SixTangents)
        {
            facts.AddPoint(touchId, touch);
            facts.AddLine("T" + i, line);
            facts.AddFact(new TangentFact("T" + i, "C", touchId), "初始");
            i++;
        }

        new TheoremEngine().Register<BrianchonRule>().RunToFixedPoint(facts);

        var d = facts.DerivationLog.Single(x => x.RuleId == RuleIds.Brianchon);
        Assert.Null(d.Note);
        Assert.Equal(6, d.Premises.OfType<TangentFact>().Count());
        Assert.Contains("Brianchon", ProofNarrator.Render(facts, d));
    }

    // ---------- 初始事实的出处 ----------
    [Fact]
    public void RenderFact_InitialFact_ReturnsInitialNote()
    {
        var facts = new FactBase();
        var fact = new OnConicFact("A", "C");
        facts.AddPoint("A", HPoint.FromCartesian(0, 0));
        facts.AddConic("C", Scenes.UnitCircle);
        facts.AddFact(fact, "初始条件");

        Assert.Equal("初始条件", ProofNarrator.Render(facts, facts.DerivationLog.Single()));
        Assert.Equal("来自初始前提事实", ProofNarrator.RenderFact(facts, new OnConicFact("X", "Y")));
    }
}
