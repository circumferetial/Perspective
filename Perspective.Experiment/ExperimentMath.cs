using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Experiment;

/// <summary>
///     实验共用的几何小工具（P1/P2 共享，避免重复实现）。
/// </summary>
internal static class ExperimentMath
{
    /// <summary>
    ///     两条直线 l1, l2 的线对圆锥曲线：C = l1·l2ᵀ + l2·l1ᵀ（对称化），
    ///     对应退化二次型 l1·l2 = 0（两条直线的并集）。
    /// </summary>
    public static Conic LinePair(HLine a, HLine b)
    {
        var av = new[] { a.A, a.B, a.C };
        var bv = new[] { b.A, b.B, b.C };
        var m = new ExactScalar[3, 3];
        for (var i = 0; i < 3; i++)
        for (var j = 0; j < 3; j++)
            m[i, j] = av[i] * bv[j] + bv[i] * av[j];
        return new Conic(new Matrix3x3(
            m[0, 0], m[0, 1], m[0, 2],
            m[1, 0], m[1, 1], m[1, 2],
            m[2, 0], m[2, 1], m[2, 2]));
    }

    /// <summary>
    ///     求退化圆锥曲线（线对）与无穷远直线 z=0 的交点方向。
    ///     解 M00·x² + 2·M01·xy + M11·y² = 0，返回两个方向点 (x, y, 0)。
    ///     线对的两条直线方向即为此二解（重根表示两线平行）。
    /// </summary>
    public static HPoint[] DirectionsAtInfinity(Matrix3x3 m)
    {
        var a = m.M00;
        var b = 2 * m.M01;
        var c = m.M11;

        if (a.VerifyStrictZero() != StrictResult.StrictlyTrue)
        {
            var disc = b * b - 4 * a * c;
            var root = ExactScalar.Sqrt(disc);
            var x1 = (-b - root) / (2 * a);
            var x2 = (-b + root) / (2 * a);
            return
            [
                new HPoint(x1, ExactScalar.FromInt(1), ExactScalar.FromInt(0)),
                new HPoint(x2, ExactScalar.FromInt(1), ExactScalar.FromInt(0))
            ];
        }

        // a = 0：方程为 y(bx + cy) = 0，方向为 (1,0,0) 与 (-c, b, 0)
        var x = -c / b;
        return
        [
            new HPoint(ExactScalar.FromInt(1), ExactScalar.FromInt(0), ExactScalar.FromInt(0)),
            new HPoint(-c, b, ExactScalar.FromInt(0))
        ];
    }

    /// <summary>
    ///     找出过给定基点、方向为 direction、且作为退化圆锥曲线分支的直线。
    ///     判据：该直线至少再含一个基点（线对的每条分支恰过两点）。
    /// </summary>
    public static List<HLine> ComponentLines(HPoint[] points, HPoint direction)
    {
        var result = new List<HLine>();
        foreach (var p in points)
        {
            var l = HLine.FromPoints(p, direction);
            var count = points.Count(q => l.ContainsStrictly(q) == StrictResult.StrictlyTrue);
            if (count >= 2 && !result.Any(existing => AreProportional(existing, l)))
                result.Add(l);
        }
        return result;
    }

    /// <summary>两条直线是否成比例（表示同一直线）。</summary>
    public static bool AreProportional(HLine l1, HLine l2)
    {
        ExactScalar[] v1 = [l1.A, l1.B, l1.C];
        ExactScalar[] v2 = [l2.A, l2.B, l2.C];
        for (var i = 0; i < 3; i++)
        {
            if (v1[i].VerifyStrictZero() != StrictResult.StrictlyTrue)
            {
                var k = v2[i] / v1[i];
                return v1.Select((x, j) => x * k - v2[j])
                    .All(d => d.VerifyStrictZero() == StrictResult.StrictlyTrue);
            }
        }
        return false;
    }

    /// <summary>点格式：(x, y) 或 无穷远方向(x, y)。</summary>
    public static string Fmt(HPoint p) =>
        p.IsAtInfinity ? $"无穷远({p.X.Approx:G4}, {p.Y.Approx:G4})" : $"({p.X.Approx:G6}, {p.Y.Approx:G6})";

    /// <summary>直线格式：Ax + By + C = 0。</summary>
    public static string Fmt(HLine l) =>
        $"{l.A.Approx:G6}x + {l.B.Approx:G6}y + {l.C.Approx:G6} = 0";

    /// <summary>计算点 p 关于圆锥曲线 c 的二次型值 pᵀ·C·p。</summary>
    public static ExactScalar QuadraticValue(Conic c, HPoint p)
    {
        var Cp = c.Matrix * p;// 矩阵乘点 → 向量（HPoint）
        return p.X * Cp.X + p.Y * Cp.Y + p.W * Cp.W;
    }
}