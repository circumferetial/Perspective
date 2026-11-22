using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Tests;

/// <summary>圆锥曲线分类：与符号探索脚本验证过的 10 个 canonical 例子逐一对照。</summary>
public class ConicClassificationTests
{
    [Theory]
    [InlineData("椭圆 x²/4+y²/3=1", 3, 0, 4, 0, 0, -12, ConicKind.Ellipse, TransformLevel.Affine)]
    [InlineData("圆 x²+y²-4x=0", 1, 0, 1, -4, 0, 0, ConicKind.Circle, TransformLevel.Similarity)]
    [InlineData("双曲线 x²/4-y²/3=1", 3, 0, -4, 0, 0, -12, ConicKind.Hyperbola, TransformLevel.Affine)]
    [InlineData("抛物线 y²=2x", 0, 0, 1, -2, 0, 0, ConicKind.Parabola, TransformLevel.Affine)]
    [InlineData("点 x²+y²=0", 1, 0, 1, 0, 0, 0, ConicKind.Point, TransformLevel.Projective)]
    [InlineData("两相交线 x²-y²=0", 1, 0, -1, 0, 0, 0, ConicKind.TwoIntersectingLines, TransformLevel.Projective)]
    [InlineData("两平行线 x²-1=0", 1, 0, 0, 0, 0, -1, ConicKind.TwoParallelLines, TransformLevel.Projective)]
    [InlineData("空 x²+1=0", 1, 0, 0, 0, 0, 1, ConicKind.Empty, TransformLevel.Projective)]
    [InlineData("重线 x²=0", 1, 0, 0, 0, 0, 0, ConicKind.DoubleLine, TransformLevel.Projective)]
    [InlineData("旋转椭圆 x²+xy+y²=1", 1, 1, 1, 0, 0, -1, ConicKind.Ellipse, TransformLevel.Affine)]
    public void Classify_MatchesCanonicalTable(
        string name, int a, int b, int c, int d, int e, int f,
        ConicKind expectedKind, TransformLevel expectedLevel)
    {
        var conic = Conic.FromGeneralEquation(a, b, c, d, e, f);
        var result = ConicTools.Classify(conic);

        Assert.Equal(expectedKind, result.Kind);
        Assert.Equal(expectedLevel, result.MeaningfulAt);
        Assert.True(result.IsExact, $"{name} 应被严格判定");
    }

    [Fact]
    public void Classify_ApproximateInput_IsMarkedInexact()
    {
        var approx = Conic.FromGeneralEquation(
            ExactScalar.FromApprox(1), ExactScalar.FromApprox(1), ExactScalar.FromApprox(1),
            ExactScalar.FromApprox(0), ExactScalar.FromApprox(0), ExactScalar.FromApprox(-1));
        var result = ConicTools.Classify(approx);

        Assert.Equal(ConicKind.Ellipse, result.Kind);
        Assert.False(result.IsExact);
    }
}

/// <summary>主轴与半轴。</summary>
public class ConicAxesTests
{
    private static void AssertClose(double expected, double actual) =>
        Assert.True(System.Math.Abs(expected - actual) < 1e-9, $"期望 {expected}，实际 {actual}");

    [Fact]
    public void Circle_CenterAndEqualSemiAxes()
    {
        var axes = ConicTools.PrincipalAxes(Conic.FromGeneralEquation(1, 0, 1, -4, 0, 0));
        Assert.NotNull(axes);
        AssertClose(2, axes!.Value.Center.X.Approx);
        AssertClose(0, axes.Value.Center.Y.Approx);
        AssertClose(4, axes.Value.SemiAxis1Squared.Approx);
        AssertClose(4, axes.Value.SemiAxis2Squared.Approx);
        Assert.True(axes.Value.IsCircle);
    }

    [Fact]
    public void TranslatedEllipse_CenterAndSemiAxes()
    {
        // 3x²+4y²-12x+8y+4=0 -> (x-2)²/4+(y+1)²/3=1
        var axes = ConicTools.PrincipalAxes(Conic.FromGeneralEquation(3, 0, 4, -12, 8, 4));
        Assert.NotNull(axes);
        AssertClose(2, axes!.Value.Center.X.Approx);
        AssertClose(-1, axes.Value.Center.Y.Approx);
        AssertClose(3, axes.Value.SemiAxis1Squared.Approx);
        AssertClose(4, axes.Value.SemiAxis2Squared.Approx);
    }

    [Fact]
    public void RotatedEllipse_SemiAxesAndDirections()
    {
        var axes = ConicTools.PrincipalAxes(Conic.FromGeneralEquation(1, 1, 1, 0, 0, -1));
        Assert.NotNull(axes);
        AssertClose(0, axes!.Value.Center.X.Approx);
        AssertClose(0, axes.Value.Center.Y.Approx);
        AssertClose(2.0 / 3, axes.Value.SemiAxis1Squared.Approx);
        AssertClose(2, axes.Value.SemiAxis2Squared.Approx);
        // 主轴方向应比例于 (1,1) 与 (1,-1)
        var d1 = axes.Value.Axis1Direction;
        var d2 = axes.Value.Axis2Direction;
        AssertClose(System.Math.Abs(d1.X.Approx), System.Math.Abs(d1.Y.Approx));
        AssertClose(System.Math.Abs(d2.X.Approx), System.Math.Abs(d2.Y.Approx));
        Assert.NotEqual(System.Math.Sign(d1.X.Approx * d1.Y.Approx), System.Math.Sign(d2.X.Approx * d2.Y.Approx));
    }

    [Fact]
    public void Parabola_HasNoPrincipalAxes()
    {
        Assert.Null(ConicTools.PrincipalAxes(Conic.FromGeneralEquation(0, 0, 1, -2, 0, 0)));
    }
}
