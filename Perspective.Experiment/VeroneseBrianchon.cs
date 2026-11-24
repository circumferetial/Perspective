using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Experiment;

/// <summary>
///     实验 P3：Veronese 超平面视角 + Brianchon 对偶。
///     <para>
///         <b>Veronese 视角</b>：二次的圆锥曲线之所以表现出"线性的性质"，是因为有嵌入
///         ν₂: RP² → RP⁵，(x,y,z) ↦ (x², xy, y², xz, yz, z²)。在这个 5 维射影空间里：
///         平面上的每条圆锥曲线就是一个普通线性超平面（6 维系数向量）；
///         "点在圆锥曲线上"和"点在直线上"一样，只是 6 维内积等于 0；
///         于是"过 5 点定圆锥曲线"退化成解一个 5×6 线性方程组。
///         对偶圆锥曲线 C* = adj(C) 则把"点 ↔ 切线"互换：l 是切线 ⟺ lᵀC*l = 0。
///     </para>
///     <para>
///         <b>Brianchon 定理</b>：Pascal（内接六边形 → 三组对边交点共线）在点线对偶下
///         自动变成 Brianchon（外切六边形 → 三条主对角线共点）。同一套楔积/交比机器，
///         点线互换即得对偶定理，无需改底层逻辑。
///     </para>
/// </summary>
public static class VeroneseBrianchon
{
    public static void Run()
    {
        Console.WriteLine("\n=== P3：Veronese 超平面视角 + Brianchon 对偶 ===");
        VeroneseView();
        BrianchonDual();
    }

    // ---------- 1) Veronese 嵌入：二次的圆锥曲线 = 线性的超平面 ----------

    private static void VeroneseView()
    {
        Console.WriteLine("\n--- 1) Veronese 嵌入：二次的圆锥曲线 = 线性的超平面 ---");

        var circle = Conic.Circle(0, 0, ExactScalar.FromInt(1));
        var coeffs = ConicCoefficients(circle);
        Console.WriteLine("  Veronese 映射 ν₂: (x,y,z) ↦ (x², xy, y², xz, yz, z²)");
        Console.WriteLine($"  单位圆的系数向量（RP⁵ 中超平面的法向量）= ({Fmt6(coeffs)})");

        // 点在圆锥曲线上 = 6 维内积 = 0，与二次型值一致
        var testPoints = new[]
        {
            HPoint.FromCartesian(1, 0),
            HPoint.FromCartesian(0, 1),
            HPoint.FromCartesian(2, 3),
            HPoint.FromCartesian(-1, 2)
        };
        Console.WriteLine("  点在圆锥曲线上 = 6 维内积 = 0（与二次型值对照）：");
        foreach (var p in testPoints)
        {
            var v = Veronese(p);
            var inner = Dot6(v, coeffs);
            var quad = ExperimentMath.QuadraticValue(circle, p);
            Console.WriteLine($"    {ExperimentMath.Fmt(p)}：内积 = {inner.Simplified()}，" +
                              $"二次型 = {quad.Simplified()}（一致？{inner.Simplified() == quad.Simplified()}）");
        }

        // 过 5 点定圆锥曲线 = 解 5×6 线性方程组
        Console.WriteLine("\n  过 5 点定圆锥曲线 = 解线性方程组：");
        var five = new[] { OnCircle(0), OnCircle(1), OnCircle(2), OnCircle(3), OnCircle(4) };
        var rows = five.Select(Veronese).ToList();
        var nullity = LinearAlgebra.NullspaceDimension(rows, 6);
        Console.WriteLine($"    5 个点的 Veronese 向量 → 5×6 矩阵，解空间维数 = {nullity}");
        Console.WriteLine($"    单位圆系数向量在解空间中？" +
                          $"{five.All(p => Dot6(Veronese(p), coeffs).Simplified() == ExactScalar.FromInt(0))}");
        Console.WriteLine("  → 圆锥曲线是二次的，但在 RP⁵ 里只是超平面，所以求过点曲线是线性问题。");

        // 对偶圆锥曲线 C* = adj(C)：切线 ⟺ lᵀC*l = 0
        Console.WriteLine("\n  对偶圆锥曲线 C* = adj(C)：切线 ⟺ lᵀC*l = 0");
        var cStar = circle.Matrix.Adjugate();
        Console.WriteLine($"    单位圆 C* = diag({cStar.M00}, {cStar.M11}, {cStar.M22})");
        var lines = new (string Label, HLine L, bool IsTangent)[]
        {
            ("x = 1（切线）", new HLine(1, 0, -1), true),
            ("y = 1（切线）", new HLine(0, 1, -1), true),
            ("x = 2（非切线）", new HLine(1, 0, -2), false),
            ("x+y = 1（非切线）", new HLine(1, 1, -1), false)
        };
        foreach (var (label, l, isTangent) in lines)
        {
            var val = LineDualValue(cStar, l);
            Console.WriteLine($"    {label}：lᵀC*l = {val.Simplified()}" +
                              $"（切线应为 0：{(isTangent ? "是" : "否")}）");
        }
        Console.WriteLine("  → 圆锥曲线既是点的轨迹，也是切线的包络；点线互换靠 C* 完成。");
    }

