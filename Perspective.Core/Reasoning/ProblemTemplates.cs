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

// ============ 剩余模板（M3 第二批） ============

/// <summary>定点模板的槽位：参数化的直线族（λ ↦ 直线）。</summary>
public sealed record FixedPointInput(Func<ExactScalar, HLine> LineFamily);

/// <summary>定点模板：发现动直线恒过的定点（DiscoveryEngine + 符号验证）。</summary>
public sealed class FixedPointTemplate : IProblemTemplate<FixedPointInput>
{
    public string Name => "动直线过定点";

    public ProblemAnswer Solve(ProblemScene scene, FixedPointInput input)
    {
        var finding = DiscoveryEngine.DiscoverFixedPoint(input.LineFamily);
        if (finding is null)
            return new("", "FixedPoint", "未发现定点（或数值/符号验证未通过）");

        return new("", "FixedPoint", finding.Detail,
            Steps: [finding.Status.ToString(), finding.Detail]);
    }
}

/// <summary>一元极值模板的槽位。</summary>
public sealed record ExtremumInput(
    string VariableName,
    ExactScalar Objective,
    IReadOnlyList<RelationStatement> Domain,
    ExtremumKind Kind,
    IAlgebraicOracle? Oracle = null);

/// <summary>一元极值模板：候选生成 + （可选）神谕闭合。</summary>
public sealed class UnivariateExtremumTemplate : IProblemTemplate<ExtremumInput>
{
    public string Name => "一元极值";

    public ProblemAnswer Solve(ProblemScene scene, ExtremumInput input)
    {
        var report = ExtremumSolver.FindExtremum(
            input.VariableName, input.Objective, input.Domain, input.Kind, input.Oracle);
        if (report is null)
            return new("", "Extremum", "未找到可行候选（检查定义域/目标）");

        return new("", "Extremum", report.Explanation,
            Numeric: report.Value,
            Attained: report.Attained,
            Steps: [report.Explanation]);
    }
}

/// <summary>归约证书模板的槽位：归约提案 + 内层/外层不等式证书。</summary>
public sealed record CertificateInput(
    ReductionProposal Proposal,
    IReadOnlyList<InequalityCase> InnerCases,
    IReadOnlyList<InequalityCase> OuterCases,
    IReadOnlyList<string> Variables,
    IAlgebraicOracle Oracle);

/// <summary>归约证书模板：归约验证 + 内层最小化 + 外层严格性，三段都过才算成立。</summary>
public sealed class ReductionCertificateTemplate : IProblemTemplate<CertificateInput>
{
    public string Name => "归约证书链";

    public ProblemAnswer Solve(ProblemScene scene, CertificateInput input)
    {
        var reduction = ReductionVerifier.Verify(input.Proposal);
        if (reduction.Status == ReductionStatus.Rejected)
            return new("", "Certificate", "归约未通过：" + reduction.Explanation, Strict: true);

        var inner = InequalityCertificateVerifier.Verify(input.Oracle, input.Variables, input.InnerCases);
        if (!inner.Verified)
            return new("", "Certificate", "内层最小化未通过：" + inner.Explanation, Strict: true);

        var outer = InequalityCertificateVerifier.Verify(input.Oracle, input.Variables, input.OuterCases);
        if (!outer.Verified)
            return new("", "Certificate", "外层严格性未通过：" + outer.Explanation, Strict: true);

        return new("", "Certificate",
            $"三段验证通过：{reduction.Explanation}；{inner.Explanation}；{outer.Explanation}",
            Attained: false, Strict: true,
            Steps: [reduction.Explanation, inner.Explanation, outer.Explanation]);
    }
}

/// <summary>分段曲线求交模板的槽位。</summary>
public sealed record PiecewiseLineInput(PiecewiseConic Curve, HLine Line);

/// <summary>分段曲线与直线求交（按段条件过滤）。</summary>
public sealed class PiecewiseLineTemplate : IProblemTemplate<PiecewiseLineInput>
{
    public string Name => "分段曲线求交";

    public ProblemAnswer Solve(ProblemScene scene, PiecewiseLineInput input)
    {
        var points = input.Curve.IntersectLine(input.Line);
        string summary = points.Count == 0
            ? "无交点（在分段条件下）"
            : $"交点 {points.Count} 个：" + string.Join("；", points.Select(p =>
                $"({p.X.Approx / p.W.Approx:0.####}, {p.Y.Approx / p.W.Approx:0.####})"));
        return new("", "Intersection", summary, Steps: [summary]);
    }
}