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

    // ---------- 未迁移规则：回退到 Note ----------
    [Fact]
    public void UnmigratedRule_FallsBackToNote()
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
        Assert.Equal(RuleIds.AdHoc, d.RuleId);   // 尚未迁移：仍带 Note
        Assert.False(string.IsNullOrEmpty(d.Note));
        Assert.Equal(d.Note, ProofNarrator.Render(facts, d));
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
