using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

// ============================================================================
// 1. 待证命题陈述（GeoClaim）—— 绝不直接放进 FactBase.Facts，避免循环论证！
// ============================================================================

public abstract record GeoClaim
{
    /// <summary>该命题在埃尔朗根纲领下有意义的最低视角要求</summary>
    public abstract TransformLevel RequiredLevel { get; }
}

public sealed record ClaimOnLine(string PointId, string LineId) : GeoClaim
{
    public override TransformLevel RequiredLevel => TransformLevel.Projective;
}

public sealed record ClaimOnConic(string PointId, string ConicId) : GeoClaim
{
    public override TransformLevel RequiredLevel => TransformLevel.Projective;
}

public sealed record ClaimTangent(string LineId, string ConicId) : GeoClaim
{
    public override TransformLevel RequiredLevel => TransformLevel.Projective;
}

public sealed record ClaimPolarOf(string LineId, string PoleId, string ConicId) : GeoClaim
{
    public override TransformLevel RequiredLevel => TransformLevel.Projective;
}

public sealed record ClaimHarmonic(string AId, string BId, string CId, string DId) : GeoClaim
{
    public override TransformLevel RequiredLevel => TransformLevel.Projective;
}

public sealed record ClaimPerpendicular(string Line1Id, string Line2Id) : GeoClaim
{
    public override TransformLevel RequiredLevel => TransformLevel.Similarity;
}

public sealed record ClaimParallel(string Line1Id, string Line2Id) : GeoClaim
{
    public override TransformLevel RequiredLevel => TransformLevel.Affine;
}

public sealed record ClaimCollinear(string P1Id, string P2Id, string P3Id) : GeoClaim
{
    public override TransformLevel RequiredLevel => TransformLevel.Projective;
}

// ============================================================================
// 2. 裁决结果与“判断表述是否为真”的链接核心（ProofJudge）
// ============================================================================

/// <summary>
/// 裁决档位。
/// 【边界说明】<see cref="TrueByAlgebraOnly"/> 的含义是"**当前坐标实现**满足该命题"，
/// 不等于"从声明的前提事实可推出"——它绕过了事实库，直接对坐标做严通道核算。
/// 因此若场景前提本身有误，预言机仍会判真；需要"从前提可证"时请认
/// <see cref="ProvedByTheoremChain"/>。
/// </summary>
public enum ClaimVerdict
{
    ProvedByTheoremChain, // 命题库前向链直接推出（逻辑可解释！）
    TrueByAlgebraOnly,    // 定理库未覆盖该路径，但底层代数严通道证实为真
    StrictlyFalse,        // 代数严通道确定证伪
    InvalidInCurrentView, // 当前视角不保持该性质（例如射影视角问垂直）
    Unverified            // 缺精确符号基因或符号树未能化简
}

public sealed record JudgmentReport(ClaimVerdict Verdict, string Explanation);

public static class ProofJudge
{
    /// <summary>
    /// 【真伪裁决链接】在给定的已知前提库和定理引擎下，严格判断一条待证命题是否为真
    /// </summary>
    public static JudgmentReport Evaluate(FactBase facts, TheoremEngine engine, GeoClaim claim)
    {
        // 第 1 关：埃尔朗根视角前提检查
        if (facts.CurrentLevel > claim.RequiredLevel)
        {
            return new JudgmentReport(
                ClaimVerdict.InvalidInCurrentView,
                $"视角不兼容：当前处于 {facts.CurrentLevel} 视角，而待证命题至少需要 {claim.RequiredLevel} 视角才有意义。");
        }

        // 第 2 关：先让定理引擎推演到不动点（在不污染待证命题的前提下穷尽已知事实）
        engine.RunToFixedPoint(facts);

        // 第 3 关：检查待证命题是否已被定理库推演命中（逻辑证明路径）
        var logicalHit = TryFindInFactBase(facts, claim);
        if (logicalHit is not null)
        {
            return new JudgmentReport(
                ClaimVerdict.ProvedByTheoremChain,
                $"【定理链已证】{logicalHit}");
        }

        // 第 4 关：定理库没推出来，启动“代数严通道预言机”直接核算真伪！
        var strictCheck = VerifyByAlgebraOracle(facts, claim);
        return strictCheck switch
        {
            StrictResult.StrictlyTrue => new JudgmentReport(
                ClaimVerdict.TrueByAlgebraOnly,
                "【代数严证为真】当前命题库暂未包含推导它的规则链，但底层符号代数已严格证实该表述成立！"),

            StrictResult.StrictlyFalse => new JudgmentReport(
                ClaimVerdict.StrictlyFalse,
                "【严通道证伪】该表述在数学上严格不成立！"),

            _ => new JudgmentReport(
                ClaimVerdict.Unverified,
                $"【无法严格判定】状态：{strictCheck}（可能使用了纯近似拖拽坐标，或含高阶复杂公式）。")
        };
    }

