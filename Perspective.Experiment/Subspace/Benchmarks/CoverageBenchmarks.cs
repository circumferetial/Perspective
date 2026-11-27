using System.Diagnostics;
using Perspective.Core.Math;
using Perspective.Experiment.Subspace;

namespace Perspective.Experiment.Subspace.Benchmarks;

/// <summary>
///     覆盖洞专项：焦点/准线、切线/极线、圆交圆。
///     这三项分别压 ConicFeatures、TangentsFromPoint/Polar、CircleCircleIntersection。
/// </summary>
public static class CoverageBenchmarks
{
    public static IReadOnlyList<BenchmarkResult> RunAll()
    {
        Console.WriteLine("\n================ 覆盖洞基准（3 项） ================");
        var results = new List<BenchmarkResult>
        {
            Run("COV-1", "抛物线焦点/准线定义", CheckFocusDirectrix),
            Run("COV-2", "外点切线切点弦 = 极线", CheckTangentChord),
            Run("COV-3", "两圆交于两点", CheckCircleCircle)
        };
        var passed = results.Count(r => r.Passed);
        Console.WriteLine($"---- {passed}/{results.Count} 通过，{results.Count - passed} 失败 ----");
        return results;
    }

    private static BenchmarkResult Run(string id, string expected, Func<(bool Ok, string Detail)> check)
    {
        var sw = Stopwatch.StartNew();
        var (ok, detail) = check();
        sw.Stop();
        Console.WriteLine($"[{id}] {(ok ? "PASS" : "FAIL")}  {sw.ElapsedMilliseconds,4}ms  {detail}");
        return new BenchmarkResult(id, expected, "覆盖洞", ok, detail, sw.ElapsedMilliseconds);
    }

    private static (bool, string) CheckFocusDirectrix()
    {
        // y²=4x，焦点 (1,0)，准线 x=-1；P=(4,4) 在抛物线上。
        var parabola = new BilinearForm3<ExactScalar>(
            ExactScalar.Zero, ExactScalar.Zero, -ExactScalar.FromInt(2),
            ExactScalar.One, ExactScalar.Zero, ExactScalar.Zero);
        var data = parabola.Foci();
        var directrices = parabola.Directrices();
        if (!data.TryGet(out var conic) || !directrices.TryGet(out var lines) || lines.Count != 1)
            return (false, "焦点/准线提取失败");

        var p = Primal3<ExactScalar>.Fixed(4, 4);
        var dx = p.X - conic.Focus1.X;
        var dy = p.Y - conic.Focus1.Y;
        var distanceToFocusSq = dx * dx + dy * dy;

        var line = lines[0];
        var incidence = SubspaceAlgebra.Pair(line, p);
        var lineNormSq = line.A * line.A + line.B * line.B;
        var distanceToDirectrixSq = incidence * incidence / lineNormSq;

        var ok = (distanceToFocusSq - distanceToDirectrixSq).VerifyStrictZero()
                 == StrictResult.StrictlyTrue;
        return ok
            ? (true, "|PF|²=25=|P 到准线|²，焦点/准线定义成立")
            : (false, "焦点/准线距离不相等");
    }

    private static (bool, string) CheckTangentChord()
    {
        // 单位圆，外点 P=(2,0)：两条切线切点都在极线 x=1/2 上。
        var circle = new BilinearForm3<ExactScalar>(
            ExactScalar.One, ExactScalar.Zero, ExactScalar.Zero,
            ExactScalar.One, ExactScalar.Zero, -ExactScalar.One);
        var p = Primal3<ExactScalar>.Fixed(2, 0);

        var tangents = ExactGeometryOps.TangentsFromPoint(p, circle);
        if (!tangents.TryGet(out var points) || points.Count != 2)
            return (false, "切线切点数量不为 2");

        var polar = circle.Polar(p);
        foreach (var t in points)
        {
            if (circle.Eval(t).VerifyStrictZero() != StrictResult.StrictlyTrue)
                return (false, "切点不在圆上");
            if (SubspaceAlgebra.Pair(polar, t).VerifyStrictZero() != StrictResult.StrictlyTrue)
                return (false, "切点不在极线上");
        }

        return (true, "两个切点均在圆上且在极线 x=1/2 上");
    }

    private static (bool, string) CheckCircleCircle()
    {
        var c1 = new BilinearForm3<ExactScalar>(
            ExactScalar.One, ExactScalar.Zero, ExactScalar.Zero,
            ExactScalar.One, ExactScalar.Zero, -ExactScalar.One);
        var c2 = new BilinearForm3<ExactScalar>(
            ExactScalar.One, ExactScalar.Zero, -ExactScalar.One,
            ExactScalar.One, ExactScalar.Zero, ExactScalar.Zero);
        var hit = ExactGeometryOps.CircleCircleIntersection(c1, c2);
        if (!hit.TryGet(out var result) || result.Kind != LineConicKind.TwoPoints
            || result.P1 is null || result.P2 is null)
            return (false, "两圆未交于两点");

        foreach (var p in new[] { result.P1.Value, result.P2.Value })
            if (c1.Eval(p).VerifyStrictZero() != StrictResult.StrictlyTrue
                || c2.Eval(p).VerifyStrictZero() != StrictResult.StrictlyTrue)
                return (false, "交点不在两圆上");

        return (true, "两圆交于两点，且两点同时在两圆上");
    }
}
