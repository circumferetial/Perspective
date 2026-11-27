using System.Numerics;
using Perspective.Core.Math;

namespace Perspective.Tests;

public sealed class QuadScalarTests
{
    private static BigRational Q(long num, long den = 1) => new(num, den);

    private static StrictVerdict TrySignOf<T>(T value, out int sign)
        where T : IOrderedRing<T> => T.TrySign(value, out sign);

    private static StrictVerdict TryInverseOf<T>(T value, out T inverse)
        where T : IComputableField<T> => T.TryInverse(value, out inverse);

    [Fact]
    public void FromSqrt_Normalizes()
    {
        var sqrt2 = QuadScalar.FromSqrt(Q(2));
        Assert.True(sqrt2.A.IsZero);
        Assert.Equal(Q(1), sqrt2.B);
        Assert.Equal(new BigInteger(2), sqrt2.Radicand);

        var sqrt4 = QuadScalar.FromSqrt(Q(4));
        Assert.Equal(Q(2), sqrt4.A);
        Assert.True(sqrt4.B.IsZero);

        var sqrt2Over9 = QuadScalar.FromSqrt(Q(2, 9));
        Assert.Equal(Q(1, 3), sqrt2Over9.B);
        Assert.Equal(new BigInteger(2), sqrt2Over9.Radicand);
    }

    [Fact]
    public void Arithmetic_And_Sign_AreExact()
    {
        var sqrt2 = QuadScalar.FromSqrt(Q(2));
        var onePlus = QuadScalar.One + sqrt2;
        var oneMinus = QuadScalar.One - sqrt2;

        Assert.Equal(QuadScalar.FromLong(-1), onePlus * oneMinus);

        var inverse = (onePlus * onePlus).A; // 3
        Assert.Equal(Q(3), inverse);

        Assert.Equal(StrictVerdict.StrictlyTrue, TrySignOf(oneMinus, out var sign));
        Assert.Equal(-1, sign);
        Assert.Equal(StrictVerdict.StrictlyTrue, TrySignOf(onePlus, out var positive));
        Assert.Equal(1, positive);
    }

    [Fact]
    public void Inverse_And_MixedRadicals_AreExplicit()
    {
        var sqrt2 = QuadScalar.FromSqrt(Q(2));
        var value = QuadScalar.One + sqrt2;
        Assert.Equal(StrictVerdict.StrictlyTrue, TryInverseOf(value, out var inverse));
        Assert.Equal(QuadScalar.One, value * inverse);

        var sqrt3 = QuadScalar.FromSqrt(Q(3));
        Assert.Throws<NotSupportedException>(() => _ = value + sqrt3);
    }
}
