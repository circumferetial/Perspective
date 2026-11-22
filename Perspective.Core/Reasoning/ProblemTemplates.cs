using AngouriMath;
using Perspective.Core.Discovery;
using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

/// <summary>弦族斜率模板的槽位：曲线 + 其上一点（求"AP、AQ 斜率和为零"时的弦斜率）。</summary>
public sealed record ChordSlopeInput(string ConicId, string PointId);

/// <summary>
/// 弦族斜率模板：给定曲线上一点 A，求弦 l 满足"AP、AQ 斜率之和为零"时的斜率。
/// 内部走 <see cref="InverseExamSolver.SolveChordSlopeForZeroSlopeSum"/>。
/// </summary>
public sealed class ChordSlopeTemplate : IProblemTemplate<ChordSlopeInput>
{
    public string Name => "弦族斜率条件";

    public ProblemAnswer Solve(ProblemScene scene, ChordSlopeInput input)
    {
        var facts = scene.Facts;
        var conic = facts.Conics[input.ConicId];
        var point = facts.Points[input.PointId];

        var solutions = InverseExamSolver.SolveChordSlopeForZeroSlopeSum(conic, point);
        var steps = new List<string> { $"求解 {input.ConicId} 上 {input.PointId} 的弦族斜率条件" };

        if (solutions.Equals(Entity.Set.Empty))
            return new("", "SlopeSet", "条件不能唯一确定斜率（或输入退化）", Solutions: solutions, Steps: steps);

        ExactScalar? numeric = null;
        if (solutions is Entity.Set.FiniteSet finite)
        {
            var realRoots = finite
                .Where(root => root.EvalNumerical() is Entity.Number.Complex complex && complex.ImaginaryPart.IsZero)
                .Select(root => new ExactScalar((double)root.EvalNumerical(), root.Simplify()))
                .ToList();
            if (realRoots.Count == 1) numeric = realRoots[0];
            steps.Add($"实斜率：{string.Join(", ", realRoots.Select(r => r.ToString()))}");
        }

        string summary = numeric is not null
            ? $"弦的斜率为 {numeric}"
            : $"斜率集合：{RealSets.Describe(solutions)}";
        return new("", "SlopeSet", summary, Numeric: numeric, Solutions: solutions, Steps: steps);
    }
}

/// <summary>切线面积模板的槽位：曲线、顶点、弦斜率参数名（从前几问注入）、目标 tan。</summary>
public sealed record AreaFromTangentInput(
    string ConicId, string VertexId, string ChordSlopeParameter, ExactScalar TanTarget);

/// <summary>
/// 已知弦斜率与 ∠PAQ 的 tan，求三角形面积（内部走 <see cref="InverseExamSolver.SolveAreaFromTangent"/>）。
/// 弦斜率从参数表读取——这就是"第 (2) 问用第 (1) 问结论"的依赖通道。
/// </summary>
public sealed class AreaFromTangentTemplate : IProblemTemplate<AreaFromTangentInput>
{
    public string Name => "已知 tan 求面积";

    public ProblemAnswer Solve(ProblemScene scene, AreaFromTangentInput input)
    {
        var facts = scene.Facts;
        var conic = facts.Conics[input.ConicId];
        var vertex = facts.Points[input.VertexId];
        var slope = scene.GetParameter(input.ChordSlopeParameter);

        var results = InverseExamSolver.SolveAreaFromTangent(conic, vertex, slope, input.TanTarget);
        var steps = new List<string>
        {
            $"取弦斜率 = {input.ChordSlopeParameter} = {slope}",
            $"tan∠PAQ = {input.TanTarget}"
        };

        if (results.Count == 0)
            return new("", "Area", "无有效解（或退化被过滤）", Steps: steps);

        var first = results[0];
        steps.Add($"截距 c = {first.Intercept}；面积 = {first.Area}");
        return new("", "Area", $"面积 = {first.Area}", Numeric: first.Area, Steps: steps);
    }
}

/// <summary>证明型模板的槽位：待证命题（几何命题，不是自然语言）。</summary>
public sealed record ProveClaimInput(GeoClaim Claim);

/// <summary>
/// 证明型模板：对陈设中的事实库判定一个几何命题（定理链优先，代数兜底）。
/// 说明：模板接收的是**结构化命题对象**，不是题干文字。
/// </summary>
public sealed class ProveClaimTemplate : IProblemTemplate<ProveClaimInput>
{
    private readonly TheoremEngine _engine;

    public ProveClaimTemplate(TheoremEngine engine) => _engine = engine;

    public string Name => "几何命题判定";

    public ProblemAnswer Solve(ProblemScene scene, ProveClaimInput input)
    {
        var report = ProofJudge.Evaluate(scene.Facts, _engine, input.Claim);
        bool? proved = report.Verdict switch
        {
            ClaimVerdict.ProvedByTheoremChain => true,
            ClaimVerdict.TrueByAlgebraOnly => true,
            ClaimVerdict.StrictlyFalse => false,
            _ => null
        };

        return new("", "Verdict", $"{report.Verdict}：{report.Explanation}",
            Steps: [report.Verdict.ToString(), report.Explanation]);
    }
}
