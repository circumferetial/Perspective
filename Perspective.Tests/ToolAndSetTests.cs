using AngouriMath;
using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Tests;

/// <summary>点/线/圆锥曲线工具与集合代数。</summary>
public class ToolAndSetTests
{
    [Fact]
    public void PointTools_SameAndCollinear()
    {
        var a = HPoint.FromCartesian(1, 2);
        var b = HPoint.FromCartesian(2, 4);      // 与 a 不成比例，但共线于原点
        var o = HPoint.FromCartesian(0, 0);

        Assert.Equal(StrictResult.StrictlyTrue, PointTools.SameStrictly(a, a));
        Assert.Equal(StrictResult.StrictlyFalse, PointTools.SameStrictly(a, b));
        Assert.Equal(StrictResult.StrictlyTrue, PointTools.Collinear(o, a, b));
    }

    [Fact]
    public void LineTools_SameDetectsCoincidenceNotParallel()
    {
        Assert.Equal(StrictResult.StrictlyTrue, LineTools.Same(new HLine(1, 0, 0), new HLine(2, 0, 0)));
        Assert.Equal(StrictResult.StrictlyFalse, LineTools.Same(new HLine(1, 0, 0), new HLine(1, 0, -1)));
    }

    [Fact]
    public void ConicTools_TangentVersusSecant()
    {
        var circle = Scenes.Circle4;
        Assert.Equal(StrictResult.StrictlyTrue, ConicTools.IsTangent(circle, new HLine(0, 1, -2)));   // y=2 切线
        Assert.Equal(StrictResult.StrictlyFalse, ConicTools.IsTangent(circle, new HLine(0, 1, 0)));   // y=0 割线
    }

    [Fact]
    public void ConicTools_IsPolarLine()
    {
        var circle = Scenes.Circle4;
        var p = HPoint.FromCartesian(0, 2);
        Assert.Equal(StrictResult.StrictlyTrue, ConicTools.IsPolarLine(circle, p, circle.PolarLine(p)));
        Assert.Equal(StrictResult.StrictlyFalse, ConicTools.IsPolarLine(circle, p, new HLine(0, 1, 0)));
    }

    /// <summary>齐次坐标归一化：有限点出口处 W 应化为 1，避免表达式膨胀。</summary>
    [Fact]
    public void ConicIntersections_AreNormalized()
    {
        var circle = Scenes.Circle4;
        var pts = circle.Intersect(new HLine(0, 1, -1));   // y = 1

        Assert.Equal(2, pts.Length);
        foreach (var p in pts)
        {
            Assert.Equal(1.0, p.W.Approx, 12);
            Assert.True(System.Math.Abs(p.X.Approx) < 10);
        }
    }

    [Fact]
    public void OtherIntersection_IsNormalized()
    {
        var circle = Scenes.Circle4;
        var known = HPoint.FromCartesian(0, 2);
        var other = circle.OtherIntersection(known, new HLine(1, -1, -1));

        Assert.Equal(1.0, other.W.Approx, 12);
        Assert.Equal(StrictResult.StrictlyTrue, circle.ContainsStrictly(other));
    }

    /// <summary>二次型限制到直线的无根号弦长公式：圆 x²+y²=4 与 y=1 的弦长² = 12。</summary>
    [Fact]
    public void ConicRestrictToLine_ChordLengthSquared()
    {
        var circle = Scenes.Circle4;
        var line = new HLine(0, 1, -1);          // y = 1
        var (a, b, c) = circle.RestrictToLine(line);

        Assert.False(a.VerifyStrictZero() == StrictResult.StrictlyTrue);
        Assert.Equal(12.0, circle.IntersectionChordLengthSquared(line).Approx, 12);

        // 与显式交点结果一致
        var pts = circle.Intersect(line);
        Assert.Equal(2, pts.Length);
        Assert.Equal(12.0, Metric.DistanceSquared(pts[0], pts[1]).Approx, 12);
    }

    [Fact]
    public void RealSets_UnionIntersectSubtract()
    {
        Entity.Variable k = "k";
        var sol1 = RealSets.Solve(MathS.LessThan(k * k - 4, 0), k);   // (-2,2)
        var sol2 = RealSets.Solve(MathS.LessThan(k, 0), k);           // (-oo,0)

        var both = RealSets.Intersect(sol1, sol2);
        Assert.Equal(StrictResult.StrictlyTrue, RealSets.Contains(both, ExactScalar.FromInt(-1)));
        Assert.Equal(StrictResult.StrictlyFalse, RealSets.Contains(both, ExactScalar.FromInt(1)));

        var a = RealSets.Solve(MathS.LessThan(k * k - 1, 0), k);                    // (-1,1)
        var b = RealSets.Solve(MathS.LessThan((k - 2) * (k - 3), 0), k);           // (2,3)
        var uni = RealSets.Union(a, b);
        Assert.Equal(StrictResult.StrictlyTrue, RealSets.Contains(uni, ExactScalar.FromInt(0)));
        Assert.Equal(StrictResult.StrictlyTrue, RealSets.Contains(uni, ExactScalar.FromFormula("5/2")));

        var withHole = RealSets.SubtractPoints(sol1, ExactScalar.FromInt(0));
        Assert.Equal(StrictResult.StrictlyFalse, RealSets.Contains(withHole, ExactScalar.FromInt(0)));
        Assert.Equal(StrictResult.StrictlyTrue, RealSets.Contains(withHole, ExactScalar.FromInt(1)));
    }

    [Fact]
    public void RealSets_IrrationalEndpointsStayExact()
    {
        Entity.Variable k = "k";
        var range = RealSets.Solve(MathS.LessThan(k * k - 2 * k - 1, 0), k);  // (1-√2, 1+√2)

        Assert.Equal(StrictResult.StrictlyTrue, RealSets.Contains(range, ExactScalar.FromInt(1)));
        Assert.Equal(StrictResult.StrictlyFalse, RealSets.Contains(range, ExactScalar.FromInt(3)));
        Assert.Contains("sqrt(2)", RealSets.Describe(range));
    }
}
