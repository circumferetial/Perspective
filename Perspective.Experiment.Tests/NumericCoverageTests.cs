using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Xunit;

namespace Perspective.Experiment.Tests;

public sealed class NumericCoverageTests
{
    private static BigRational Q(long num, long den = 1) => new(num, den);
    private static QuadScalar R(long num, long den = 1) => QuadScalar.FromLong(num) / QuadScalar.FromLong(den);
    private static QuadScalar Sqrt(long n) => QuadScalar.FromSqrt(Q(n));

    [Fact]
    public void Rational_ProjectiveConic_IsExact()
    {
        var circle = new BilinearForm3<BigRational>(
            BigRational.One, BigRational.Zero, BigRational.Zero,
            BigRational.One, BigRational.Zero, -BigRational.One);
        var point = new Primal3<BigRational>(Q(3, 5), Q(4, 5), BigRational.One);

        Assert.Equal(StrictResult.StrictlyTrue,
            BigRational.VerifyZero(circle.Eval(point)));
    }

    [Fact]
    public void SingleSqrt_CircleLineIntersection()
    {
        // x²+y²=1, y=1/2 ⇒ x=±√3/2
        var x = Sqrt(3) / R(2);
        var y = R(1, 2);
        var circle = new BilinearForm3<QuadScalar>(
            QuadScalar.One, QuadScalar.Zero, QuadScalar.Zero,
            QuadScalar.One, QuadScalar.Zero, -QuadScalar.One);

        Assert.Equal(StrictResult.StrictlyTrue,
            QuadScalar.VerifyZero(circle.Eval(
                new Primal3<QuadScalar>(x, y, QuadScalar.One))));
    }

    [Fact]
    public void SingleSqrt_EllipseFocus()
    {
        // x²/4 + y²/2 = 1 ⇒ c²=2 ⇒ c=√2
        var c = Sqrt(2);
        Assert.Equal(StrictResult.StrictlyTrue,
            QuadScalar.VerifyZero(c * c - QuadScalar.FromLong(2)));

        var focus = new Primal3<QuadScalar>(c, QuadScalar.Zero, QuadScalar.One);
        Assert.Equal(StrictResult.StrictlyTrue,
            QuadScalar.VerifyZero(focus.X * focus.X - QuadScalar.FromLong(2)));
    }

    [Fact]
    public void MultipleIndependentRadicals_AreExplicitlyUnsupported()
    {
        Assert.Throws<NotSupportedException>(() => _ = Sqrt(2) + Sqrt(3));
    }
}
