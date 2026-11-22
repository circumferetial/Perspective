using AngouriMath;
using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;
using Perspective.Oracle.Z3;

namespace Perspective.Oracle.Z3.Tests;

/// <summary>
/// M1-b2：有理目标函数极值（保持多项式的驻点与闭合）。
/// 2022 浙江 21(ⅱ)：椭圆 x²/12+y²=1，Q(0,1/2) 在弦 AB 上，PA、PB 交 y=−x/2+3 于 C、D，求 |CD| 最小值。
/// </summary>
public class RationalExtremumTests
{
    private static readonly Z3Oracle Oracle = new(timeoutMs: 5000);

    private static ExactScalar Scalar(Entity entity) =>
        new(entity.EvaluableNumerical ? (double)entity.EvalNumerical() : double.NaN, entity);

    [Fact]
    public void Question2022_Zhejiang_21_2_MinCD()
    {
        var ellipse = Conic.Ellipse(ExactScalar.FromFormula("2*sqrt(3)"), ExactScalar.FromInt(1));
        var p = HPoint.FromCartesian(0, 1);
        var m = ExactScalar.FromSymbol("m");

        // 过 Q(0,1/2) 的弦：y = m x + 1/2；A、B 是交点。
        // 用"线对二次型 PA∪PB"避开 A、B 的根号坐标。
        var chord = new HLine(-m, 1, ExactScalar.FromFormula("-1/2"));
        var pair = ellipse.LinePairFrom(p, chord);

        // 定直线 ℓ: x + 2y − 6 = 0；C、D = ℓ 与线对二次型的交点
        var ell = new HLine(1, 2, -6);
        var lengthSquared = new Conic(pair.Matrix).IntersectionChordLengthSquared(ell);

        // 有理式拆分 f = N/D（D > 0）
        var fraction = (Entity.Divf)lengthSquared.Exact!.Simplify().AsSingleFraction();
        var report = ExtremumSolver.FindExtremumOfRational(
            "m", Scalar(fraction.Dividend), Scalar(fraction.Divisor),
            ExtremumKind.Minimum, Oracle);

        Assert.NotNull(report);
        Assert.True(report!.Attained);
        Assert.NotNull(report.Closure);
        Assert.Equal(OracleVerdictKind.Unsat, report.Closure!.Kind);   // 不存在更小的点

        // |CD|² 最小值 = 180（m = 3/16），故 |CD| = 6√5
        Assert.Equal(StrictResult.StrictlyTrue,
            (report.Value - ExactScalar.FromInt(180)).VerifyStrictZero());
        Assert.Equal(6.0 * System.Math.Sqrt(5), System.Math.Sqrt(report.Value.Approx), 9);
    }
}
