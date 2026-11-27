using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Split = Perspective.Experiment.Subspace.SplitComplex<Perspective.Core.Math.ExactScalar>;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>
///     双曲列演示：split-complex（j²=1）+ Lorentz boost + 既有的 Klein 模型 lens。
///     与 ParabolicGeometryDemo 对称：环 / 算子 / 空间三种写法各来一遍。
/// </summary>
public static class HyperbolicGeometryDemo
{
    private static void Say(string message) => Console.WriteLine("  " + message);

    public static void Run()
    {
        Console.WriteLine("\n================ 双曲列：split-complex + Lorentz boost（j² = 1） ================");

        // [1] 环：j² = 1；有零因子，所以不提供除法
        var a = new Split(1, 2); // 1 + 2j
        var b = new Split(3, 4); // 3 + 4j
        Say($"[1] (1+2j)(3+4j) = {a * b}（应为 11 + 10j）");

        var light = new Split(1, 1); // 1 + j
        var lightConj = new Split(1, -1); // 1 − j
        Say($"[1] (1+j)(1−j) = {light * lightConj}，严格为零？{(light * lightConj).VerifyZero()}");
        Say($"[1] 1+j 自身为零？{light.VerifyZero()}（非零；Norm = {light.Norm().ToSimplifiedString()}，类光）");

        // [2] boost：半快度参数化 u = tanh(t/2)，cosh² − sinh² = 1
        var boost = UnitBoost.FromHalfRapidity(ExactScalar.FromFormula("1/2")).Value;
        Say($"[2] 半快度 1/2 → cosh = {boost.Cosh.ToSimplifiedString()}，sinh = {boost.Sinh.ToSimplifiedString()}");
        Say($"[2] cosh² − sinh² − 1 严格为零？" +
            $"{(boost.Cosh * boost.Cosh - boost.Sinh * boost.Sinh - ExactScalar.One).VerifyStrictZero()}");

        // [3] 群律：半快度相加 u ⊕ v = (u+v)/(1+uv)
        var b1 = UnitBoost.FromHalfRapidity(ExactScalar.FromFormula("1/2")).Value;
        var b2 = UnitBoost.FromHalfRapidity(ExactScalar.FromFormula("1/3")).Value;
        var composed = b1 * b2;
        var expected = UnitBoost.FromHalfRapidity(ExactScalar.FromFormula("5/7")).Value;
        Say($"[3] B(1/2)·B(1/3) ≡ B(5/7)？" +
            $"{ (composed.Cosh - expected.Cosh).VerifyStrictZero()} / {(composed.Sinh - expected.Sinh).VerifyStrictZero()}");

        // [4] boost 保持绝对形 x² + y² − w²
        var absolute = new BilinearForm3<ExactScalar>(1, 0, 0, 1, 0, -1);
        var onAbsolute = Primal3<ExactScalar>.Fixed(0, 1); // (0,1,1)，在 x²+y²=w² 上
        var offAbsolute = Primal3<ExactScalar>.Fixed(1, 1); // (1,1,1)，不在绝对形上
        Say($"[4] 绝对形上的点 boost 后仍在绝对形上？{absolute.Eval(boost.Apply(onAbsolute)).VerifyStrictZero()}");
        Say($"[4] 任意点 Q(boost·p) − Q(p) 严格为零？" +
            $"{(absolute.Eval(boost.Apply(offAbsolute)) - absolute.Eval(offAbsolute)).VerifyStrictZero()}");

        // [5] “空间”写法就是已有的 Klein lens：绝对形切线是它的迷向线
        var lens = GeometryLens<ExactScalar>.Hyperbolic;
        var tangentLine = new Dual3<ExactScalar>(1, 0, -1); // x = w，切单位圆于 (1,0)
        Say($"[5] Hyperbolic lens 的绝对形切线 x=w 满足 Ω*(l)=0？" +
            $"{lens.OmegaStar.Eval(tangentLine).VerifyStrictZero()}（boost 就是保持它的线性变换）");
    }
}
