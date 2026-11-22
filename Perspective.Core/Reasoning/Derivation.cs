namespace Perspective.Core.Reasoning;

/// <summary>
/// 结构化证明记录（**不含任何自然语言**）：某条事实由哪条规则、从哪些前提推出。
/// 解释文本一律由 <see cref="ProofNarrator"/> 从本结构渲染，规则代码不再拼字符串。
/// </summary>
/// <param name="RuleId">规则稳定 id（见 <see cref="RuleIds"/>）；初始/辅助事实用 <see cref="RuleIds.AdHoc"/>。</param>
/// <param name="Fact">被推出的事实。</param>
/// <param name="Premises">推导所依据的前提事实（可为空）。</param>
/// <param name="Note">仅用于"非规则产生"的事实（初始前提、辅助连线）；规则产生的记录应为 null。</param>
public sealed record Derivation(
    string RuleId,
    GeoFact Fact,
    IReadOnlyList<GeoFact> Premises,
    string? Note = null);

/// <summary>
/// 黑箱代数步骤的一等记录：不解析判定器的内部推导，只诚实记录"在什么约束下、
/// 由哪个判定器、得到什么结论"。解释层只能据此描述，不得伪造数学步骤。
/// </summary>
public sealed record OracleStep(
    string Claim,
    IReadOnlyList<string> Constraints,
    string Solver,
    OracleVerdictKind Verdict,
    string? Model = null);

/// <summary>
/// 规则 id 常量：规则与解释层共用的稳定键，避免两边各写一份魔法字符串而漂移。
/// </summary>
public static class RuleIds
{
    /// <summary>非规则产生（初始事实、辅助连线）。</summary>
    public const string AdHoc = "ad-hoc";

    public const string ChordOfContactIsPolar = "chord-of-contact-is-polar";
    public const string PolePolarReciprocity = "pole-polar-reciprocity";
    public const string HarmonicPolarBundle = "harmonic-polar-bundle";
    public const string ConicMidpointSlope = "conic-midpoint-slope";
    public const string PolarFundamental = "polar-fundamental";
    public const string ConjugateDiameterBisectsChord = "conjugate-diameter-bisects-chord";
    public const string CollinearFromCommonLine = "collinear-from-common-line";
    public const string ConcurrentFromCommonPoint = "concurrent-from-common-point";
    public const string Pascal = "pascal";
    public const string CompleteQuadrangleHarmonic = "complete-quadrangle-harmonic";
    public const string Brianchon = "brianchon";

    /// <summary>外部构造（人/LLM/构造搜索提出、机器验收）产生的事实。</summary>
    public const string ExternalConstruction = "external-construction";

    public const string ParallelTransitivity = "parallel-transitivity";

    public const string PerpendicularParallel = "perpendicular-parallel";

    public const string AngleFromParallel = "angle-from-parallel";

    public const string AngleTransitivity = "angle-transitivity";

    public const string AreaRatioTransitivity = "area-ratio-transitivity";

    public const string SegmentRatioTransitivity = "segment-ratio-transitivity";
}
