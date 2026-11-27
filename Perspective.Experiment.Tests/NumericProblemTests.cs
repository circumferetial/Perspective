using Perspective.Core.Math;
using Xunit;

namespace Perspective.Experiment.Tests;

public sealed class NumericProblemTests
{
    private static BigRational Q(long num, long den = 1) => new(num, den);
    private static QuadScalar R(long num, long den = 1) => QuadScalar.FromLong(num) / QuadScalar.FromLong(den);
    private static QuadScalar Sqrt(long n) => QuadScalar.FromSqrt(Q(n));

    [Fact]
    public void HS1_RationalInstance_PerpendicularCondition()
    {
        // x²/4+y²=1, l:y=x−3/5；用 Vieta 直接验 AM⊥AN。
        var k = Q(1);
        var m = Q(-3, 5);
        var one = BigRational.One;
        var lc = one + Q(4) * k * k;

        var sum = -Q(8) * k * m / lc;
        var product = Q(4) * (m * m - one) / lc;
        var perpendicular = (one + k * k) * product
                            + k * (m - one) * sum
                            + (m - one) * (m - one);

        Assert.Equal(StrictResult.StrictlyTrue,
            BigRational.VerifyZero(perpendicular));
    }

    [Fact]
    public void HS3_Rational_FocusChordReciprocal()
    {
        // y²=4x 的焦点弦端点 y1=1, y2=-4。
        var y1 = Q(1);
        var y2 = Q(-4);
        var invFm = Q(4) / (y1 * y1 + Q(4));
        var invFn = Q(4) / (y2 * y2 + Q(4));

        Assert.Equal(StrictResult.StrictlyTrue,
            BigRational.VerifyZero(invFm + invFn - BigRational.One));
    }

    [Fact]
    public void HS7_Rational_CentroidLocus()
    {
        var m = Q(1);
        var den = Q(3) * (m * m + Q(2));
        var x = Q(4) / den;
        var y = Q(-2) * m / den;

        Assert.Equal(StrictResult.StrictlyTrue,
            BigRational.VerifyZero(Q(3) * x * x + Q(6) * y * y - Q(2) * x));
    }

    [Fact]
    public void HS8_SingleSqrt_Witness()
    {
        // t=2/3 时 x=2/3 与椭圆 x²/4+y²=1 交于 y=±√(8/3)。
        var t = R(2, 3);
        var y = QuadScalar.FromSqrt(Q(8, 3)); // √(8/3) = 2√6/3
        var k1 = y / (t + R(2));
        var k2 = -y / (t - R(2));

        Assert.Equal(StrictResult.StrictlyTrue,
            QuadScalar.VerifyZero(k2 - R(2) * k1));
    }

    [Fact]
    public void HS5_SingleSqrt_TangentArea()
    {
        // x²−y²/3=1, tangent y=2x+1；与渐近线 y=±√3x 的交点面积²。
        var sqrt3 = Sqrt(3);
        var xP = QuadScalar.One / (sqrt3 - R(2));
        var xQ = -QuadScalar.One / (sqrt3 + R(2));
        var areaSquared = R(3) * xP * xP * xQ * xQ;

        Assert.Equal(StrictResult.StrictlyTrue,
            QuadScalar.VerifyZero(areaSquared - R(3)));
    }
}
