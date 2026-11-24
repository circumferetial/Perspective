using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Experiment;

/// <summary>四维向量（三维射影点/平面的齐次坐标）。</summary>
public readonly record struct Vec4(ExactScalar X, ExactScalar Y, ExactScalar Z, ExactScalar W);

/// <summary>直线的 Plücker 坐标（六个 2×2 小行列式）。</summary>
public readonly record struct Plucker6(
    ExactScalar P01,
    ExactScalar P02,
    ExactScalar P03,
    ExactScalar P12,
    ExactScalar P13,
    ExactScalar P23);

/// <summary>
///     三维射影几何的"本质描述"：点=1维子空间（4向量）、线=2维子空间（Plücker 6 元组）、
///     平面=3维子空间（对偶 4 向量）。同一楔积既做 join 又判关联（为零即"点在线上"）。
/// </summary>
public static class GrassmannExperiment
{
    public static void Run()
    {
        Console.WriteLine("\n=== 三维子空间：点/线/平面的统一楔积 ===");

        var a = new Vec4(ExactScalar.FromInt(1), ExactScalar.FromInt(0), ExactScalar.FromInt(0),
            ExactScalar.FromInt(1));
        var b = new Vec4(ExactScalar.FromInt(0), ExactScalar.FromInt(1), ExactScalar.FromInt(0),
            ExactScalar.FromInt(1));
        var c = new Vec4(ExactScalar.FromInt(0), ExactScalar.FromInt(0), ExactScalar.FromInt(1),
            ExactScalar.FromInt(1));

        // 线 = 两点楔积（Plücker 坐标）
        var line = Join(a, b);
        Console.WriteLine($"  过 a,b 的直线 = ({line.P01}, {line.P02}, {line.P03}, {line.P12}, {line.P13}, {line.P23})");
        Console.WriteLine($"  Plücker 关系 p01·p23 − p02·p13 + p03·p12 = " +
                          $"{line.P01 * line.P23 - line.P02 * line.P13 + line.P03 * line.P12}（恒为 0，无需额外约束）");

        // 自检：对 Z≠0 的一般点也验证 Plücker 恒等式（防止 P23 之类笔误被特殊点掩盖）
        var lineBC = Join(b, c);
        var lineCA = Join(c, a);
        CheckPlucker(line);
        CheckPlucker(lineBC);
        CheckPlucker(lineCA);
        Console.WriteLine($"  一般位置直线也通过 Plücker 自检：bc={FmtPlucker(lineBC)}；ca={FmtPlucker(lineCA)}");
        CheckJoinAgainstManual();

        // 关联自检：bc 的中点应在线 bc 上
        var mid = new Vec4(
            b.X + c.X, b.Y + c.Y, b.Z + c.Z, b.W + c.W);
        Console.WriteLine($"  bc 中点是否在 bc 线上？{IsZero(Wedge3(mid, b, c))}");

        // 关联：点 c 在线上 ⇔ 楔积 p∧a∧b = 0（四分量全零）
        var onLine = Wedge3(c, a, b);
        Console.WriteLine($"  c 是否在 a,b 的线上？{IsZero(onLine)}（楔积 {Fmt(onLine)}）");
        var d = new Vec4(ExactScalar.FromFormula("1/2"), ExactScalar.FromFormula("1/2"), ExactScalar.FromInt(0),
            ExactScalar.FromInt(1));
        Console.WriteLine($"  d=(1/2,1/2,0) 是否在线上？{IsZero(Wedge3(d, a, b))}");

        // 平面 = 三点楔积（对偶 4 向量）；点 p 在平面上 ⇔ 配对为 0
        var plane = Wedge3(a, b, c);
        Console.WriteLine($"  过 a,b,c 的平面 = ({plane.X}, {plane.Y}, {plane.Z}, {plane.W})");
        Console.WriteLine(
            $"  a 在平面上？{IsZeroScalar(Dot(plane, a))}；原点在平面上？{IsZeroScalar(Dot(plane, new Vec4(0, 0, 0, 1)))}");
        Console.WriteLine("  → 点、线、平面只是不同维数的子空间；join 和关联判定都是同一个楔积。");

        Console.WriteLine("\n=== Pascal = Plücker/线性依赖（直线上的三点共线 ⇒ 3×3 行列式为零）===");
        PascalAsPluckerRelation();
    }

    /// <summary>Pascal 定理：圆内接六边形三组对边交点共线；共线 ⇔ 三点楔积（3×3 行列式）为零。</summary>
    private static void PascalAsPluckerRelation()
    {
        // 单位圆的有理参数化：( (1−t²)/(1+t²), 2t/(1+t²) )
        static HPoint OnCircle(int t)
        {
            var tt = ExactScalar.FromInt(t * t);
            var den = ExactScalar.FromInt(1) + tt;
            return new HPoint(
                ExactScalar.FromInt(1) - tt,
                ExactScalar.FromInt(2 * t),
                den);
        }

        var p1 = OnCircle(0);
        var p2 = OnCircle(1);
        var p3 = OnCircle(2);
        var p4 = OnCircle(3);
        var p5 = OnCircle(4);
        var p6 = OnCircle(5);

        // 对边交点：X=P1P2∩P4P5，Y=P2P3∩P5P6，Z=P3P4∩P6P1
        var x = Intersect(p1, p2, p4, p5);
        var y = Intersect(p2, p3, p5, p6);
        var z = Intersect(p3, p4, p6, p1);

        var det = Determinant(x, y, z);
        Console.WriteLine($"  交点 X={Fmt(x)}，Y={Fmt(y)}，Z={Fmt(z)}");
        Console.WriteLine($"  det[X;Y;Z] = {det}（0 ⇒ 三点共线，这就是 Pascal 定理）");
        Console.WriteLine("  → 所谓「定理」，在这里就是一条 Plücker 关系/线性依赖，可以被算出来，而不是被假设。");

        // Pascal 线 = 三点张成空间的零空间（叉积），并验证三点都在线上
        var pascalLine = HLine.FromPoints(x, y);
        Console.WriteLine($"  Pascal 线 Z 在线上？{pascalLine.ContainsStrictly(z)}");
    }

