namespace Perspective.Core.Reasoning;

/// <summary>
/// 解释层：把结构化的 <see cref="Derivation"/> 渲染成人类可读文本。
/// <para>
/// 与计算层的边界：计算结构（事实、证明记录）不携带自然语言；文案全部集中在这里，
/// 换措辞、换语言、换格式（如 LaTeX）都只改本文件，不动任何规则代码。
/// </para>
/// <para>
/// 目前只为已迁移的 4 条规则（切点弦极线、La Hire、同线三点共线、Pascal）提供模板；
/// 其余规则仍以 <see cref="RuleIds.AdHoc"/> 记录，回退到 <see cref="Derivation.Note"/>。
/// </para>
/// </summary>
public static class ProofNarrator
{
    /// <summary>把一条证明记录渲染成中文解释。</summary>
    public static string Render(FactBase facts, Derivation derivation) => derivation.RuleId switch
    {
        RuleIds.ChordOfContactIsPolar => RenderChordOfContact(derivation),
        RuleIds.PolePolarReciprocity => RenderLaHire(derivation),
        RuleIds.CollinearFromCommonLine => RenderCommonLine(derivation),
        RuleIds.Pascal => RenderPascal(derivation),
        _ => derivation.Note ?? "（规则已记录，但解释层尚未提供模板）"
    };

    /// <summary>渲染某条事实的推导出处；事实只作为初始条件登记时返回固定说明。</summary>
    public static string? RenderFact(FactBase facts, GeoFact fact)
    {
        var d = facts.DerivationLog.FirstOrDefault(x => x.Fact == fact);
        return d is null ? "来自初始前提事实" : Render(facts, d);
    }

    private static string RenderChordOfContact(Derivation d)
    {
        if (d.Fact is not PolarOfFact polar) return Fallback(d);
        var tangents = d.Premises.OfType<TangentFact>().ToList();
        if (tangents.Count < 2) return Fallback(d);

        return $"两切线 {tangents[0].LineId}、{tangents[1].LineId} 相切于曲线 {polar.ConicId} 且交于 {polar.PoleId}，"
             + $"故切点弦 {polar.LineId} 就是 {polar.PoleId} 的极线";
    }

    private static string RenderLaHire(Derivation d)
    {
        if (d.Fact is not OnLineFact onLine) return Fallback(d);

        var polars = d.Premises.OfType<PolarOfFact>().ToList();
        var pPolar = polars.FirstOrDefault(p => p.PoleId == onLine.PointId);
        var qPolar = polars.FirstOrDefault(p => p.LineId == onLine.LineId);
        if (pPolar is null || qPolar is null) return Fallback(d);

        return $"因 {qPolar.PoleId} 在 {pPolar.PoleId} 的极线 {pPolar.LineId} 上，"
             + $"由配极互反（La Hire）得 {qPolar.PoleId} 的极线 {qPolar.LineId} 必过 {pPolar.PoleId}";
    }

    private static string RenderCommonLine(Derivation d)
    {
        if (d.Fact is not CollinearFact collinear) return Fallback(d);
        var onLine = d.Premises.OfType<OnLineFact>().FirstOrDefault();
        string where = onLine is null ? "" : $"同在直线 {onLine.LineId} 上，";
        return $"{collinear.P1Id}、{collinear.P2Id}、{collinear.P3Id} {where}故三点共线";
    }

    private static string RenderPascal(Derivation d)
    {
        if (d.Fact is not CollinearFact collinear) return Fallback(d);
        var onConic = d.Premises.OfType<OnConicFact>().ToList();
        if (onConic.Count < 6) return Fallback(d);

        string hexagon = string.Concat(onConic.Take(6).Select(x => x.PointId));
        return $"六边形 {hexagon} 内接于曲线 {onConic[0].ConicId}，由 Pascal 定理，"
             + $"三组对边交点 {collinear.P1Id}、{collinear.P2Id}、{collinear.P3Id} 共线";
    }

    private static string Fallback(Derivation d) =>
        d.Note ?? "（推导记录不完整，无法渲染解释）";
}
