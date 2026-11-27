using Perspective.Core.Math;
using Perspective.Experiment.Subspace;

namespace Perspective.Experiment.Subspace.Demos;

public static class ComplexGeometryExperiment
{
    public static void Run()
    {
        Console.WriteLine("\n================ 复扩展整理版：共形 CP¹ + e^(iθ) + 射影 CP² ================");

        // [1] 零除法反演 + 共圆（全部 StrictlyTrue）
        Console.WriteLine("\n[1] 齐次 CP¹ 严证圆反演保角题（零除法）：");
        var u = ExactScalar.FromSymbol("u", 0.3);
        var e = HComplex.From(ExactComplex.Zero);
        var p = HComplex.From(new ExactComplex(0, 2));
        var pPrime = HComplex.Invert(p, ExactComplex.Zero, 16);

        var a = new HComplex(4, new ExactComplex(1, -u));
        var b = new HComplex(4, new ExactComplex(1, -(2 - u)));
        var aPrime = HComplex.Invert(a, ExactComplex.Zero, 16);
        var bPrime = HComplex.Invert(b, ExactComplex.Zero, 16);
        var midPrime = HComplex.Midpoint(aPrime, bPrime);

        Console.WriteLine($"  P、A(u)、B(2-u) 共线？{HComplex.AreCollinear(p, a, b)}");
        Console.WriteLine($"  反演后 E、P'、A'、B' 严格共圆？{HComplex.AreConcyclicOrCollinear(e, pPrime, aPrime, bPrime)}");
        Console.WriteLine($"  A'B' 中点纵坐标恒为 4？" +
                          $"{((midPrime.Z * midPrime.W.Conjugate()).Im - 4 * midPrime.W.NormSquared()).VerifyStrictZero()}");

        // [2] e^(iθ) 相位算子与极坐标反演
        Console.WriteLine("\n[2] 单位相位 e^(iθ)（特殊角复合 + 半角极坐标反演）：");
        var pi = ExactScalar.FromFormula("pi");
        var rotSum = UnitPhase.FromAngle(pi / 6) * UnitPhase.FromAngle(pi / 3);
        var rot90 = UnitPhase.FromAngle(pi / 2);
        Console.WriteLine($"  e^(i·π/6) · e^(i·π/3) ≡ e^(i·π/2)？{(rotSum.ToComplex() - rot90.ToComplex()).VerifyStrictZero()}");

        var t = ExactScalar.FromSymbol("t", 0.7);
        var r = ExactScalar.FromSymbol("r", 5.0);
        var polarPt = HComplex.FromPolarHalfAngle(r, t); // r · e^(iθ)
        var polarInv = HComplex.Invert(polarPt, ExactComplex.Zero, 100); // 反演：极角不变，半径变 100/r
        Console.WriteLine($"  极坐标点与反演点共射线（零三角函数）？{HComplex.AreCollinear(e, polarPt, polarInv)}");

        // [3] CP² 复射影平面（依然是同一套 Primal3<T> / BilinearForm3<T>）
        Console.WriteLine("\n[3] 复射影 CP²：圆环点与 Plücker 焦点定理");
        var ptI = new Primal3<ExactComplex>(1, ExactComplex.I, 0);
        var ptJ = new Primal3<ExactComplex>(1, -ExactComplex.I, 0);
        var anyCircle = new BilinearForm3<ExactComplex>(1, 0, -3, 1, 5, 27);
        Console.WriteLine($"  任意圆过 I, J？{anyCircle.Eval(ptI).VerifyStrictZero()} / {anyCircle.Eval(ptJ).VerifyStrictZero()}");

        var ellipse = new BilinearForm3<ExactComplex>(9, 0, 0, 25, 0, -225);
        var isotropicLine = SubspaceAlgebra.Join(Primal3<ExactComplex>.Fixed(4, 0), ptI);
        Console.WriteLine($"  焦点 F=(4,0) 连向 I 的直线相切于椭圆？{ellipse.DualForm().Eval(isotropicLine).VerifyStrictZero()}");
    }
}
