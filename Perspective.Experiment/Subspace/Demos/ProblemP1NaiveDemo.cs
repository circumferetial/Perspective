using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using PointE = Perspective.Experiment.Subspace.Primal3<Perspective.Core.Math.ExactScalar>;
using LineE = Perspective.Experiment.Subspace.Dual3<Perspective.Core.Math.ExactScalar>;
using ConicE = Perspective.Experiment.Subspace.BilinearForm3<Perspective.Core.Math.ExactScalar>;
using LensE = Perspective.Experiment.Subspace.GeometryLens<Perspective.Core.Math.ExactScalar>;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>
///     图一的 naive 对照：原坐标 + 斜率参数 k（过 P 的直线），交点靠
///     <see cref="ConicIntersection.Intersect" /> 开方得到——完全不使用有理参数化。
///     预期：表达式带 sqrt(二次式)，符号严证会非常慢或证不出来。
/// </summary>
public static class ProblemP1NaiveDemo
{
    private static void Say(string message) => Console.WriteLine("  " + message);

    public static void Run()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("\n================ 图一 naive 对照：原坐标 + 斜率参数（开方） ================");
        Say("C: x² + 2y² − 8w²；直线 l 过 P=(4√2,4)，斜率 k；S、T 由 ConicIntersection.Intersect 开方得到");

        var e = ExactScalar.FromSymbol("e", 3.0);
        var f = ExactScalar.FromSymbol("f", 5.0);
        var k = ExactScalar.FromSymbol("k", 1.2);
        var sqrt2 = ExactScalar.FromFormula("sqrt(2)");

        var conic = new ConicE(ExactScalar.FromInt(1), ExactScalar.FromInt(0), ExactScalar.FromInt(0),
            ExactScalar.FromInt(2), ExactScalar.FromInt(0), ExactScalar.FromInt(-8));

        var A = new PointE(-2 * sqrt2, ExactScalar.FromInt(0), ExactScalar.FromInt(1));
        var B = new PointE(2 * sqrt2, ExactScalar.FromInt(0), ExactScalar.FromInt(1));
        var C = new PointE(ExactScalar.FromInt(0), ExactScalar.FromInt(2), ExactScalar.FromInt(1));
        var D = new PointE(ExactScalar.FromInt(0), -ExactScalar.FromInt(2), ExactScalar.FromInt(1));
        var P = new PointE(4 * sqrt2, ExactScalar.FromInt(4), ExactScalar.FromInt(1));
        var Q = new PointE(4 * sqrt2, ExactScalar.FromInt(0), ExactScalar.FromInt(1));
        var E = new PointE(e, ExactScalar.FromInt(4), ExactScalar.FromInt(1));
        var F = new PointE(4 * sqrt2, f, ExactScalar.FromInt(1));

        var H = ConicIntersection.OtherIntersection(SubspaceAlgebra.Join(E, C), C, conic).Value;
        var G = ConicIntersection.OtherIntersection(SubspaceAlgebra.Join(E, D), D, conic).Value;
        var N = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(G, H), new LineE(ExactScalar.FromInt(1), ExactScalar.FromInt(0), ExactScalar.FromInt(0)));
        var I = ConicIntersection.OtherIntersection(SubspaceAlgebra.Join(F, A), A, conic).Value;
        var J = ConicIntersection.OtherIntersection(SubspaceAlgebra.Join(F, B), B, conic).Value;
        var M = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(I, J), new LineE(ExactScalar.FromInt(0), ExactScalar.FromInt(1), ExactScalar.FromInt(0)));

        var l = new LineE(k, -ExactScalar.FromInt(1),
            ExactScalar.FromInt(4) - 4 * sqrt2 * k); // k(x−4√2) − (y−4) = 0
        var st = ConicIntersection.Intersect(l, conic);
        Say($"Intersect 类型 = {st.Kind}");
        if (st.P1 is null || st.P2 is null)
        {
            Say("没有两个实交点，naive 版不适用。");
            return;
        }

        var lineMN = SubspaceAlgebra.Join(M, N);
        var R = SubspaceAlgebra.Meet(l, lineMN);
        var lens = LensE.EuclideanXoy;

        Check("P1 当 T、P2 当 S", st.P1.Value, st.P2.Value);
        Check("P1 当 S、P2 当 T", st.P2.Value, st.P1.Value);

        void Check(string label, PointE Pt, PointE Ps)
        {
            var lineSQ = SubspaceAlgebra.Join(Ps, Q);
            var verticalThroughT = new LineE(Pt.W, ExactScalar.FromInt(0), -Pt.X);
            var L = SubspaceAlgebra.Meet(lineSQ, verticalThroughT);
            var K = lens.Affine.Midpoint(Pt, L).Value;
            var det = SubspaceAlgebra.Det(R, K, Q);
            sw.Restart();
            var result = det.VerifyStrictZero(allowFastNumericReject: false);
            sw.Stop();
            Say($"[{label}] Det 严证 = {result}（耗时 {sw.ElapsedMilliseconds} ms，Approx = {det.Approx:G6}）");
        }
    }
}
