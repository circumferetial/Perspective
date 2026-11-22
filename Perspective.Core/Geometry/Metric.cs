using Perspective.Core.Math;

namespace Perspective.Core.Geometry;

/// <summary>
/// 度量层：距离 / 点积 / 叉积 / 角度 / 面积。
/// <para>
/// 【与严通道的关系】这些量本身是精确代数式（含根号基因），不是"结论"；
/// 结论仍由 <see cref="ExactScalar.VerifyStrictZero"/> 判定。
/// </para>
/// <para>
/// 【视角】度量不是射影不变量：距离/角度/面积只在 Similarity（或更低）视角有意义，
/// 规则/命题用到这些量时，调用方必须确保当前视角 ≤ <see cref="TransformLevel.Similarity"/>。
/// </para>
/// </summary>
public static class Metric
{
    /// <summary>有限点的仿射坐标差（p − q）。要求两点都不是无穷远点（W ≠ 0）。</summary>
    public static (ExactScalar Dx, ExactScalar Dy) Difference(HPoint p, HPoint q) =>
        (p.X / p.W - q.X / q.W, p.Y / p.W - q.Y / q.W);

    /// <summary>点积 p·q（以原点为共同起点；若要夹角请先做 Difference）。</summary>
    public static ExactScalar Dot(HPoint p, HPoint q)
    {
        var (ax, ay) = Difference(p, HPoint.FromCartesian(0, 0));
        var (bx, by) = Difference(q, HPoint.FromCartesian(0, 0));
        return ax * bx + ay * by;
    }

    /// <summary>二维叉积 p × q = x₁y₂ − y₁x₂（以原点为共同起点）。</summary>
    public static ExactScalar Cross(HPoint p, HPoint q)
    {
        var (ax, ay) = Difference(p, HPoint.FromCartesian(0, 0));
        var (bx, by) = Difference(q, HPoint.FromCartesian(0, 0));
        return ax * by - ay * bx;
    }

    /// <summary>两点距离的平方（精确，无根号）。</summary>
    public static ExactScalar DistanceSquared(HPoint p, HPoint q)
    {
        var (dx, dy) = Difference(p, q);
        return dx * dx + dy * dy;
    }

    /// <summary>两点距离（保留根号基因，如 √5）。</summary>
    public static ExactScalar Distance(HPoint p, HPoint q) => ExactScalar.Sqrt(DistanceSquared(p, q));

    /// <summary>以 p 为顶点、指向 q 的向量。</summary>
    public static (ExactScalar X, ExactScalar Y) Vector(HPoint from, HPoint to) => Difference(to, from);

    /// <summary>
    /// 两向量夹角的 tan（带符号：dot &lt; 0 时为负；dot = 0 表示直角，返回 null）。
    /// 【公式】tan θ = |u × v| / (u · v)。
    /// </summary>
    public static ExactScalar? TanAngle(
        (ExactScalar X, ExactScalar Y) u,
        (ExactScalar X, ExactScalar Y) v)
    {
        var dot = u.X * v.X + u.Y * v.Y;
        if (dot.VerifyStrictZero() == StrictResult.StrictlyTrue) return null;

        var cross = u.X * v.Y - u.Y * v.X;
        return cross.Abs() / dot;
    }

    /// <summary>两向量夹角的 tan（取绝对值，恒非负；dot = 0 时返回 null）。</summary>
    public static ExactScalar? TanAngleUnsigned(
        (ExactScalar X, ExactScalar Y) u,
        (ExactScalar X, ExactScalar Y) v)
    {
        var dot = (u.X * v.X + u.Y * v.Y).Abs();
        if (dot.VerifyStrictZero() == StrictResult.StrictlyTrue) return null;

        var cross = (u.X * v.Y - u.Y * v.X).Abs();
        return cross / dot;
    }

    /// <summary>三角形面积的两倍（= |(B−A) × (C−A)|），精确无根号。</summary>
    public static ExactScalar TriangleArea2(HPoint a, HPoint b, HPoint c)
    {
        var (abx, aby) = Difference(b, a);
        var (acx, acy) = Difference(c, a);
        return (abx * acy - aby * acx).Abs();
    }

    /// <summary>三角形面积（= 面积两倍 / 2，保留根号基因）。</summary>
    public static ExactScalar TriangleArea(HPoint a, HPoint b, HPoint c) =>
        TriangleArea2(a, b, c) / 2;
}
