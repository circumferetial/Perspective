using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Poly = Perspective.Experiment.Subspace.PolyExpr;
using PointP = Perspective.Experiment.Subspace.Primal3<Perspective.Experiment.Subspace.PolyExpr>;
using LineP = Perspective.Experiment.Subspace.Dual3<Perspective.Experiment.Subspace.PolyExpr>;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>
///     图二：椭圆 x²/4 + y² = 1，A=(-1,0)、B=(1,0)，过 B 的直线交椭圆于 P、Q、交 y 轴于 T，
///     M 为 △APQ 的外心，求 OT·OM 的取值范围。
///     路线：椭圆有理参数化（弦过 B ⇒ uv = −1/3）→ f = N/D（多项式）→ 两条多项式恒等式夹出区间。
/// </summary>
public static class ProblemP2Demo
{
    private static ExactScalar S(long v) => ExactScalar.FromInt(v);
    private static void Say(string message) => Console.WriteLine("  " + message);

    public static void Run()
    {
        Console.WriteLine("\n================ 图二：OT·OM 的取值范围（有理参数化 + 自动值域） ================");
        Say("Γ: x²/4 + y² = 1；A=(-1,0)、B=(1,0)；椭圆参数化 P(t) = (2(1−t²), 2t, 1+t²)");
        Say("弦过 B ⇔ uv = −1/3，取 P=P(u)、Q=P(−1/(3u)) = (2(9u²−1), −6u, 9u²+1)");

        var u = Poly.Var(0);
        var s = u * u;
        var one = Poly.One;
        var two = Poly.FromLong(2);
        var nine = Poly.FromLong(9);
        var lens = GeometryLens<Poly>.EuclideanXoy;

        var P = new PointP(two * (one - s), two * u, one + s);
        var Q = new PointP(two * (nine * s - one), -Poly.FromLong(6) * u, nine * s + one);
        var A = PointP.Fixed(-1, 0);

        var linePQ = SubspaceAlgebra.Join(P, Q);
        var M = SubspaceAlgebra.Meet(
            lens.Metric.PerpendicularThrough(lens.Affine.Midpoint(P, Q).Value, linePQ).Value,
            lens.Metric.PerpendicularThrough(lens.Affine.Midpoint(A, P).Value, SubspaceAlgebra.Join(A, P)).Value);
        var T = SubspaceAlgebra.Meet(linePQ, new LineP(one, Poly.Zero, Poly.Zero)); // x = 0

        var n = T.Y * M.Y; // f = n / d
        var d = T.W * M.W;
        Say($"f = OT·OM = N(u) / D(u)，deg N = {n.DegreeIn(0)}，deg D = {d.DegreeIn(0)}");

        // 自动值域 + 上下界符号证书
        var certificate = RationalRange.Certify(n, d, 0, S(1) / S(3));
        var range = certificate.Range;
        Say($"约分后 deg N = {range.ReducedDegreeN}，deg D = {range.ReducedDegreeD}（偶函数 → s = u² 的 2/2）");
        foreach (var c in range.Candidates
                     .GroupBy(c => Math.Round(c.Value.Approx, 9))
                     .Select(g => g.OrderByDescending(x => x.Closed).First()))
            Say($"  候选 {c.Value.Approx:G6}（{c.Source}，{(c.Closed ? "可取" : "极限/排除")}）");
        Say($"取值范围 = {range.Format()}");
        Say($"下界证书：{certificate.Lower.Valid}（{certificate.Lower.Detail}）");
        Say($"上界证书：{certificate.Upper.Valid}（{certificate.Upper.Detail}）");
        Say("端点排除：u=0 时弦为 x 轴、△APQ 退化；u²=1/3 时弦竖直、T 不存在。");
    }
}
