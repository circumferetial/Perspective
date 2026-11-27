using Perspective.Core.Math;
using Perspective.Experiment.Subspace;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>
///     方向 B 原型：约束与零空间自动发现定理。
///     1) 关联零空间自动发现 Pascal 线，对偶同构自动生成 Brianchon 共点；
///     2) Veronese 6 维零空间从任意线性条件自动长出圆锥曲线；
///     3) 对吴方法三角特征列回代采样 + 零空间猜想 + 连分数还原 + 符号严证，自动发现动弦的未知包络圆！
/// </summary>
public static class DiscoveryPrototypeExperiment
{
    public static void Run()
    {
        Console.WriteLine("\n================ 方向 B：零空间自动发现定理 & 盲猜包络 ================");

        DiscoverPascalAndDualBrianchon();
        DiscoverConicFromLinearConstraints();
        DiscoverAndProveCoupledEnvelope();
    }

    // ------------------------------------------------------------------------
    // [1] 零空间自动冒出 Pascal 线 → 对偶一次自动变身 Brianchon 点
    // ------------------------------------------------------------------------
    private static void DiscoverPascalAndDualBrianchon()
    {
        Console.WriteLine("\n[1] 关联矩阵零空间自动发现定理（Pascal 线 → 一键对偶 → Brianchon 点）");

        static Primal3<ExactScalar> OnUnitCircle(int t) =>
            new(1 - t * t, 2 * t, 1 + t * t);

        var circle = new BilinearForm3<ExactScalar>(1, 0, 0, 1, 0, -1);
        Primal3<ExactScalar>[] pts =
        [
            OnUnitCircle(0), OnUnitCircle(1), OnUnitCircle(2),
            OnUnitCircle(3), OnUnitCircle(4), OnUnitCircle(5)
        ];

        // 对边交点 X, Y, Z（完全不告诉程序它们共线）
        var x = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(pts[0], pts[1]), SubspaceAlgebra.Join(pts[3], pts[4]));
        var y = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(pts[1], pts[2]), SubspaceAlgebra.Join(pts[4], pts[5]));
        var z = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(pts[2], pts[3]), SubspaceAlgebra.Join(pts[5], pts[0]));

        // 算 3×3 坐标矩阵的零空间：非平凡零向量就是"同时过 X, Y, Z 的直线"！
        var pMat = new ExactScalar[,]
        {
            { x.X, x.Y, x.W },
            { y.X, y.Y, y.W },
            { z.X, z.Y, z.W }
        };
        var pascalVec = ExactGeometryOps.FindExactNullVector1D(pMat)
                        ?? throw new InvalidOperationException("未发现共线依赖。");
        var pascalLine = new Dual3<ExactScalar>(pascalVec[0], pascalVec[1], pascalVec[2]);

        Console.WriteLine($"  自动发现 Pascal 线 = ({pascalLine.A.ToSimplifiedString()}·X + " +
                          $"{pascalLine.B.ToSimplifiedString()}·Y + {pascalLine.C.ToSimplifiedString()}·W = 0)");
        Console.WriteLine($"  三交点严格共线？{SubspaceAlgebra.AreCollinear(x, y, z)}");

        // --- 对偶化：6 个点变成 6 条切线，Join ↔ Meet 互换，喂给完全同一个零空间函数！ ---
        var tangents = pts.Select(circle.Polar).ToArray();
        var d1 = SubspaceAlgebra.Join(SubspaceAlgebra.Meet(tangents[0], tangents[1]),
            SubspaceAlgebra.Meet(tangents[3], tangents[4]));
        var d2 = SubspaceAlgebra.Join(SubspaceAlgebra.Meet(tangents[1], tangents[2]),
            SubspaceAlgebra.Meet(tangents[4], tangents[5]));
        var d3 = SubspaceAlgebra.Join(SubspaceAlgebra.Meet(tangents[2], tangents[3]),
            SubspaceAlgebra.Meet(tangents[5], tangents[0]));

        var dMat = new ExactScalar[,]
        {
            { d1.A, d1.B, d1.C },
            { d2.A, d2.B, d2.C },
            { d3.A, d3.B, d3.C }
        };
        var brianchonVec = ExactGeometryOps.FindExactNullVector1D(dMat)
                           ?? throw new InvalidOperationException("未发现共点依赖。");
        var brianchonPt = new Primal3<ExactScalar>(brianchonVec[0], brianchonVec[1], brianchonVec[2]);

        Console.WriteLine($"  对偶自动生成 Brianchon 共点 = [{brianchonPt.X.ToSimplifiedString()}, " +
                          $"{brianchonPt.Y.ToSimplifiedString()}, {brianchonPt.W.ToSimplifiedString()}]");
        Console.WriteLine($"  三条主对角线严格共点？{SubspaceAlgebra.AreConcurrent(d1, d2, d3)}");
    }

    // ------------------------------------------------------------------------
    // [2] Veronese 6 维零空间：只给线性条件，自动“长出”圆锥曲线（无分类）
    // ------------------------------------------------------------------------
    private static void DiscoverConicFromLinearConstraints()
    {
        Console.WriteLine("\n[2] Veronese 6 维零空间：纯线性约束直接长出圆锥曲线（Q00, Q01, Q02, Q11, Q12, Q22）");

        // 未知二次型 Q(x,y,w) = Q00·x² + 2Q01·xy + 2Q02·xw + Q11·y² + 2Q12·yw + Q22·w²
        static ExactScalar[] PassThrough(Primal3<ExactScalar> p) => Veronese.Row(p);

        // 例：已知圆心 (1, -2)、且是圆（过圆环点 I,J：Q00-Q11=0, Q01=0）、过点 (4, 2)
        //     总计 2 + 2 + 1 = 5 条线性约束，自动长出唯一圆！
        var rows = new List<ExactScalar[]>
        {
            new ExactScalar[] { 1, 0, 0, -1, 0, 0 }, // IsCircle: Q00 = Q11
            new ExactScalar[] { 0, 1, 0, 0, 0, 0 }, // IsCircle: Q01 = 0
            new ExactScalar[] { 1, -2, 1, 0, 0, 0 }, // Center(1,-2): Polar(O) 的 X 分量为 0 → Q00·1 + Q01·(-2) + Q02·1 = 0
            new ExactScalar[] { 0, 1, 0, -2, 1, 0 }, // Center(1,-2): Polar(O) 的 Y 分量为 0 → Q01·1 + Q11·(-2) + Q12·1 = 0
            PassThrough(Primal3<ExactScalar>.Fixed(4, 2))
        };

        var q = ExactGeometryOps.FindExactNullVector1D(ToRect(rows))!;
        var circle = new BilinearForm3<ExactScalar>(q[0], q[1], q[2], q[3], q[4], q[5]);
        Console.WriteLine($"  自动解出圆矩阵 = ({q[0].ToSimplifiedString()}, {q[1].ToSimplifiedString()}, " +
                          $"{q[2].ToSimplifiedString()}, {q[3].ToSimplifiedString()}, {q[4].ToSimplifiedString()}, {q[5].ToSimplifiedString()})");
        // 验证：圆心 (1,-2)、过 (4,2) 应为 (x-1)²+(y+2)²=25 → 过 (-3, 1) 也成立！
        Console.WriteLine($"  自动推导出的圆严格过对称点 (-3, 1)（3-4-5 半径 5）？" +
                          $"{circle.Eval(Primal3<ExactScalar>.Fixed(-3, 1)).VerifyStrictZero()}");
    }

    // ------------------------------------------------------------------------
    // [3] 自动猜想 + 吴特征列严证：未知 OA⊥OB 动弦，自己摸出包络圆 5x²+5y²=4 并证死！
    // ------------------------------------------------------------------------
    private static void DiscoverAndProveCoupledEnvelope()
    {
        Console.WriteLine("\n[3] 自动发现并证明未知包络：椭圆 x²+4y²=4 上 OA⊥OB 动弦（不提前告诉它答案！）");

        var space = new UnifiedGeoSpace();
        var origin = Primal3<PolyExpr>.Fixed(0, 0);
        var ellipse = new BilinearForm3<PolyExpr>(1, 0, 0, 4, 0, -4);

        var ptA = space.NewFreePoint("A");
        var ptB = space.NewFreePoint("B");
        space.RequireOnConic(ptA, ellipse);
        space.RequireOnConic(ptB, ellipse);
        space.RequirePerpendicular(SubspaceAlgebra.Join(origin, ptA), SubspaceAlgebra.Join(origin, ptB));

        var chordAB = SubspaceAlgebra.Join(ptA, ptB);

        // 第 1 步：取出三角特征列，顺着特征列“自底向上”数值求解 6 条严格满足耦合约束的动弦
        Console.WriteLine("  [dbg] 开始求特征列...");
        Console.Out.Flush();
        var (consistent, chain) = space.GetCharacteristicSet();
        Console.WriteLine($"  [dbg] 特征列完成，共 {chain.Count} 条");
        Console.Out.Flush();
        if (!consistent) throw new InvalidOperationException("约束矛盾。");

        double[] axProbes = [0.25, 0.55, 0.85, 1.15, 1.45, 1.75];
        var veroneseRows = new double[axProbes.Length, 6];

        for (int k = 0; k < axProbes.Length; k++)
        {
            var env = SolveChainNumerically(chain, freeVarId: 0, freeValue: axProbes[k], space.VariableCount);
            double a = chordAB.A.EvalApprox(id => env[id]);
            double b = chordAB.B.EvalApprox(id => env[id]);
            double c = chordAB.C.EvalApprox(id => env[id]);

            // 对偶 Veronese 特征行：S00·A² + 2S01·AB + 2S02·AC + S11·B² + 2S12·BC + S22·C² = 0
            var row = Veronese.LineRow(a, b, c);
            for (var j = 0; j < 6; j++) veroneseRows[k, j] = row[j];
        }

        // 第 2 步：对偶 Veronese 零空间猜出包络对偶二次型（浮点数 → 连分数有理还原）
        var approxNull = FindNumericNullVector1D(veroneseRows)
                         ?? throw new InvalidOperationException("未发现稳定的 1 维包络曲线。");

        var rationals = approxNull.Select(v => SnapRational(v, maxDen: 1000)
                ?? throw new InvalidOperationException($"系数 {v} 未能还原为简单有理数。")).ToArray();

        long lcm = 1;
        foreach (var (_, den) in rationals)
            lcm = Lcm(lcm, den);

        var coeffs = rationals.Select(r => PolyExpr.FromLong(r.Num * (lcm / r.Den))).ToArray();
        var discoveredEnvelopeDual = new DualBilinearForm3<PolyExpr>(
            coeffs[0], coeffs[1], coeffs[2], coeffs[3], coeffs[4], coeffs[5]);

        Console.WriteLine($"  数值采样+连分数猜出的对偶包络 S = ({string.Join(", ", coeffs.Select(space.Format))})");
        Console.Out.Flush();

        // 伴随回到原空间二次曲线（即包络圆方程）
        var primalGuess = new BilinearForm3<PolyExpr>(
            coeffs[3] * coeffs[5] - coeffs[4] * coeffs[4],
            coeffs[2] * coeffs[4] - coeffs[1] * coeffs[5],
            coeffs[1] * coeffs[4] - coeffs[2] * coeffs[3],
            coeffs[0] * coeffs[5] - coeffs[2] * coeffs[2],
            coeffs[1] * coeffs[2] - coeffs[0] * coeffs[4],
            coeffs[0] * coeffs[3] - coeffs[1] * coeffs[1]);
        Console.WriteLine(
            $"  还原到原空间二次曲线 Q = (Q00={space.Format(primalGuess.Q00)}, Q11={space.Format(primalGuess.Q11)}, Q22={space.Format(primalGuess.Q22)})");

        // 第 3 步：送回吴特征列做符号级严格证明！
        var proof = space.ProveZero(discoveredEnvelopeDual.Eval(chordAB));
        Console.WriteLine($"  吴特征列严格验证猜想结果：{proof.Status}（余式：{space.Format(proof.Remainder)}）");
    }

    // ========================================================================
    // 通用数值辅助工具（Numeric 零空间 / 三角链采样 / 连分数还原）
    // ========================================================================

    private static double[]? FindNumericNullVector1D(double[,] m)
    {
        int rows = m.GetLength(0), cols = m.GetLength(1);
        var a = (double[,])m.Clone();
        var pivotCol = new int[rows];
        Array.Fill(pivotCol, -1);
        int r = 0;

        for (int col = 0; col < cols && r < rows; col++)
        {
            int piv = -1;
            double bestAbs = 1e-8;
            for (int i = r; i < rows; i++)
            {
                double abs = Math.Abs(a[i, col]);
                if (abs > bestAbs) { bestAbs = abs; piv = i; }
            }
            if (piv < 0) continue;

            if (piv != r)
                for (int j = col; j < cols; j++)
                    (a[r, j], a[piv, j]) = (a[piv, j], a[r, j]);

            double pv = a[r, col];
            for (int j = col; j < cols; j++) a[r, j] /= pv;

            for (int i = 0; i < rows; i++)
            {
                if (i == r || Math.Abs(a[i, col]) < 1e-12) continue;
                double factor = a[i, col];
                for (int j = col; j < cols; j++) a[i, j] -= factor * a[r, j];
            }
            pivotCol[r] = col;
            r++;
        }

        if (cols - r != 1) return null;

        var used = new bool[cols];
        for (int i = 0; i < r; i++) used[pivotCol[i]] = true;
        int freeCol = Array.IndexOf(used, false);

        var vec = new double[cols];
        vec[freeCol] = 1.0;
        for (int i = 0; i < r; i++) vec[pivotCol[i]] = -a[i, freeCol];

        double maxAbs = vec.Max(Math.Abs);
        if (maxAbs > 0)
            for (int i = 0; i < cols; i++) vec[i] /= maxAbs;
        return vec;
    }

    private static double[] SolveChainNumerically(
        IReadOnlyList<PolyExpr> chain, int freeVarId, double freeValue, int varCount)
    {
        var env = new double[varCount];
        env[freeVarId] = freeValue;

        foreach (var g in chain)
        {
            int v = g.HighestVar;
            var coeffs = g.CoeffsIn(v);
            int deg = coeffs.Length - 1;

            if (deg == 1)
            {
                double c0 = coeffs[0].EvalApprox(id => env[id]);
                double c1 = coeffs[1].EvalApprox(id => env[id]);
                env[v] = -c0 / c1;
            }
            else if (deg == 2)
            {
                double c0 = coeffs[0].EvalApprox(id => env[id]);
                double c1 = coeffs[1].EvalApprox(id => env[id]);
                double c2 = coeffs[2].EvalApprox(id => env[id]);
                double disc = Math.Max(0, c1 * c1 - 4 * c2 * c0);
                env[v] = (-c1 + Math.Sqrt(disc)) / (2 * c2);
            }
            else
            {
                throw new NotSupportedException($"暂仅演示 1~2 次特征列自底向上求解，当前次数={deg}");
            }
        }
        return env;
    }

    private static (long Num, long Den)? SnapRational(double value, long maxDen = 1000, double tol = 1e-8)
    {
        if (Math.Abs(value) < tol) return (0, 1);
        int sign = value < 0 ? -1 : 1;
        double x = Math.Abs(value);
        long p0 = 0, q0 = 1, p1 = 1, q1 = 0;
        double rem = x;

        for (int i = 0; i < 32; i++)
        {
            long a = (long)Math.Floor(rem);
            long p2 = a * p1 + p0, q2 = a * q1 + q0;
            if (q2 > maxDen) break;
            (p0, q0) = (p1, q1);
            (p1, q1) = (p2, q2);
            if (Math.Abs(x - (double)p1 / q1) < tol) return (sign * p1, q1);
            double frac = rem - a;
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

    private static ExactScalar[,] ToRect(List<ExactScalar[]> rows)
    {
        int r = rows.Count, c = rows[0].Length;
        var res = new ExactScalar[r, c];
        for (int i = 0; i < r; i++)
        for (int j = 0; j < c; j++) res[i, j] = rows[i][j];
        return res;
    }
}
