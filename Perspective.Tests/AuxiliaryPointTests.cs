using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// 辅助点：固定点 / 线上点 / 一般点 / 曲线上的点，以及非退化假设（NDG）。
/// </summary>
public class AuxiliaryPointTests
{
    private static HPoint PoleOf(Conic conic, HLine line)
    {
        var pole = conic.Matrix.Adjugate() * line;
        return new HPoint(pole.A, pole.B, pole.C);
    }

    [Fact]
    public void PointOnLine_StrictlySatisfiesLineEquation()
    {
        var line = new HLine(1, -1, -1);          // y = x - 1
        var p = AuxiliaryPoints.OnLine(line, "P");

        Assert.Equal(StrictResult.StrictlyTrue, line.ContainsStrictly(p));
    }

    /// <summary>
    /// 经典推导：在直线 L 上任取一点 P，则 L 的极点 Q 恒在 P 的极线上。
    /// 这是 La Hire 互反在"P ∈ L"下的特例，结论对参数 t 恒成立。
    /// </summary>
    [Fact]
    public void PoleOfLine_LiesOnPolarOfArbitraryPointOnLine()
    {
        var ellipse = Conic.Ellipse(2, ExactScalar.FromFormula("sqrt(3)"));
        var line = new HLine(1, -1, -1);

        var p = AuxiliaryPoints.OnLine(line, "P");
        var pole = PoleOf(ellipse, line);

        Assert.Equal(StrictResult.StrictlyTrue, ellipse.PolarLine(p).ContainsStrictly(pole));
    }

    [Fact]
    public void PointOnConic_StrictlySatisfiesConicEquation()
    {
        var circle = Scenes.Circle4;
        var known = HPoint.FromCartesian(0, 2);
        var p = AuxiliaryPoints.OnConic(circle, known, "P");

        Assert.Equal(StrictResult.StrictlyTrue, circle.ContainsStrictly(p));
    }

    [Fact]
    public void GenericPoint_IsNotForcedOntoAnyLine_AndAssumptionIsAdmissible()
    {
        var p = AuxiliaryPoints.Generic("P");
        var line = new HLine(1, -1, 0);           // y = x

        // 一般点不会被"证明"在直线上（恒等式不成立）
        Assert.NotEqual(StrictResult.StrictlyTrue, line.ContainsStrictly(p));

        // "P 不在 y=x 上" 是合法的非退化假设（谓词不恒为零）
        var facts = new FactBase();
        Assert.True(facts.AssumePointNotOnLine(p, line, "P 不在 y=x 上"));
        Assert.Single(facts.Assumptions);
    }

    [Fact]
    public void Assumption_ThatIsIdenticallyFalse_IsRejected()
    {
        var facts = new FactBase();
        // 恒为零的表达式作为"非零假设"必然矛盾（构形退化）
        Assert.False(facts.AssumeNonZero(ExactScalar.FromInt(0), "退化表达式"));
        Assert.Empty(facts.Assumptions);
    }
}
