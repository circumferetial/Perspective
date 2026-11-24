using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Experiment;

/// <summary>
///     实验：从"最一般的对象"出发，逐条加条件（线性约束），观察对象如何被约束成
///     更小的线性系，直到唯一。
///     <para>
///         直线：3 维系数空间（齐次意义下 2 维）；条件 l·P = 0 是线性方程。
///         无约束 → 2 维（整个平面直线）；1 个点 → 1 维（过该点的线束）；2 个点 → 0 维（唯一）。
///     </para>
/// </summary>
public static class ConstraintLadder
{
    public static void Run()
    {
        Console.WriteLine("\n=== 条件演化：直线 ===");
        var points = new[]
        {
            Vec3.Point(ExactScalar.FromInt(0), ExactScalar.FromInt(0)),
            Vec3.Point(ExactScalar.FromInt(1), ExactScalar.FromInt(1))
        };

        for (var k = 0; k <= points.Length; k++)
        {
            var rows = points.Take(k)
                .Select(p => new[] { p.X, p.Y, p.Z })
                .ToList();
            var nullity = LinearAlgebra.NullspaceDimension(rows, 3);
            var meaning = k switch
            {
                0 => "所有直线（射影 2 维）",
                1 => "过一点的直线束（1 维 = 线系）",
                _ => nullity == 1 ? "唯一一条直线（0 维）" : "条件矛盾（无直线）"
            };
            var dims = nullity > 0
                ? $"线性维数 {nullity}（射影维数 {nullity - 1}）"
                : "线性维数 0（无解）";
            Console.WriteLine($"  加 {k} 个「点在线上」约束 → {dims}：{meaning}");
        }

        Console.WriteLine("\n=== 条件演化：圆锥曲线（线性系）===");
        var p1 = HPoint.FromCartesian(1, 0);
        var p2 = HPoint.FromCartesian(0, 1);
        var p3 = HPoint.FromCartesian(-1, 0);
        var p4 = HPoint.FromCartesian(0, -1);

        // C1：单位圆；C2：线对 (x+y−1)(x+y+1)=0，均过上面四点
        var c1 = Conic.Circle(0, 0, ExactScalar.FromInt(1));
        var l1 = new HLine(1, 1, -1);// x + y − 1 = 0
        var l2 = new HLine(1, 1, 1);// x + y + 1 = 0
        var c2 = ExperimentMath.LinePair(l1, l2);

        Console.WriteLine("  过 4 点 ⇒ 1 维线性系（曲线系）C(λ) = C1 + λ·C2");
        var lambdas = new[] { ExactScalar.FromInt(0), ExactScalar.FromInt(1), ExactScalar.FromInt(-1) };
        foreach (var lambda in lambdas)
        {
            var c = Pencil(c1, c2, lambda);
            var onAll = new[] { p1, p2, p3, p4 }.All(p => c.ContainsStrictly(p) == StrictResult.StrictlyTrue);
            Console.WriteLine($"    λ={lambda}: 是否仍过 4 点？{onAll}；det={c.Matrix.Determinant()}");
        }

        // 再加第 5 个点：齐次参数 (λ1:λ2) 满足 λ1·v1 + λ2·v2 = 0。
        // 不能直接假设 v2≠0：点可能落在 C2 上，此时唯一曲线就是 C2 本身（齐次解 (0:1)）。
        var isZero = (Func<ExactScalar, bool>)(s => s.VerifyStrictZero() == StrictResult.StrictlyTrue);

        void SolveFifthPoint(string label, HPoint p)
        {
            var v1 = Evaluate(c1, p);
            var v2 = Evaluate(c2, p);

            if (!isZero(v2))
            {
                var lambdaStar = -(v1 / v2);
                var unique = Pencil(c1, c2, lambdaStar);
                var ok = new[] { p1, p2, p3, p4, p }
                    .All(q => unique.ContainsStrictly(q) == StrictResult.StrictlyTrue);
                Console.WriteLine($"  再加第 5 点 {label} ⇒ λ* = {lambdaStar}，唯一曲线，过全部 5 点？{ok}");
            }
            else if (!isZero(v1))
            {
                var ok = new[] { p1, p2, p3, p4, p }
                    .All(q => c2.ContainsStrictly(q) == StrictResult.StrictlyTrue);
                Console.WriteLine($"  第 5 点 {label} 在 C2 上 ⇒ (λ1:λ2)=(0:1)，唯一曲线是 C2，过全部 5 点？{ok}");
            }
            else
            {
                Console.WriteLine($"  第 5 点 {label} 落在整族上（C1、C2 都过它）⇒ 条件未进一步约束，仍是曲线系");
            }
        }

        SolveFifthPoint("(1,1)", HPoint.FromCartesian(1, 1));
        SolveFifthPoint("(2,−1)（边界：在 C2 上）", HPoint.FromCartesian(2, -1));
        Console.WriteLine("  （λ 的取值就是「约束如何选出一条具体曲线」的演化结果）");
    }

    private static Conic Pencil(Conic c1, Conic c2, ExactScalar lambda) =>
        new(new Matrix3x3(
            c1.Matrix.M00 + lambda * c2.Matrix.M00, c1.Matrix.M01 + lambda * c2.Matrix.M01,
            c1.Matrix.M02 + lambda * c2.Matrix.M02,
            c1.Matrix.M10 + lambda * c2.Matrix.M10, c1.Matrix.M11 + lambda * c2.Matrix.M11,
            c1.Matrix.M12 + lambda * c2.Matrix.M12,
            c1.Matrix.M20 + lambda * c2.Matrix.M20, c1.Matrix.M21 + lambda * c2.Matrix.M21,
            c1.Matrix.M22 + lambda * c2.Matrix.M22));

    private static ExactScalar Evaluate(Conic c, HPoint p) =>
        p.X * (c.Matrix.M00 * p.X + c.Matrix.M01 * p.Y + c.Matrix.M02 * p.W)
        + p.Y * (c.Matrix.M10 * p.X + c.Matrix.M11 * p.Y + c.Matrix.M12 * p.W)
        + p.W * (c.Matrix.M20 * p.X + c.Matrix.M21 * p.Y + c.Matrix.M22 * p.W);
}

/// <summary>极简精确线性代数（实验用）：解空间维数。</summary>
public static class LinearAlgebra
{
    public static int NullspaceDimension(IReadOnlyList<ExactScalar[]> rows, int columns)
    {
        var matrix = rows.Select(r => r.ToArray()).ToList();
        var rank = 0;
        for (var col = 0; col < columns && rank < matrix.Count; col++)
        {
            var pivot = -1;
            for (var row = rank; row < matrix.Count; row++)
                if (matrix[row][col].VerifyStrictZero() != StrictResult.StrictlyTrue)
                {
                    pivot = row;
                    break;
                }
            if (pivot < 0) continue;

            (matrix[rank], matrix[pivot]) = (matrix[pivot], matrix[rank]);
            var pv = matrix[rank][col];
            for (var j = col; j < columns; j++)
                matrix[rank][j] = matrix[rank][j] / pv;

            for (var row = 0; row < matrix.Count; row++)
            {
                if (row == rank) continue;
                var factor = matrix[row][col];
                if (factor.VerifyStrictZero() == StrictResult.StrictlyTrue) continue;
                for (var j = col; j < columns; j++)
                    matrix[row][j] = matrix[row][j] - factor * matrix[rank][j];
            }
            rank++;
        }
        return columns - rank;
    }
}