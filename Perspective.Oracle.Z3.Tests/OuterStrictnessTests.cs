using Perspective.Core.Math;
using Perspective.Core.Reasoning;
using Perspective.Oracle.Z3;

namespace Perspective.Oracle.Z3.Tests;

/// <summary>
/// M1-c：外层严格性证书——2023 新高考Ⅰ 22(2) 的"周长 > 3√3"。
/// <para>
/// 记 A = t²(|p|+|s|)（s=(1+t²+pt)/t²），则 P² = 4A²(1+t²)/t⁴。
/// 要证 P &gt; 3√3 ⇔ P² &gt; 27 ⇔ 4A²(1+t²) − 27t⁴ &gt; 0。
/// 按 sign(p)、sign(core)（core = t²s = 1+t²+pt）拆分支，并对区域加非退化守卫：
/// |t|≥1 时退化边界是 p=0（A=B），|t|≤1 时是 core=0（C=B）。
/// </para>
/// </summary>
public class OuterStrictnessTests
{
    private static readonly Z3Oracle Oracle = new(timeoutMs: 10_000);

    private static RelationStatement Relation(RelationKind kind, ExactScalar left, ExactScalar right) =>
        new(kind, left, right);

    [Fact]
    public void PerimeterSquared_IsStrictlyGreaterThan27_ForNonDegenerate()
    {
        var t = ExactScalar.FromSymbol("t");
        var p = ExactScalar.FromSymbol("p");
        var one = ExactScalar.FromInt(1);
        var two = ExactScalar.FromInt(2);
        var zero = ExactScalar.FromInt(0);
        var t2 = t * t;
        var core = one + t2 + p * t;         // t²s
        var t4 = t2 * t2;

        // 关于 t 的两个区域：|t|≥1（退化在 p=0）与 |t|≤1（退化在 core=0）
        var regions = new (string Name, RelationStatement[] Guards, RelationStatement Degenerate)[]
        {
            ("t≥1",   [Relation(RelationKind.GreaterOrEqual, t, one)],   Relation(RelationKind.NotEqual, p, zero)),
            ("t≤-1",  [Relation(RelationKind.LessOrEqual, t, -one)],     Relation(RelationKind.NotEqual, p, zero)),
            ("0<t≤1", [Relation(RelationKind.GreaterThan, t, zero), Relation(RelationKind.LessOrEqual, t, one)],
                      Relation(RelationKind.NotEqual, core, zero)),
            ("-1≤t<0",[Relation(RelationKind.LessThan, t, zero), Relation(RelationKind.GreaterOrEqual, t, -one)],
                      Relation(RelationKind.NotEqual, core, zero))
        };

        var cases = new List<InequalityCase>();
        foreach (var region in regions)
        {
            // 四个符号分支：A = t²(±p) + (±core)
            foreach (var (pSign, coreSign) in new[] { (+1, +1), (+1, -1), (-1, +1), (-1, -1) })
            {
                string pLabel = pSign > 0 ? "p≥0" : "p<0";
                string coreLabel = coreSign > 0 ? "s≥0" : "s<0";

                var a = t2 * p * ExactScalar.FromInt(pSign) + core * ExactScalar.FromInt(coreSign);
                // 要证 4A²(1+t²) − 27t⁴ > 0；证书形式为"该式 ≥ 0"，等号处由退化守卫排除
                var polynomial = ExactScalar.FromInt(4) * a * a * (one + t2) - ExactScalar.FromInt(27) * t4;

                var guards = new List<RelationStatement>(region.Guards) { region.Degenerate };
                guards.Add(pSign > 0
                    ? Relation(RelationKind.GreaterOrEqual, p, zero)
                    : Relation(RelationKind.LessThan, p, zero));
                guards.Add(coreSign > 0
                    ? Relation(RelationKind.GreaterOrEqual, core, zero)
                    : Relation(RelationKind.LessThan, core, zero));

                cases.Add(new InequalityCase(guards, polynomial, $"{region.Name}, {pLabel}, {coreLabel}", Strict: true));
            }
        }

        var result = InequalityCertificateVerifier.Verify(Oracle, ["t", "p"], cases);

        Assert.True(result.Verified, result.Explanation);
    }

    [Fact]
    public void WithoutNonDegeneracyGuard_CertificateFails()
    {
        // 去掉 p≠0 守卫：t=√2、p=0 处等号成立（退化 A=B），严格性不成立
        var t = ExactScalar.FromSymbol("t");
        var p = ExactScalar.FromSymbol("p");
        var one = ExactScalar.FromInt(1);
        var t2 = t * t;
        var core = one + t2 + p * t;

        var a = t2 * p + core;   // 只取 p≥0, s≥0 分支
        var polynomial = ExactScalar.FromInt(4) * a * a * (one + t2) - ExactScalar.FromInt(27) * t2 * t2;

        List<InequalityCase> cases =
        [
            new([Relation(RelationKind.GreaterOrEqual, t, one),
                 Relation(RelationKind.GreaterOrEqual, p, ExactScalar.FromInt(0)),
                 Relation(RelationKind.GreaterOrEqual, core, ExactScalar.FromInt(0))],
                polynomial, "t≥1, p≥0, s≥0（缺 p≠0 守卫）", Strict: true)
        ];

        var result = InequalityCertificateVerifier.Verify(Oracle, ["t", "p"], cases);

        // 等号可在 p=0 处达到，因此"严格 >"不成立、应找到反例（或至少不通过严格性检查）
        Assert.False(result.Verified);
    }
}
