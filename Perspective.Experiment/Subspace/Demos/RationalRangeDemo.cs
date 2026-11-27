using Perspective.Experiment.Subspace;
using Poly = Perspective.Experiment.Subspace.PolyExpr;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>v2 值域演示：任意次、带极点/多区间的有理函数在 R 上的值域。</summary>
public static class RationalRangeDemo
{
    private static void Say(string message) => Console.WriteLine("  " + message);

    public static void Run()
    {
        Console.WriteLine("\n================ 有理函数值域 v2（数值根 + Sturm 校验 + 极点/多区间） ================");

        var u = Poly.Var(0);
        var one = Poly.One;

        var f1 = RationalRangeApprox.OfReal(u, u * u + one, 0);
        Say($"u / (u²+1) 在 R 上：{f1.Format()}（应为 [-0.5, 0.5]）");

        var f2 = RationalRangeApprox.OfReal(u * u - one, u * u + one, 0);
        Say($"(u²−1) / (u²+1) 在 R 上：{f2.Format()}（应为 [-1, 1)）");

        var f3 = RationalRangeApprox.OfReal(one, u * u - one, 0);
        Say($"1 / (u²−1) 在 R 上：{f3.Format()}（应为 (-∞, -1] ∪ (0, +∞)）");
    }
}
