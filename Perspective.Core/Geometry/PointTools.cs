using Perspective.Core.Math;

namespace Perspective.Core.Geometry;

/// <summary>
/// 点与点之间的通用判定工具（严格通道）。
/// 与 <see cref="GeometryAudit"/> 的分工：GeometryAudit 管"几何性质在哪个视角下保持"，
/// 本类只做纯粹的坐标级谓词，不含埃尔朗根等级语义。
/// </summary>
public static class PointTools
{
    /// <summary>
    /// 两个齐次点是否代表同一个射影点。
    /// 【判据】叉积 (p × q) 的三个分量全为零 ⟺ 齐次坐标成比例。
    /// 【说明】数值快判（容差 1e-9），用于去重/守卫；需要严格结论请用 <see cref="SameStrictly"/>。
    /// </summary>
    public static bool Same(HPoint p, HPoint q) =>
        (p.Y * q.W - p.W * q.Y).IsNumericallyZero()
        && (p.W * q.X - p.X * q.W).IsNumericallyZero()
        && (p.X * q.Y - p.Y * q.X).IsNumericallyZero();

    /// <summary>
    /// 严格版：两个齐次点是否重合。
    /// 【结论】三个叉积分量都严格为零 ⟺ 同一射影点。
    /// </summary>
    public static StrictResult SameStrictly(HPoint p, HPoint q)
    {
        var cx = (p.Y * q.W - p.W * q.Y).VerifyStrictZero();
        var cy = (p.W * q.X - p.X * q.W).VerifyStrictZero();
        var cz = (p.X * q.Y - p.Y * q.X).VerifyStrictZero();
        return Combine(cx, cy, cz);
    }

    /// <summary>
    /// 三点是否共线（严格通道）。
    /// 【判据】齐次行列式 det[p, q, r] = 0 ⟺ 三点共线（对无穷远点同样成立）。
    /// </summary>
    public static StrictResult Collinear(HPoint p, HPoint q, HPoint r)
    {
        var det = p.X * (q.Y * r.W - q.W * r.Y)
                - p.Y * (q.X * r.W - q.W * r.X)
                + p.W * (q.X * r.Y - q.Y * r.X);
        return det.VerifyStrictZero();
    }

    /// <summary>三个分量的"与"：全真才真，任一假即假，缺符号数据则降级。</summary>
    private static StrictResult Combine(StrictResult a, StrictResult b, StrictResult c)
    {
        if (a == StrictResult.StrictlyFalse || b == StrictResult.StrictlyFalse || c == StrictResult.StrictlyFalse)
            return StrictResult.StrictlyFalse;
        if (a == StrictResult.NoSymbolicData || b == StrictResult.NoSymbolicData || c == StrictResult.NoSymbolicData)
            return StrictResult.NoSymbolicData;
        if (a == StrictResult.StrictlyTrue && b == StrictResult.StrictlyTrue && c == StrictResult.StrictlyTrue)
            return StrictResult.StrictlyTrue;
        return StrictResult.NearZeroUnproven;
    }
}
