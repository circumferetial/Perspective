using Perspective.Core.Math;

namespace Perspective.Core.Geometry;

/// <summary>
/// 圆锥曲线相关的通用判定工具（严格通道）。
/// 点是否在曲线上已有 <see cref="Conic.ContainsStrictly"/>，本类补"曲线—直线"的组合谓词，
/// 供定理规则与裁决器共用，避免同一条判据在多处重复实现而漂移。
/// </summary>
public static class ConicTools
{
    /// <summary>
    /// 直线是否严格切于圆锥曲线。
    /// 【判据】判别式 lᵀ·adj(C)·l 严格为零 ⟺ 相切（符号约定见 <see cref="Conic.CheckLineRelation"/>）。
    /// 【返回】相切 → StrictlyTrue；确定为相交/相离 → StrictlyFalse；判别式无法严格化简 → 原样降级。
    /// </summary>
    public static StrictResult IsTangent(Conic conic, HLine line)
    {
        var check = conic.CheckLineRelation(line);
        if (check.Relation == LineConicRelation.DegenerateConic)
            return StrictResult.StrictlyFalse;
        if (check.Certainty == StrictResult.StrictlyTrue)
            return check.Relation == LineConicRelation.Tangent
                ? StrictResult.StrictlyTrue
                : StrictResult.StrictlyFalse; // 确知是相交/相离 => 确定不相切
        if (check.Certainty == StrictResult.StrictlyFalse)
            return StrictResult.StrictlyFalse;
        return check.Certainty; // NearZeroUnproven / NoSymbolicData
    }

    /// <summary>
    /// 直线 line 是否恰为点 pole 关于 conic 的极线。
    /// 【判据】line 与 C·pole 成比例 ⟺ 两者叉积全为零（<see cref="LineTools.Same"/>）。
    /// 【用途】定理规则回验 PolarOfFact 标签、裁决器验证 ClaimPolarOf，共用同一判据。
    /// </summary>
    public static StrictResult IsPolarLine(Conic conic, HPoint pole, HLine line) =>
        LineTools.Same(conic.PolarLine(pole), line);
}
