using System.Diagnostics;
using Perspective.Core.Math;
using Perspective.Experiment.Subspace;

namespace Perspective.Experiment.Subspace.Benchmarks;

/// <summary>
///     最小 PolyExpr 基准：抛物线 y²=2px 上 OA⊥OB ⇒ 弦 AB 恒过 (2p,0)。
///     只做参数化 + Join + 吴方法，不引入新几何类型。
/// </summary>
public static class ParabolaFixedPointBenchmark
{
    public static BenchmarkResult Run()
    {
        var sw = Stopwatch.StartNew();

        var p = PolyExpr.Var(0);
        var a = PolyExpr.Var(1);
        var b = PolyExpr.Var(2);
        var two = PolyExpr.FromLong(2);

        // P(t) = (2p t², 2p t, 1)
        var pa = new Primal3<PolyExpr>(two * p * a * a, two * p * a, PolyExpr.One);
        var pb = new Primal3<PolyExpr>(two * p * b * b, two * p * b, PolyExpr.One);
        var fixedPoint = new Primal3<PolyExpr>(two * p, PolyExpr.Zero, PolyExpr.One);

        var space = new UnifiedGeoSpace();
        space.RequireNonZero(p);
        space.RequireNonZero(a);
        space.RequireNonZero(b);
        space.RequireNonZero(a - b);
        space.RequireZero(a * b + PolyExpr.One); // OA ⟂ OB

        var incidence = SubspaceAlgebra.Pair(SubspaceAlgebra.Join(pa, pb), fixedPoint);
        var outcome = space.ProveZero(incidence);
        sw.Stop();

        var ok = outcome.Status == UnifiedProofStatus.Proved;
        var detail = ok
            ? $"proved: AB 过 (2p,0)，chain={outcome.CharacteristicSet.Count}"
            : $"status={outcome.Status}, ndg={outcome.NdgConditions.Count}, remainder={space.Format(outcome.Remainder)}";

        Console.WriteLine($"[P-FIX] {(ok ? "PASS" : "FAIL")}  {sw.ElapsedMilliseconds}ms  {detail}");
        return new BenchmarkResult("P-FIX", "(2p,0)", "PolyExpr + Wu", ok, detail, sw.ElapsedMilliseconds);
    }
}
