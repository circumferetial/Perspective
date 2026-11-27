using Perspective.Core.Math;
using Perspective.Experiment.Subspace;

namespace Perspective.Experiment.Subspace.Demos;

// ============================================================================
// 合流验证实验：一套几何函数同时驱动 ExactScalar 与 PolyExpr（含 π 和 √3）
// ============================================================================

public static class UnifiedAlgebraExperiment
{
    /// <summary>完全同一份泛型几何代码：既能吃 ExactScalar，也能吃 PolyExpr！</summary>
    private static T OrthocenterDet<T>(Primal3<T> a, Primal3<T> b, Primal3<T> c) where T : IExactRing<T>
    {
        var lens = GeometryLens<T>.EuclideanXoy;
        var altA = lens.Metric.PerpendicularThrough(a, SubspaceAlgebra.Join(b, c)).Value;
        var altB = lens.Metric.PerpendicularThrough(b, SubspaceAlgebra.Join(c, a)).Value;
        var altC = lens.Metric.PerpendicularThrough(c, SubspaceAlgebra.Join(a, b)).Value;
        return SubspaceAlgebra.Det(altA, altB, altC);
    }

    public static void Run()
    {
        Console.WriteLine("\n================ 重接法：ExactScalar + PolyExpr 彻底合流 ================");

        // [1] 同一份 OrthocenterDet<T> 跑在 ExactScalar（含 π 与 √3）上
        var pi = ExactScalar.FromFormula("pi");
        var sqrt3 = ExactScalar.FromFormula("sqrt(3)");
        var detExact = OrthocenterDet(
            Primal3<ExactScalar>.Fixed(0, 0),
            new Primal3<ExactScalar>(pi, 0, 1),
            new Primal3<ExactScalar>(sqrt3, pi + 1, 1));
        Console.WriteLine($"  [1a] 同一套泛型垂心函数跑 ExactScalar(π, √3)：{detExact.VerifyStrictZero()}");

        // [1b] 同一份 OrthocenterDet<T> 跑在 PolyExpr（任意符号自由点 C）上
        var s1 = new UnifiedGeoSpace();
        var resPoly = s1.ProveZero(OrthocenterDet(
            Primal3<PolyExpr>.Fixed(0, 0),
            Primal3<PolyExpr>.Fixed(1, 0),
            s1.NewFreePoint("C")));
        Console.WriteLine($"  [1b] 同一套泛型垂心函数跑 PolyExpr(自由点 C)：{resPoly.Status}");

        // [2] 含超越数 π 系数的线性约束自动缩维成精确齐次定点
        Console.WriteLine("\n  [2] 含 π 的线性约束自动缩维：");
        var s2 = new UnifiedGeoSpace();
        var p = s2.NewFreePoint("P");
        s2.RequireIncident(new Dual3<PolyExpr>(pi, -1, -1), p); // π·x - y - 1 = 0
        s2.RequireIncident(Dual3<PolyExpr>.Fixed(1, 1, -3), p); // x + y - 3 = 0
        Console.WriteLine($"    动点 P 自动坍缩为精确定点 = {s2.Format(s2.Resolve(p))}");

        // [3] 半轴为超越数 π 和 √3 的椭圆：x²/π² + y²/3 = 1（即 3x² + π²y² - 3π²w² = 0）
        //     两个耦合动点 OA ⊥ OB，证明弦 AB 恒切于定圆 (3+π²)x² + (3+π²)y² - 3π²w² = 0！
        Console.WriteLine("\n  [3] 超越数 π + √3 椭圆上的耦合动弦 OA⊥OB 证明：");
        var s3 = new UnifiedGeoSpace();
        var pi2 = pi * pi;
        var origin = Primal3<PolyExpr>.Fixed(0, 0);
        var ellipse = new BilinearForm3<PolyExpr>(3, 0, 0, pi2, 0, -3 * pi2);
        var targetCircle = new BilinearForm3<PolyExpr>(3 + pi2, 0, 0, 3 + pi2, 0, -3 * pi2);

        var ptA = s3.NewFreePoint("A");
        var ptB = s3.NewFreePoint("B");
        s3.RequireOnConic(ptA, ellipse);
        s3.RequireOnConic(ptB, ellipse);
        s3.RequirePerpendicular(SubspaceAlgebra.Join(origin, ptA), SubspaceAlgebra.Join(origin, ptB));

        var chordAB = SubspaceAlgebra.Join(ptA, ptB);
        var proof = s3.ProveZero(targetCircle.DualForm().Eval(chordAB));

        Console.WriteLine($"    证明状态：{proof.Status}（余式：{s3.Format(proof.Remainder)}）");
        foreach (var eq in proof.CharacteristicSet)
            Console.WriteLine($"    特征列：{s3.Format(eq)} = 0");
        foreach (var ndg in proof.NdgConditions)
            Console.WriteLine($"    NDG：  {s3.Format(ndg)} ≠ 0");
    }
}
