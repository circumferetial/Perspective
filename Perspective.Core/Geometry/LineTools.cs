using Perspective.Core.Math;

namespace Perspective.Core.Geometry;

/// <summary>
/// 直线与直线之间的通用判定工具（严格通道）。
/// 点是否在线上已有 <see cref="HLine.ContainsStrictly"/>，本类只补"线—线"关系。
/// </summary>
public static class LineTools
{
    /// <summary>
    /// 两条齐次直线是否重合（严格通道）。
    /// 【判据】叉积 (l₁ × l₂) 三个分量全为零 ⟺ 系数向量成比例 ⟺ 同一条直线。
    /// 【注意】不能用"两点都在另一条线上"代替：那只能证共线，不能证同一条线。
    /// </summary>
    public static StrictResult Same(HLine a, HLine b)
    {
        var cx = (a.B * b.C - a.C * b.B).VerifyStrictZero();
        var cy = (a.C * b.A - a.A * b.C).VerifyStrictZero();
        var cz = (a.A * b.B - a.B * b.A).VerifyStrictZero();

        if (cx == StrictResult.StrictlyFalse || cy == StrictResult.StrictlyFalse || cz == StrictResult.StrictlyFalse)
            return StrictResult.StrictlyFalse;
        if (cx == StrictResult.NoSymbolicData || cy == StrictResult.NoSymbolicData || cz == StrictResult.NoSymbolicData)
            return StrictResult.NoSymbolicData;
        if (cx == StrictResult.StrictlyTrue && cy == StrictResult.StrictlyTrue && cz == StrictResult.StrictlyTrue)
            return StrictResult.StrictlyTrue;
        return StrictResult.NearZeroUnproven;
    }

    /// <summary>严格版便利封装：两条直线是否确实重合。</summary>
    public static bool IsSame(HLine a, HLine b) => Same(a, b) == StrictResult.StrictlyTrue;
}
