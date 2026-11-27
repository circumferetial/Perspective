using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using PointE = Perspective.Experiment.Subspace.Primal3<Perspective.Core.Math.ExactScalar>;
using LineE = Perspective.Experiment.Subspace.Dual3<Perspective.Core.Math.ExactScalar>;
using ConicE = Perspective.Experiment.Subspace.BilinearForm3<Perspective.Core.Math.ExactScalar>;
using LensE = Perspective.Experiment.Subspace.GeometryLens<Perspective.Core.Math.ExactScalar>;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>
///     图三：椭圆 x²/24 + y²/16 = 1，P 在直线 x/12 + y/8 = 1 上，
///     射线 OP 交椭圆于 R，Q 在 OP 上且 |OQ|·|OP| = |OR|²，求 Q 的轨迹。
///     用发现管线：精确采样 → 零空间 → 有理还原 → 符号证明。
/// </summary>
public static class ProblemP3Demo
{
    private static ExactScalar S(long v) => ExactScalar.FromInt(v);
    private static void Say(string message) => Console.WriteLine("  " + message);

    public static void Run()
    {
        Console.WriteLine("\n================ 图三：Q 的轨迹（发现管线：采样→零空间→有理还原→证明） ================");
        var lens = LensE.EuclideanXoy;

        // P(t) = (12(1−t), 8t) 在 x/12 + y/8 = 1 上。
        // |OQ|·|OP| = |OR|² ⇒ Q = P/D，D = x_P²/24 + y_P²/16（平方后无根号）。
        double[] ts = [0.2, 0.6, 1.1, 1.7, 2.3, 2.9];
        var rows = new List<double[]>(ts.Length);
        foreach (var t in ts)
        {
            var px = 12 * (1 - t);
            var py = 8 * t;
            var d = px * px / 24 + py * py / 16;
            rows.Add(Veronese.Row(px / d, py / d));
        }

        var vMatrix = Veronese.Matrix(rows);
        Say($"V 矩阵：{rows.Count} × 6（每行 = [x², 2xy, 2xw, y², 2yw, w²]）");
        var nullVec = FindNumericNullVector1D(vMatrix)
                      ?? throw new InvalidOperationException("未发现 1 维圆锥曲线空间。");
        var rationals = nullVec.Select(v => SnapRational(v, maxDen: 1000)
            ?? throw new InvalidOperationException($"系数 {v} 未能还原为简单有理数。")).ToArray();

        long lcm = 1;
        foreach (var (_, den) in rationals) lcm = Lcm(lcm, den);

        var coeffs = rationals.Select(r => r.Num * (lcm / r.Den)).ToArray();
        long gcd = 0;
        foreach (var c in coeffs) gcd = Gcd(gcd, c);
        if (gcd > 1)
            for (var i = 0; i < coeffs.Length; i++) coeffs[i] /= gcd;
        var firstNonZero = coeffs.FirstOrDefault(c => c != 0);
        if (firstNonZero < 0)
            for (var i = 0; i < coeffs.Length; i++) coeffs[i] = -coeffs[i];

        Say($"自动发现轨迹圆锥曲线：{Eq(coeffs)}");
        Say($"系数 (Q00,Q01,Q02,Q11,Q12,Q22) = [{string.Join(", ", coeffs)}]");

        // 符号证明：把 Q(t) = (12(1−t), 8t, D) 直接代入（齐次化，自动清分母）
        var conic = Veronese.ToConic(coeffs.Select(c => S(c)).ToArray());
        var tSym = ExactScalar.FromSymbol("t", 0.7);
        var pxSym = S(12) * (ExactScalar.One - tSym);
        var pySym = S(8) * tSym;
        var dSym = pxSym * pxSym / S(24) + pySym * pySym / S(16);
        var qHom = new PointE(pxSym, pySym, dSym);
        var vQ = Veronese.Row(qHom);
        var qCoeffs = Veronese.Coefficients(conic);
        Say($"符号代入 ⟨V(Q(t)), Q⟩ 严格为零？{Veronese.Dot(vQ, qCoeffs).VerifyStrictZero()}（清分母后是 t 的多项式）");

        // 分类：中心 = l∞ 的极点；二次项正定 ⇒ 椭圆
        var center = conic.DualForm().Pole(new LineE(S(0), S(0), S(1)));
        Say($"中心（l∞ 的极点）= {lens.Format(center)}");

        var det2 = conic.Q00 * conic.Q11 - conic.Q01 * conic.Q01;
        Say($"二次项判别：Q00 = {conic.Q00.Approx:G6}，Q00·Q11 − Q01² = {det2.Approx:G6}（>0 且 Q00>0 ⇒ 椭圆）");

        if (lens.TryToCartesian(center, out var cx, out var cy))
        {
            var centerAffine = new PointE(cx, cy, ExactScalar.One);
            var f = conic.Eval(centerAffine);
            var a2 = -f / conic.Q00;
            var b2 = -f / conic.Q11;
            Say($"平移到中心后：{conic.Q00.Approx:G6}·x² + {conic.Q11.Approx:G6}·y² = {-f} " +
                $"⇒ a² = {a2.ToSimplifiedString()}，b² = {b2.ToSimplifiedString()}");
            Say($"结论：轨迹是中心 ({cx.ToSimplifiedString()}, {cy.ToSimplifiedString()}) 的椭圆。");
        }
        else
        {
            Say("中心在无穷远：抛物线/双曲情形，未继续算轴。");
        }
    }

