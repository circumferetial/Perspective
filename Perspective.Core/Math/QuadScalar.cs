using System.Numerics;

namespace Perspective.Core.Math;

/// <summary>
///     单二次扩张 Q(√d) 中的数：a + b√d，d&gt;1 且无平方因子。
///     只支持同一个 d；不同 d 的加法/乘法会抛 NotSupportedException，
///     表示“超出单扩张片段”。
/// </summary>
public readonly struct QuadScalar :
    IExactRing<QuadScalar>,
    IComputableField<QuadScalar>,
    IOrderedRing<QuadScalar>,
    IComparable<QuadScalar>
{
    public BigRational A { get; }
    public BigRational B { get; }
    public BigInteger Radicand { get; }

    public QuadScalar(BigRational a, BigRational b, BigInteger radicand)
    {
        if (radicand < 1)
            throw new ArgumentOutOfRangeException(nameof(radicand), "Radicand 必须 ≥ 1。");

        if (b.IsZero || radicand.IsOne)
        {
            A = a;
            B = BigRational.Zero;
            Radicand = BigInteger.One;
            return;
        }

        A = a;
        B = b;
        Radicand = radicand;
    }

    public static QuadScalar Zero => new(BigRational.Zero, BigRational.Zero, BigInteger.One);
    public static QuadScalar One => new(BigRational.One, BigRational.Zero, BigInteger.One);

    public static QuadScalar FromLong(long value) =>
        new(BigRational.FromLong(value), BigRational.Zero, BigInteger.One);

    public static implicit operator QuadScalar(int value) => FromLong(value);
    public static implicit operator QuadScalar(long value) => FromLong(value);

    /// <summary>由有理数 x≥0 构造 √x；x 必须是平方数或可分解出平方因子。</summary>
    public static QuadScalar FromSqrt(BigRational x)
    {
        if (x.Num.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(x), "负数不能构造实平方根。");
        if (x.IsZero) return Zero;

        var n = x.Num * x.Den;
        var outside = BigInteger.One;
        var radicand = BigInteger.One;

        for (var p = new BigInteger(2); p * p <= n; p += p.IsEven ? 1 : 2)
        {
            var count = 0;
            while (n % p == 0)
            {
                n /= p;
                count++;
            }
            if (count == 0) continue;

            outside *= BigInteger.Pow(p, count / 2);
            if (count % 2 == 1) radicand *= p;
        }
        if (n > 1) radicand *= n;

        var coefficient = new BigRational(outside, x.Den);
        return radicand.IsOne
            ? new QuadScalar(coefficient, BigRational.Zero, BigInteger.One)
            : new QuadScalar(BigRational.Zero, coefficient, radicand);
    }

    private static void RequireSameRadicand(QuadScalar a, QuadScalar b)
    {
        if (a.B.IsZero || b.B.IsZero) return;
        if (a.Radicand != b.Radicand)
            throw new NotSupportedException("QuadScalar 只支持单个二次扩张；不同 √d 不能混合。");
    }

    public static QuadScalar operator +(QuadScalar a, QuadScalar b)
    {
        RequireSameRadicand(a, b);
        var d = a.B.IsZero ? b.Radicand : a.Radicand;
        return new QuadScalar(a.A + b.A, a.B + b.B, d);
    }

    public static QuadScalar operator -(QuadScalar a, QuadScalar b)
    {
        RequireSameRadicand(a, b);
        var d = a.B.IsZero ? b.Radicand : a.Radicand;
        return new QuadScalar(a.A - b.A, a.B - b.B, d);
    }

    public static QuadScalar operator -(QuadScalar a) => new(-a.A, -a.B, a.Radicand);

    public static QuadScalar operator *(QuadScalar a, QuadScalar b)
    {
        RequireSameRadicand(a, b);
        var d = a.B.IsZero ? b.Radicand : a.Radicand;
        var dd = new BigRational(d, BigInteger.One);
        return new QuadScalar(
            a.A * b.A + a.B * b.B * dd,
            a.A * b.B + a.B * b.A,
            d);
    }

    public static QuadScalar operator /(QuadScalar a, QuadScalar b)
    {
        var inverse = InverseOf(b);
        return a * inverse;
    }

    private static QuadScalar InverseOf(QuadScalar value)
    {
        if (value.IsZero)
            throw new DivideByZeroException("QuadScalar 除数不能为 0。");

        var d = new BigRational(value.Radicand, BigInteger.One);
        var norm = value.A * value.A - value.B * value.B * d;
        if (norm.IsZero)
            throw new DivideByZeroException("QuadScalar 范数为 0，元素不可逆。");

        return new QuadScalar(value.A / norm, -value.B / norm, value.Radicand);
    }

    public bool IsZero => A.IsZero && B.IsZero;

    public static StrictResult VerifyZero(QuadScalar a) =>
        a.IsZero ? StrictResult.StrictlyTrue : StrictResult.StrictlyFalse;

    static StrictResult IExactRing<QuadScalar>.VerifyZero(QuadScalar a) => VerifyZero(a);

    static StrictVerdict IExactRing<QuadScalar>.IsUnit(QuadScalar a) =>
        a.IsZero ? StrictVerdict.StrictlyFalse : StrictVerdict.StrictlyTrue;

    static StrictVerdict IMultiplicativeGroup<QuadScalar>.TryInverse(
        QuadScalar a, out QuadScalar inverse)
    {
        if (a.IsZero)
        {
            inverse = default;
            return StrictVerdict.StrictlyFalse;
        }

        inverse = InverseOf(a);
        return StrictVerdict.StrictlyTrue;
    }

    static StrictVerdict IOrderedRing<QuadScalar>.TrySign(QuadScalar value, out int sign)
    {
        sign = SignOf(value);
        return StrictVerdict.StrictlyTrue;
    }

    private static int SignOf(QuadScalar value)
    {
        if (value.B.IsZero) return value.A.Num.Sign;
        if (value.A.IsZero) return value.B.Num.Sign;

        var sa = value.A.Num.Sign;
        var sb = value.B.Num.Sign;
        if (sa == sb) return sa;

        var d = new BigRational(value.Radicand, BigInteger.One);
        var a2 = value.A * value.A;
        var b2d = value.B * value.B * d;

        return sa > 0
            ? a2.CompareTo(b2d) > 0 ? 1 : -1
            : b2d.CompareTo(a2) > 0 ? 1 : -1;
    }

    public int CompareTo(QuadScalar other) => SignOf(this - other);

    public override string ToString() =>
        B.IsZero ? A.ToString() : $"{A} + ({B})√{Radicand}";
}
