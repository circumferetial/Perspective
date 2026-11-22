using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// 组合型规则的候选池与截断诊断：砍候选可以，但**不能静默漏解**。
/// </summary>
public class RuleLimitTests
{
    [Fact]
    public void DerivedPoint_IsNotStructural_ButCanBePromoted()
    {
        var facts = new FactBase();
        facts.AddPoint("AUX:X", HPoint.FromCartesian(0, 0), structural: false);
        facts.AddPoint("U", HPoint.FromCartesian(1, 0));

        Assert.False(facts.IsStructural("AUX:X"));
        Assert.True(facts.IsStructural("U"));
        Assert.Equal(["U"], facts.StructuralPointIds);

        // 用户明确希望派生点参与组合时可以提升
        facts.PromoteToStructural("AUX:X");
        Assert.True(facts.IsStructural("AUX:X"));
    }

    [Fact]
    public void Quadrangle_Truncation_IsReportedNotSilent()
    {
        var facts = Scenes.DemoScene();          // 9 个结构化点 > MaxPool(6)
        new TheoremEngine().Register<CompleteQuadrangleHarmonicRule>().RunToFixedPoint(facts);

        Assert.Contains(facts.Diagnostics, d => d.Contains("候选点") && d.Contains("可能漏解"));
    }

    [Fact]
    public void Quadrangle_SmallScene_HasNoTruncationDiagnostic()
    {
        var facts = Scenes.QuadrangleScene();    // 恰好 4 个点
        new TheoremEngine().Register<CompleteQuadrangleHarmonicRule>().RunToFixedPoint(facts);

        Assert.DoesNotContain(facts.Diagnostics, d => d.Contains("可能漏解"));
    }
}
