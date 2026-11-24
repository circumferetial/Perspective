using AngouriMath;
using Perspective.Core.Math;

namespace Perspective.Core.Geometry;

public enum GeometryProperty
{
    DistanceOrLength, // 长度/距离 -> 仅 Isometry 保持
    Perpendicular,    // 垂直      -> Similarity 及以上保持
    Parallel,         // 平行      -> Affine 及以上保持
    MidpointOrRatio,  // 中点/定比 -> Affine 及以上保持
    Tangency,         // 相切      -> Projective 及以上全部保持！
    CrossRatio        // 交比      -> Projective 及以上全部保持！
}

public static class GeometryAudit
{
    /// <summary>判断在当前变换视角下，某种几何性质是否依然被数学保证有效</summary>
    public static (bool IsPreserved, string Reason) CheckInvariance(TransformLevel currentLevel, GeometryProperty prop)
    {
        TransformLevel required = prop switch
        {
            GeometryProperty.DistanceOrLength => TransformLevel.Isometry,
            GeometryProperty.Perpendicular    => TransformLevel.Similarity,
            GeometryProperty.Parallel         => TransformLevel.Affine,
            GeometryProperty.MidpointOrRatio  => TransformLevel.Affine,
            GeometryProperty.Tangency         => TransformLevel.Projective,
            GeometryProperty.CrossRatio       => TransformLevel.Projective,
            _ => TransformLevel.Isometry
        };

        // 枚举值越小，限制越严格；currentLevel <= required 说明视角没有越界
        bool preserved = currentLevel <= required;
        string msg = preserved
            ? $"[有效] {prop} 在当前 {currentLevel} 视角下恒成立"
            : $"[已失效] {prop} 需要至少 {required} 视角，当前 {currentLevel} 视角不再保证它成立！";

        return (preserved, msg);
    }

    /// <summary>严格检验：两条直线是否垂直（A1*A2 + B1*B2 == 0）</summary>
    public static StrictResult ArePerpendicular(HLine l1, HLine l2) =>
        (l1.A * l2.A + l1.B * l2.B).VerifyStrictZero();

    /// <summary>严格检验：两条直线是否平行（A1*B2 - B1*A2 == 0）</summary>
    public static StrictResult AreParallel(HLine l1, HLine l2) =>
        (l1.A * l2.B - l1.B * l2.A).VerifyStrictZero();

    /// <summary>严格检验：m 是否严格是 a 与 b 的中点（全多项式无除法检验）</summary>
    public static StrictResult IsMidpoint(HPoint m, HPoint a, HPoint b)
    {
        if (m.IsAtInfinity || a.IsAtInfinity || b.IsAtInfinity)
            return StrictResult.StrictlyFalse;

        var rx = 2 * m.X * a.W * b.W - m.W * (a.X * b.W + b.X * a.W);
        var ry = 2 * m.Y * a.W * b.W - m.W * (a.Y * b.W + b.Y * a.W);

        var cx = rx.VerifyStrictZero();
        var cy = ry.VerifyStrictZero();

        if (cx == StrictResult.StrictlyTrue && cy == StrictResult.StrictlyTrue)
            return StrictResult.StrictlyTrue;
        if (cx == StrictResult.StrictlyFalse || cy == StrictResult.StrictlyFalse)
            return StrictResult.StrictlyFalse;
        if (cx == StrictResult.NoSymbolicData || cy == StrictResult.NoSymbolicData)
            return StrictResult.NoSymbolicData;
        return StrictResult.NearZeroUnproven;
    }

