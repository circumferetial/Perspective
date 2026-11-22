using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

/// <summary>外部构造的认领条件（必须逐条通过严通道验证）。</summary>
public abstract record ConstructionClaim;

public sealed record ClaimPointOnLine(string PointId, string LineId) : ConstructionClaim;

public sealed record ClaimPointOnConic(string PointId, string ConicId) : ConstructionClaim;

public sealed record ClaimMidpoint(string MidId, string AId, string BId) : ConstructionClaim;

/// <summary>垂足：H 是 P 到直线 L 的垂足（要求 H∈L 且 PH ⟂ L）。</summary>
public sealed record ClaimFoot(string FootId, string FromPointId, string LineId) : ConstructionClaim;

/// <summary>直线过两点（要求两点互异且都在线上）。</summary>
public sealed record ClaimLineThroughPoints(string LineId, string P1Id, string P2Id) : ConstructionClaim;

/// <summary>两条直线平行（不重合）。</summary>
public sealed record ClaimLineParallel(string LineId, string OtherLineId) : ConstructionClaim;

/// <summary>两条直线垂直。</summary>
public sealed record ClaimLinePerpendicular(string LineId, string OtherLineId) : ConstructionClaim;

/// <summary>直线是某点关于某圆锥曲线的极线。</summary>
public sealed record ClaimLinePolarOf(string LineId, string PoleId, string ConicId) : ConstructionClaim;

/// <summary>直线切某曲线于某点。</summary>
public sealed record ClaimTangentAt(string LineId, string ConicId, string TouchPointId) : ConstructionClaim;

/// <summary>外部构造的验收结果。</summary>
public sealed record ConstructionResult(
    bool Accepted,
    IReadOnlyList<string> VerifiedClaims,
    IReadOnlyList<string> FailedClaims,
    string? RejectReason);

/// <summary>
/// 【外部构造注入】接受"人 / 外部 LLM / 构造搜索"提出的辅助对象，**由机器严格验收**。
/// <para>
/// 【为什么需要】用户或外部模型可能想到惊艳的辅助点，而我们的规则库没想到。
/// 系统的职责不是猜出它，而是：
/// ① 逐条验证该对象满足的几何关系（严通道，不合格就拒绝并说明哪条不成立）；
/// ② 合格则登记对象与事实，并记录**来源（provenance）**；
/// ③ 之后照常跑规则链/反向链——外部构造与机器构造在证明里地位相同。
/// </para>
/// <para>
/// 这正是 AlphaGeometry / HAGeo 的分工：LLM/启发式负责"提构造"，符号引擎负责"验真假"。
/// 我们的额外要求是：每个外部步骤都写入 <see cref="Derivation"/> 的 Note，可追溯。
/// </para>
/// </summary>
public static class ConstructionEngine
{
    /// <summary>
    /// 提出一个辅助点（坐标由外部给出），并声明它应满足的关系。
    /// 全部关系严格成立才接受；否则拒绝且不写入任何事实。
    /// </summary>
    public static ConstructionResult ProposePoint(
        FactBase facts,
        string pointId,
        HPoint point,
        IReadOnlyList<ConstructionClaim> claims,
        string proposedBy = "external")
    {
        var verified = new List<string>();
        var failed = new List<string>();

        foreach (var claim in claims)
        {
            var (ok, description) = VerifyClaim(facts, claim, point);
            if (ok) verified.Add(description);
            else failed.Add(description);
        }

        if (failed.Count > 0)
        {
            return new ConstructionResult(
                Accepted: false,
                VerifiedClaims: verified,
                FailedClaims: failed,
                RejectReason: $"外部构造未通过严通道：{string.Join("；", failed)}");
        }

        // 验收通过：登记点与事实（来源写入 Derivation.Note）
        facts.AddPoint(pointId, point, structural: true);
        string provenance = $"外部构造（来源：{proposedBy}）";
        foreach (var claim in claims)
            RegisterClaimFact(facts, claim, provenance);

        return new ConstructionResult(true, verified, [], null);
    }

    private static (bool Ok, string Description) VerifyClaim(
        FactBase facts, ConstructionClaim claim, HPoint point) => claim switch
    {
        ClaimPointOnLine c when facts.Lines.TryGetValue(c.LineId, out var line) =>
            (line.ContainsStrictly(point) == StrictResult.StrictlyTrue,
             $"{c.PointId} ∈ {c.LineId}"),

        ClaimPointOnConic c when facts.Conics.TryGetValue(c.ConicId, out var conic) =>
            (conic.ContainsStrictly(point) == StrictResult.StrictlyTrue,
             $"{c.PointId} ∈ 曲线 {c.ConicId}"),

        ClaimMidpoint c when facts.Points.TryGetValue(c.AId, out var a) &&
                             facts.Points.TryGetValue(c.BId, out var b) =>
            (GeometryAudit.IsMidpoint(point, a, b) == StrictResult.StrictlyTrue,
             $"{c.MidId} 是 {c.AId}{c.BId} 的中点"),

        ClaimFoot c when facts.Points.TryGetValue(c.FromPointId, out var from) &&
                         facts.Lines.TryGetValue(c.LineId, out var footLine) =>
            (IsFoot(point, from, footLine),
             $"{c.FootId} 是 {c.FromPointId} 到 {c.LineId} 的垂足"),

        _ => (false, $"无法验证的构造条件：{claim}")
    };