    // --- 数值发现工具（与 DiscoveryPrototypeExperiment 的数值工具同构） ---

    private static double[]? FindNumericNullVector1D(double[,] m)
    {
        int rows = m.GetLength(0), cols = m.GetLength(1);
        var a = (double[,])m.Clone();
        var pivotCol = new int[rows];
        Array.Fill(pivotCol, -1);
        var r = 0;

        for (var col = 0; col < cols && r < rows; col++)
        {
            var piv = -1;
            var bestAbs = 1e-8;
            for (var i = r; i < rows; i++)
            {
                var abs = Math.Abs(a[i, col]);
                if (abs > bestAbs)
                {
                    bestAbs = abs;
                    piv = i;
                }
            }
            if (piv < 0) continue;

            if (piv != r)
                for (var j = col; j < cols; j++)
                    (a[r, j], a[piv, j]) = (a[piv, j], a[r, j]);

            var pv = a[r, col];
            for (var j = col; j < cols; j++) a[r, j] /= pv;

            for (var i = 0; i < rows; i++)
            {
                if (i == r || Math.Abs(a[i, col]) < 1e-12) continue;
                var factor = a[i, col];
                for (var j = col; j < cols; j++) a[i, j] -= factor * a[r, j];
            }
            pivotCol[r] = col;
            r++;
        }

        if (cols - r != 1) return null;

        var used = new bool[cols];
        for (var i = 0; i < r; i++) used[pivotCol[i]] = true;
        var freeCol = Array.IndexOf(used, false);

        var vec = new double[cols];
        vec[freeCol] = 1.0;
        for (var i = 0; i < r; i++) vec[pivotCol[i]] = -a[i, freeCol];

        var maxAbs = vec.Max(Math.Abs);
        if (maxAbs > 0)
            for (var i = 0; i < cols; i++) vec[i] /= maxAbs;
        return vec;
    }

    private static (long Num, long Den)? SnapRational(double value, long maxDen = 1000, double tol = 1e-8)
    {
        if (Math.Abs(value) < tol) return (0, 1);
        var sign = value < 0 ? -1 : 1;
        double x = Math.Abs(value);
        long p0 = 0, q0 = 1, p1 = 1, q1 = 0;
        var rem = x;

        for (var i = 0; i < 32; i++)
        {
            var a = (long)Math.Floor(rem);
            long p2 = a * p1 + p0, q2 = a * q1 + q0;
            if (q2 > maxDen) break;
            (p0, q0) = (p1, q1);
            (p1, q1) = (p2, q2);
            if (Math.Abs(x - (double)p1 / q1) < tol) return (sign * p1, q1);
            var frac = rem - a;
            if (frac < 1e-12) break;
            rem = 1.0 / frac;
        }
        return Math.Abs(x - (double)p1 / q1) < tol ? (sign * p1, q1) : null;
    }

    private static long Gcd(long a, long b)
    {
        while (true)
        {
            if (b == 0) return Math.Abs(a);
            var a1 = a;
            a = b;
            b = a1 % b;
        }
    }

    private static long Lcm(long a, long b) => a / Gcd(a, b) * b;

    private static string Eq(long[] q)
    {
        var sb = new System.Text.StringBuilder();

        void Term(long coeff, string term)
        {
            if (coeff == 0) return;
            if (sb.Length > 0) sb.Append(coeff > 0 ? " + " : " - ");
            else if (coeff < 0) sb.Append('-');
            var abs = Math.Abs(coeff);
            if (abs != 1 || term.Length == 0) sb.Append(abs);
            sb.Append(term);
        }

        Term(q[0], "x²");
        Term(2 * q[1], "xy");
        Term(2 * q[2], "xw");
        Term(q[3], "y²");
        Term(2 * q[4], "yw");
        Term(q[5], "w²");
        return sb.Length == 0 ? "0 = 0" : sb.Append(" = 0").ToString();
    }
}