    private static string? TryFindInFactBase(FactBase facts, GeoClaim claim)
    {
        // 用事实库的类型视图做声明式匹配（不再对全表 OfType 扫描）
        GeoFact? matched = claim switch
        {
            ClaimOnLine c => facts.OnLines.FirstOrDefault(f => f == new OnLineFact(c.PointId, c.LineId)),
            ClaimOnConic c => facts.OnConics.FirstOrDefault(f => f == new OnConicFact(c.PointId, c.ConicId)),
            ClaimTangent c => facts.Tangents.FirstOrDefault(f => f.LineId == c.LineId && f.ConicId == c.ConicId),
            ClaimPolarOf c => facts.Polars.FirstOrDefault(f => f == new PolarOfFact(c.LineId, c.PoleId, c.ConicId)),
            ClaimHarmonic c => facts.Harmonics.FirstOrDefault(hf =>
                ((hf.AId == c.AId && hf.BId == c.BId) || (hf.AId == c.BId && hf.BId == c.AId)) &&
                ((hf.CId == c.CId && hf.DId == c.DId) || (hf.CId == c.DId && hf.DId == c.CId))),
            // 以下三种命题形态目前**没有**对应的事实类型（FactBase 尚无
            // CollinearFact / PerpendicularFact / ParallelFact），所以不可能通过定理链命中，
            // 只能落到代数预言机。一旦加入对应事实类型，必须在这里补映射，
            // 否则会出现"规则推得出来、裁决器却看不见"的静默缺口。
            ClaimCollinear => null,
            ClaimPerpendicular => null,
            ClaimParallel => null,
            _ => null
        };

        if (matched is null) return null;
        return facts.DerivationLog.FirstOrDefault(x => x.Fact == matched).Trace ?? "来自初始前提事实";
    }

    private static StrictResult VerifyByAlgebraOracle(FactBase facts, GeoClaim claim)
    {
        switch (claim)
        {
            case ClaimOnLine c:
                return (facts.Points.TryGetValue(c.PointId, out var ptL) && facts.Lines.TryGetValue(c.LineId, out var ln))
                    ? ln.ContainsStrictly(ptL)
                    : StrictResult.NoSymbolicData;

            case ClaimOnConic c:
                return (facts.Points.TryGetValue(c.PointId, out var ptC) && facts.Conics.TryGetValue(c.ConicId, out var cn))
                    ? cn.ContainsStrictly(ptC)
                    : StrictResult.NoSymbolicData;

            case ClaimTangent c:
                if (!facts.Lines.TryGetValue(c.LineId, out var tLine) || !facts.Conics.TryGetValue(c.ConicId, out var tConic))
                    return StrictResult.NoSymbolicData;
                return ConicTools.IsTangent(tConic, tLine);

            case ClaimPolarOf c:
                if (!facts.Lines.TryGetValue(c.LineId, out var lTarget) ||
                    !facts.Points.TryGetValue(c.PoleId, out var polePt) ||
                    !facts.Conics.TryGetValue(c.ConicId, out var pConic))
                    return StrictResult.NoSymbolicData;

                // 验证 C*P 与给定直线严格成比例（判据收敛在 ConicTools.IsPolarLine）
                return ConicTools.IsPolarLine(pConic, polePt, lTarget);

            case ClaimHarmonic c:
                if (!facts.Points.TryGetValue(c.AId, out var pa) || !facts.Points.TryGetValue(c.BId, out var pb) ||
                    !facts.Points.TryGetValue(c.CId, out var pc) || !facts.Points.TryGetValue(c.DId, out var pd))
                    return StrictResult.NoSymbolicData;
                return GeometryAudit.IsHarmonic(pa, pb, pc, pd);

            case ClaimPerpendicular c:
                return (facts.Lines.TryGetValue(c.Line1Id, out var lp1) && facts.Lines.TryGetValue(c.Line2Id, out var lp2))
                    ? GeometryAudit.ArePerpendicular(lp1, lp2)
                    : StrictResult.NoSymbolicData;

            case ClaimParallel c:
                return (facts.Lines.TryGetValue(c.Line1Id, out var lr1) && facts.Lines.TryGetValue(c.Line2Id, out var lr2))
                    ? GeometryAudit.AreParallel(lr1, lr2)
                    : StrictResult.NoSymbolicData;

            case ClaimCollinear c:
                if (!facts.Points.TryGetValue(c.P1Id, out var c1) ||
                    !facts.Points.TryGetValue(c.P2Id, out var c2) ||
                    !facts.Points.TryGetValue(c.P3Id, out var c3))
                    return StrictResult.NoSymbolicData;
                return PointTools.Collinear(c1, c2, c3);

            default:
                return StrictResult.NoSymbolicData; 
        }
    }
}