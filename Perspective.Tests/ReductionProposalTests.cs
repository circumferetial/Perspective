using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// M1-a：归约提案验证（Proposer/Verifier）。
/// 2023 新高考Ⅰ 22(2)：把周长 P(a,b) 归约为 2(|p|+|s|)√(1+t²)，
/// 定义 t=a+b、p=b−a、s=(1+t²+pt)/t²。
/// </summary>
public class ReductionProposalTests
{
    [Fact]
    public void SymbolicIdentity_IsProved()
    {
        var a = ExactScalar.FromSymbol("a");
        var left = (a + ExactScalar.FromInt(1)) * (a + ExactScalar.FromInt(1));
        var right = a * a + ExactScalar.FromInt(2) * a + ExactScalar.FromInt(1);

        var result = ReductionVerifier.Verify(new ReductionProposal(
            ["a"], left, right, [], [], "user"));

        Assert.Equal(ReductionStatus.SymbolicProved, result.Status);
        Assert.Contains("严通道", result.Explanation);
    }

    [Fact]
    public void Question2023_22_2_Reduction_IsVerified()
    {
        var a = ExactScalar.FromSymbol("a");
        var b = ExactScalar.FromSymbol("b");
        var one = ExactScalar.FromInt(1);
        var quarter = ExactScalar.FromFormula("1/4");

        // 归约定义：t = a+b，p = b−a，s = (1+t²+pt)/t²
        var t = a + b;
        var p = b - a;
        var s = (one + t * t + p * t) / (t * t);

        // 原式：矩形周长（A、B 在 W 上；C = B + (−s·t, s) 在 W 上且 AB⊥BC）
        var pointA = HPoint.FromCartesian(a, a * a + quarter);
        var pointB = HPoint.FromCartesian(b, b * b + quarter);
        var pointC = HPoint.FromCartesian(b - s * t, b * b + quarter + s);
        var objective = ExactScalar.FromInt(2) *
            (Metric.Distance(pointA, pointB) + Metric.Distance(pointB, pointC));

        // 归约式：P = 2(|p|+|s|)√(1+t²)
        var reduced = ExactScalar.FromInt(2) * (p.Abs() + s.Abs()) * ExactScalar.Sqrt(one + t * t);

        var proposal = new ReductionProposal(
            BaseParameters: ["a", "b"],
            Objective: objective,
            ReducedForm: reduced,
            Definitions: [new SymbolDefinition("t", t), new SymbolDefinition("p", p), new SymbolDefinition("s", s)],
            Domain:
            [
                new RelationStatement(RelationKind.NotEqual, t, ExactScalar.FromInt(0)),   // t ≠ 0
                new RelationStatement(RelationKind.NotEqual, p, ExactScalar.FromInt(0))    // 非退化：A ≠ B
            ],
            ProposedBy: "user");

        var result = ReductionVerifier.Verify(proposal);

        Assert.NotEqual(ReductionStatus.Rejected, result.Status);
        Assert.True(result.SamplesUsed >= 16 || result.Status == ReductionStatus.SymbolicProved);
        Assert.True(result.MaxDeviation <= 1e-9);
    }

    [Fact]
    public void WrongReduction_IsRejected()
    {
        var a = ExactScalar.FromSymbol("a");
        var b = ExactScalar.FromSymbol("b");
        var one = ExactScalar.FromInt(1);
        var quarter = ExactScalar.FromFormula("1/4");

        var t = a + b;
        var p = b - a;
        var s = (one + t * t + p * t) / (t * t);

        var pointA = HPoint.FromCartesian(a, a * a + quarter);
        var pointB = HPoint.FromCartesian(b, b * b + quarter);
        var pointC = HPoint.FromCartesian(b - s * t, b * b + quarter + s);
        var objective = ExactScalar.FromInt(2) *
            (Metric.Distance(pointA, pointB) + Metric.Distance(pointB, pointC));

        // 错误归约：漏掉因子 2
        var wrongReduced = (p.Abs() + s.Abs()) * ExactScalar.Sqrt(one + t * t);

        var result = ReductionVerifier.Verify(new ReductionProposal(
            ["a", "b"], objective, wrongReduced,
            [new("t", t), new("p", p), new("s", s)],
            [
                new RelationStatement(RelationKind.NotEqual, t, ExactScalar.FromInt(0)),
                new RelationStatement(RelationKind.NotEqual, p, ExactScalar.FromInt(0))
            ],
            "llm"));

        Assert.Equal(ReductionStatus.Rejected, result.Status);
    }
}
