using Perspective.Core.Reasoning;
using Perspective.Oracle.Z3;

namespace Perspective.Oracle.Z3.Tests;

/// <summary>
/// M1-d：2023 新高考Ⅰ 22(2) 端到端回归。
/// 证书链：归约提案（M1-a）+ 内层最小化（M1-b）+ 外层严格性（M1-c）
/// ⇒ 矩形周长 &gt; 3√3。
/// </summary>
public class EndToEnd2023_22_2_Tests
{
    private static readonly Z3Oracle Oracle = new(timeoutMs: 10_000);

    [Fact]
    public void RectanglePerimeter_IsStrictlyGreaterThanThreeSqrtThree()
    {
        // ① 归约提案：P(a,b) = 2(|p|+|s|)√(1+t²)
        var reduction = ReductionVerifier.Verify(Exam2023_22_2.BuildReductionProposal());
        Assert.NotEqual(ReductionStatus.Rejected, reduction.Status);

        // ② 内层最小化：t≥1 时 |p|+|s| ≥ (1+t²)/t²
        var inner = InequalityCertificateVerifier.Verify(
            Oracle, ["t", "p"], Exam2023_22_2.BuildInnerMinimizationCases());
        Assert.True(inner.Verified, inner.Explanation);

        // ③ 外层严格性：非退化时 4A²(1+t²) > 27t⁴ ⇔ P > 3√3
        var outer = InequalityCertificateVerifier.Verify(
            Oracle, ["t", "p"], Exam2023_22_2.BuildOuterStrictnessCases());
        Assert.True(outer.Verified, outer.Explanation);

        // 结论链（可交给 ProofNarrator 渲染）
        string conclusion =
            "归约：" + reduction.Explanation + "；" +
            "内层：" + inner.Explanation + "；" +
            "外层：" + outer.Explanation + "；" +
            "结论：矩形 ABCD 的周长严格大于 3√3（等号仅当 A=B 或 C=B 的退化构形）";
        Assert.Contains("严格大于 3√3", conclusion);
    }
}
