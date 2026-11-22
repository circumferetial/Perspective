using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

/// <summary>定理搬运的报告：哪些事实在变换下保持、哪些被丢弃（以及目标视角等级）。</summary>
public sealed record SceneTransferReport(
    TransformLevel SourceLevel,
    TransformLevel TargetLevel,
    int TotalFacts,
    IReadOnlyList<GeoFact> Kept,
    IReadOnlyList<GeoFact> Dropped);

/// <summary>
/// 【视角门控的定理搬运】把整个场景按变换 g 搬到新坐标，并逐条**重新验证**事实是否仍成立。
/// <para>
/// 【数学依据】Klein/Erlangen：在群 G 下不变的关系可以整体搬运；非不变关系会失效。
/// 因此搬运后不能盲目继承事实，必须逐条过严通道（本类负责"过滤 + 报告"），
/// 目标视角等级取 max(源等级, g 的等级)——规则门控据此禁止越级使用。
/// </para>
/// <para>
/// 【能力边界】可搬并回验：OnLine/OnConic/Tangent/Polar/Midpoint/Harmonic/Collinear/
/// Concurrent/Parallel/Perpendicular/AngleEqual/AreaRatio/SegmentRatio/CrossRatio。
/// 其余（中心、共轭方向、斜率积、代数关系）暂直接丢弃并计入报告，避免静默错误。
/// </para>
/// </summary>
public static class SceneTransfer
{
    public static (FactBase Transformed, SceneTransferReport Report) Apply(
        FactBase source, Matrix3x3 transform)
    {
        var target = new FactBase();

        foreach (var (id, point) in source.Points)
            target.AddPoint(id, transform * point, source.IsStructural(id));
        foreach (var (id, line) in source.Lines)
            target.AddLine(id, transform.Transform(line));
        foreach (var (id, conic) in source.Conics)
            target.AddConic(id, transform.Transform(conic));

        var kept = new List<GeoFact>();
        var dropped = new List<GeoFact>();
        foreach (var fact in source.Facts)
        {
            if (Holds(target, fact))
            {
                kept.Add(fact);
                var derivation = source.DerivationLog.FirstOrDefault(d => d.Fact == fact);
                target.AddFact(fact, derivation?.Note ?? "定理搬运（原事实经验证）");
            }
            else
            {
                dropped.Add(fact);
            }
        }

        var targetLevel = (TransformLevel)System.Math.Max(
            (int)source.CurrentLevel, (int)PerspectiveTransform.Classify(transform));
        target.CurrentLevel = targetLevel;

        return (target, new SceneTransferReport(
            source.CurrentLevel, targetLevel, source.Facts.Count, kept, dropped));
    }

