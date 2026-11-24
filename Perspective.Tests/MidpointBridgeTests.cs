using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// P3a 中点桥：调和共轭于无穷远点 ⇒ 中点（含 A≠B 守卫与交叉验证）。
/// </summary>
public class MidpointBridgeTests
{
    private static ExactScalar I(int v) => ExactScalar.FromInt(v);

    private static FactBase CollinearScene()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Affine };
        facts.AddPoint("A", HPoint.FromCartesian(0, 0));
        facts.AddPoint("B", HPoint.FromCartesian(4, 0));
        facts.AddPoint("M", HPoint.FromCartesian(2, 0));
        facts.AddPoint("D", new HPoint(1, 0, 0));   // x 轴方向无穷远点
        return facts;
    }

    [Fact]
    public void HarmonicWithInfinityPoint_ProducesMidpoint()
    {
        var facts = CollinearScene();
        var a = facts.Points["A"];
        var b = facts.Points["B"];
        var m = facts.Points["M"];
        var d = facts.Points["D"];
        Assert.Equal(StrictResult.StrictlyTrue, GeometryAudit.IsHarmonic(a, b, m, d));

        facts.AddFact(new HarmonicFact("A", "B", "M", "D"), "测试");

        var derived = MidpointFromHarmonicInfinityRule.Apply(facts);

        Assert.Equal(1, derived);
        var mid = Assert.Single(facts.Midpoints);
        Assert.Equal("M", mid.MidId);
        Assert.Equal(StrictResult.StrictlyTrue,
            GeometryAudit.IsMidpoint(facts.Points[mid.MidId], facts.Points[mid.AId], facts.Points[mid.BId]));
    }

    [Fact]
    public void PermutedHarmonicOrder_StillDerivesMidpoint()
    {
        var facts = CollinearScene();
        // 调和交比在 Klein 四元置换下不变；规则应通过"穷举候选"识别
        facts.AddFact(new HarmonicFact("B", "A", "D", "M"), "测试");

        var derived = MidpointFromHarmonicInfinityRule.Apply(facts);

        Assert.Equal(1, derived);
        Assert.Single(facts.Midpoints);
    }

    [Fact]
    public void CoincidentEndpoints_DoNotProduceMidpoint()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Affine };
        facts.AddPoint("A", HPoint.FromCartesian(0, 0));
        facts.AddPoint("B", HPoint.FromCartesian(0, 0));   // 与 A 坐标重合
        facts.AddPoint("M", HPoint.FromCartesian(0, 0));
        facts.AddPoint("D", new HPoint(1, 0, 0));
        facts.AddFact(new HarmonicFact("A", "B", "M", "D"), "退化测试");

        var derived = MidpointFromHarmonicInfinityRule.Apply(facts);

        Assert.Equal(0, derived);
        Assert.Empty(facts.Midpoints);
    }

    [Fact]
    public void HarmonicWithoutInfinityPoint_DoesNotProduceMidpoint()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Affine };
        facts.AddPoint("A", HPoint.FromCartesian(0, 0));
        facts.AddPoint("B", HPoint.FromCartesian(4, 0));
        facts.AddPoint("M", HPoint.FromCartesian(2, 0));
        facts.AddPoint("P", HPoint.FromCartesian(8, 0));   // 有限点，非无穷远
        facts.AddFact(new HarmonicFact("A", "B", "M", "P"), "测试");

        var derived = MidpointFromHarmonicInfinityRule.Apply(facts);

        Assert.Equal(0, derived);   // 无穷远点是中点桥的必要前提
        Assert.Empty(facts.Midpoints);
    }
}
