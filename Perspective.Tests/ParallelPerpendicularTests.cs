using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// P1-4（第一批）：平行/垂直事实与传递规则。
/// 这两类事实此前是 ProofJudge 注释里明确标记的缺口。
/// </summary>
public class ParallelPerpendicularTests
{
    private static FactBase LineScene()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Isometry };
        facts.AddLine("L1", new HLine(0, 1, 0));     // y = 0
        facts.AddLine("L2", new HLine(0, 1, -1));    // y = 1
        facts.AddLine("L3", new HLine(0, 1, -2));    // y = 2
        facts.AddLine("V", new HLine(1, 0, 0));      // x = 0
        return facts;
    }

    [Fact]
    public void ParallelFact_IsCanonicalizedToSortedPair()
    {
        var facts = LineScene();
        facts.AddFact(new ParallelFact("L1", "L2"), "初始");
        facts.AddFact(new ParallelFact("L2", "L1"), "重复（逆序）");

        Assert.Single(facts.Parallels);
        Assert.Equal("L1", facts.Parallels[0].Line1Id);
        Assert.Equal("L2", facts.Parallels[0].Line2Id);
        Assert.Contains("L2", facts.LinesParallelTo("L1"));
        Assert.Contains("L1", facts.LinesParallelTo("L2"));
    }

    [Fact]
    public void PerpendicularFact_IsCanonicalizedToSortedPair()
    {
        var facts = LineScene();
        facts.AddFact(new PerpendicularFact("V", "L1"), "初始");
        facts.AddFact(new PerpendicularFact("L1", "V"), "重复（逆序）");

        Assert.Single(facts.Perpendiculars);
        Assert.Contains("L1", facts.LinesPerpendicularTo("V"));
    }

    [Fact]
    public void ParallelTransitivity_DerivesThirdParallel_ThroughInfinityPoint()
    {
        var facts = LineScene();
        facts.AddFact(ParallelFact.Create("L1", "L2"), "初始");
        facts.AddFact(ParallelFact.Create("L2", "L3"), "初始");

        new TheoremEngine()
            .Register<ParallelImpliesSharedInfinityPointRule>()
            .Register<ConcurrentFromCommonPointRule>()
            .Register<ConcurrentAtInfinityImpliesParallelRule>()
            .RunToFixedPoint(facts);

        Assert.Contains(facts.Parallels, f => f == ParallelFact.Create("L1", "L3"));
        // 传递结论来自 l∞ 方向点 + 共点桥，而不是旧的传递规则
        Assert.Contains(facts.DerivationLog, d => d.RuleId == RuleIds.ConcurrentAtInfinityImpliesParallel);
        Assert.DoesNotContain(facts.DerivationLog, d => d.RuleId == RuleIds.ParallelTransitivity);
    }

    [Fact]
    public void MixedParallelAndCrossingLines_DoNotCreateFalseParallels()
    {
        var facts = LineScene();
        facts.AddFact(ParallelFact.Create("L1", "L2"), "初始");

        new TheoremEngine()
            .Register<ParallelImpliesSharedInfinityPointRule>()
            .Register<ConcurrentFromCommonPointRule>()
            .Register<ConcurrentAtInfinityImpliesParallelRule>()
            .RunToFixedPoint(facts);

        // V 与 L1/L2 相交于有限点；不应被并进"方向点共点"
        Assert.Single(facts.Parallels);
        Assert.DoesNotContain(facts.Parallels, p =>
            p == ParallelFact.Create("V", "L1") || p == ParallelFact.Create("V", "L2"));
    }

    [Fact]
    public void PerpendicularParallel_TransfersPerpendicularity()
    {
        var facts = LineScene();
        facts.AddFact(PerpendicularFact.Create("V", "L1"), "初始");
        facts.AddFact(ParallelFact.Create("L1", "L2"), "初始");

        new TheoremEngine().Register<PerpendicularParallelRule>().RunToFixedPoint(facts);

        // V ⟂ L1 且 L1 ∥ L2 ⇒ V ⟂ L2
        Assert.Contains(facts.Perpendiculars, f => f == PerpendicularFact.Create("V", "L2"));
    }

    [Fact]
    public void ProofJudge_ResolvesParallelClaimFromFactBase()
    {
        var facts = LineScene();
        facts.AddFact(ParallelFact.Create("L1", "L2"), "已知平行");
        var engine = new TheoremEngine();

        var report = ProofJudge.Evaluate(facts, engine, new ClaimParallel("L2", "L1"));

        Assert.Equal(ClaimVerdict.ProvedByTheoremChain, report.Verdict);
    }

    [Fact]
    public void ProofJudge_ResolvesPerpendicularClaimFromFactBase()
    {
        var facts = LineScene();
        facts.AddFact(PerpendicularFact.Create("V", "L1"), "已知垂直");
        var engine = new TheoremEngine();

        var report = ProofJudge.Evaluate(facts, engine, new ClaimPerpendicular("L1", "V"));

        Assert.Equal(ClaimVerdict.ProvedByTheoremChain, report.Verdict);
    }

    [Fact]
    public void ExternalParallelConstruction_NowRegistersFact()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Affine };
        facts.AddLine("L1", new HLine(0, 1, 0));

        var result = ConstructionEngine.ProposeLine(facts, "L2", new HLine(0, 1, -3),
            [new ClaimLineParallel("L2", "L1")], "user");

        Assert.True(result.Accepted);
        Assert.Contains(facts.Parallels, f => f == ParallelFact.Create("L2", "L1"));
    }
}