    /// <summary>垂足判定：H ∈ L 且 PH ⟂ L（后者用方向向量点积严格为零）。</summary>
    private static bool IsFoot(HPoint foot, HPoint from, HLine line)
    {
        if (line.ContainsStrictly(foot) != StrictResult.StrictlyTrue) return false;

        var (dx, dy) = Metric.Difference(from, foot);
        var dot = dx * (-line.B) + dy * line.A;
        return dot.VerifyStrictZero() == StrictResult.StrictlyTrue;
    }

    /// <summary>
    /// 提出一条辅助直线（由外部给出），并声明它应满足的关系。
    /// 全部关系严格成立才接受；否则拒绝且不写入任何事实。
    /// </summary>
    public static ConstructionResult ProposeLine(
        FactBase facts,
        string lineId,
        HLine line,
        IReadOnlyList<ConstructionClaim> claims,
        string proposedBy = "external")
    {
        var verified = new List<string>();
        var failed = new List<string>();

        foreach (var claim in claims)
        {
            var (ok, description) = VerifyLineClaim(facts, claim, line);
            if (ok) verified.Add(description);
            else failed.Add(description);
        }

        if (failed.Count > 0)
        {
            return new ConstructionResult(
                Accepted: false,
                VerifiedClaims: verified,
                FailedClaims: failed,
                RejectReason: $"外部构造（直线 {lineId}）未通过严通道：{string.Join("；", failed)}");
        }

        facts.AddLine(lineId, line);
        string provenance = $"外部构造（来源：{proposedBy}）";
        foreach (var claim in claims)
            RegisterLineClaimFact(facts, claim, provenance);

        return new ConstructionResult(true, verified, [], null);
    }

    private static (bool Ok, string Description) VerifyLineClaim(
        FactBase facts, ConstructionClaim claim, HLine line) => claim switch
    {
        ClaimLineThroughPoints c
            when facts.Points.TryGetValue(c.P1Id, out var p1) &&
                 facts.Points.TryGetValue(c.P2Id, out var p2) =>
            (PointTools.Same(p1, p2) == false &&
             line.ContainsStrictly(p1) == StrictResult.StrictlyTrue &&
             line.ContainsStrictly(p2) == StrictResult.StrictlyTrue,
             $"{c.LineId} 过 {c.P1Id}, {c.P2Id}"),

        ClaimLineParallel c when facts.Lines.TryGetValue(c.OtherLineId, out var other) =>
            ((line.A * other.B - line.B * other.A).VerifyStrictZero() == StrictResult.StrictlyTrue &&
             LineTools.Same(line, other) != StrictResult.StrictlyTrue,
             $"{c.LineId} ∥ {c.OtherLineId}"),

        ClaimLinePerpendicular c when facts.Lines.TryGetValue(c.OtherLineId, out var other) =>
            ((line.A * other.A + line.B * other.B).VerifyStrictZero() == StrictResult.StrictlyTrue,
             $"{c.LineId} ⟂ {c.OtherLineId}"),

        ClaimLinePolarOf c
            when facts.Points.TryGetValue(c.PoleId, out var pole) &&
                 facts.Conics.TryGetValue(c.ConicId, out var conic) =>
            (ConicTools.IsPolarLine(conic, pole, line) == StrictResult.StrictlyTrue,
             $"{c.LineId} 是 {c.PoleId} 关于 {c.ConicId} 的极线"),

        ClaimTangentAt c
            when facts.Conics.TryGetValue(c.ConicId, out var conic) &&
                 facts.Points.TryGetValue(c.TouchPointId, out var touch) =>
            (ConicTools.IsTangent(conic, line) == StrictResult.StrictlyTrue &&
             conic.ContainsStrictly(touch) == StrictResult.StrictlyTrue &&
             line.ContainsStrictly(touch) == StrictResult.StrictlyTrue,
             $"{c.LineId} 切 {c.ConicId} 于 {c.TouchPointId}"),

        _ => (false, $"无法验证的直线构造条件：{claim}")
    };

    private static void RegisterLineClaimFact(FactBase facts, ConstructionClaim claim, string provenance)
    {
        switch (claim)
        {
            case ClaimLineThroughPoints c:
                facts.AddConstructedFact(new OnLineFact(c.P1Id, c.LineId), provenance);
                facts.AddConstructedFact(new OnLineFact(c.P2Id, c.LineId), provenance);
                break;
            case ClaimLinePolarOf c:
                facts.AddConstructedFact(new PolarOfFact(c.LineId, c.PoleId, c.ConicId), provenance);
                break;
            case ClaimTangentAt c:
                facts.AddConstructedFact(new TangentFact(c.LineId, c.ConicId, c.TouchPointId), provenance);
                break;
            // 平行/垂直：暂无对应事实类型（P1-4 补 ParallelFact/PerpendicularFact），仅验收不登记
        }
    }

    private static void RegisterClaimFact(FactBase facts, ConstructionClaim claim, string provenance)
    {
        GeoFact? fact = claim switch
        {
            ClaimPointOnLine c => new OnLineFact(c.PointId, c.LineId),
            ClaimPointOnConic c => new OnConicFact(c.PointId, c.ConicId),
            ClaimMidpoint c => new MidpointFact(c.MidId, c.AId, c.BId),
            ClaimFoot c => new OnLineFact(c.FootId, c.LineId),
            _ => null
        };
        if (fact is not null) facts.AddConstructedFact(fact, provenance);
    }
}
