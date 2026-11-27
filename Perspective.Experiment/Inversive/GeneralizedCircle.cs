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
///     实广义圆：[A,B,C,D] 表示 A(x²+y²)+Bx+Cy+D=0。
///     A=0 为直线；判别式 B²+C²−4AD 决定圆、点圆、虚圆。
/// </summary>
public readonly record struct GeneralizedCircle(ExactScalar A, ExactScalar B, ExactScalar C, ExactScalar D)
{
    public static GeneralizedCircle FromCircle(Vec2<ExactScalar> center, ExactScalar radiusSquared) =>
        new(ExactScalar.One,
            -ExactScalar.FromInt(2) * center.X,
            -ExactScalar.FromInt(2) * center.Y,
            center.Dot(center) - radiusSquared);

    public static GeneralizedCircle FromLine(ExactScalar b, ExactScalar c, ExactScalar d) =>
        new(ExactScalar.Zero, b, c, d);

    public static GeneralizedCircle FromPoint(Vec2<ExactScalar> point) =>
        new(ExactScalar.One,
            -ExactScalar.FromInt(2) * point.X,
            -ExactScalar.FromInt(2) * point.Y,
            point.Dot(point));

    public GeneralizedCircleKind Kind
    {
        get
        {
            if (IsZero(A))
                return IsZero(B) && IsZero(C) ? GeneralizedCircleKind.Empty : GeneralizedCircleKind.Line;

            var discriminant = B * B + C * C - ExactScalar.FromInt(4) * A * D;
            if (IsZero(discriminant)) return GeneralizedCircleKind.Point;
            return discriminant.Approx > 0 ? GeneralizedCircleKind.Circle : GeneralizedCircleKind.Imaginary;
        }
    }

    public bool IsLine => Kind == GeneralizedCircleKind.Line;
    public bool IsCircle => Kind is GeneralizedCircleKind.Circle or GeneralizedCircleKind.Point;

    public ExactScalar Eval(Vec2<ExactScalar> p) =>
        A * p.Dot(p) + B * p.X + C * p.Y + D;

    public Result<Vec2<ExactScalar>, GeometryError> Center()
    {
        if (IsZero(A))
            return Result<Vec2<ExactScalar>, GeometryError>.Fail(GeometryError.AtInfinity);

        var twoA = ExactScalar.FromInt(2) * A;
        return Result<Vec2<ExactScalar>, GeometryError>.Ok(
            new Vec2<ExactScalar>(-B / twoA, -C / twoA));
    }

    public Result<ExactScalar, GeometryError> RadiusSquared()
    {
        if (IsZero(A))
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.AtInfinity);

        var twoA = ExactScalar.FromInt(2) * A;
        var center = new Vec2<ExactScalar>(-B / twoA, -C / twoA);
        return Result<ExactScalar, GeometryError>.Ok(center.Dot(center) - D / A);
    }

    /// <summary>两广义圆的极化内积为零 ⇔ 正交（圆/线统一公式）。</summary>
    public ExactScalar InnerProduct(GeneralizedCircle other) =>
        B * other.B + C * other.C
        - ExactScalar.FromInt(2) * A * other.D
        - ExactScalar.FromInt(2) * other.A * D;

    public bool IsOrthogonal(GeneralizedCircle other) =>
        IsZero(InnerProduct(other));

    /// <summary>单位圆反演：交换 A 与 D。直线不过原点 ↔ 过原点的圆。</summary>
    public GeneralizedCircle InvertInUnitCircle() => new(D, B, C, A);

    /// <summary>以 center 为心、radiusSquared 为半径² 的反演。</summary>
    public Result<GeneralizedCircle, GeometryError> InvertInCircle(
        Vec2<ExactScalar> center,
        ExactScalar radiusSquared)
    {
        if (radiusSquared.VerifyStrictZero() == StrictResult.StrictlyTrue
            || radiusSquared.Approx < 0)
            return Result<GeneralizedCircle, GeometryError>.Fail(GeometryError.UndefinedParameter);

        // 平移到圆心：x = u + h, y = v + k。
        var shifted = new GeneralizedCircle(
            A,
            ExactScalar.FromInt(2) * A * center.X + B,
            ExactScalar.FromInt(2) * A * center.Y + C,
            A * center.Dot(center) + B * center.X + C * center.Y + D);

        // 归一化到单位圆：u = x'/r, v = y'/r。
        var radius = ExactScalar.Sqrt(radiusSquared);
        var normalized = new GeneralizedCircle(
            shifted.A,
            shifted.B / radius,
            shifted.C / radius,
            shifted.D / radiusSquared);

        // 单位反演后，再缩放回半径 r。
        var inverted = normalized.InvertInUnitCircle();
        var scaled = new GeneralizedCircle(
            inverted.A,
            inverted.B * radius,
            inverted.C * radius,
            inverted.D * radiusSquared);

        // 平移回原坐标系。
        return Result<GeneralizedCircle, GeometryError>.Ok(new GeneralizedCircle(
            scaled.A,
            scaled.B - ExactScalar.FromInt(2) * scaled.A * center.X,
            scaled.C - ExactScalar.FromInt(2) * scaled.A * center.Y,
            scaled.A * center.Dot(center) - scaled.B * center.X - scaled.C * center.Y + scaled.D));
    }

    private static bool IsZero(ExactScalar x) =>
        x.VerifyStrictZero() == StrictResult.StrictlyTrue;
}
