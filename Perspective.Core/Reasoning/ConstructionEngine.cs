using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

/// <summary>外部构造的认领条件（必须逐条通过严通道验证）。</summary>
public abstract record ConstructionClaim;

public sealed record ClaimPointOnLine(string PointId, string LineId) : ConstructionClaim;

public sealed record ClaimPointOnConic(string PointId, string ConicId) : ConstructionClaim;

public sealed record ClaimMidpoint(string MidId, string AId, string BId) : ConstructionClaim;

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

        _ => (false, $"无法验证的构造条件：{claim}")
    };

    private static void RegisterClaimFact(FactBase facts, ConstructionClaim claim, string provenance)
    {
        GeoFact? fact = claim switch
        {
            ClaimPointOnLine c => new OnLineFact(c.PointId, c.LineId),
            ClaimPointOnConic c => new OnConicFact(c.PointId, c.ConicId),
            ClaimMidpoint c => new MidpointFact(c.MidId, c.AId, c.BId),
            _ => null
        };
        if (fact is not null) facts.AddConstructedFact(fact, provenance);
    }
}
