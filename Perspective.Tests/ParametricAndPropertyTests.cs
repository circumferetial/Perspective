using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Tests;

/// <summary>
/// 参数族（坐标含符号变量）与"属性测试"。
/// <para>
/// 回归测试只防已知用例；本文件用两类手段防未知错误：
/// (1) 参数恒等式（符号坐标 = ∀ 参数语义，含有理函数通分）；
/// (2) 随机实例上的不变量（射影变换保共线、保交比）——只断言性质，不记具体答案。
/// </para>
/// </summary>
public class ParametricAndPropertyTests
{
    // ---------- 参数族：符号坐标 = "对所有参数成立" ----------

    [Fact]
    public void ParametricPoint_OnLine_IsIdentityInParameter()
    {
        var line = new HLine(1, -1, 0);              // y = x
        var t = ExactScalar.FromSymbol("t");
        var p = HPoint.FromCartesian(t, t);          // P(t) = (t, t)

        Assert.Equal(StrictResult.StrictlyTrue, line.ContainsStrictly(p));
    }

    [Fact]
    public void ParametricTrigPoint_OnCircle_IsIdentityInParameter()
    {
        var circle = Scenes.Circle4;                 // x²+y²=4
        var t = ExactScalar.FromSymbol("t");
        var p = HPoint.FromCartesian(2 * ExactScalar.Cos(t), 2 * ExactScalar.Sin(t));

        Assert.Equal(StrictResult.StrictlyTrue, circle.ContainsStrictly(p));
    }

    [Fact]
    public void ParametricChordMidpoint_LiesOnFixedCircle_ForAllK()
    {
        // 期中题第 2 问的"参数版"：M(k) 恒在定点 (1,1)、半径²=2 的圆上。
        // 这是有理函数恒等式，需要既约分数通分（VerifyStrictZero 的有理通道）。
        var circle = Conic.FromGeneralEquation(1, 0, 1, -4, 0, 0);
        var k = ExactScalar.FromSymbol("k");
        var mid = circle.ChordMidpoint(new HLine(k, -1, 2));
        var dx = mid.X / mid.W;
        var dy = mid.Y / mid.W;

        var diff = (dx - 1) * (dx - 1) + (dy - 1) * (dy - 1) - 2;
        Assert.Equal(StrictResult.StrictlyTrue, diff.VerifyStrictZero());
    }

    [Fact]
    public void ParametricPoint_WrongCenter_IsNotProvedTrue()
    {
        var circle = Conic.FromGeneralEquation(1, 0, 1, -4, 0, 0);
        var k = ExactScalar.FromSymbol("k");
        var mid = circle.ChordMidpoint(new HLine(k, -1, 2));
        var dx = mid.X / mid.W;
        var dy = mid.Y / mid.W;

        // 以原点为心、半径²=2 的圆并不恒定包含 M(k)
        var wrong = dx * dx + dy * dy - 2;
        Assert.NotEqual(StrictResult.StrictlyTrue, wrong.VerifyStrictZero());
    }

    // ---------- 属性测试：随机实例上的不变量 ----------

    /// <summary>随机生成一个行列式非零的整数 3×3 矩阵（射影变换）。</summary>
    private static Matrix3x3 RandomInvertible(Random rng)
    {
        while (true)
        {
            ExactScalar E() => ExactScalar.FromInt(rng.Next(-3, 4));
            var m = new Matrix3x3(
                E(), E(), E(),
                E(), E(), E(),
                E(), E(), E());
            if (!m.Determinant().IsNumericallyZero()) return m;
        }
    }

    [Fact]
    public void Collinearity_IsInvariantUnderRandomProjectiveTransforms()
    {
        var rng = new Random(20261122);
        var origin = HPoint.FromCartesian(0, 0);

        for (int trial = 0; trial < 20; trial++)
        {
            // 共线三点：原点与同方向的整数倍点
            var a = HPoint.FromCartesian(rng.Next(-3, 4), rng.Next(-3, 4));
            var b = HPoint.FromCartesian(
                ExactScalar.FromInt(2) * a.X, ExactScalar.FromInt(2) * a.Y);
            Assert.Equal(StrictResult.StrictlyTrue, PointTools.Collinear(origin, a, b));

            var m = RandomInvertible(rng);
            var a2 = m * origin;
            var b2 = m * a;
            var c2 = m * b;

            Assert.Equal(StrictResult.StrictlyTrue, PointTools.Collinear(a2, b2, c2));
        }
    }

    [Fact]
    public void CrossRatio_IsInvariantUnderRandomProjectiveTransforms()
    {
        var rng = new Random(20261122);

        for (int trial = 0; trial < 15; trial++)
        {
            // 共线四点（x 轴上的整数点，互异）
            int x1 = rng.Next(-4, -1), x2 = rng.Next(-1, 1), x3 = rng.Next(1, 3), x4 = rng.Next(3, 6);
            var a = HPoint.FromCartesian(x1, 0);
            var b = HPoint.FromCartesian(x2, 0);
            var c = HPoint.FromCartesian(x3, 0);
            var d = HPoint.FromCartesian(x4, 0);

            var cr1 = GeometryAudit.CrossRatio(a, b, c, d);
            var m = RandomInvertible(rng);
            var cr2 = GeometryAudit.CrossRatio(m * a, m * b, m * c, m * d);

            Assert.Equal(StrictResult.StrictlyTrue, (cr1 - cr2).VerifyStrictZero());
        }
    }
}
