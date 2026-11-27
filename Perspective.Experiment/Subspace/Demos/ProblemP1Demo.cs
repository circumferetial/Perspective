using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using PointE = Perspective.Experiment.Subspace.Primal3<Perspective.Core.Math.ExactScalar>;
using LineE = Perspective.Experiment.Subspace.Dual3<Perspective.Core.Math.ExactScalar>;
using ConicE = Perspective.Experiment.Subspace.BilinearForm3<Perspective.Core.Math.ExactScalar>;
using LensE = Perspective.Experiment.Subspace.GeometryLens<Perspective.Core.Math.ExactScalar>;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>
///     图一（椭圆 e=√2/2、(√2,√3) 在椭圆上；E、F 在过 P 的两条平行线上；
///     H、G、N、I、J、M、S、R、L、K 如题）——先取具体值验证编码。
/// </summary>
public static class ProblemP1Demo
{
    private static ExactScalar S(int v) => ExactScalar.FromInt(v);
    private static ExactScalar F(string formula) => ExactScalar.FromFormula(formula);
    private static void Say(string message) => Console.WriteLine("  " + message);

    public static void Run()
    {
        Console.WriteLine("\n================ 图一：具体值验证（E=(3,4)，F=(4√2,5)，T 取椭圆上一点） ================");
        var lens = LensE.EuclideanXoy;
        var sqrt2 = F("sqrt(2)");

        // (1) 由 e² = 1/2 与 (√2,√3) 在椭圆上，得 a² = 8、b² = 4
        var a2 = F("8");
        var b2 = F("4");
        Say($"(1) a² = {a2.ToSimplifiedString()}，b² = {b2.ToSimplifiedString()}  ⇒  C: x²/8 + y²/4 = 1");

        var conic = new ConicE(S(1), S(0), S(0), S(2), S(0), S(-8)); // x² + 2y² − 8w² = 0

        // 固定点
        var A = new PointE(-2 * sqrt2, S(0), S(1)); // 左顶点
        var B = new PointE(2 * sqrt2, S(0), S(1)); // 右顶点
        var C = new PointE(S(0), S(2), S(1)); // 上顶点
        var D = new PointE(S(0), -S(2), S(1)); // 下顶点
        var P = new PointE(4 * sqrt2, S(4), S(1));
        var Q = new PointE(4 * sqrt2, S(0), S(1));

        // 动点取定值
        var E = new PointE(S(3), S(4), S(1)); // l₁: y = 4
        var Fp = new PointE(4 * sqrt2, S(5), S(1)); // l₂: x = 4√2

        // 第一组：E 连 C、D 得 H、G，GH 交 y 轴于 N
        var H = ConicIntersection.OtherIntersection(SubspaceAlgebra.Join(E, C), C, conic).Value;
        var G = ConicIntersection.OtherIntersection(SubspaceAlgebra.Join(E, D), D, conic).Value;
        var N = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(G, H), new LineE(S(1), S(0), S(0)));
        Say($"H = {lens.Format(H)}（在 C 上？{conic.Eval(H).VerifyStrictZero()}）");
        Say($"G = {lens.Format(G)}（在 C 上？{conic.Eval(G).VerifyStrictZero()}）");
        Say($"N = {lens.Format(N)}（N 在 y 轴上？{SubspaceAlgebra.Pair(new LineE(S(1), S(0), S(0)), N).VerifyStrictZero()}）");

        // 第二组：F 连 A、B 得 I、J，IJ 交 x 轴于 M
        var I = ConicIntersection.OtherIntersection(SubspaceAlgebra.Join(Fp, A), A, conic).Value;
        var J = ConicIntersection.OtherIntersection(SubspaceAlgebra.Join(Fp, B), B, conic).Value;
        var M = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(I, J), new LineE(S(0), S(1), S(0)));
        Say($"I = {lens.Format(I)}（在 C 上？{conic.Eval(I).VerifyStrictZero()}）");
        Say($"J = {lens.Format(J)}（在 C 上？{conic.Eval(J).VerifyStrictZero()}）");
        Say($"M = {lens.Format(M)}（M 在 x 轴上？{SubspaceAlgebra.Pair(new LineE(S(0), S(1), S(0)), M).VerifyStrictZero()}）");

        // 第三组：过 P 的动直线取一个参数点 T（椭圆上的有理点 T=(8,2,3)）
        var T = new PointE(S(8), S(2), S(3));
        Say($"T = {lens.Format(T)}（在 C 上？{conic.Eval(T).VerifyStrictZero()}）");

        var linePT = SubspaceAlgebra.Join(P, T);
        var Spt = ConicIntersection.OtherIntersection(linePT, T, conic).Value;
        var R = SubspaceAlgebra.Meet(linePT, SubspaceAlgebra.Join(M, N));
        Say($"S = {lens.Format(Spt)}（在 C 上？{conic.Eval(Spt).VerifyStrictZero()}）");
        Say($"R = {lens.Format(R)}（在 MN 上？" +
            $"{SubspaceAlgebra.Pair(SubspaceAlgebra.Join(M, N).Normalized(), R.Normalized()).VerifyStrictZero()}）");

        // 两种标签都试：T 为参数点 / S 为参数点
        Check("T=参数点、S=另一交点", T, Spt);
        Check("T=另一交点、S=参数点", Spt, T);

        void Check(string label, PointE Pt, PointE Ps)
        {
            var lineSQ = SubspaceAlgebra.Join(Ps, Q);
            var verticalThroughT = new LineE(Pt.W, S(0), -Pt.X); // 过 T 的竖直线
            var L = SubspaceAlgebra.Meet(lineSQ, verticalThroughT);
            var K = lens.Affine.Midpoint(Pt, L).Value;
            var rawDet = SubspaceAlgebra.Det(R, K, Q);
            var det = SubspaceAlgebra.Det(R.Normalized(), K.Normalized(), Q.Normalized());
            Say($"[{label}] L = {lens.Format(L)}，K = {lens.Format(K)}");
            Say($"   TL ∥ y 轴（构造保证）？{SubspaceAlgebra.Pair(verticalThroughT, L).VerifyStrictZero()}");
            Say($"   未规范化：严通道 = {rawDet.VerifyStrictZero()}（Approx = {rawDet.Approx:G6}）");
            Say($"   规范化后：严通道 = {det.VerifyStrictZero()}，" +
                $"关快判 = {det.VerifyStrictZero(allowFastNumericReject: false)}（Approx = {det.Approx:G6}）");
        }
    }
}
