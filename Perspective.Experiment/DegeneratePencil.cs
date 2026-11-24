using AngouriMath;
using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Experiment;

/// <summary>
///     实验 P1：四点曲线系里的特征退化。
///     <para>
///         过同一组 4 点的圆锥曲线构成一个 1 维线性系 C(λ) = C1 + λ·C2。
///         解 det(C1 + λ·C2) = 0 得到曲线系里的退化成员（线对）；
///         过 4 点的线对恰好有 3 种两两配对，对应 3 个退化成员（含 λ=∞ 的 C2 本身）。
///     </para>
///     <para>
///         这 3 组对边的 3 个交点构成整族曲线共同的自极三角形：
///         把坐标系对齐到该三角形（对角化两个二次型），整族曲线同时变成对角矩阵。
///     </para>
/// </summary>
public static class DegeneratePencil
{
    public static void Run()
    {
        Console.WriteLine("\n=== P1：四点曲线系里的特征退化（3 组退化线对 + 自极三角形）===");

        // 四个公共点（单位圆与线对的交点）
        var p = new[]
        {
            HPoint.FromCartesian(1, 0),
            HPoint.FromCartesian(0, 1),
            HPoint.FromCartesian(-1, 0),
            HPoint.FromCartesian(0, -1)
        };
        Console.WriteLine($"  四点：p1={ExperimentMath.Fmt(p[0])} p2={ExperimentMath.Fmt(p[1])} " +
                          $"p3={ExperimentMath.Fmt(p[2])} p4={ExperimentMath.Fmt(p[3])}");

        var c1 = Conic.Circle(0, 0, ExactScalar.FromInt(1));
        var c2 = ExperimentMath.LinePair(new HLine(1, 1, -1), new HLine(1, 1, 1));
        Console.WriteLine($"  C1 = {c1.ToEquationString()}");
        Console.WriteLine($"  C2 = {c2.ToEquationString()}（线对 (x+y−1)(x+y+1)=0）");

        // ---- det(C1 + λ·C2)，求退化成员的 λ ----
        var lam = ExactScalar.FromSymbol("lambda");
        var det = Conic.Pencil(c1, c2, ExactScalar.FromInt(1), lam).Matrix.Determinant().Simplified();
        Console.WriteLine($"  det(C1 + λ·C2) = {det}");

        var lamVar = MathS.Var("lambda");
        var detEntity = det.Exact ?? throw new InvalidOperationException("det 缺少精确表达式");
        Console.WriteLine($"  因式分解 = {MathS.Polynomials.Factor(detEntity, lamVar)!.Simplify()}");
        var rootSet = (IEnumerable<Entity>)MathS.SolveEquation(detEntity, lamVar);
        var roots = rootSet.ToList();
        Console.WriteLine($"  有限根 λ* = {string.Join(", ", roots.Select(r => r.ToString()))}" +
                          "（λ=∞ 给出 C2 本身，也是退化成员）");

        // ---- 每个有限根 → 退化线对 + 配对 ----
        var pairings = new List<(int A, int B, int C, int D)>();
        foreach (var root in roots)
        {
            var lambdaStar = ExactScalar.FromFormula(root.ToString());
            var c = Conic.Pencil(c1, c2, ExactScalar.FromInt(1), lambdaStar);

            Console.WriteLine($"\n  [λ* = {lambdaStar}]  C(λ*) = {c.ToEquationString()}");
            Console.WriteLine($"    det = {c.Matrix.Determinant().Simplified()}（退化）");

            var (lines, onA, onB) = Decompose(c, p);
            Console.WriteLine($"    两条直线：{ExperimentMath.Fmt(lines[0])}、{ExperimentMath.Fmt(lines[1])}");
            Console.WriteLine($"    配对：{{p{onA[0] + 1},p{onA[1] + 1}}} 与 {{p{onB[0] + 1},p{onB[1] + 1}}}");

            var allFour = p.All(pt => c.ContainsStrictly(pt) == StrictResult.StrictlyTrue);
            Console.WriteLine($"    过全部 4 点？{allFour}");

            pairings.Add((onA[0], onA[1], onB[0], onB[1]));
        }

        // ---- λ=∞ 的 C2 本身 ----
        Console.WriteLine("\n  [λ = ∞]  C2 本身（线对）");
        var (linesInf, onAInf, onBInf) = Decompose(c2, p);
        Console.WriteLine($"    两条直线：{ExperimentMath.Fmt(linesInf[0])}、{ExperimentMath.Fmt(linesInf[1])}");
        Console.WriteLine($"    配对：{{p{onAInf[0] + 1},p{onAInf[1] + 1}}} 与 {{p{onBInf[0] + 1},p{onBInf[1] + 1}}}");
        pairings.Add((onAInf[0], onAInf[1], onBInf[0], onBInf[1]));

        Console.WriteLine("\n  → 曲线系里不多不少 3 个退化成员，对应 4 点两两配对的全部 3 种方式。");

        // ---- 自极三角形：3 组对边的交点 ----
        Console.WriteLine("\n=== 自极三角形（3 组对边的交点）===");
        var diag = new HPoint[3];
        var labels = new string[3];
        for (var k = 0; k < 3; k++)
        {
            var (a, b, c, d) = pairings[k];
            var l1 = HLine.FromPoints(p[a], p[b]);
            var l2 = HLine.FromPoints(p[c], p[d]);
            diag[k] = HLine.Intersect(l1, l2);
            labels[k] = $"D{k + 1} = l(p{a + 1}p{b + 1}) ∩ l(p{c + 1}p{d + 1})";
            Console.WriteLine($"  {labels[k]} = {ExperimentMath.Fmt(diag[k])}");
        }

        // 两两共轭（关于 C1 与 C2）
        Console.WriteLine("  两两共轭（关于 C1）：");
        for (var i = 0; i < 3; i++)
        for (var j = i + 1; j < 3; j++)
            Console.WriteLine($"    {labels[i]} · C1 · {labels[j]} = " +
                              Conjugacy(c1, diag[i], diag[j]));
        Console.WriteLine("  两两共轭（关于 C2）：");
        for (var i = 0; i < 3; i++)
        for (var j = i + 1; j < 3; j++)
            Console.WriteLine($"    {labels[i]} · C2 · {labels[j]} = " +
                              Conjugacy(c2, diag[i], diag[j]));

        // ---- 对角化：以 {D1,D2,D3} 为基，C1 与 C2 同时对角化 ----
        Console.WriteLine("\n=== 对角化：以自极三角形为基，两个二次型同时对角化 ===");
        var basis = new Matrix3x3(
            diag[0].X, diag[1].X, diag[2].X,
            diag[0].Y, diag[1].Y, diag[2].Y,
            diag[0].W, diag[1].W, diag[2].W);

        foreach (var (name, c) in new[] { ("C1", c1), ("C2", c2) })
        {
            var diagM = basis.Transpose() * c.Matrix * basis;
            Console.WriteLine($"  {name} 在 {{D1,D2,D3}} 基下：");
            Console.WriteLine($"    对角元 = ({diagM.M00}, {diagM.M11}, {diagM.M22})");
            Console.WriteLine($"    非对角元 = ({diagM.M01}, {diagM.M02}, {diagM.M12})（应全为 0）");
        }
        Console.WriteLine("  → 两个二次型在同一基下同时对角化：整族曲线都变成对角矩阵。");
    }

