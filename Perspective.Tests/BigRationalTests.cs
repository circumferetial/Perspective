using System.Numerics;
using Perspective.Core.Math;

namespace Perspective.Tests;

public sealed class BigRationalTests
{
    private static StrictVerdict TryInverseOf<T>(T value, out T inverse)
        where T : IComputableField<T> => T.TryInverse(value, out inverse);

    private static StrictVerdict TrySignOf<T>(T value, out int sign)
        where T : IOrderedRing<T> => T.TrySign(value, out sign);

    private static StrictVerdict TrySqrtOf<T>(T value, out T root)
        where T : ISqrtable<T> => T.TrySqrt(value, out root);

    [Fact]
    public void Construction_IsNormalized()
    {
        var half = new BigRational(2, 4);
        Assert.Equal(BigInteger.One, half.Num);
        Assert.Equal(new BigInteger(2), half.Den);

        var negative = new BigRational(3, -6);
        Assert.Equal(new BigInteger(-1), negative.Num);
        Assert.Equal(new BigInteger(2), negative.Den);
    }

    [Fact]
    public void Arithmetic_And_Sign_AreExact()
    {
        var a = new BigRational(1, 3);
        var b = new BigRational(1, 6);
        var sum = a + b;
        Assert.Equal(BigInteger.One, sum.Num);
        Assert.Equal(new BigInteger(2), sum.Den);

        var tiny = new BigRational(BigInteger.One, BigInteger.Pow(10, 100));
        Assert.NotEqual(StrictResult.StrictlyTrue, BigRational.VerifyZero(tiny));

        Assert.Equal(StrictVerdict.StrictlyTrue, TrySignOf(a, out var sign));
        Assert.Equal(1, sign);
        Assert.Equal(StrictVerdict.StrictlyTrue, TrySignOf(a - a, out var zeroSign));
        Assert.Equal(0, zeroSign);
    }

    [Fact]
    public void Inverse_And_Sqrt_AreTriState()
    {
        var half = new BigRational(1, 2);
        Assert.Equal(StrictVerdict.StrictlyTrue, TryInverseOf(half, out var inverse));
        Assert.Equal(BigInteger.One, (half * inverse).Num);
        Assert.Equal(StrictVerdict.StrictlyFalse, TryInverseOf(BigRational.Zero, out _));

        Assert.Equal(StrictVerdict.StrictlyTrue,
            TrySqrtOf(new BigRational(4, 9), out var root));
        Assert.Equal(new BigRational(2, 3), root);

        Assert.Equal(StrictVerdict.StrictlyFalse,
            TrySqrtOf(new BigRational(2, 1), out _));
        Assert.Equal(StrictVerdict.StrictlyFalse,
            TrySqrtOf(new BigRational(-1, 1), out _));
    }
}
