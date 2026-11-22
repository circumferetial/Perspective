using Perspective.Core.Math;

namespace Perspective.Core.Geometry;

/// <summary>
/// 齐次二维点 (X, Y, W)
/// 当 W != 0 时，对应直角坐标 (X/W, Y/W)
/// 当 W == 0 时，代表平行线的无穷远交点（方向向量）
/// </summary>
public readonly record struct HPoint(ExactScalar X, ExactScalar Y, ExactScalar W)
{
    public static HPoint FromCartesian(ExactScalar x, ExactScalar y) => new(x, y, 1);

    public static HPoint FromFormula(string xFormula, string yFormula) =>
        new(ExactScalar.FromFormula(xFormula), ExactScalar.FromFormula(yFormula), 1);

    public bool IsAtInfinity
    {
        get
        {
            if (W.Exact is not null && W.VerifyStrictZero() == StrictResult.StrictlyTrue)
                return true;

            double maxAbs = System.Math.Max(System.Math.Abs(X.Approx),
                System.Math.Max(System.Math.Abs(Y.Approx), System.Math.Abs(W.Approx)));

            return maxAbs > 0 && System.Math.Abs(W.Approx) <= 1e-9 * maxAbs;
        }
    }

    public (double X, double Y)? ToCartesianApprox() =>
        IsAtInfinity ? null : (X.Approx / W.Approx, Y.Approx / W.Approx);

    /// <summary>
    /// 归一到"最简齐次代表"：有限点除以 W 使 W=1；无穷远点除以 |X|、|Y| 中较大者。
    /// <para>
    /// 【为什么需要】逐层复合的齐次构造（交点、极线、中点…）会不断乘尺度因子，
    /// 精确表达式与 double 近似都会膨胀，导致化简失败或数值溢出。
    /// 在构造出口处归一化可显著缩小表达式树（实测把某真题符号证明从 99s 降到秒级）。
    /// </para>
    /// <para>
    /// 【注意】按 W 归一隐含 W≠0（有限点）；W 的零点是 NDG，不在该表示的定义域内。
    /// </para>
    /// </summary>
    public HPoint Normalized()
    {
        if (!W.IsNumericallyZero())
            return new(X / W, Y / W, 1);

        // 无穷远点：除以绝对值最大的方向分量
        if (System.Math.Abs(X.Approx) >= System.Math.Abs(Y.Approx))
        {
            if (X.IsNumericallyZero()) return this;   // 零向量：保持原样
            return new(1, Y / X, W / X);
        }

        if (Y.IsNumericallyZero()) return this;
        return new(X / Y, 1, W / Y);
    }

    public override string ToString()
    {
        var pt = ToCartesianApprox();
        return pt is null ? $"无穷远点方向({X.Approx:G4}, {Y.Approx:G4})" : $"({pt.Value.X:G6}, {pt.Value.Y:G6})";
    }
}

/// <summary>
/// 齐次二维直线 (A, B, C)，方程为 A*x + B*y + C*w = 0
/// </summary>
public readonly record struct HLine(ExactScalar A, ExactScalar B, ExactScalar C)
{
    /// <summary>
    /// 两点确定一条直线（三维向量的叉积，自动兼容竖直线 x = c，永不除零！）
    /// </summary>
    public static HLine FromPoints(HPoint p1, HPoint p2) => new(
        A: p1.Y * p2.W - p1.W * p2.Y,
        B: p1.W * p2.X - p1.X * p2.W,
        C: p1.X * p2.Y - p1.Y * p2.X
    );

    /// <summary>
    /// 两直线求交点（也是叉积！如果两线平行，会自动算出 W=0 的无穷远点）。
    /// 返回值已归一化为最简齐次代表（见 <see cref="HPoint.Normalized"/>）。
    /// </summary>
    public static HPoint Intersect(HLine l1, HLine l2) => new HPoint(
        X: l1.B * l2.C - l1.C * l2.B,
        Y: l1.C * l2.A - l1.A * l2.C,
        W: l1.A * l2.B - l1.B * l2.A
    );

    /// <summary>
    /// 直线系：lambda * l1 + mu * l2（所有过 l1 和 l2 交点的直线集合）
    /// </summary>
    public static HLine Pencil(HLine l1, HLine l2, ExactScalar lambda, ExactScalar mu) => new(
        lambda * l1.A + mu * l2.A,
        lambda * l1.B + mu * l2.B,
        lambda * l1.C + mu * l2.C
    );

    /// <summary>
    /// 严格检验：点 p 是否在直线上？（代入 A*X + B*Y + C*W 并走严通道验证）
    /// </summary>
    public StrictResult ContainsStrictly(HPoint p)
    {
        ExactScalar residual = A * p.X + B * p.Y + C * p.W;
        return residual.VerifyStrictZero();
    }
    
    /// <summary>已知一点 p 和斜率 k，构造直线（自动兼容 W != 1 的齐次点）</summary>
    public static HLine FromPointAndSlope(HPoint p, ExactScalar slope) => new(
        A: slope * p.W,
        B: -p.W,
        C: p.Y - slope * p.X
    );

    /// <summary>获取直线斜率 k = -A / B；若为竖直线则返回 null</summary>
    public ExactScalar? TryGetSlope() => B.IsNumericallyZero() ? null : -A / B;
}