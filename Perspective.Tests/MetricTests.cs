using Perspective.Core.Discovery;
using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Tests;

/// <summary>
/// 度量层（距离/点积/叉积/角度/面积）+ 2022 新高考Ⅰ 21 回归。
/// </summary>
public class MetricTests
{
    [Fact]
    public void DistanceDotCross_On345Triangle()
    {
        var a = HPoint.FromCartesian(0, 0);
        var b = HPoint.FromCartesian(3, 0);
        var c = HPoint.FromCartesian(0, 4);

        Assert.Equal(5.0, Metric.Distance(b, c).Approx, 12);
        Assert.Equal(25.0, Metric.DistanceSquared(b, c).Approx, 12);
        Assert.Equal(0.0, Metric.Dot(b, c).Approx, 12);          // 垂直
        Assert.Equal(12.0, Metric.Cross(b, c).Approx, 12);       // |叉积| = 12
        Assert.Equal(6.0, Metric.TriangleArea(a, b, c).Approx, 12);
        Assert.Equal(12.0, Metric.TriangleArea2(a, b, c).Approx, 12);
    }

    [Fact]
    public void TanAngle_IsRatioCrossOverDot()
    {
        var u = (X: ExactScalar.FromInt(1), Y: ExactScalar.FromInt(0));
        var v = (X: ExactScalar.FromInt(1), Y: ExactScalar.FromInt(1));

        var tan = Metric.TanAngle(u, v);
        Assert.NotNull(tan);
        Assert.Equal(1.0, tan!.Value.Approx, 12);                // 45°

        // 垂直时 tan 无定义
        var perpendicular = Metric.TanAngle(u, (ExactScalar.FromInt(0), ExactScalar.FromInt(1)));
        Assert.Null(perpendicular);
    }

    [Fact]
    public void AngleHelpers_CosineAndDegrees()
    {
        var xAxis = (X: ExactScalar.FromInt(1), Y: ExactScalar.FromInt(0));
        var diagonal = (X: ExactScalar.FromInt(1), Y: ExactScalar.FromInt(1));

        var cosine = Metric.CosineAngle(xAxis, diagonal);
        Assert.NotNull(cosine);
        Assert.Equal(System.Math.Sqrt(2) / 2, cosine!.Value.Approx, 9);

        var degrees = Metric.AngleDegrees(xAxis, diagonal);
        Assert.NotNull(degrees);
        Assert.Equal(45.0, degrees!.Value, 9);

        // 垂直：cos=0，角度 90°
        var yAxis = (X: ExactScalar.FromInt(0), Y: ExactScalar.FromInt(1));
        Assert.Equal(0.0, Metric.CosineAngle(xAxis, yAxis)!.Value.Approx, 9);
        Assert.Equal(90.0, Metric.AngleDegrees(xAxis, yAxis)!.Value, 9);
    }

    // ---------- 2022 新高考Ⅰ 21 ----------
    private static Conic Hyperbola => Conic.Hyperbola(ExactScalar.FromFormula("sqrt(2)"), 1);
    private static HPoint A => HPoint.FromCartesian(2, 1);

    [Fact]
    public void Question2022_1_SlopeIsMinusOne()
    {
        var slopes = InverseExamSolver.SolveChordSlopeForZeroSlopeSum(Hyperbola, A);

        Assert.Equal(StrictResult.StrictlyTrue, RealSets.Contains(slopes, ExactScalar.FromInt(-1)));
        Assert.Equal(StrictResult.StrictlyFalse, RealSets.Contains(slopes, ExactScalar.FromInt(0)));
        Assert.Equal(StrictResult.StrictlyFalse, RealSets.Contains(slopes, ExactScalar.FromInt(1)));
    }

    [Fact]
    public void Question2022_2_AreaIs16Sqrt2Over9()
    {
        var results = InverseExamSolver.SolveAreaFromTangent(
            Hyperbola, A,
            chordSlope: ExactScalar.FromInt(-1),
            tanTarget: ExactScalar.FromFormula("2 * sqrt(2)"));

        // 唯一有效解：截距 c = 5/3，面积 = 16√2/9（cc=3 是"弦过 A"的退化解，应被回验滤掉）
        Assert.Single(results);
        Assert.Equal(5.0 / 3.0, results[0].Intercept.Approx, 9);
        Assert.Equal(16.0 * System.Math.Sqrt(2) / 9.0, results[0].Area.Approx, 9);

        // 精确形式核对：面积² = 512/81
        var areaSq = results[0].Area * results[0].Area;
        Assert.Equal(StrictResult.StrictlyTrue,
            (areaSq - ExactScalar.FromFormula("512/81")).VerifyStrictZero());
    }
}
