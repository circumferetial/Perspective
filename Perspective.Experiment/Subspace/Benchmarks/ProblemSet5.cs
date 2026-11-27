using System.Diagnostics;
using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace.Benchmarks;

public sealed record BenchmarkResult(
    string Id,
    string Expected,
    string Status,
    bool Passed,
    string Detail,
    long ElapsedMs);

/// <summary>
///     5 道椭圆题的基准：题面摘要、预期答案、当前管线状态、可执行检查。
///     目的：用实际数据判断“是否需要引入 SymPy/Z3 这类 oracle”。
/// </summary>
public static class ProblemSet5
{
    private static readonly (string Id, string Statement, string Expected, string Status, Func<(bool Ok, string Detail)> Check)[] Entries =
    [
        ("12(1)", "C: 焦距 2，过 (1, 3/2)，求方程", "x²/4 + y²/3 = 1", "已编码·直接验算", Check12a),
        ("12(2)", "A,B 在 C 上，∠AOB=90°，证 1/OA²+1/OB² 定值", "7/12", "已编码·ExactScalar 化简", Check12b),
        ("11(1)", "E: e=√2/2，上下顶点距 2，求方程", "x²/2 + y² = 1", "已编码·直接验算", Check11a),
        ("11(2)", "过 F₁ 的弦，S₁≤S₂，求 |AM| 最大值", "√2 + √3/3", "已编码·化到 y 的区间", Check11b),
        ("8", "距离比 √3/2，求轨迹", "x²/4 + y² = 1", "已编码·符号平方", Check08),
        ("9", "焦点张角 2π/3、面积 √3，求方程", "x²/4 + y² = 1", "已编码·点积+面积", Check09),
        ("10", "椭圆 x²/4+y²=1，P,Q 对称，∠PFQ=90°，求 |PQ|", "2√3", "已编码·圆交椭圆", Check10),
    ];

    public static IReadOnlyList<BenchmarkResult> RunAll()
    {
        var results = new List<BenchmarkResult>();
        Console.WriteLine($"\n================ 5 题基准（{Entries.Length} 项） ================");
        Console.WriteLine($"{"Id",-6} {"状态",-24} {"结果",-6} {"耗时",6}  说明");

        foreach (var entry in Entries)
        {
            var sw = Stopwatch.StartNew();
            var (ok, detail) = entry.Check();
            sw.Stop();
            results.Add(new BenchmarkResult(entry.Id, entry.Expected, entry.Status, ok, detail, sw.ElapsedMilliseconds));
            Console.WriteLine($"{entry.Id,-6} {entry.Status,-24} {(ok ? "PASS" : "FAIL"),-6} {sw.ElapsedMilliseconds,6}ms  {detail}");
        }

        var failed = results.Count(r => !r.Passed);
        Console.WriteLine($"---- {results.Count - failed}/{results.Count} 通过，{failed} 失败 ----");
        return results;
    }

    private static (bool, string) Check12a()
    {
        var a2 = ExactScalar.FromInt(4);
        var b2 = ExactScalar.FromInt(3);
        var c2 = a2 - b2;
        var point = ExactScalar.One / a2 + (ExactScalar.FromInt(9) / 4) / b2;

        var ok = ExactScalar.AreStrictlyEqual(c2, ExactScalar.One) == StrictResult.StrictlyTrue
                 && ExactScalar.AreStrictlyEqual(point, ExactScalar.One) == StrictResult.StrictlyTrue;
        return (ok, $"c²={c2.Approx:G6}，Q(1,3/2)={point.Approx:G6}");
    }

    private static (bool, string) Check12b()
    {
        var m = ExactScalar.FromSymbol("m");
        var conic = new BilinearForm3<ExactScalar>(
            ExactScalar.One / 4, ExactScalar.Zero, ExactScalar.Zero,
            ExactScalar.One / 3, ExactScalar.Zero, -ExactScalar.One);

        var (oa2, ob2) = conic.PerpendicularCentralRadii(m);
        var sum = ExactScalar.One / oa2 + ExactScalar.One / ob2;
        var simplified = sum.Simplified();
        var target = ExactScalar.FromInt(7) / ExactScalar.FromInt(12);

        var verdict = (simplified - target).VerifyStrictZero(allowFastNumericReject: false);
        return (verdict == StrictResult.StrictlyTrue,
            $"sum→{simplified.Approx:G8}（精确式 {simplified.ToSimplifiedString()}），Verify={verdict}");
    }

    private static (bool, string) Check11a()
    {
        var a2 = ExactScalar.FromInt(2);
        var b2 = ExactScalar.One;
        var e2 = ExactScalar.One - b2 / a2;
        var ab = ExactScalar.FromInt(2) * ExactScalar.Sqrt(b2);

        var ok = ExactScalar.AreStrictlyEqual(e2, ExactScalar.One / 2) == StrictResult.StrictlyTrue
                 && ExactScalar.AreStrictlyEqual(ab, ExactScalar.FromInt(2)) == StrictResult.StrictlyTrue;
        return (ok, $"e²={e2.Approx:G6}，|AB|={ab.Approx:G6}");
    }

