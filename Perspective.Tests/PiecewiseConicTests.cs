using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// M2：分段曲线（PiecewiseConic）——2020 上海 20 中 Γ = Γ₁/Γ₂ 按 x 条件取并集的地基。
/// </summary>
public class PiecewiseConicTests
{
    private static PiecewiseConic UpperHalfCircle() =>
        new PiecewiseConic()
            .Add(Conic.Circle(0, 0, ExactScalar.FromInt(1)),
                PieceCondition.Compare(RelationKind.GreaterOrEqual,
                    ExactScalar.FromSymbol(PiecewiseConic.YSymbol), ExactScalar.FromInt(0)));

    [Fact]
    public void Contains_RespectsPieceCondition()
    {
        var curve = UpperHalfCircle();

        Assert.True(curve.Contains(HPoint.FromCartesian(0, 1)));       // 上半圆
        Assert.False(curve.Contains(HPoint.FromCartesian(0, -1)));     // 下半圆不在
        Assert.False(curve.Contains(HPoint.FromCartesian(2, 0)));      // 不在圆上
    }

    [Fact]
    public void IntersectLine_FiltersByCondition_AndDeduplicates()
    {
        var curve = UpperHalfCircle();

        // 直线 y=1/2 与单位圆交于 x=±√3/2，两条都 y>0 → 都在上半圆
        var both = curve.IntersectLine(new HLine(0, 2, -1));
        Assert.Equal(2, both.Count);

        // 直线 y=0 交于 (±1,0)：y≥0 边界，两个点都保留（y=0 满足 ≥0）
        var onAxis = curve.IntersectLine(new HLine(0, 1, 0));
        Assert.Equal(2, onAxis.Count);

        // 直线 y=−1/2：交点 y<0，全部被过滤
        var below = curve.IntersectLine(new HLine(0, 2, 1));
        Assert.Empty(below);
    }

    /// <summary>
    /// 2020 上海 20(2) 的几何内核：b=√5 时 Γ₁: x²/4−y²/5=1 与 Γ₂: x²+y²=9，
    /// 取 x&gt;x_A（x_A=2√14/3）的部分；P 在第一象限且 |PF|=8（F=(2,0)）。
    /// 这里只验证"P 落在 Γ₁ 段"这一几何事实（原卷问法待确认）。
    /// </summary>
    [Fact]
    public void Shanghai2020_20_2_PointP_LiesOnHyperbolaPiece()
    {
        var bSquared = ExactScalar.FromInt(5);
        var xA = ExactScalar.FromFormula("2*sqrt(14)/3");

        var gamma1 = Conic.FromGeneralEquation(
            ExactScalar.FromInt(1), ExactScalar.FromInt(0),
            -ExactScalar.FromFormula("4/5"), ExactScalar.FromInt(0), ExactScalar.FromInt(0),
            -ExactScalar.FromInt(4));   // x²/4 − y²/5 = 1  ⇔ x² − (4/5)y² − 4 = 0

        var gamma2 = Conic.Circle(0, 0, ExactScalar.FromFormula("sqrt(9)"));   // x²+y²=9

        var curve = new PiecewiseConic()
            .Add(gamma1, PieceCondition.Compare(RelationKind.GreaterThan,
                ExactScalar.FromSymbol(PiecewiseConic.XSymbol), xA))
            .Add(gamma2, PieceCondition.Compare(RelationKind.GreaterThan,
                ExactScalar.FromSymbol(PiecewiseConic.XSymbol), xA));

        // 由 |PF|=8 与 Γ₁ 联立消去 y²：x = (8+2√601)/9（取正根）
        var x = ExactScalar.FromFormula("(8 + 2*sqrt(601)) / 9");
        var ySquared = ExactScalar.FromFormula("(5/4)") * x * x - ExactScalar.FromInt(5);
        var y = ExactScalar.Sqrt(ySquared);

        var p = HPoint.FromCartesian(x, y);

        Assert.True(curve.Contains(p));
        Assert.Equal(StrictResult.StrictlyTrue, gamma1.ContainsStrictly(p));
        Assert.Equal(StrictResult.StrictlyFalse, gamma2.ContainsStrictly(p));
        Assert.True(y.Approx > 0);   // 第一象限
    }
}
