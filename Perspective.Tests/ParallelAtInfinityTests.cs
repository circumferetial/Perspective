using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// P2：平行传递的"基础设施版"——三线共点于无穷远线 ⇒ 两两平行。
/// 用 l∞ 上的方向点统一表达平行，不依赖传递链。
/// </summary>
public class ParallelAtInfinityTests
{
    private static ExactScalar I(int v) => ExactScalar.FromInt(v);

    private static TheoremEngine Engine() =>
        new TheoremEngine()
            .Register<ConcurrentFromCommonPointRule>()
            .Register<ConcurrentAtInfinityImpliesParallelRule>();

    [Fact]
    public void ThreeParallelLines_SharingInfinityPoint_BecomePairwiseParallel()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Affine };
        HLine[] lines =
        [
            new HLine(0, 1, 0),   // y = 0
            new HLine(0, 1, -1),  // y = 1
            new HLine(0, 1, -2)   // y = 2
        ];
        for (var i = 0; i < lines.Length; i++)
            facts.AddLine($"L{i + 1}", lines[i]);

        var d = new HPoint(1, 0, 0);        // x 轴方向的无穷远点
        facts.AddPoint("D", d);
        for (var i = 0; i < lines.Length; i++)
            facts.AddFact(new OnLineFact("D", $"L{i + 1}"), "方向点");

        Engine().RunToFixedPoint(facts);

        Assert.Equal(3, facts.Parallels.Count);
        Assert.All(facts.Parallels, p =>
            Assert.Equal(StrictResult.StrictlyTrue,
                GeometryAudit.AreParallel(facts.Lines[p.Line1Id], facts.Lines[p.Line2Id])));
    }

    [Fact]
    public void ThreeLinesThroughFinitePoint_DoNotProduceParallels()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Affine };
        facts.AddLine("L1", new HLine(1, 0, 0));    // x = 0
        facts.AddLine("L2", new HLine(0, 1, 0));    // y = 0
        facts.AddLine("L3", new HLine(1, 1, 0));    // x + y = 0
        facts.AddPoint("O", HPoint.FromCartesian(0, 0));
        foreach (var line in new[] { "L1", "L2", "L3" })
            facts.AddFact(new OnLineFact("O", line), "共点于原点");

        Engine().RunToFixedPoint(facts);

        Assert.NotEmpty(facts.Concurrents);
        Assert.Empty(facts.Parallels);
    }
}
