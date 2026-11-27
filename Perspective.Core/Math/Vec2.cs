namespace Perspective.Core.Math;

/// <summary>
///     2D 向量（交换环上的模）：只有加法、标量乘、点积、二维行列式，没有“向量乘法”。
///     与 <see cref="ExactScalar" />/复数不同：复数是域元素，向量是模元素；
///     二维行列式返回标量（有向面积），不是三维向量叉积。
/// </summary>
public readonly record struct Vec2<T>(T X, T Y) where T : IExactRing<T>
{
    public static Vec2<T> Zero => new(T.Zero, T.Zero);
    public static Vec2<T> UnitX => new(T.One, T.Zero);
    public static Vec2<T> UnitY => new(T.Zero, T.One);

    public static Vec2<T> operator +(Vec2<T> a, Vec2<T> b) =>
        new(a.X + b.X, a.Y + b.Y);

    public static Vec2<T> operator -(Vec2<T> a, Vec2<T> b) =>
        new(a.X - b.X, a.Y - b.Y);

    public static Vec2<T> operator -(Vec2<T> a) =>
        new(-a.X, -a.Y);

    public static Vec2<T> operator *(T k, Vec2<T> v) =>
        new(k * v.X, k * v.Y);

    public static Vec2<T> operator *(Vec2<T> v, T k) =>
        new(v.X * k, v.Y * k);

    /// <summary>点积（对称双线性型）。</summary>
    public T Dot(Vec2<T> other) => X * other.X + Y * other.Y;

    /// <summary>二维行列式/有向面积：x₁y₂ − y₁x₂。不是三维向量叉积。</summary>
    public T Determinant(Vec2<T> other) => X * other.Y - Y * other.X;

    /// <summary>Determinant 的几何别名：两向量张成的有向面积。</summary>
    public T SignedArea(Vec2<T> other) => Determinant(other);

    public T NormSquared => Dot(this);

    /// <summary>逆时针旋转 90°：Perp(x,y) = (−y,x)。</summary>
    public Vec2<T> Perp => new(-Y, X);

    public bool IsZero =>
        T.VerifyZero(X) == StrictResult.StrictlyTrue
        && T.VerifyZero(Y) == StrictResult.StrictlyTrue;

    public override string ToString() => $"({X}, {Y})";
}
