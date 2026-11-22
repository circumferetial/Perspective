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
        RuleIds.HarmonicPolarBundle => RenderHarmonicBundle(derivation),
        RuleIds.ConicMidpointSlope => RenderMidpointSlope(derivation),
        RuleIds.PolarFundamental => RenderPolarFundamental(derivation),
        RuleIds.ConjugateDiameterBisectsChord => RenderConjugateDiameter(derivation),
        RuleIds.ConcurrentFromCommonPoint => RenderConcurrent(derivation),
        RuleIds.CompleteQuadrangleHarmonic => RenderQuadrangle(derivation),
        RuleIds.Brianchon => RenderBrianchon(derivation),
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

    private static string RenderHarmonicBundle(Derivation d)
    {
        if (d.Fact is not HarmonicFact harmonic) return Fallback(d);
        var polar = d.Premises.OfType<PolarOfFact>().FirstOrDefault();
        if (polar is null) return Fallback(d);

        // 割线 = 过极点、但不是极线本身的那条线
        var secant = d.Premises.OfType<OnLineFact>()
            .FirstOrDefault(x => x.PointId == polar.PoleId && x.LineId != polar.LineId);

        string secDesc = secant is null ? "" : $"割线 {secant.LineId} ";
        return $"{secDesc}过 {polar.PoleId}，交极线 {polar.LineId} 于 {harmonic.BId}、"
             + $"交曲线于 {harmonic.CId}、{harmonic.DId}，故 ({harmonic.AId},{harmonic.BId};{harmonic.CId},{harmonic.DId}) = -1";
    }

    private static string RenderMidpointSlope(Derivation d)
    {
        var midpoint = d.Premises.OfType<MidpointFact>().FirstOrDefault();
        string chord = midpoint is null ? "AB" : $"{midpoint.AId}{midpoint.BId}";

        return d.Fact switch
        {
            ConjugateDirectionsFact conj =>
                $"{midpoint?.MidId} 是弦 {chord} 的中点，故弦方向与 {conj.Line1Id} 关于曲线二次部分共轭",
            ConstantSlopeProductFact slope =>
                $"{midpoint?.MidId} 是弦 {chord} 的中点，故斜率之积 k({slope.Line1Id}) · k({slope.Line2Id}) = {slope.Value}（= −M₀₀/M₁₁）",
            _ => Fallback(d)
        };
    }

    private static string RenderPolarFundamental(Derivation d)
    {
        if (d.Fact is not TangentFact tangent) return Fallback(d);
        return $"{tangent.TouchPointId} 在曲线 {tangent.ConicId} 上，其极线 {tangent.LineId} 就是过该点的切线";
    }

    private static string RenderConjugateDiameter(Derivation d)
    {
        if (d.Fact is not MidpointFact midpoint) return Fallback(d);
        var conj = d.Premises.OfType<ConjugateDirectionsFact>().FirstOrDefault();
        if (conj is null) return Fallback(d);

        return $"弦 {midpoint.AId}{midpoint.BId} 与直径 {conj.Line1Id} 共轭，其交点 {midpoint.MidId} 即为该弦的中点";
    }

    private static string RenderConcurrent(Derivation d)
    {
        var onLines = d.Premises.OfType<OnLineFact>().ToList();
        if (onLines.Count < 3) return Fallback(d);

        string lines = string.Join("、", onLines.Take(3).Select(x => x.LineId));
        return $"{lines} 同过点 {onLines[0].PointId}，故三线共点";
    }

    private static string RenderQuadrangle(Derivation d)
    {
        if (d.Fact is not HarmonicFact harmonic) return Fallback(d);
        return $"由完全四点形的调和性质，边 {harmonic.AId}{harmonic.BId} 上 "
             + $"({harmonic.AId},{harmonic.BId};{harmonic.CId},{harmonic.DId}) = -1";
    }

    private static string RenderBrianchon(Derivation d)
    {
        var tangents = d.Premises.OfType<TangentFact>().ToList();
        if (tangents.Count < 6 || d.Fact is not ConcurrentFact concurrent) return Fallback(d);

        string lines = string.Join("、", tangents.Take(6).Select(x => x.LineId));
        return $"六边形外切于曲线 {tangents[0].ConicId}（边 {lines}），由 Brianchon 定理，"
             + $"三条主对角线 {concurrent.L1Id}、{concurrent.L2Id}、{concurrent.L3Id} 共点";
    }

    /// <summary>
    /// 渲染黑箱代数步骤：**只诚实描述"经判定器验证"，绝不编造数学步骤**。
    /// </summary>
    public static string RenderOracle(OracleStep step)
    {
        string constraints = step.Constraints.Count == 0
            ? "无额外约束"
            : string.Join("；", step.Constraints);

        return step.Verdict switch
        {
            OracleVerdictKind.Unsat =>
                $"在约束（{constraints}）下，由实代数判定器 {step.Solver} 验证不存在反例，故「{step.Claim}」成立",
            OracleVerdictKind.Sat =>
                $"在约束（{constraints}）下存在反例（{step.Model}），「{step.Claim}」不成立",
            _ =>
                $"实代数判定器 {step.Solver} 无法判定「{step.Claim}」（约束：{constraints}）"
        };
    }

    private static string Fallback(Derivation d) =>
        d.Note ?? "（推导记录不完整，无法渲染解释）";
}
