using Perspective.Core.Math;
using Perspective.Core.Reasoning;
using Perspective.Oracle.Z3;

namespace Perspective.Oracle.Z3.Tests;

/// <summary>M3 第二批：用证书模板复现 2023 新高考Ⅰ 22(2) 的"周长 > 3√3"。</summary>
public class TemplateCertificateTests
{
    [Fact]
    public void ReductionCertificateTemplate_Reproduces2023I22_2()
    {
        var oracle = new Z3Oracle(timeoutMs: 10_000);
        var board = new ProblemBoard();

        var answer = board.Solve(
            new ReductionCertificateTemplate(),
            new CertificateInput(
                Exam2023_22_2.BuildReductionProposal(),
                Exam2023_22_2.BuildInnerMinimizationCases(),
                Exam2023_22_2.BuildOuterStrictnessCases(),
                ["t", "p"],
                oracle),
            "22(2)");

        Assert.Contains("三段验证通过", answer.Summary);
        Assert.False(answer.Attained);   // 严格下界不可达
        Assert.True(answer.Strict);
    }
}