    /// <summary>
    ///     把退化圆锥曲线分解成两条直线，并返回每条直线穿过的基点下标。
    /// </summary>
    private static (HLine[] Lines, int[] OnA, int[] OnB) Decompose(Conic c, HPoint[] pts)
    {
        var lines = new List<HLine>();
        foreach (var dir in ExperimentMath.DirectionsAtInfinity(c.Matrix))
            lines.AddRange(ExperimentMath.ComponentLines(pts, dir));

        var onA = pts.Select((pt, i) => (pt, i))
            .Where(x => lines[0].ContainsStrictly(x.pt) == StrictResult.StrictlyTrue)
            .Select(x => x.i).ToArray();
        var onB = pts.Select((pt, i) => (pt, i))
            .Where(x => lines[1].ContainsStrictly(x.pt) == StrictResult.StrictlyTrue)
            .Select(x => x.i).ToArray();
        return ([lines[0], lines[1]], onA, onB);
    }

    /// <summary>计算 uᵀ·C·v（两个点关于圆锥曲线的共轭量），返回化简后的表达式。</summary>
    private static string Conjugacy(Conic c, HPoint u, HPoint v)
    {
        var Cv = c.Matrix * v;// 矩阵乘点 → 向量（HPoint）
        var val = u.X * Cv.X + u.Y * Cv.Y + u.W * Cv.W;
        return val.Simplified().ToString();
    }
}