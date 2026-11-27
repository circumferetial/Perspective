using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Poly = Perspective.Experiment.Subspace.PolyExpr;
using PointP = Perspective.Experiment.Subspace.Primal3<Perspective.Experiment.Subspace.PolyExpr>;
using LineP = Perspective.Experiment.Subspace.Dual3<Perspective.Experiment.Subspace.PolyExpr>;
using ConicP = Perspective.Experiment.Subspace.BilinearForm3<Perspective.Experiment.Subspace.PolyExpr>;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>
///     图一的对照实验：完全不做仿射归一化，直接在原坐标（含 √2）上用 PolyExpr 算 Det。
///     与 ProblemP1SymbolicDemo 比耗时和项数，用来量化“视角”值多少钱。
/// </summary>
public static class ProblemP1SymbolicControlDemo
{
    private static Poly C(ExactScalar c) => Poly.FromConst(c);
    private static void Say(string message) => Console.WriteLine("  " + message);

    public static void Run()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("\n================ 图一对照：原坐标（含 √2，不做归一化）的符号版 ================");
        Say("C: x² + 2y² − 8w²；A=(−2√2,0)、B=(2√2,0)、C=(0,2)、D=(0,−2)、P=(4√2,4)、Q=(4√2,0)");
        Say("E=(e,4)、F=(4√2,f)、T=(−8t, 2−4t², 1+2t²)");

        var e = Poly.Var(0);
        var f = Poly.Var(1);
        var t = Poly.Var(2);
        var two = Poly.FromLong(2);
        var four = Poly.FromLong(4);
        var sqrt2 = ExactScalar.FromFormula("sqrt(2)");

        var conic = new ConicP(Poly.One, Poly.Zero, Poly.Zero, two, Poly.Zero, -Poly.FromLong(8)); // x² + 2y² − 8w²
        var A = new PointP(C(-2 * sqrt2), Poly.Zero, Poly.One);
        var B = new PointP(C(2 * sqrt2), Poly.Zero, Poly.One);
        var Cp = new PointP(Poly.Zero, two, Poly.One);
        var D = new PointP(Poly.Zero, -two, Poly.One);
        var P = new PointP(C(4 * sqrt2), four, Poly.One);
        var Q = new PointP(C(4 * sqrt2), Poly.Zero, Poly.One);
        var E = new PointP(e, four, Poly.One);
        var F = new PointP(C(4 * sqrt2), f, Poly.One);
        var T = new PointP(Poly.FromLong(-8) * t, two - four * t * t, Poly.One + two * t * t);

        var H = conic.OtherIntersection(Cp, E).Value;
        var G = conic.OtherIntersection(D, E).Value;
        var N = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(G, H), new LineP(Poly.One, Poly.Zero, Poly.Zero));
        var I = conic.OtherIntersection(A, F).Value;
        var J = conic.OtherIntersection(B, F).Value;
        var M = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(I, J), new LineP(Poly.Zero, Poly.One, Poly.Zero));
        var S = conic.OtherIntersection(T, P).Value;
        var linePT = SubspaceAlgebra.Join(P, T);
        var R = SubspaceAlgebra.Meet(linePT, SubspaceAlgebra.Join(M, N));
        var verticalThroughT = new LineP(T.W, Poly.Zero, -T.X);
        var L = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(S, Q), verticalThroughT);
        var lens = GeometryLens<Poly>.EuclideanXoy;
        var K = lens.Affine.Midpoint(T, L).Value;

        var det = SubspaceAlgebra.Det(R, K, Q);
        var simplified = det.SimplifyCoeffs();
        sw.Stop();
        Say($"Det(R,K,Q)：原始项数 = {det.TermCount}，规范化后 IsZero = {simplified.IsZero}，" +
            $"VerifyZero = {Poly.VerifyZero(det)}，耗时 {sw.ElapsedMilliseconds} ms（不含打印）");
    }
}
