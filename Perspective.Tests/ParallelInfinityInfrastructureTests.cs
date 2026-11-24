using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// P2 基础设施：l∞ 惰性登记、方向点严格去重、用"共点于 l∞ ⇒ 平行"替代 #12 的传递链。
/// </summary>
public class ParallelInfinityInfrastructureTests
{
    private static TheoremEngine Engine() =>
        new TheoremEngine()
            .Register<ParallelImpliesSharedInfinityPointRule>()
            .Register<ConcurrentFromCommonPointRule>()
            .Register<ConcurrentAtInfinityImpliesParallelRule>();

    private static FactBase ThreeParallelLines(TransformLevel level)
    {
        var facts = new FactBase { CurrentLevel = level };
        facts.AddLine("L1", new HLine(0, 1, 0));   // y = 0
        facts.AddLine("L2", new HLine(0, 1, -1));  // y = 1
        facts.AddLine("L3", new HLine(0, 1, -2));  // y = 2
        facts.AddFact(ParallelFact.Create("L1", "L2"), "初始");
        facts.AddFact(ParallelFact.Create("L2", "L3"), "初始");
        return facts;
    }

    [Fact]
    public void Transitivity_IsRecoveredThroughSharedInfinityPoint()
    {
        var facts = ThreeParallelLines(TransformLevel.Affine);

        Engine().RunToFixedPoint(facts);

        // 第三条平行关系由 l∞ 方向点 + #8 + 桥接推出（不调用 #12）
        Assert.Contains(facts.Parallels, p =>
            (p.Line1Id == "L1" && p.Line2Id == "L3") ||
            (p.Line1Id == "L3" && p.Line2Id == "L1"));

        // 惰性登记了 l∞ 与唯一方向点
        Assert.True(facts.Lines.ContainsKey("L_inf"));
        Assert.Single(facts.Points.Values, p => p.IsAtInfinity);
    }

    [Fact]
    public void ProjectiveView_DoesNotRegisterInfinityStructure()
    {
        var facts = ThreeParallelLines(TransformLevel.Projective);

        Engine().RunToFixedPoint(facts);

        // 射影视角下 Affine 级规则不激活：不应凭空登记 l∞ / 方向点
        Assert.False(facts.Lines.ContainsKey("L_inf"));
        Assert.DoesNotContain(facts.Points.Values, p => p.IsAtInfinity);
        Assert.DoesNotContain(facts.Parallels, p => p.Line1Id == "L1" && p.Line2Id == "L3");
    }

    [Fact]
    public void PointAtInfinity_DedupesByStrictProportion()
    {
        var facts = new FactBase();

        var same1 = facts.GetOrCreatePointAtInfinity(new HPoint(2, 0, 0));
        var same2 = facts.GetOrCreatePointAtInfinity(new HPoint(4, 0, 0));   // 同方向、不同缩放
        var different = facts.GetOrCreatePointAtInfinity(new HPoint(0, 1, 0));

        Assert.Equal(same1, same2);
        Assert.NotEqual(same1, different);
        Assert.Equal(2, facts.Points.Count);
    }

    [Fact]
    public void NonParallelParallelFact_DoesNotCreateInfinityPoint()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Affine };
        facts.AddLine("L1", new HLine(1, 0, 0));   // x = 0
        facts.AddLine("L2", new HLine(0, 1, 0));   // y = 0
        facts.AddFact(ParallelFact.Create("L1", "L2"), "伪造的平行事实");

        ParallelImpliesSharedInfinityPointRule.Apply(facts);

        Assert.False(facts.Lines.ContainsKey("L_inf"));
        Assert.DoesNotContain(facts.Points.Values, p => p.IsAtInfinity);
    }
}