    private static (bool, string) Check11b()
    {
        // S1=|y|, S2=|x| ⇒ |y|≤|x| ⇒ y²≤x²=2(1−y²) ⇒ y∈[−√(2/3),√(2/3)]
        // |AM|² = x²+(y−1)² = 3−2y−y²，在区间左端点取最大
        var bound = ExactScalar.Sqrt(ExactScalar.FromInt(2) / ExactScalar.FromInt(3));
        var (value2, at) = ExactGeometryOps.QuadraticMaxOnInterval(
            -ExactScalar.One, -ExactScalar.FromInt(2), ExactScalar.FromInt(3), -bound, bound);

        var expected = ExactScalar.Sqrt(2) + ExactScalar.Sqrt(3) / ExactScalar.FromInt(3);
        var verdict = (value2 - expected * expected).VerifyStrictZero(allowFastNumericReject: false);
        return (verdict == StrictResult.StrictlyTrue,
            $"y*={at.Approx:G8}，|AM|²={value2.Approx:G8}，Verify={verdict}");
    }

    private static (bool, string) Check08()
    {
        var sqrt3 = ExactScalar.Sqrt(ExactScalar.FromInt(3));
        var focus = new Primal3<ExactScalar>(sqrt3, ExactScalar.Zero, ExactScalar.One);
        var directrix = new Dual3<ExactScalar>(
            ExactScalar.One, ExactScalar.Zero,
            -ExactScalar.FromInt(4) * sqrt3 / ExactScalar.FromInt(3));

        var conic = ExactGeometryOps.ConicFromFocusDirectrix(
            focus, directrix, sqrt3 / ExactScalar.FromInt(2));

        var expected = new BilinearForm3<ExactScalar>(
            ExactScalar.One / 4, ExactScalar.Zero, ExactScalar.Zero,
            ExactScalar.One, ExactScalar.Zero, -ExactScalar.One);

        var ok = ExactScalar.AreStrictlyEqual(conic.Q00, expected.Q00) == StrictResult.StrictlyTrue
                 && ExactScalar.AreStrictlyEqual(conic.Q02, expected.Q02) == StrictResult.StrictlyTrue
                 && ExactScalar.AreStrictlyEqual(conic.Q11, expected.Q11) == StrictResult.StrictlyTrue
                 && ExactScalar.AreStrictlyEqual(conic.Q12, expected.Q12) == StrictResult.StrictlyTrue
                 && ExactScalar.AreStrictlyEqual(conic.Q22, expected.Q22) == StrictResult.StrictlyTrue;

        return (ok, $"Q=[{conic.Q00.Approx:G6},{conic.Q11.Approx:G6},{conic.Q22.Approx:G6}]");
    }

    private static (bool, string) Check09()
    {
        var c = ExactScalar.Sqrt(ExactScalar.FromInt(3));
        var f1 = new Primal3<ExactScalar>(-c, ExactScalar.Zero, ExactScalar.One);
        var f2 = new Primal3<ExactScalar>(c, ExactScalar.Zero, ExactScalar.One);
        var m = new Primal3<ExactScalar>(ExactScalar.Zero, ExactScalar.One, ExactScalar.One);

        var cos = ExactGeometryOps.CosAngleAt(m, f1, f2);
        if (!cos.TryGet(out var cosValue)) return (false, cos.Error.ToString());

        var twiceArea = ExactGeometryOps.TwiceSignedArea(f1, m, f2).Value;
        var area2 = twiceArea * twiceArea / ExactScalar.FromInt(4);

        var ok = ExactScalar.AreStrictlyEqual(cosValue, -ExactScalar.One / 2) == StrictResult.StrictlyTrue
                 && ExactScalar.AreStrictlyEqual(area2, ExactScalar.FromInt(3)) == StrictResult.StrictlyTrue;
        return (ok, $"cos={cosValue.Approx:G6}，面积²={area2.Approx:G6}");
    }

    private static (bool, string) Check10()
    {
        // |OP|²=|OF|²=3；与 x²/4+y²=1 联立 ⇒ x²=8/3, y²=1/3
        var x2 = ExactScalar.FromInt(8) / ExactScalar.FromInt(3);
        var y2 = ExactScalar.One / ExactScalar.FromInt(3);
        var onEllipse = x2 / ExactScalar.FromInt(4) + y2;
        var r2 = x2 + y2;
        var pq = ExactScalar.FromInt(2) * ExactScalar.Sqrt(r2);
        var expected = ExactScalar.FromInt(2) * ExactScalar.Sqrt(ExactScalar.FromInt(3));

        var ok = ExactScalar.AreStrictlyEqual(onEllipse, ExactScalar.One) == StrictResult.StrictlyTrue
                 && ExactScalar.AreStrictlyEqual(pq, expected) == StrictResult.StrictlyTrue;
        return (ok, $"x²={x2.Approx:G6}，y²={y2.Approx:G6}，|PQ|={pq.Approx:G6}");
    }
}
