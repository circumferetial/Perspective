using System.Numerics;

namespace Perspective.Core.Math;

/// <summary>
///     规范化的任意精度有理数：Num/Den，Den > 0，gcd(Num,Den)=1。
///     只做域运算与精确符号；不携带任何浮点近似。
/// </summary>
public readonly struct BigRational :
    IExactRing<BigRational>,
    IComputableField<BigRational>,
    IOrderedRing<BigRational>,
    ISqrtable<BigRational>,
    IComparable<BigRational>
{
    public BigInteger Num { get; }
    public BigInteger Den { get; }

    public BigRational(BigInteger num, BigInteger den)
    {
        if (den.IsZero)
            throw new DivideByZeroException("BigRational 分母不能为 0。");

        if (num.IsZero)
        {
            Num = BigInteger.Zero;
            Den = BigInteger.One;
            return;
        }

        if (den.Sign < 0)
        {
            num = -num;
            den = -den;
        }

        var g = BigInteger.GreatestCommonDivisor(BigInteger.Abs(num), den);
        Num = num / g;
        Den = den / g;
    }

    public static BigRational Zero => new(BigInteger.Zero, BigInteger.One);
    public static BigRational One => new(BigInteger.One, BigInteger.One);

    public static BigRational FromLong(long value) => new(value, BigInteger.One);

    public static implicit operator BigRational(int value) => FromLong(value);
    public static implicit operator BigRational(long value) => FromLong(value);

    public static BigRational operator +(BigRational a, BigRational b) =>
        new(a.Num * b.Den + b.Num * a.Den, a.Den * b.Den);

    public static BigRational operator -(BigRational a, BigRational b) =>
        new(a.Num * b.Den - b.Num * a.Den, a.Den * b.Den);

    public static BigRational operator -(BigRational a) => new(-a.Num, a.Den);

    public static BigRational operator *(BigRational a, BigRational b) =>
        new(a.Num * b.Num, a.Den * b.Den);

    public static BigRational operator /(BigRational a, BigRational b) =>
        b.Num.IsZero
            ? throw new DivideByZeroException("BigRational 除数不能为 0。")
            : new(a.Num * b.Den, a.Den * b.Num);

    public bool IsZero => Num.IsZero;

    public static StrictResult VerifyZero(BigRational a) =>
        a.Num.IsZero ? StrictResult.StrictlyTrue : StrictResult.StrictlyFalse;

    static StrictResult IExactRing<BigRational>.VerifyZero(BigRational a) => VerifyZero(a);

    static StrictVerdict IExactRing<BigRational>.IsUnit(BigRational a) =>
        a.Num.IsZero ? StrictVerdict.StrictlyFalse : StrictVerdict.StrictlyTrue;

    static StrictVerdict IMultiplicativeGroup<BigRational>.TryInverse(
        BigRational a, out BigRational inverse)
    {
        if (a.Num.IsZero)
        {
            inverse = default;
            return StrictVerdict.StrictlyFalse;
        }

        inverse = new BigRational(a.Den, a.Num);
        return StrictVerdict.StrictlyTrue;
    }

    static StrictVerdict IOrderedRing<BigRational>.TrySign(BigRational value, out int sign)
    {
        sign = value.Num.Sign;
        return StrictVerdict.StrictlyTrue;
    }

    static StrictVerdict ISqrtable<BigRational>.TrySqrt(BigRational value, out BigRational root)
    {
        if (value.Num.Sign < 0)
        {
            root = default;
            return StrictVerdict.StrictlyFalse;
        }

        var numRoot = IntegerSqrt(value.Num);
        var denRoot = IntegerSqrt(value.Den);
        if (numRoot * numRoot != value.Num || denRoot * denRoot != value.Den)
        {
            root = default;
            return StrictVerdict.StrictlyFalse;
        }

        root = new BigRational(numRoot, denRoot);
        return StrictVerdict.StrictlyTrue;
    }

    private static BigInteger IntegerSqrt(BigInteger value)
    {
        if (value.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(value));

        if (value < 2) return value;

        var x = value;
        var y = (x + value / x) / 2;
        while (y < x)
        {
            x = y;
            y = (x + value / x) / 2;
        }
        return x;
    }

    public int CompareTo(BigRational other) =>
        (Num * other.Den).CompareTo(other.Num * Den);

    public override string ToString() => Den.IsOne ? Num.ToString() : $"{Num}/{Den}";
}
