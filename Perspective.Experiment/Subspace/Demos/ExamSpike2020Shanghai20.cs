using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using PointE = Perspective.Experiment.Subspace.Primal3<Perspective.Core.Math.ExactScalar>;
using LineE = Perspective.Experiment.Subspace.Dual3<Perspective.Core.Math.ExactScalar>;
using ConicE = Perspective.Experiment.Subspace.BilinearForm3<Perspective.Core.Math.ExactScalar>;
using LensE = Perspective.Experiment.Subspace.GeometryLens<Perspective.Core.Math.ExactScalar>;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>
///     G0 闸门 spike：用 Subspace（纯射影骨架 + GeometryLens 结构层）重做 2020 上海 20。
///     目的不是"再解一遍"，而是测量：这套表示表达高中解析几何题的**顺手程度**与**缺口**。
/// </summary>
public static class ExamSpike2020Shanghai20
{
    private static ExactScalar S(int v) => ExactScalar.FromInt(v);
    private static ExactScalar F(string formula) => ExactScalar.FromFormula(formula);

    private static void Say(string message) => Console.WriteLine("  " + message);

    public static void Run()
    {
        Console.WriteLine("\n================ G0 spike：2020 上海 20（Subspace 表示） ================");
        var lens = LensE.EuclideanXoy;

        // ---- 陈设：Γ1: x²/4 − y²/b² = 1；Γ2: x²+y² = 4+b² ----
        var b = ExactScalar.FromSymbol("b", 2.2360679);
        var b2 = b * b;

        var g1 = new ConicE(
            S(1) / S(4), S(0), S(0),
            -S(1) / b2, S(0), -S(1)); // x²/4 − y²/b² − w² = 0

        var g2 = new ConicE(
            S(1), S(0), S(0),
            S(1), S(0), -(S(4) + b2)); // x² + y² − (4+b²)w² = 0

        // A 的横坐标平方：消去 y² 的曲线系组合 g1 + (1/b²)·g2
        var combo = Add(g1, S(1) / b2, g2);
        var xA2 = -combo.Q22 / combo.Q00; // (x/w)² = −Q22/Q00
        var expectedXA2 = S(8) * (b2 + S(2)) / (b2 + S(4));
        Say($"(1) x_A² = {xA2.ToSimplifiedString()}；与 8(b²+2)/(b²+4) 严格相等？" +
            $"{ExactScalar.AreStrictlyEqual(xA2, expectedXA2)}");

        // x_A = √6 ⇒ 8(b²+2) = 6(b²+4) ⇒ 2b² − 8 = 0
        var equation = S(8) * (b2 + S(2)) - S(6) * (b2 + S(4));
        Say($"(1) 代入 x_A=√6：8(b²+2)−6(b²+4) = {equation.ToSimplifiedString()}（b=2 时应为 0）");
        var bTwo = S(2);
        var checkB = S(8) * (bTwo * bTwo + S(2)) - S(6) * (bTwo * bTwo + S(4));
        Say($"(1) b=2 时方程值 = {checkB.ToSimplifiedString()}（严格零？{checkB.VerifyStrictZero()}）");

        // ---- (2) b=√5：P∈Γ，|PF1|=8，求 cos∠F1PF2 ----
        Console.WriteLine("\n  --- (2) b=√5 ---");
        var b5 = F("sqrt(5)");
        var g1b5 = new ConicE(S(1) / S(4), S(0), S(0), -S(1) / (b5 * b5), S(0), -S(1));
        var g2b5 = new ConicE(S(1), S(0), S(0), S(1), S(0), -(S(4) + b5 * b5));
        var xA2b5 = S(8) * (b5 * b5 + S(2)) / (b5 * b5 + S(4));

        // Γ2 ∩ x 轴：F1, F2
        var xAxis = new LineE(S(0), S(1), S(0));
        var fs = ConicIntersection.Intersect(xAxis, g2b5);
        var fsSorted = new[] { fs.P1!.Value, fs.P2!.Value }
            .OrderBy(q => Affine(q)!.Value.X.Approx)
            .ToArray();
        var f1 = fsSorted[0]; // (−3, 0)
        var f2 = fsSorted[1]; // (3, 0)
        Say($"(2) Γ2∩x轴：{fs.Kind}；F1={lens.Format(f1)}，F2={lens.Format(f2)}");

        // |PF1|=8 的圆：x²+y²+6x−55 = 0（F1=(−3,0)）
        var distCircle = new ConicE(S(1), S(0), S(3), S(1), S(0), -S(55));

        // 与 Γ1 消去 y²：g1b5 + (1/5)·distCircle ⇒ 关于 x 的二次式
        var combined = Add(g1b5, S(1) / S(5), distCircle);
        // 取 w=1：combined.Q00 x² + 2·combined.Q02 x + combined.Q22 = 0
        var quadA = combined.Q00;
        var quadB = S(2) * combined.Q02;
        var quadC = combined.Q22;
        var roots = QuadraticRoots(quadA, quadB, quadC);
        Say($"(2) 与 Γ1 消元后 x 的根：{string.Join("，", roots.Select(r => r.ToSimplifiedString()))}");

        // 正根 x=4 ⇒ y² = 5(x²/4−1) = 15
        var xP = roots.OrderBy(r => r.Approx).Last();
        var yP2 = S(5) * (xP * xP / S(4) - S(1));
        var p = new PointE(xP, ExactScalar.Sqrt(yP2), S(1));
        Say($"(2) P = {lens.Format(p)}；在 Γ1 上？{g1b5.Eval(p).VerifyStrictZero()}；" +
            $"在 Γ2 上？{g2b5.Eval(p).VerifyStrictZero()}");

        // 分段条件 |x| > x_A：用平方比较（避免开方）
        var xOk = xP * xP - xA2b5;
        Say($"(2) x²−x_A² = {xOk.ToSimplifiedString()}（>0，P 落在 Γ 的 Γ1 段）");

        // |PF1|² = 64，|PF2|² = 16，|F1F2|² = 36 ⇒ u·v = (64+16−36)/2 = 22
        var pf1 = lens.EuclideanDistanceSquared(p, f1).Value;
        var pf2 = lens.EuclideanDistanceSquared(p, f2).Value;
        var ff = lens.EuclideanDistanceSquared(f1, f2).Value;
        var dot = (pf1 + pf2 - ff) / S(2);
        Say($"(2) |PF1|²={pf1.ToSimplifiedString()}，|PF2|²={pf2.ToSimplifiedString()}，u·v={dot.ToSimplifiedString()}");

        // cos² = 121/256（无根号、无开方）
        var cos2 = dot * dot / (pf1 * pf2);
        Say($"(2) cos²∠F1PF2 = {cos2.ToSimplifiedString()}；严格等于 121/256？" +
            $"{ExactScalar.AreStrictlyEqual(cos2, F("121/256"))} → cos = 11/16");

        // Γ2 段无解：|PF1|=8 与 Γ2 相减得直线 x=23/3，与 Γ2 无实交点
        var radical = Add(distCircle, S(0) - S(1), g2b5); // D − G2 = 6x − 46 = 0
        var lineX = new LineE(S(2) * radical.Q02, S(0), radical.Q22);
        var onG2 = ConicIntersection.Intersect(lineX, g2b5);
        Say($"(2) Γ2 段：{onG2.Kind}（无实交点，故 P 只能在 Γ1 段）");

        // ---- (3) 过 S(0,(b²+4)/2)、斜率 −b/2 的直线，切圆于 M，交 Γ1 于 N ----
        Console.WriteLine("\n  --- (3) 切线与 OM·ON ---");
        var lineL = new LineE(b, S(2), -(b2 + S(4))); // b·x + 2y − (b²+4)w = 0
        var tangent = g2.DualForm().Eval(lineL);
        Say($"(3) 与 Γ2 相切（对偶二次型值）？{tangent.VerifyStrictZero()}");

        var m = new PointE(b, S(2), S(1)); // 切点 M=(b,2)
        Say($"(3) M=(b,2) 在直线上？{SubspaceAlgebra.IsIncident(lineL, m)}；在 Γ2 上？{g2.Eval(m).VerifyStrictZero()}");

        var nResult = ConicIntersection.Intersect(lineL, g1);
        // 斜率 −b/2 恰是渐近线斜率 ⇒ 一个交点在无穷远、一个为有限点 N
        var n = new[] { nResult.P1, nResult.P2 }
            .Where(q => q is not null && Affine(q.Value) is not null)
            .Select(q => q!.Value)
            .Single();
        Say($"(3) 直线∩Γ1：{nResult.Kind}；有限交点 N={lens.Format(n)}" +
            $"（另一交点在无穷远：{((nResult.P1 is not null && Affine(nResult.P1.Value) is null) ? "P1" : "P2")}）");
        var onL = SubspaceAlgebra.IsIncident(lineL, n);
        var onG1 = g1.Eval(n).VerifyStrictZero();
        Say($"(3) N 在直线上？{onL}；在 Γ1 上？{onG1}");

        var omOn = Dot(lens, m, n);
        var identity = omOn - (b2 + S(4));
        Say($"(3) OM·ON = {omOn.ToSimplifiedString()}；OM·ON−(b²+4) 严格为 0？" +
            $"{identity.VerifyStrictZero()}");
        Say("(3) 范围：OM·ON=b²+4，条件 b²>2+2√5 ⇒ 值域 (6+2√5,∞)（不等式/可达性仍需主线的证书/神谕层）");
    }

