using Perspective.Core.Math;
using Perspective.Experiment.Subspace;

namespace Perspective.Experiment.Inversive;

public enum GeneralizedCircleKind
{
    Circle,
    Line,
    Point,
    Imaginary,
    Empty
}

/// <summary>
///     广义圆：[A,B,C,D] 表示 A(x²+y²)+Bx+Cy+D=0。
///     A=0 为直线；判别式 B²+C²−4AD 决定圆、点圆、虚圆。
///     只要求可计算域；符号分类额外要求 IRealField。
/// </summary>
public readonly record struct GeneralizedCircle<T>(T A, T B, T C, T D)
    where T : IComputableField<T>
{
    private static T Two => T.FromLong(2);

    public static GeneralizedCircle<T> FromCircle(Vec2<T> center, T radiusSquared) =>
        new(T.One,
            -Two * center.X,
            -Two * center.Y,
            center.Dot(center) - radiusSquared);

    public static GeneralizedCircle<T> FromLine(T b, T c, T d) =>
        new(T.Zero, b, c, d);

    public static GeneralizedCircle<T> FromPoint(Vec2<T> point) =>
        new(T.One, -Two * point.X, -Two * point.Y, point.Dot(point));

    public T Eval(Vec2<T> p) =>
        A * p.Dot(p) + B * p.X + C * p.Y + D;

    public Result<Vec2<T>, GeometryError> Center()
    {
        if (IsZero(A))
            return Result<Vec2<T>, GeometryError>.Fail(GeometryError.AtInfinity);
        if (T.TryInverse(Two * A, out var inv) != StrictVerdict.StrictlyTrue)
            return Result<Vec2<T>, GeometryError>.Fail(GeometryError.UndefinedParameter);

        return Result<Vec2<T>, GeometryError>.Ok(new Vec2<T>(-B * inv, -C * inv));
    }

    public Result<T, GeometryError> RadiusSquared()
    {
        var center = Center();
        if (!center.TryGet(out var c))
            return Result<T, GeometryError>.Fail(center.Error);
        if (T.TryInverse(A, out var invA) != StrictVerdict.StrictlyTrue)
            return Result<T, GeometryError>.Fail(GeometryError.UndefinedParameter);

        return Result<T, GeometryError>.Ok(c.Dot(c) - D * invA);
    }

    /// <summary>两广义圆的极化内积为零 ⇔ 正交（圆/线统一公式）。</summary>
    public T InnerProduct(GeneralizedCircle<T> other) =>
        B * other.B + C * other.C
        - Two * A * other.D
        - Two * other.A * D;

    public bool IsOrthogonal(GeneralizedCircle<T> other) =>
        IsZero(InnerProduct(other));

    /// <summary>单位圆反演：交换 A 与 D。直线不过原点 ↔ 过原点的圆。</summary>
    public GeneralizedCircle<T> InvertInUnitCircle() => new(D, B, C, A);

    /// <summary>以 center 为心、radiusSquared 为半径² 的反演；只需域逆，不需开方。</summary>
    public Result<GeneralizedCircle<T>, GeometryError> InvertInCircle(
        Vec2<T> center,
        T radiusSquared)
    {
        if (IsZero(radiusSquared))
            return Result<GeneralizedCircle<T>, GeometryError>.Fail(GeometryError.UndefinedParameter);
        if (T.TryInverse(radiusSquared, out var invR) != StrictVerdict.StrictlyTrue)
            return Result<GeneralizedCircle<T>, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var shifted = new GeneralizedCircle<T>(
            A,
            Two * A * center.X + B,
            Two * A * center.Y + C,
            A * center.Dot(center) + B * center.X + C * center.Y + D);

        var a4 = shifted.D * invR;
        var b4 = shifted.B - Two * a4 * center.X;
        var c4 = shifted.C - Two * a4 * center.Y;
        var d4 = a4 * center.Dot(center) - shifted.B * center.X - shifted.C * center.Y + A * radiusSquared;

        return Result<GeneralizedCircle<T>, GeometryError>.Ok(new GeneralizedCircle<T>(a4, b4, c4, d4));
    }

    private static bool IsZero(T x) =>
        T.VerifyZero(x) == StrictResult.StrictlyTrue;
}

public static class GeneralizedCircleClassify
{
    public static GeneralizedCircleKind Classify<T>(this GeneralizedCircle<T> c)
        where T : IRealField<T>
    {
        if (IsZero(c.A))
            return IsZero(c.B) && IsZero(c.C)
                ? GeneralizedCircleKind.Empty
                : GeneralizedCircleKind.Line;

        var discriminant = c.B * c.B + c.C * c.C - T.FromLong(4) * c.A * c.D;
        return T.Sign(discriminant) switch
        {
            < 0 => GeneralizedCircleKind.Imaginary,
            0 => GeneralizedCircleKind.Point,
            _ => GeneralizedCircleKind.Circle
        };
    }

    public static bool IsLine<T>(this GeneralizedCircle<T> c) where T : IRealField<T> =>
        c.Classify() == GeneralizedCircleKind.Line;

    public static bool IsCircle<T>(this GeneralizedCircle<T> c) where T : IRealField<T> =>
        c.Classify() is GeneralizedCircleKind.Circle or GeneralizedCircleKind.Point;

    private static bool IsZero<T>(T x) where T : IRealField<T> =>
        T.VerifyZero(x) == StrictResult.StrictlyTrue;
}
