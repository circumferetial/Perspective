using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// P1-4（第二批）：有向角相等（mod π）事实与传递规则。
/// 角度是相似不变量；这里用 tan 判据（cross/dot）避免开方，直角分支也正确。
/// </summary>
public class AngleEqualTests
{
    private static FactBase AngleScene()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Similarity };
        facts.AddLine("L0", new HLine(0, 1, 0));      // y = 0
        facts.AddLine("L45", new HLine(1, -1, 0));    // y = x
        facts.AddLine("L90", new HLine(1, 0, 0));     // x = 0
        facts.AddLine("L135", new HLine(1, 1, 0));    // y = -x
        facts.AddLine("L0b", new HLine(0, 1, -1));    // y = 1（平行于 L0）
        return facts;
    }

    [Fact]
    public void AngleEqualFact_IsCanonicalized()
    {
        // 交换线对内顺序 / 交换两对，都应归一到同一事实
        var a = AngleEqualFact.Create("L0", "L45", "L45", "L90");
        var b = AngleEqualFact.Create("L45", "L0", "L90", "L45");
        var c = AngleEqualFact.Create("L45", "L90", "L0", "L45");

        Assert.Equal(a, b);
        Assert.Equal(a, c);
    }

    [Fact]
    public void AngleEquality_StrictCheck()
    {
        var facts = AngleScene();
        var f = AngleEqualFact.Create("L0", "L45", "L45", "L90");

        var ok = GeometryAudit.AreAngleEqual(
            facts.Lines[f.A1], facts.Lines[f.A2], facts.Lines[f.B1], facts.Lines[f.B2], f.Sign);
        Assert.Equal(StrictResult.StrictlyTrue, ok);

        // 45° 与 90° 不相等
        var bad = AngleEqualFact.Create("L0", "L45", "L0", "L90");
        var badCheck = GeometryAudit.AreAngleEqual(
            facts.Lines[bad.A1], facts.Lines[bad.A2], facts.Lines[bad.B1], facts.Lines[bad.B2], bad.Sign);
        Assert.NotEqual(StrictResult.StrictlyTrue, badCheck);
    }

    [Fact]
    public void ParallelLines_GiveEqualAngles()
    {
        var facts = AngleScene();
        facts.AddFact(ParallelFact.Create("L0", "L0b"), "初始");

        new TheoremEngine().Register<AngleFromParallelRule>().RunToFixedPoint(facts);

        Assert.Contains(facts.AngleEquals,
            f => f == AngleEqualFact.Create("L0", "L45", "L0b", "L45"));
    }

    [Fact]
    public void AngleTransitivity_ChainsThreeAngles()
    {
        var facts = AngleScene();
        facts.AddFact(AngleEqualFact.Create("L0", "L45", "L45", "L90"), "初始");
        facts.AddFact(AngleEqualFact.Create("L45", "L90", "L90", "L135"), "初始");

        new TheoremEngine().Register<AngleTransitivityRule>().RunToFixedPoint(facts);

        Assert.Contains(facts.AngleEquals,
            f => f == AngleEqualFact.Create("L0", "L45", "L90", "L135"));
    }

    [Fact]
    public void WrongAngleFact_IsNotDerived()
    {
        var facts = AngleScene();
        // 伪造：45° 等于 90°（不成立）
        facts.AddFact(AngleEqualFact.Create("L0", "L45", "L0", "L90"), "伪造");

        new TheoremEngine().Register<AngleTransitivityRule>().RunToFixedPoint(facts);

        // 不应推出 θ(L0,L45) = θ(L45,L90) 之外的任何新东西
        Assert.DoesNotContain(facts.AngleEquals,
            f => f == AngleEqualFact.Create("L0", "L45", "L90", "L135"));
    }
}