    // ---------- 工具 ----------

    private static ConicE Add(ConicE a, ExactScalar k, ConicE b) => new(
        a.Q00 + k * b.Q00, a.Q01 + k * b.Q01, a.Q02 + k * b.Q02,
        a.Q11 + k * b.Q11, a.Q12 + k * b.Q12, a.Q22 + k * b.Q22);

    private static ExactScalar[] QuadraticRoots(ExactScalar a, ExactScalar b, ExactScalar c)
    {
        var disc = b * b - S(4) * a * c;
        if (disc.VerifyStrictZero() == StrictResult.StrictlyTrue) return [-b / (S(2) * a)];
        var s = ExactScalar.Sqrt(disc);
        return [(-b + s) / (S(2) * a), (-b - s) / (S(2) * a)];
    }

    /// <summary>有限点的仿射坐标（W=1）。</summary>
    private static (ExactScalar X, ExactScalar Y)? Affine(PointE p)
    {
        if (p.W.VerifyStrictZero() == StrictResult.StrictlyTrue) return null;
        return (p.X / p.W, p.Y / p.W);
    }

    /// <summary>两点内积（只用结构层的距离²，不引入开方）：u·v = (|u|²+|v|²−|u−v|²)/2。</summary>
    private static ExactScalar Dot(LensE lens, PointE p, PointE q)
    {
        var o = new PointE(S(0), S(0), S(1));
        return (lens.EuclideanDistanceSquared(p, o).Value + lens.EuclideanDistanceSquared(q, o).Value
                - lens.EuclideanDistanceSquared(p, q).Value) / S(2);
    }
}