    // ---------- 2) Brianchon 定理（Pascal 的对偶）----------

    private static void BrianchonDual()
    {
        Console.WriteLine("\n--- 2) Brianchon 定理：外切六边形的主对角线共点（Pascal 的对偶）---");

        var circle = Conic.Circle(0, 0, ExactScalar.FromInt(1));

        // 6 条切线（六边形外切于单位圆）
        var l = new HLine[6];
        for (var i = 0; i < 6; i++)
            l[i] = circle.PolarLine(OnCircle(i));
        Console.WriteLine("  6 条切线（外切六边形的边）：");
        for (var i = 0; i < 6; i++)
            Console.WriteLine($"    l{i + 1} = {ExperimentMath.Fmt(l[i])}");

        // 6 个顶点：V_i = l_{i-1} ∩ l_i
        var V = new HPoint[6];
        for (var i = 0; i < 6; i++)
            V[i] = HLine.Intersect(l[(i + 5) % 6], l[i]);
        Console.WriteLine("  6 个顶点（相邻切线的交点）：");
        for (var i = 0; i < 6; i++)
            Console.WriteLine($"    V{i + 1} = {ExperimentMath.Fmt(V[i])}");

        // 3 条主对角线：V1V4、V2V5、V3V6
        var D = new HLine[3];
        D[0] = HLine.FromPoints(V[0], V[3]);
        D[1] = HLine.FromPoints(V[1], V[4]);
        D[2] = HLine.FromPoints(V[2], V[5]);
        Console.WriteLine("  3 条主对角线：");
        for (var i = 0; i < 3; i++)
            Console.WriteLine($"    D{i + 1} = {ExperimentMath.Fmt(D[i])}");

        // 三线共点 ⟺ 系数矩阵行列式为 0
        var det = new Matrix3x3(
            D[0].A, D[0].B, D[0].C,
            D[1].A, D[1].B, D[1].C,
            D[2].A, D[2].B, D[2].C).Determinant();
        Console.WriteLine($"  det[D1;D2;D3] = {det.Simplified()}（0 ⇒ 三线共点，这就是 Brianchon 定理）");

        // 求实际交点并验证第三条线过该点
        var X = HLine.Intersect(D[0], D[1]);
        var onThird = D[2].ContainsStrictly(X);
        Console.WriteLine($"  D1∩D2 = {ExperimentMath.Fmt(X)}，D3 过该点？{onThird}");
        Console.WriteLine("  → Pascal（内接 → 对边交点共线）对偶成 Brianchon（外切 → 主对角线共点），" +
                          "点线互换即得，无需改底层逻辑。");
    }

    // ---------- 工具 ----------

    /// <summary>Veronese 映射：(x,y,z) ↦ (x², xy, y², xz, yz, z²)。</summary>
    private static ExactScalar[] Veronese(HPoint p) =>
    [
        p.X * p.X,
        p.X * p.Y,
        p.Y * p.Y,
        p.X * p.W,
        p.Y * p.W,
        p.W * p.W
    ];

    /// <summary>圆锥曲线在 RP⁵ 中的超平面法向量：(M00, 2M01, M11, 2M02, 2M12, M22)。</summary>
    private static ExactScalar[] ConicCoefficients(Conic c) =>
    [
        c.Matrix.M00,
        2 * c.Matrix.M01,
        c.Matrix.M11,
        2 * c.Matrix.M02,
        2 * c.Matrix.M12,
        c.Matrix.M22
    ];

    private static ExactScalar Dot6(ExactScalar[] a, ExactScalar[] b)
    {
        var sum = ExactScalar.FromInt(0);
        for (var i = 0; i < 6; i++)
            sum = sum + a[i] * b[i];
        return sum;
    }

    /// <summary>对偶圆锥曲线二次型：lᵀ·C*·l（判断直线 l 是否为切线）。</summary>
    private static ExactScalar LineDualValue(Matrix3x3 cStar, HLine l)
    {
        var CStarL = cStar * l;// 矩阵乘直线 → 向量（HLine）
        return l.A * CStarL.A + l.B * CStarL.B + l.C * CStarL.C;
    }

    /// <summary>单位圆的有理参数化点：t ↦ ((1−t²)/(1+t²), 2t/(1+t²))。</summary>
    private static HPoint OnCircle(int t)
    {
        var tt = ExactScalar.FromInt(t * t);
        var den = ExactScalar.FromInt(1) + tt;
        return new HPoint(
            ExactScalar.FromInt(1) - tt,
            ExactScalar.FromInt(2 * t),
            den);
    }

    private static string Fmt6(ExactScalar[] v) =>
        "(" + string.Join(", ", v.Select(x => x.Simplified().ToString())) + ")";
}