    private static HPoint Intersect(HPoint a, HPoint b, HPoint c, HPoint d) =>
        HLine.Intersect(HLine.FromPoints(a, b), HLine.FromPoints(c, d));

    /// <summary>三点共线 ⇔ 齐次坐标 3×3 行列式为 0（= 三阶楔积的 Plücker 关系）。</summary>
    private static ExactScalar Determinant(HPoint a, HPoint b, HPoint c) =>
        new Matrix3x3(a.X, a.Y, a.W, b.X, b.Y, b.W, c.X, c.Y, c.W).Determinant();

    // ---------- 四维楔积工具 ----------

    private static Plucker6 Join(Vec4 a, Vec4 b) => new(
        a.X * b.Y - a.Y * b.X, a.X * b.Z - a.Z * b.X, a.X * b.W - a.W * b.X,
        a.Y * b.Z - a.Z * b.Y, a.Y * b.W - a.W * b.Y, a.Z * b.W - a.W * b.Z);

    /// <summary>三向量楔积（四分量），用于"平面"（对偶 4 向量）与"点在线上"的零判定。</summary>
    private static Vec4 Wedge3(Vec4 a, Vec4 b, Vec4 c) => new(
        a.Y * (b.Z * c.W - b.W * c.Z) - a.Z * (b.Y * c.W - b.W * c.Y) + a.W * (b.Y * c.Z - b.Z * c.Y),
        -(a.X * (b.Z * c.W - b.W * c.Z) - a.Z * (b.X * c.W - b.W * c.X) + a.W * (b.X * c.Z - b.Z * c.X)),
        a.X * (b.Y * c.W - b.W * c.Y) - a.Y * (b.X * c.W - b.W * c.X) + a.W * (b.X * c.Y - b.Y * c.X),
        -(a.X * (b.Y * c.Z - b.Z * c.Y) - a.Y * (b.X * c.Z - b.Z * c.X) + a.Z * (b.X * c.Y - b.Y * c.X)));

    private static ExactScalar Dot(Vec4 a, Vec4 b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    private static bool IsZeroScalar(ExactScalar s) => s.VerifyStrictZero() == StrictResult.StrictlyTrue;

    private static bool IsZero(Vec4 v) =>
        v.X.VerifyStrictZero() == StrictResult.StrictlyTrue &&
        v.Y.VerifyStrictZero() == StrictResult.StrictlyTrue &&
        v.Z.VerifyStrictZero() == StrictResult.StrictlyTrue &&
        v.W.VerifyStrictZero() == StrictResult.StrictlyTrue;

    /// <summary>用一般位置点（所有分量非零）逐分量对照手算结果，防止"特殊点把笔误掩盖"。</summary>
    private static void CheckJoinAgainstManual()
    {
        var g1 = new Vec4(1, 2, 3, 4);
        var g2 = new Vec4(5, 6, 7, 8);
        var l = Join(g1, g2);
        var expected = new[]
        {
            1 * 6 - 2 * 5, 1 * 7 - 3 * 5, 1 * 8 - 4 * 5,
            2 * 7 - 3 * 6, 2 * 8 - 4 * 6, 3 * 8 - 4 * 7
        };
        var actual = new[] { l.P01, l.P02, l.P03, l.P12, l.P13, l.P23 };
        for (var i = 0; i < 6; i++)
        {
            var diff = actual[i] - ExactScalar.FromInt(expected[i]);
            if (diff.VerifyStrictZero() != StrictResult.StrictlyTrue)
                throw new InvalidOperationException(
                    $"Join 分量 P{i} 与手算不符：{actual[i]} ≠ {expected[i]}");
        }
        Console.WriteLine("  Join 分量与手算逐项一致（一般位置点：Z≠0、W≠0）");
    }

    private static void CheckPlucker(Plucker6 l)
    {
        var relation = l.P01 * l.P23 - l.P02 * l.P13 + l.P03 * l.P12;
        if (relation.VerifyStrictZero() != StrictResult.StrictlyTrue)
            throw new InvalidOperationException($"Plücker 恒等式失败：{relation}");
    }

    private static string FmtPlucker(Plucker6 l) =>
        $"({l.P01},{l.P02},{l.P03},{l.P12},{l.P13},{l.P23})";

    private static string Fmt(Vec4 v) => $"({v.X}, {v.Y}, {v.Z}, {v.W})";
    private static string Fmt(HPoint p) => $"({p.X}, {p.Y}, {p.W})";
}