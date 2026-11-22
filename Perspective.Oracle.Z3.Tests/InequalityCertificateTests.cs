using Perspective.Core.Math;
using Perspective.Core.Reasoning;
using Perspective.Oracle.Z3;

namespace Perspective.Oracle.Z3.Tests;

/// <summary>
/// M1-b：内层最小化的不等式证书验证（Proposer/Verifier）。
/// 例：固定 t≥1 时，对一切 p 有 |p| + |s| ≥ c(t)，其中 s=(1+t²+pt)/t²、c=(1+t²)/t²。
/// 提案方拆 4 个符号分支并清除分母（乘 t²>0），机器逐分支做反例检查。
/// </summary>
public class InequalityCertificateTests
{
    private static readonly Z3Oracle Oracle = new(timeoutMs: 5000);

    private static RelationStatement Relation(RelationKind kind, ExactScalar left, ExactScalar right) =>
        new(kind, left, right);

    [Fact]
    public void InnerMinimization_ForTAtLeastOne_IsVerified()
    {
        var t = ExactScalar.FromSymbol("t");
        var p = ExactScalar.FromSymbol("p");
        var one = ExactScalar.FromInt(1);
        var t2 = t * t;

        // 目标：t²(|p|+|s|) − (1+t²) ≥ 0，其中 s=(1+t²+pt)/t² ⇒ t²|s| = |1+t²+pt|。
        // 按 sign(p)、sign(1+t²+pt) 拆 4 个分支，并乘 t²>0 清分母。
        // 分支多项式 = t²·(±p) + (±(1+t²+pt)) − (1+t²)。
        var core = one + t2 + p * t;          // t²s
        var cTerm = one + t2;                 // t²c

        List<InequalityCase> cases =
        [
            // p ≥ 0 且 s ≥ 0：t²p + core − cTerm = p·t² + p·t ≥ 0
            new([Relation(RelationKind.GreaterOrEqual, p, ExactScalar.FromInt(0)),
                 Relation(RelationKind.GreaterOrEqual, core, ExactScalar.FromInt(0))],
                t2 * p + core - cTerm, "p≥0, s≥0"),

            // p ≥ 0 且 s < 0（此时与守卫矛盾，分支为空）
            new([Relation(RelationKind.GreaterOrEqual, p, ExactScalar.FromInt(0)),
                 Relation(RelationKind.LessThan, core, ExactScalar.FromInt(0))],
                t2 * p - core - cTerm, "p≥0, s<0"),

            // p < 0 且 s ≥ 0：−t²p + core − cTerm = p·t(1−t) ≥ 0
            new([Relation(RelationKind.LessThan, p, ExactScalar.FromInt(0)),
                 Relation(RelationKind.GreaterOrEqual, core, ExactScalar.FromInt(0))],
                -t2 * p + core - cTerm, "p<0, s≥0"),

            // p < 0 且 s < 0：−t²p − core − cTerm ≥ 0
            new([Relation(RelationKind.LessThan, p, ExactScalar.FromInt(0)),
                 Relation(RelationKind.LessThan, core, ExactScalar.FromInt(0))],
                -t2 * p - core - cTerm, "p<0, s<0")
        ];

        // 每个分支都加上 t≥1 与 t≠0（清分母前提）
        cases = cases.Select(c => c with
        {
            Guards = [.. c.Guards,
                      Relation(RelationKind.GreaterOrEqual, t, one),
                      Relation(RelationKind.NotEqual, t, ExactScalar.FromInt(0))]
        }).ToList();

        var result = InequalityCertificateVerifier.Verify(Oracle, ["t", "p"], cases);

        Assert.True(result.Verified, result.Explanation);
    }

    [Fact]
    public void WrongCertificate_IsRejectedWithCounterexample()
    {
        var x = ExactScalar.FromSymbol("x");

        // 假证书：x² − 1 ≥ 0 对一切 x 成立（不成立，x=0 是反例）
        List<InequalityCase> cases =
        [
            new([], x * x - ExactScalar.FromInt(1), "x²−1≥0")
        ];

        var result = InequalityCertificateVerifier.Verify(Oracle, ["x"], cases);

        Assert.False(result.Verified);
        Assert.Single(result.FailedCases);
        Assert.Contains("反例", result.FailedCases[0]);
    }

    [Fact]
    public void EmptyCertificate_IsRejectedHonestly()
    {
        var result = InequalityCertificateVerifier.Verify(Oracle, ["x"], []);

        Assert.False(result.Verified);
        Assert.Contains("证书为空", result.Explanation);
    }
}
