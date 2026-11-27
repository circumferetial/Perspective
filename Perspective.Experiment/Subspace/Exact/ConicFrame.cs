using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

/// <summary>
///     圆锥曲线参数 t 的 Möbius 框架：把三个互异参数送到 0、1、∞。
///     目前只实现标准抛物线 y²=xz 的参数化 P(t)=(t²,t,1)。
///     完整 PGL(2) 群作用引擎的边界见 docs/pgl2-automorphism.md。
/// </summary>
public readonly record struct ConicFrame(ExactScalar A, ExactScalar B, ExactScalar C, ExactScalar D)
{
    public ExactScalar Determinant => A * D - B * C;

    public bool IsInvertible => Determinant.VerifyStrictZero() != StrictResult.StrictlyTrue;

    /// <summary>this ∘ other（先作用 other，再作用 this）。</summary>
    public Result<ConicFrame, GeometryError> Compose(ConicFrame other)
    {
        if (!IsInvertible || !other.IsInvertible)
            return Result<ConicFrame, GeometryError>.Fail(GeometryError.UndefinedParameter);

        return Result<ConicFrame, GeometryError>.Ok(new ConicFrame(
            A * other.A + B * other.C,
            A * other.B + B * other.D,
            C * other.A + D * other.C,
            C * other.B + D * other.D));
    }

    public Result<ConicFrame, GeometryError> Inverse()
    {
        if (!IsInvertible)
            return Result<ConicFrame, GeometryError>.Fail(GeometryError.UndefinedParameter);

        return Result<ConicFrame, GeometryError>.Ok(new ConicFrame(D, -B, -C, A));
    }

    /// <summary>由三个互异参数构造 t ↦ (At+B)/(Ct+D)，使 t0→0、t1→1、t2→∞。</summary>
    public static Result<ConicFrame, GeometryError> FromThreeParameters(
        ExactScalar t0,
        ExactScalar t1,
        ExactScalar t2)
    {
        if (IsZero(t0 - t1) || IsZero(t1 - t2) || IsZero(t0 - t2))
            return Result<ConicFrame, GeometryError>.Fail(GeometryError.CoincidentPoints);

        var a = t1 - t2;
        var c = t1 - t0;
        return Result<ConicFrame, GeometryError>.Ok(new ConicFrame(
            a,
            -t0 * a,
            c,
            -t2 * c));
    }

    public Result<ExactScalar, GeometryError> ApplyParameter(ExactScalar t)
    {
        var den = C * t + D;
        if (IsZero(den))
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.AtInfinity);

        return Result<ExactScalar, GeometryError>.Ok((A * t + B) / den);
    }

    /// <summary>作用于标准抛物线 y²=xz 上的点；只接受有限参数点。</summary>
    public Result<Primal3<ExactScalar>, GeometryError> ApplyPoint(Primal3<ExactScalar> p)
    {
        if (p.W.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.AtInfinity);

        var t = p.Y / p.W;
        var applied = ApplyParameter(t);
        if (!applied.TryGet(out var s))
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(applied.Error);

        return Result<Primal3<ExactScalar>, GeometryError>.Ok(
            new Primal3<ExactScalar>(s * s, s, ExactScalar.One));
    }

    private static bool IsZero(ExactScalar x) =>
        x.VerifyStrictZero() == StrictResult.StrictlyTrue;
}
