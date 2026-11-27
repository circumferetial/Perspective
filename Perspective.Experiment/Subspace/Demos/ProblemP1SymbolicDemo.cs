using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Poly = Perspective.Experiment.Subspace.PolyExpr;
using PointP = Perspective.Experiment.Subspace.Primal3<Perspective.Experiment.Subspace.PolyExpr>;
using LineP = Perspective.Experiment.Subspace.Dual3<Perspective.Experiment.Subspace.PolyExpr>;
using ConicP = Perspective.Experiment.Subspace.BilinearForm3<Perspective.Experiment.Subspace.PolyExpr>;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>
///     图一的符号版：先用仿射归一化把椭圆变成单位圆（保中点、保平行、保共线、保圆锥关联），
///     再用有理参数化 T(t) = (1−t², 2t, 1+t²) 把椭圆上的点全变成多项式，最后直接算 Det(R,K,Q)。
///     全程零除法，Polynomial 环一次算到底。
/// </summary>
public static class ProblemP1SymbolicDemo
{
    private static string Vars(int id) => id switch
    {
        0 => "e",
        1 => "f",
        2 => "t",
        _ => $"v{id}"
    };

    private static string Fmt(PointP p) => $"[{p.X.Format(Vars)}, {p.Y.Format(Vars)}, {p.W.Format(Vars)}]";
    private static void Say(string message) => Console.WriteLine("  " + message);

    public static void Run()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("\n================ 图一：仿射归一化 + 有理参数化后的符号证明 ================");
        Say("归一化 x/√8、y/2 ⇒ 单位圆；P=(2,2)、Q=(2,0)、A=(-1,0)、B=(1,0)、C=(0,1)、D=(0,-1)");
        Say("E=(e,2)、F=(2,f)；T 由 ConicParametrization 自动生成（与手写的 (1−t², 2t, 1+t²) 相差整体 −1）");
        Say("归一化是可逆仿射变换，保共线/中点/平行/圆锥关联，所以归一化后的 Det=0 等价于原题。");

        var e = Poly.Var(0);
        var f = Poly.Var(1);
        var t = Poly.Var(2);
        var two = Poly.FromLong(2);

        var conic = new ConicP(Poly.One, Poly.Zero, Poly.Zero, Poly.One, Poly.Zero, -Poly.One); // x² + y² − w²
        var A = PointP.Fixed(-1, 0);
        var B = PointP.Fixed(1, 0);
        var C = PointP.Fixed(0, 1);
        var D = PointP.Fixed(0, -1);
        var P = PointP.Fixed(2, 2);
        var Q = PointP.Fixed(2, 0);
        var E = new PointP(e, two, Poly.One);
        var F = new PointP(two, f, Poly.One);
        var parametrization = new ConicParametrization<Poly>(conic, PointP.Fixed(1, 0));
        var T = parametrization.At(t).Value;
        Say($"自动参数化 T(t) = {Fmt(T)}（由 ConicParametrization 生成，t=∞ 是漏掉的竖直方向）");
        Say($"T(t) 在 C 上？{Poly.VerifyZero(conic.Eval(T))}");

        // 第一组：EC、ED 的另一个交点 H、G，GH 交 y 轴于 N
        var H = conic.OtherIntersection(C, E).Value;
        var G = conic.OtherIntersection(D, E).Value;
        var N = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(G, H),
            new LineP(Poly.One, Poly.Zero, Poly.Zero));

        // 第二组：FA、FB 的另一个交点 I、J，IJ 交 x 轴于 M
        var I = conic.OtherIntersection(A, F).Value;
        var J = conic.OtherIntersection(B, F).Value;
        var M = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(I, J),
            new LineP(Poly.Zero, Poly.One, Poly.Zero));

        // 第三组：过 P 的动直线 PT 与椭圆另一交点 S；R = PT ∩ MN
        var S = conic.OtherIntersection(T, P).Value;
        var linePT = SubspaceAlgebra.Join(P, T);
        var R = SubspaceAlgebra.Meet(linePT, SubspaceAlgebra.Join(M, N));

        // L = SQ ∩ (过 T 的竖直线)，K = TL 中点
        var verticalThroughT = new LineP(T.W, Poly.Zero, -T.X);
        var L = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(S, Q), verticalThroughT);
        var lens = GeometryLens<Poly>.EuclideanXoy;
        var K = lens.Affine.Midpoint(T, L).Value;

        var det = SubspaceAlgebra.Det(R, K, Q);
        var simplified = det.SimplifyCoeffs();
        sw.Stop();

        Say($"H = {Fmt(H)}");
        Say($"G = {Fmt(G)}");
        Say($"I = {Fmt(I)}");
        Say($"J = {Fmt(J)}");
        Say($"N = {Fmt(N)}");
        Say($"M = {Fmt(M)}");
        Say($"Det(R,K,Q)：原始项数 = {det.TermCount}，规范化后 IsZero = {simplified.IsZero}，" +
            $"VerifyZero = {Poly.VerifyZero(det)}，耗时 {sw.ElapsedMilliseconds} ms（不含打印）");
    }
}