    /// <summary>对该事实类型做严通道回验；不能验证的类型返回 false（保守丢弃）。</summary>
    private static bool Holds(FactBase facts, GeoFact fact)
    {
        switch (fact)
        {
            case OnLineFact f:
                return facts.Lines.TryGetValue(f.LineId, out var line) &&
                       facts.Points.TryGetValue(f.PointId, out var point) &&
                       line.ContainsStrictly(point) == StrictResult.StrictlyTrue;

            case OnConicFact f:
                return facts.Conics.TryGetValue(f.ConicId, out var conic) &&
                       facts.Points.TryGetValue(f.PointId, out var point2) &&
                       conic.ContainsStrictly(point2) == StrictResult.StrictlyTrue;

            case TangentFact f:
                return facts.Conics.TryGetValue(f.ConicId, out var conic2) &&
                       facts.Lines.TryGetValue(f.LineId, out var line2) &&
                       facts.Points.TryGetValue(f.TouchPointId, out var touch) &&
                       ConicTools.IsTangent(conic2, line2) == StrictResult.StrictlyTrue &&
                       conic2.ContainsStrictly(touch) == StrictResult.StrictlyTrue &&
                       line2.ContainsStrictly(touch) == StrictResult.StrictlyTrue;

            case PolarOfFact f:
                return facts.Conics.TryGetValue(f.ConicId, out var conic3) &&
                       facts.Lines.TryGetValue(f.LineId, out var line3) &&
                       facts.Points.TryGetValue(f.PoleId, out var pole) &&
                       ConicTools.IsPolarLine(conic3, pole, line3) == StrictResult.StrictlyTrue;

            case MidpointFact f:
                return facts.Points.TryGetValue(f.MidId, out var mid) &&
                       facts.Points.TryGetValue(f.AId, out var a) &&
                       facts.Points.TryGetValue(f.BId, out var b) &&
                       GeometryAudit.IsMidpoint(mid, a, b) == StrictResult.StrictlyTrue;

            case HarmonicFact f:
                return facts.Points.TryGetValue(f.AId, out var ha) &&
                       facts.Points.TryGetValue(f.BId, out var hb) &&
                       facts.Points.TryGetValue(f.CId, out var hc) &&
                       facts.Points.TryGetValue(f.DId, out var hd) &&
                       GeometryAudit.IsHarmonic(ha, hb, hc, hd) == StrictResult.StrictlyTrue;

            case CollinearFact f:
                return facts.Points.TryGetValue(f.P1Id, out var c1) &&
                       facts.Points.TryGetValue(f.P2Id, out var c2) &&
                       facts.Points.TryGetValue(f.P3Id, out var c3) &&
                       PointTools.Collinear(c1, c2, c3) == StrictResult.StrictlyTrue;

            case ConcurrentFact f:
                return facts.Lines.TryGetValue(f.L1Id, out var l1) &&
                       facts.Lines.TryGetValue(f.L2Id, out var l2) &&
                       facts.Lines.TryGetValue(f.L3Id, out var l3) &&
                       LineTools.Same(l1, l2) != StrictResult.StrictlyTrue &&
                       l3.ContainsStrictly(HLine.Intersect(l1, l2)) == StrictResult.StrictlyTrue;

            case ParallelFact f:
                return facts.Lines.TryGetValue(f.Line1Id, out var p1) &&
                       facts.Lines.TryGetValue(f.Line2Id, out var p2) &&
                       LineTools.Same(p1, p2) != StrictResult.StrictlyTrue &&
                       GeometryAudit.AreParallel(p1, p2) == StrictResult.StrictlyTrue;

            case PerpendicularFact f:
                return facts.Lines.TryGetValue(f.Line1Id, out var q1) &&
                       facts.Lines.TryGetValue(f.Line2Id, out var q2) &&
                       GeometryAudit.ArePerpendicular(q1, q2) == StrictResult.StrictlyTrue;

            case AngleEqualFact f:
                return facts.Lines.TryGetValue(f.A1, out var a1) &&
                       facts.Lines.TryGetValue(f.A2, out var a2) &&
                       facts.Lines.TryGetValue(f.B1, out var b1) &&
                       facts.Lines.TryGetValue(f.B2, out var b2) &&
                       GeometryAudit.AreAngleEqual(a1, a2, b1, b2, f.Sign) == StrictResult.StrictlyTrue;

            case AreaRatioFact f:
                return facts.Points.TryGetValue(f.A1, out var ar1) &&
                       facts.Points.TryGetValue(f.A2, out var ar2) &&
                       facts.Points.TryGetValue(f.A3, out var ar3) &&
                       facts.Points.TryGetValue(f.B1, out var br1) &&
                       facts.Points.TryGetValue(f.B2, out var br2) &&
                       facts.Points.TryGetValue(f.B3, out var br3) &&
                       GeometryAudit.IsAreaRatio(ar1, ar2, ar3, br1, br2, br3, f.Value) == StrictResult.StrictlyTrue;

            case SegmentRatioFact f:
                return facts.Points.TryGetValue(f.A1, out var sr1) &&
                       facts.Points.TryGetValue(f.A2, out var sr2) &&
                       facts.Points.TryGetValue(f.B1, out var sr3) &&
                       facts.Points.TryGetValue(f.B2, out var sr4) &&
                       GeometryAudit.IsSegmentRatio(sr1, sr2, sr3, sr4, f.Value) == StrictResult.StrictlyTrue;

            case CrossRatioFact f:
                return facts.Points.TryGetValue(f.A, out var cr1) &&
                       facts.Points.TryGetValue(f.B, out var cr2) &&
                       facts.Points.TryGetValue(f.C, out var cr3) &&
                       facts.Points.TryGetValue(f.D, out var cr4) &&
                       GeometryAudit.IsCrossRatio(cr1, cr2, cr3, cr4, f.Value) == StrictResult.StrictlyTrue;

            default:
                return false;   // 未知/不可验证类型：保守丢弃，报告可见
        }
    }
}
