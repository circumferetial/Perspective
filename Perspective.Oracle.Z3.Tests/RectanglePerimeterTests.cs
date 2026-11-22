using Perspective.Core.Math;
using Perspective.Core.Reasoning;
using Perspective.Oracle.Z3;

namespace Perspective.Oracle.Z3.Tests;

/// <summary>
/// 2023 新高考Ⅰ 22(2) 降维探针：矩形三顶点在 W: y=x²+1/4 上，证周长 > 3√3。
/// <para>
/// 降维结论（人工推导，机器验证）：
/// A=(a,a²+1/4), B=(b,b²+1/4)，令 t=a+b、p=b−a；
/// 由 C∈W 且 AB⊥BC 得 s=(1+t²+pt)/t²，周长 P=2(|p|+|s|)√(1+t²)。
/// 对 p 取最小后：|t|≥1 时 P ≥ 2(1+t²)^{3/2}/t²；|t|≤1 时 P ≥ 2(1+t²)^{3/2}/|t|。
/// 令 u=t²，剩两个多项式非负性（本文件用 Z3 闭合），等号只在退化边界（A=B 或 C=B）。
/// </para>
/// </summary>
public class RectanglePerimeterTests
{
    private static readonly Z3Oracle Oracle = new(timeoutMs: 5000);

    private static RelationStatement Relation(RelationKind kind, ExactScalar left, ExactScalar right) =>
        new(kind, left, right);

    [Fact]
    public void FactorizationIdentities_AreExact()
    {
        var u = ExactScalar.FromSymbol("u");
        var one = ExactScalar.FromInt(1);

        var lhs1 = ExactScalar.FromInt(4) * (one + u) * (one + u) * (one + u) - ExactScalar.FromInt(27) * u * u;
        var rhs1 = (u - ExactScalar.FromInt(2)) * (u - ExactScalar.FromInt(2)) * (ExactScalar.FromInt(4) * u + one);
        Assert.Equal(StrictResult.StrictlyTrue, (lhs1 - rhs1).VerifyStrictZero());

        var lhs2 = ExactScalar.FromInt(4) * (one + u) * (one + u) * (one + u) - ExactScalar.FromInt(27) * u;
        var rhs2 = (ExactScalar.FromInt(2) * u - one) * (ExactScalar.FromInt(2) * u - one) * (u + ExactScalar.FromInt(4));
        Assert.Equal(StrictResult.StrictlyTrue, (lhs2 - rhs2).VerifyStrictZero());
    }

    [Fact]
    public void OneDimensionalInequalities_AreClosedByOracle()
    {
        var u = ExactScalar.FromSymbol("u");
        var one = ExactScalar.FromInt(1);

        // |t|≥1 ⇔ u≥1：4(1+u)³ − 27u² ≥ 0
        var high = ExactScalar.FromInt(4) * (one + u) * (one + u) * (one + u) - ExactScalar.FromInt(27) * u * u;
        var highVerdict = Oracle.DecideUniversal(["u"],
            [Relation(RelationKind.GreaterOrEqual, u, one)],
            Relation(RelationKind.GreaterOrEqual, high, ExactScalar.FromInt(0)));
        Assert.Equal(OracleVerdictKind.Unsat, highVerdict.Kind);

        // |t|≤1 ⇔ 0<u≤1：4(1+u)³ − 27u ≥ 0
        var low = ExactScalar.FromInt(4) * (one + u) * (one + u) * (one + u) - ExactScalar.FromInt(27) * u;
        var lowVerdict = Oracle.DecideUniversal(["u"],
            [
                Relation(RelationKind.GreaterThan, u, ExactScalar.FromInt(0)),
                Relation(RelationKind.LessOrEqual, u, one)
            ],
            Relation(RelationKind.GreaterOrEqual, low, ExactScalar.FromInt(0)));
        Assert.Equal(OracleVerdictKind.Unsat, lowVerdict.Kind);
    }

    [Fact]
    public void Parametrization_MatchesRealRectanglesAndBounds()
    {
        var rng = new Random(20261123);
        double bound = 3 * System.Math.Sqrt(3);

        for (int i = 0; i < 200; i++)
        {
            double a = rng.NextDouble() * 6 - 3;
            double b = rng.NextDouble() * 6 - 3;
            if (System.Math.Abs(a - b) < 1e-3) continue;

            double t = a + b, p = b - a;
            if (System.Math.Abs(t) < 1e-3) continue;

            double s = (1 + t * t + p * t) / (t * t);
            if (System.Math.Abs(s) < 1e-3) continue;

            // 真实的第三个顶点 C 与矩形检查
            double cx = b - s * t, cy = b * b + 0.25 + s;
            Assert.True(System.Math.Abs(cy - (cx * cx + 0.25)) < 1e-9);   // C ∈ W

            double ux = b - a, uy = b * b - a * a;
            double vx = cx - b, vy = cy - (b * b + 0.25);
            Assert.True(System.Math.Abs(ux * vx + uy * vy) < 1e-9);        // AB ⟂ BC

            double perimeter = 2 * (System.Math.Sqrt(ux * ux + uy * uy) + System.Math.Sqrt(vx * vx + vy * vy));
            double formula = 2 * (System.Math.Abs(p) + System.Math.Abs(s)) * System.Math.Sqrt(1 + t * t);
            Assert.True(System.Math.Abs(perimeter - formula) < 1e-9);      // 降维公式一致
            Assert.True(perimeter > bound - 1e-9);                         // 下界成立
        }
    }
}