    /// <summary>
    /// 【射影终极不变量】计算共线四点 (A, B; C, D) 的交比：
    /// 无论怎么仿射压扁、怎么射影透视（甚至把其中一个点甩到无穷远），这个比值严格不变！
    /// </summary>
    public static ExactScalar CrossRatio(HPoint a, HPoint b, HPoint c, HPoint d)
    {
        // 自动挑一个肯定不在这条直线上的参考点 O（用三个坐标基向量里离直线最远的那个）
        var line = HLine.FromPoints(a, b);
        HPoint[] candidates = [new(0, 0, 1), new(1, 0, 0), new(0, 1, 0)];
        HPoint o = candidates.MaxBy(pt => System.Math.Abs((line.A * pt.X + line.B * pt.Y + line.C * pt.W).Approx));

        static ExactScalar Det3(HPoint p, HPoint q, HPoint r) =>
            p.X * (q.Y * r.W - q.W * r.Y)
          - p.Y * (q.X * r.W - q.W * r.X)
          + p.W * (q.X * r.Y - q.Y * r.X);

        var num = Det3(o, a, c) * Det3(o, b, d);
        var den = Det3(o, b, c) * Det3(o, a, d);
        return (num / den).Simplified();

    }
    /// <summary>
    /// 严格检验面积比：S(A1A2A3)/S(B1B2B3) = value（无向面积，正值）。
    /// 【判据】area1 = value·area2（两句都用面积两倍绝对值，消去 1/2）。
    /// 分母三角形退化（面积恒为 0）时返回 StrictlyFalse。
    /// </summary>
    public static StrictResult IsAreaRatio(
        HPoint a1, HPoint a2, HPoint a3,
        HPoint b1, HPoint b2, HPoint b3,
        ExactScalar value)
    {
        var area1 = Metric.TriangleArea2(a1, a2, a3);
        var area2 = Metric.TriangleArea2(b1, b2, b3);

        if (area2.VerifyStrictZero() == StrictResult.StrictlyTrue) return StrictResult.StrictlyFalse;
        return (area1 - value * area2).VerifyStrictZero();
    }

    /// <summary>
    /// 严格检验线段比：|A1A2|/|B1B2| = value（相似不变量）。
    /// 【判据】用平方形式 |A1A2|² = value²·|B1B2|²，无根号；分母线段退化时返回 StrictlyFalse。
    /// </summary>
    public static StrictResult IsSegmentRatio(
        HPoint a1, HPoint a2, HPoint b1, HPoint b2, ExactScalar value)
    {
        // 无穷远点没有欧氏长度（DistanceSquared 会除零），必须先拦，不能抛异常。
        if (a1.IsAtInfinity || a2.IsAtInfinity || b1.IsAtInfinity || b2.IsAtInfinity)
            return StrictResult.StrictlyFalse;

        // 线段比非负；只在"确定是负常数"时拒绝，符号未定的符号参数不误杀。
        if (IsCertainlyNegative(value) == StrictResult.StrictlyTrue)
            return StrictResult.StrictlyFalse;

        var squared1 = Metric.DistanceSquared(a1, a2);
        var squared2 = Metric.DistanceSquared(b1, b2);

        if (squared2.VerifyStrictZero() == StrictResult.StrictlyTrue) return StrictResult.StrictlyFalse;
        return (squared1 - value * value * squared2).VerifyStrictZero();
    }

    /// <summary>value 是否"确定是负数"：纯近似或数值常数按符号判；符号表达式未定时返回 NearZeroUnproven（不拒绝）。</summary>
    private static StrictResult IsCertainlyNegative(ExactScalar value)
    {
        if (value.Exact is null)
            return value.Approx < 0 ? StrictResult.StrictlyTrue : StrictResult.StrictlyFalse;

        if (value.Exact.Simplify() is Entity.Number)
            return value.Approx < 0 ? StrictResult.StrictlyTrue : StrictResult.StrictlyFalse;

        return StrictResult.NearZeroUnproven;
    }

    /// <summary>严格检验交比：(A,B;C,D) = value（射影不变量）。</summary>
    public static StrictResult IsCrossRatio(
        HPoint a, HPoint b, HPoint c, HPoint d, ExactScalar value) =>
        (CrossRatio(a, b, c, d) - value).VerifyStrictZero();

