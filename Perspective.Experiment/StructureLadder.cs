using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Experiment;

/// <summary>
///     实验：同一批点，在不同变换群下，哪些量被保留——"空间 = 底层集合 + 结构"的可计算版本。
///     <para>
///         距离在相似变换下只缩放；同一直线上的线段比在仿射下保持；交比在射影下保持。
///         这对应 Erlangen 分层的 Isometry / Similarity / Affine / Projective。
///     </para>
/// </summary>
public static class StructureLadder
{
    public static void Run()
    {
        Console.WriteLine("\n=== 结构阶梯：哪些量在哪个群下保持 ===");

        // 取共线四点（直线 y = x），这样剪切变换会真正改变长度
        var a = HPoint.FromCartesian(0, 0);
        var b = HPoint.FromCartesian(1, 1);
        var c = HPoint.FromCartesian(2, 2);
        var d = HPoint.FromCartesian(4, 4);

        var transforms = new (string Name, Matrix3x3 M)[]
        {
            ("恒等", Matrix3x3.Identity),
            ("相似（旋转 30° + 缩放 2）",
                Matrix3x3.Rotation(ExactScalar.FromFormula("sqrt(3)/2"), ExactScalar.FromFormula("1/2"))
                * Matrix3x3.Scale(ExactScalar.FromInt(2), ExactScalar.FromInt(2))),
            ("仿射（剪切 x' = x + y/2）",
                new Matrix3x3(
                    ExactScalar.FromInt(1), ExactScalar.FromFormula("1/2"), ExactScalar.FromInt(0),
                    ExactScalar.FromInt(0), ExactScalar.FromInt(1), ExactScalar.FromInt(0),
                    ExactScalar.FromInt(0), ExactScalar.FromInt(0), ExactScalar.FromInt(1))),
            ("射影（M₂₀ = 1/5）",
                new Matrix3x3(
                    ExactScalar.FromInt(1), ExactScalar.FromInt(0), ExactScalar.FromInt(0),
                    ExactScalar.FromInt(0), ExactScalar.FromInt(1), ExactScalar.FromInt(0),
                    ExactScalar.FromFormula("1/5"), ExactScalar.FromInt(0), ExactScalar.FromInt(1)))
        };

        (double AB, double CD, double Ratio, double CrossRatio) Measure(Matrix3x3 m)
        {
            var a2 = m * a;
            var b2 = m * b;
            var c2 = m * c;
            var d2 = m * d;

            var ab = Metric.Distance(a2, b2).Approx;
            var cd = Metric.Distance(c2, d2).Approx;
            var cross = GeometryAudit.CrossRatio(a2, b2, c2, d2).Approx;
            return (ab, cd, ab / cd, cross);
        }

        var baseline = Measure(transforms[0].M);
        Console.WriteLine($"  {"变换",-24} {"|AB|",9} {"|CD|",9} {"|AB|/|CD|",11} {"交比",9}");
        foreach (var (name, m) in transforms)
        {
            var (ab, cd, ratio, cross) = Measure(m);
            Console.WriteLine($"  {name,-24} {ab,9:0.######} {cd,9:0.######} {ratio,11:0.######} {cross,9:0.######}");
        }

        Console.WriteLine("\n  结论：");
        Console.WriteLine("  - 相似群：长度整体缩放（|AB|、|CD| 同比变化），线段比与交比都不变；");
        Console.WriteLine("  - 仿射群：长度被剪切改变，但同一直线上的线段比保持，交比保持；");
        Console.WriteLine("  - 射影群：线段比也被破坏，只有交比保持。");
        Console.WriteLine($"  （基线：|AB|/|CD|={baseline.Ratio:0.######}，交比={baseline.CrossRatio:0.######}）");
    }
}