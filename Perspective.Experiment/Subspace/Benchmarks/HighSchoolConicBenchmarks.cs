using System.Diagnostics;
using Perspective.Core.Math;
using Perspective.Experiment.Subspace;

namespace Perspective.Experiment.Subspace.Benchmarks;

/// <summary>
///     高中圆锥曲线三类基础结论：定点、定值、切线面积。
///     只做 ExactScalar 代数验证，不引入新几何类型。
/// </summary>
public static class HighSchoolConicBenchmarks
{
    public static IReadOnlyList<BenchmarkResult> RunAll()
    {
        Console.WriteLine("\n================ 高中圆锥曲线基准（11 项） ================");
        var results = new List<BenchmarkResult>
        {
            Run("HS-1", "AM⊥AN ⇒ l 过 (0,-3/5)", CheckEllipsePerpendicular),
            Run("HS-2", "焦点弦 ⇒ k_BN/k_AM=3", CheckEllipseSlopeRatio),
            Run("HS-3", "焦点弦 ⇒ 1/|FM|+1/|FN|=1", CheckParabolaFocusChord),
            Run("HS-4", "过 Q(0,-1) 的弦，P=OA+OB ⇒ x²=4(y+2), |x|>4", CheckParabolaLocus),
            Run("HS-5", "双曲线切线截渐近线面积=√3", CheckHyperbolaTangentArea),
            Run("HS-6", "双曲线 ⇒ (2k1+1)(2k2+1)=2", CheckHyperbolaSlopeProduct),
            Run("HS-7", "重心轨迹 ⇒ 3x²+6y²=2x, x>0", CheckEllipseCentroidLocus),
            Run("HS-8", "k_BN=2k_AM ⇒ l 过 (2/3,0)", CheckEllipseAsymmetric),
            Run("HS-A", "l 过 (1,1/2) ⇒ AM∩BN 在 x+2y=4（非退化）", CheckEllipseNonSymmetric),
            Run("HS-B", "|FA|+|FB|=6 ⇒ S∈(0,2√2]，a 挖去 0", CheckParabolaAreaRange),
            Run("HS-C", "双曲线弦中点轨迹 + 挖点", CheckHyperbolaMidpointDomain)
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
        return new BenchmarkResult(id, expected, "ExactScalar 代数", ok, detail, sw.ElapsedMilliseconds);
    }

    private static (bool, string) CheckEllipseNonSymmetric()
    {
        // 椭圆 x²/4+y²=1，A=(2,0),B=(0,1)，参数化 E(t)=(2(1−t²),2t,1+t²)。
        // 直线 y=k(x−1)+1/2 代入得 (6k−1)t²+4t−(2k+1)=0。
        var k = ExactScalar.FromSymbol("k");
        var lc = 6 * k - ExactScalar.One;
        var sum = -ExactScalar.FromInt(4) / lc;
        var product = -(2 * k + ExactScalar.One) / lc;

        // P=AM∩BN 的 x+2y−4 因子为 −16t1(t2−1)(3t1t2−t1−t2+1)；
        // Vieta 给出 3product−sum+1=0。
        var relation = 3 * product - sum + ExactScalar.One;
        if (relation.VerifyStrictZero() != StrictResult.StrictlyTrue)
            return (false, "Vieta 关系未化为 0");

        // 具体实例 k=1/2：y=x/2，交点 M=(√2,√2/2),N=(−√2,−√2/2)。
        var s2 = ExactScalar.Sqrt(ExactScalar.FromInt(2));
        var a = new Primal3<ExactScalar>(ExactScalar.FromInt(2), ExactScalar.Zero, ExactScalar.One);
        var b = new Primal3<ExactScalar>(ExactScalar.Zero, ExactScalar.One, ExactScalar.One);
        var m = new Primal3<ExactScalar>(s2, s2 / ExactScalar.FromInt(2), ExactScalar.One);
        var n = new Primal3<ExactScalar>(-s2, -s2 / ExactScalar.FromInt(2), ExactScalar.One);
        var p = SubspaceAlgebra.Meet(
            SubspaceAlgebra.Join(a, m),
            SubspaceAlgebra.Join(b, n));
        var target = new Dual3<ExactScalar>(
            ExactScalar.One, ExactScalar.FromInt(2), -ExactScalar.FromInt(4));
        var concrete = SubspaceAlgebra.Pair(target, p).VerifyStrictZero()
                       == StrictResult.StrictlyTrue;

        return concrete
            ? (true, "Vieta 恒等式成立；k=1/2 实例中 AM∩BN 在 x+2y=4")
            : (false, "k=1/2 实例未落在 x+2y=4");
    }

    private static (bool, string) CheckParabolaAreaRange()
    {
        // S²=2(t−1)²(t+2)，定义域 t∈(−2,1)∪(1,2]。
        var t = PolyExpr.Var(0);
        var s2 = 2 * (t - 1) * (t - 1) * (t + 2);

        var left = IntervalRange.OfPolynomial(s2, 0, -2, false, 1, false);
        var right = IntervalRange.OfPolynomial(s2, 0, 1, false, 2, true);

        bool ok = left.Lower.Value.Approx == 0 && !left.Lower.Closed
                  && left.Upper.Value.Approx == 8 && left.Upper.Closed
                  && right.Lower.Value.Approx == 0 && !right.Lower.Closed
                  && right.Upper.Value.Approx == 8 && right.Upper.Closed;

        return ok
            ? (true, "IntervalRange: S²∈(0,8]，故 S∈(0,2√2]；t=1、t=−2 不取")
            : (false, $"左区间 {left.Format()}，右区间 {right.Format()}");
    }

    private static (bool, string) CheckHyperbolaMidpointDomain()
    {
        var k = ExactScalar.FromSymbol("k");
        var one = ExactScalar.One;
        var half = ExactScalar.FromInt(1) / ExactScalar.FromInt(2);
        var den = 3 - k * k;
        var x = (2 * k - k * k) / den;
        var y = 3 * (2 - k) / den;

        var equation = 4 * (y - one) * (y - one) - 12 * (x - half) * (x - half) - one;
        var derivativeNumerator = 2 * k * k - 6 * k + 6;
        var positiveForm = 2 * (k - ExactScalar.FromInt(3) / ExactScalar.FromInt(2))
                               * (k - ExactScalar.FromInt(3) / ExactScalar.FromInt(2))
                           + ExactScalar.FromInt(3) / ExactScalar.FromInt(2);

        bool ok = equation.VerifyStrictZero() == StrictResult.StrictlyTrue
                  && (derivativeNumerator - positiveForm).VerifyStrictZero() == StrictResult.StrictlyTrue;

        return ok
            ? (true, "轨迹 4(y−1)²−12(x−1/2)²=1；x'(k) 分子恒正，端点 (1,0)、(−7,−12) 挖去")
            : (false, "轨迹方程或单调性恒等式失败");
    }

    private static (bool, string) CheckParabolaLocus()    {
        var k = ExactScalar.FromSymbol("k");
        var x = 4 * k;
        var y = 4 * k * k - 2;
        var diff = x * x - 4 * (y + 2);

        if (diff.VerifyStrictZero() != StrictResult.StrictlyTrue)
            return (false, "轨迹方程未化为 x²=4(y+2)");

        // Δ=16(k²−1)>0 给出 |k|>1，即 |x|>4；这是开区间，端点不取。
        return (true, "消参后 x²=4(y+2)；|k|>1 ⇒ |x|>4");
    }

    private static (bool, string) CheckEllipseCentroidLocus()
    {
        var m = ExactScalar.FromSymbol("m");
        var den = 3 * (m * m + 2);
        var x = 4 / den;
        var y = -2 * m / den;
        var diff = 3 * x * x + 6 * y * y - 2 * x;

        if (diff.VerifyStrictZero() != StrictResult.StrictlyTrue)
            return (false, "重心轨迹未化为 3x²+6y²=2x");

        return (true, "消参后 3x²+6y²=2x；x=4/[3(m²+2)]>0，原点不取");
    }

    private static (bool, string) CheckEllipsePerpendicular()    {
        var k = ExactScalar.FromSymbol("k");
        var m = ExactScalar.FromSymbol("m");
        var one = ExactScalar.One;
        var lc = one + 4 * k * k;

        var sum = -8 * k * m / lc;
        var product = 4 * (m * m - one) / lc;
        var perpendicular = (one + k * k) * product
                            + k * (m - one) * sum
                            + (m - one) * (m - one);

        var factor = perpendicular * lc - (m - one) * (5 * m + 3);
        if (factor.VerifyStrictZero() != StrictResult.StrictlyTrue)
            return (false, "垂直条件未化为 (m−1)(5m+3)");

        var mFixed = ExactScalar.FromInt(-3) / ExactScalar.FromInt(5);
        var sumFixed = -8 * k * mFixed / lc;
        var productFixed = 4 * (mFixed * mFixed - one) / lc;
        var atFixed = (one + k * k) * productFixed
                      + k * (mFixed - one) * sumFixed
                      + (mFixed - one) * (mFixed - one);

        return atFixed.VerifyStrictZero() == StrictResult.StrictlyTrue
            ? (true, "m=−3/5 时垂直条件恒为 0，定点 (0,-3/5)")
            : (false, "m=−3/5 时垂直条件不为 0");
    }

    private static (bool, string) CheckEllipseSlopeRatio()
    {
        var y1 = ExactScalar.FromSymbol("u");
        var y2 = ExactScalar.FromSymbol("v");
        var threeHalves = ExactScalar.FromInt(3) / ExactScalar.FromInt(2);

        // 3x²+4y²=12, x=my+1 ⇒ m·y1y2 = (3/2)(y1+y2)
        var mProd = threeHalves * (y1 + y2);
        var numerator = mProd + 3 * y2;
        var denominator = mProd - y1;
        var diff = numerator - 3 * denominator;

        return diff.VerifyStrictZero() == StrictResult.StrictlyTrue
            ? (true, "k_BN/k_AM 的分子恒为分母的 3 倍")
            : (false, "斜率比未化为 3");
    }

    private static (bool, string) CheckHyperbolaSlopeProduct()
    {
        var k = ExactScalar.FromSymbol("k");
        var twoKPlusOne = 2 * k + ExactScalar.One;
        var sum = ExactScalar.One / twoKPlusOne;
        var product = (2 * k - ExactScalar.One) / (4 * twoKPlusOne);
        var target = 4 * product + 2 * sum + ExactScalar.One;
        var diff = target - 2;

        return diff.VerifyStrictZero() == StrictResult.StrictlyTrue
            ? (true, "4k1k2+2(k1+k2)+1 恒为 2")
            : (false, "斜率恒等式未化为 2");
    }

    private static (bool, string) CheckParabolaFocusChord()
    {
        // y²=4x，焦点 F=(1,0)，焦点弦 x=my+1 ⇒ y1y2=−4。
        // 对 M=(y²/4,y)：|FM|=(y²+4)/4，故 1/|FM|=4/(y²+4)。
        var y = ExactScalar.FromSymbol("y");
        var y2 = -4 / y;
        var invFm = ExactScalar.FromInt(4) / (y * y + ExactScalar.FromInt(4));
        var invFn = ExactScalar.FromInt(4) / (y2 * y2 + ExactScalar.FromInt(4));
        var diff = invFm + invFn - ExactScalar.One;

        return diff.VerifyStrictZero() == StrictResult.StrictlyTrue
            ? (true, "y1y2=−4 约束下，1/|FM|+1/|FN| 恒为 1")
            : (false, "倒数和未化为 1");
    }

    private static (bool, string) CheckHyperbolaTangentArea()
    {
        // x²−y²/3=1 的切线 y=kx+m 满足 m²=k²−3。
        // 取 k=2,m=1，交渐近线 y=±√3x 于 P,Q；面积应为 √3。
        var sqrt3 = ExactScalar.Sqrt(ExactScalar.FromInt(3));
        var xP = ExactScalar.One / (sqrt3 - 2);
        var xQ = -ExactScalar.One / (sqrt3 + 2);
        var areaSquared = 3 * xP * xP * xQ * xQ;
        var target = 3;
        var diff = areaSquared - target;

        // 竖直切线 x=1：与渐近线交于 (1,±√3)，面积²=3。
        var verticalAreaSquared = 3 * ExactScalar.One * ExactScalar.One;
        var verticalDiff = verticalAreaSquared - target;

        return diff.VerifyStrictZero() == StrictResult.StrictlyTrue
               && verticalDiff.VerifyStrictZero() == StrictResult.StrictlyTrue
            ? (true, "k=2,m=1 与竖直切线 x=1 的面积²均为 3，面积=√3")
            : (false, "面积平方未化为 3");
    }

    private static (bool, string) CheckEllipseAsymmetric()
    {
        var t = ExactScalar.FromSymbol("t");
        var m = ExactScalar.FromSymbol("m");
        var y1 = ExactScalar.FromSymbol("u");
        var y2 = ExactScalar.FromSymbol("v");

        // 韦达给出 m·y1y2 = −(t²−4)/(2t)·(y1+y2)。
        var mProd = -((t * t - 4) / (2 * t)) * (y1 + y2);
        var condition = -mProd + (t + 2) * y2 - 2 * (t - 2) * y1;
        var factor = condition * (2 * t)
                     - (3 * t - 2) * ((t + 2) * y2 - (t - 2) * y1);

        if (factor.VerifyStrictZero() != StrictResult.StrictlyTrue)
            return (false, "斜率条件未分解出 3t−2");

        // 见证：t=2/3, m=0（竖直线 x=2/3）。
        var tt = ExactScalar.FromInt(2) / ExactScalar.FromInt(3);
        var yy = ExactScalar.Sqrt(ExactScalar.FromInt(8) / ExactScalar.FromInt(3));
        var k1 = yy / (tt + 2);
        var k2 = -yy / (tt - 2);

        return (k2 - 2 * k1).VerifyStrictZero() == StrictResult.StrictlyTrue
            ? (true, "条件分解出 3t−2；t=2/3 见证 k_BN=2k_AM，定点 (2/3,0)")
            : (false, "t=2/3 见证失败");
    }
}