    /// <summary>
    /// 严格检验"有向角相等（mod π）"：θ(a1,a2) ≡ sign·θ(b1,b2)。
    /// 【判据】tan θ = cross/dot；θ₁ = sign·θ₂ ⇔ cross₁·dot₂ − sign·cross₂·dot₁ = 0
    /// （tan 为奇函数；直角时 dot=0，判据仍然正确：一侧直角另一侧非直角时不可能成立）。
    /// </summary>
    public static StrictResult AreAngleEqual(HLine a1, HLine a2, HLine b1, HLine b2, int sign)
    {
        // 退化直线（A=B=0：无穷远线或零直线）没有方向向量，任何角都无从谈起。
        foreach (var line in new[] { a1, a2, b1, b2 })
        {
            var degeneracy = DegenerateDirection(line);
            if (degeneracy == StrictResult.StrictlyTrue) return StrictResult.StrictlyFalse;
            if (degeneracy == StrictResult.NoSymbolicData) return StrictResult.NoSymbolicData;
        }

        static (ExactScalar Cross, ExactScalar Dot) TanPair(HLine x, HLine y)
        {
            var (x1, x2) = (-x.B, x.A);   // 方向向量 (−B, A)
            var (y1, y2) = (-y.B, y.A);
            return (x1 * y2 - x2 * y1, x1 * y1 + x2 * y2);
        }

        var (crossA, dotA) = TanPair(a1, a2);
        var (crossB, dotB) = TanPair(b1, b2);
        return (crossA * dotB - sign * crossB * dotA).VerifyStrictZero();
    }

    /// <summary>直线方向向量 (−B, A) 是否确定为零：两个分量的严判零联合。</summary>
    private static StrictResult DegenerateDirection(HLine line)
    {
        var az = line.A.VerifyStrictZero();
        var bz = line.B.VerifyStrictZero();

        if (az == StrictResult.StrictlyTrue && bz == StrictResult.StrictlyTrue) return StrictResult.StrictlyTrue;
        if (az == StrictResult.NoSymbolicData || bz == StrictResult.NoSymbolicData) return StrictResult.NoSymbolicData;
        return StrictResult.StrictlyFalse;
    }

    /// <summary>严格检验共线四点是否构成调和点列：交比 (A, B; C, D) == -1</summary>
    public static StrictResult IsHarmonic(HPoint a, HPoint b, HPoint c, HPoint d)
    {
        // 四点必须互异：重合时交比无定义，不能断言 -1
        if (PointTools.Same(a, b) || PointTools.Same(a, c) || PointTools.Same(a, d) ||
            PointTools.Same(b, c) || PointTools.Same(b, d) || PointTools.Same(c, d))
            return StrictResult.StrictlyFalse;

        var line = HLine.FromPoints(a, b);
        HPoint[] candidates = [new(0, 0, 1), new(1, 0, 0), new(0, 1, 0)];
        HPoint o = candidates.MaxBy(pt => System.Math.Abs((line.A * pt.X + line.B * pt.Y + line.C * pt.W).Approx));

        static ExactScalar Det3(HPoint p, HPoint q, HPoint r) =>
            p.X * (q.Y * r.W - q.W * r.Y)
            - p.Y * (q.X * r.W - q.W * r.X)
            + p.W * (q.X * r.Y - q.Y * r.X);

        var num = Det3(o, a, c) * Det3(o, b, d);
        var den = Det3(o, b, c) * Det3(o, a, d);

        // 交比无定义（den == 0，例如退化配置或某点落在无穷远）时，不能断言 -1
        var denRes = den.VerifyStrictZero();
        if (denRes == StrictResult.StrictlyTrue)
            return StrictResult.StrictlyFalse;
        if (denRes == StrictResult.NoSymbolicData)
            return StrictResult.NoSymbolicData;

        return (num + den).VerifyStrictZero(); // num / den == -1 等价于 num + den == 0
    }
}