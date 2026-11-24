using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

/// <summary>对称双线性型 Q: V → V*（纯射影二次曲线 / 配极映射，无任何度量假设）。</summary>
public readonly record struct BilinearForm3(
    ExactScalar Q00,
    ExactScalar Q01,
    ExactScalar Q02,
    ExactScalar Q11,
    ExactScalar Q12,
    ExactScalar Q22)
{
    public Dual3 Polar(Primal3 p) => new(
        Q00 * p.X + Q01 * p.Y + Q02 * p.W,
        Q01 * p.X + Q11 * p.Y + Q12 * p.W,
        Q02 * p.X + Q12 * p.Y + Q22 * p.W);

    public ExactScalar Eval(Primal3 p) => SubspaceOps.Pair(Polar(p), p);

    public DualBilinearForm3 DualForm() => new(
        Q11 * Q22 - Q12 * Q12, Q02 * Q12 - Q01 * Q22, Q01 * Q12 - Q02 * Q11,
        Q00 * Q22 - Q02 * Q02, Q01 * Q02 - Q00 * Q12,
        Q00 * Q11 - Q01 * Q01);
}

/// <summary>对偶空间上的对称双线性型 S: V* → V（切线二次曲线 / 绝对形）。</summary>
public readonly record struct DualBilinearForm3(
    ExactScalar S00,
    ExactScalar S01,
    ExactScalar S02,
    ExactScalar S11,
    ExactScalar S12,
    ExactScalar S22)
{
    public Primal3 Pole(Dual3 l) => new(
        S00 * l.A + S01 * l.B + S02 * l.C,
        S01 * l.A + S11 * l.B + S12 * l.C,
        S02 * l.A + S12 * l.B + S22 * l.C);

    public ExactScalar Eval(Dual3 l) => SubspaceOps.Pair(l, Pole(l));

    public StrictResult IsTangent(Dual3 l) => Eval(l).VerifyStrictZero();
}

/// <summary>把 LinearMap3 对二次曲线的零除法变换放到 Conic 层，保持 SubspaceCore 不感知 Conic。</summary>
public static class ConicTransformExtensions
{
    public static BilinearForm3 Apply(this LinearMap3 map, BilinearForm3 q)
    {
        var c = map.CofactorMatrix();
        var qMat = new LinearMap3(
            q.Q00, q.Q01, q.Q02,
            q.Q01, q.Q11, q.Q12,
            q.Q02, q.Q12, q.Q22);

        var res = c.Multiply(qMat).Multiply(c.Transpose());
        return new BilinearForm3(res.M00, res.M01, res.M02, res.M11, res.M12, res.M22);
    }
}

public enum LineConicKind
{
    TwoPoints,
    Tangent,
    NoRealIntersection,
    WholeLineOnConic
}

public readonly record struct LineConicResult(
    LineConicKind Kind,
    Primal3? P1,
    Primal3? P2,
    ExactScalar Discriminant);

/// <summary>纯射影“直线 ∩ 二次曲线”统计算法（完全不依赖 l∞）。</summary>
public static class ConicIntersection
{
    public static LineConicResult Intersect(Dual3 line, BilinearForm3 conic)
    {
        var (u, v) = SubspaceOps.SpanLine(line);
        var a = conic.Eval(u);
        var b = SubspaceOps.Pair(conic.Polar(u), v);
        var c = conic.Eval(v);

        if (a.IsZeroWithFallback())
        {
            if (b.IsZeroWithFallback())
            {
                return c.IsZeroWithFallback()
                    ? new LineConicResult(LineConicKind.WholeLineOnConic, null, null, ExactScalar.FromInt(0))
                    : new LineConicResult(LineConicKind.Tangent, u, u, ExactScalar.FromInt(0));
            }

            var pOther = c * u - 2 * b * v;
            return new LineConicResult(LineConicKind.TwoPoints, u, pOther, b * b);
        }

        var disc = b * b - a * c;
        if (disc.IsZeroWithFallback())
        {
            var touch = a * v - b * u;
            return new LineConicResult(LineConicKind.Tangent, touch, touch, disc);
        }

        try
        {
            var sqrtDisc = ExactScalar.Sqrt(disc);
            var p1 = (-b + sqrtDisc) * u + a * v;
            var p2 = (-b - sqrtDisc) * u + a * v;
            return new LineConicResult(LineConicKind.TwoPoints, p1, p2, disc);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new LineConicResult(LineConicKind.NoRealIntersection, null, null, disc);
        }
    }

    /// <summary>已知线上一点 knownOnConic 在曲线上时，零根号直接求出另一交点。</summary>
    public static Primal3 OtherIntersection(Dual3 lineThroughP, Primal3 knownOnConic, BilinearForm3 conic)
    {
        var u = SubspaceOps.PickPointOnLineDistinctFrom(lineThroughP, knownOnConic);
        var a = conic.Eval(u);
        var b = SubspaceOps.Pair(conic.Polar(u), knownOnConic);

        var other = -2 * b * u + a * knownOnConic;
        if (SubspaceOps.IsZeroVector(other))
            throw new InvalidOperationException("退化：整条直线都包含在二次曲线内，交点不唯一。");
        return other;
    }
}