using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>目标导向推演（类型级反向链）与 BFS 的对比。</summary>
public class BackwardChainingTests
{
    private static TheoremEngine FullEngine() => new TheoremEngine()
        .Register<ChordOfContactIsPolarRule>()
        .Register<PolePolarReciprocityRule>()
        .Register<HarmonicPolarBundleRule>()
        .Register<ConicMidpointSlopeRule>()
        .Register<PolarFundamentalRule>()
        .Register<ConjugateDiameterBisectsChordRule>()
        .Register<CollinearFromCommonLineRule>()
        .Register<ConcurrentFromCommonPointRule>()
        .Register<PascalRule>()
        .Register<CompleteQuadrangleHarmonicRule>()
        .Register<BrianchonRule>();

    [Fact]
    public void Prove_OnLine_ProvesWithFewerRulesAndFewerFactsThanBfs()
    {
        var engine = FullEngine();
        var claim = new OnLineFact("P", "L_AB");

        // BFS
        var bfs = Scenes.DemoScene();
        engine.RunToFixedPoint(bfs);
        Assert.Contains(claim, bfs.Facts);

        // 目标导向
        var bwd = Scenes.DemoScene();
        var attempt = engine.Prove(bwd, typeof(OnLineFact),
            f => f.Facts.Contains(claim) ? "命中 P∈L_AB" : null);

        Assert.True(attempt.Proved);
        Assert.True(attempt.RulesActivated < engine.RuleCount);
        Assert.True(attempt.FactsAfter < bfs.Facts.Count,
            $"目标导向应产生更少事实：{attempt.FactsAfter} vs BFS {bfs.Facts.Count}");
    }

    [Fact]
    public void Prove_Collinear_ProvesViaCommonLineRule()
    {
        var engine = FullEngine();
        var facts = Scenes.DemoScene();
        var attempt = engine.Prove(facts, typeof(CollinearFact),
            f => f.Facts.Contains(CollinearFact.Create("Q", "R", "M")) ? "命中 Q,R,M 共线" : null);

        Assert.True(attempt.Proved);
    }

    [Fact]
    public void Prove_FalseClaim_IsNotProved()
    {
        var engine = FullEngine();
        var facts = Scenes.DemoScene();
        var attempt = engine.Prove(facts, typeof(OnLineFact),
            f => f.Facts.Contains(new OnLineFact("P", "L_sec")) ? "错误命中" : null);

        Assert.False(attempt.Proved);
    }
}
