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
    /// <summary>严格检验共线四点是否构成调和点列：交比 (A, B; C, D) == -1</summary>
    public static StrictResult IsHarmonic(HPoint a, HPoint b, HPoint c, HPoint d)
    {
        // 四点必须互异：重合时交比无定义，不能断言 -1
        if (SamePoint(a, b) || SamePoint(a, c) || SamePoint(a, d) ||
            SamePoint(b, c) || SamePoint(b, d) || SamePoint(c, d))
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

    /// <summary>两个齐次点是否代表同一个射影点（叉积分量全为 0）</summary>
    private static bool SamePoint(HPoint p, HPoint q)
    {
        var cx = p.Y * q.W - p.W * q.Y;
        var cy = p.W * q.X - p.X * q.W;
        var cw = p.X * q.Y - p.Y * q.X;
        return cx.IsNumericallyZero() && cy.IsNumericallyZero() && cw.IsNumericallyZero();
    }
}