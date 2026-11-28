using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

public readonly record struct GenericConicLineResult<T>(
    LineConicKind Kind,
    Primal3<T>? P1,
    Primal3<T>? P2,
    T Discriminant)
    where T : IExactRing<T>;

/// <summary>
///     泛型“直线 ∩ conic”：只依赖域、符号与可开方能力。
///     超出当前数字类型（非平方判别式）时返回 Unsupported，不抛异常。
/// </summary>
public static class GenericConicLine
{
    public static Result<GenericConicLineResult<T>, GeometryError> Intersect<T>(
        Dual3<T> line,
        BilinearForm3<T> conic)
        where T : IComputableField<T>, IOrderedRing<T>, ISqrtable<T>
    {
        if (T.VerifyZero(line.A) == StrictResult.StrictlyTrue
            && T.VerifyZero(line.B) == StrictResult.StrictlyTrue)
            return Result<GenericConicLineResult<T>, GeometryError>.Fail(GeometryError.DegenerateLine);

        // 直线上的两个齐次点：u 为方向点，v 为有限点。
        var u = new Primal3<T>(-line.B, line.A, T.Zero);
        var v = new Primal3<T>(
            -line.A * line.C,
            -line.B * line.C,
            line.A * line.A + line.B * line.B);

        var a = conic.Eval(u);
        var b = SubspaceAlgebra.Pair(conic.Polar(u), v);
        var c = conic.Eval(v);

        if (T.VerifyZero(a) == StrictResult.StrictlyTrue)
        {
            if (T.VerifyZero(b) == StrictResult.StrictlyTrue)
            {
                return T.VerifyZero(c) == StrictResult.StrictlyTrue
                    ? Result<GenericConicLineResult<T>, GeometryError>.Ok(new(
                        LineConicKind.WholeLineOnConic, null, null, T.Zero))
                    : Result<GenericConicLineResult<T>, GeometryError>.Ok(new(
                        LineConicKind.Tangent, u, u, T.Zero));
            }

            // a=0: t(2b+ct)=0；两个齐次点为 u 与 c·u−2b·v。
            if (T.VerifyZero(c) == StrictResult.StrictlyTrue)
                return Result<GenericConicLineResult<T>, GeometryError>.Ok(new(
                    LineConicKind.Tangent, u, u, T.Zero));

            var other = LinearCombination(c, u, -T.FromLong(2) * b, v);
            return Result<GenericConicLineResult<T>, GeometryError>.Ok(new(
                LineConicKind.TwoPoints, u, other, b * b));
        }

        var disc = b * b - a * c;
        var discSign = T.TrySign(disc, out var sign);
        if (discSign != StrictVerdict.StrictlyTrue)
            return Result<GenericConicLineResult<T>, GeometryError>.Fail(GeometryError.Unsupported);

        if (sign < 0)
            return Result<GenericConicLineResult<T>, GeometryError>.Ok(new(
                LineConicKind.NoRealIntersection, null, null, disc));

        if (sign == 0)
        {
            var touch = LinearCombination(a, v, -b, u);
            return Result<GenericConicLineResult<T>, GeometryError>.Ok(new(
                LineConicKind.Tangent, touch, touch, disc));
        }

        var sqrtVerdict = T.TrySqrt(disc, out var sqrt);
        if (sqrtVerdict != StrictVerdict.StrictlyTrue)
        {
            // 有实根，但当前数字类型表示不了；不是“无交点”。
            return Result<GenericConicLineResult<T>, GeometryError>.Fail(GeometryError.Unsupported);
        }

        var p1 = LinearCombination(-b + sqrt, u, a, v);
        var p2 = LinearCombination(-b - sqrt, u, a, v);
        return Result<GenericConicLineResult<T>, GeometryError>.Ok(new(
            LineConicKind.TwoPoints, p1, p2, disc));
    }

    private static Primal3<T> LinearCombination<T>(T alpha, Primal3<T> u, T beta, Primal3<T> v)
        where T : IExactRing<T> =>
        new(alpha * u.X + beta * v.X, alpha * u.Y + beta * v.Y, alpha * u.W + beta * v.W);
}
