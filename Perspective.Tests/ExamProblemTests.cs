using Perspective.Core.Discovery;
using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Tests;

/// <summary>期中考试三问的回归测试（求解器 + 严通道证书）。</summary>
public class ExamProblemTests
{
    private static Conic Circle => Conic.FromGeneralEquation(1, 0, 1, -4, 0, 0); // x²+y²-4x=0
    private static HLine MovingLine(ExactScalar k) => new(k, -1, 2);           // y = kx + 2

    [Fact]
    public void Question1_SecantRangeIsNegativeReals()
    {
        var range = InverseExamSolver.SolveSecantRange(Circle, MovingLine, "k");

        // 第 1 问：k ∈ (-∞, 0)
        Assert.Equal(StrictResult.StrictlyTrue, RealSets.Contains(range, ExactScalar.FromInt(-1)));
        Assert.Equal(StrictResult.StrictlyFalse, RealSets.Contains(range, ExactScalar.FromInt(1)));
        Assert.Equal(StrictResult.StrictlyFalse, RealSets.Contains(range, ExactScalar.FromInt(0)));
    }

    [Fact]
    public void Question2_FixedDistanceCenterIsD_1_1()
    {
        ExactScalar[] probes = [-1, -2, -3, -4, -5, -6];
        var result = InverseExamSolver.FindFixedDistanceCenter(
            k => Circle.ChordMidpoint(MovingLine(k)), probes, "k");

        Assert.NotNull(result);
        var center = result!;
        Assert.Equal(ProofStatus.SymbolicProved, center.Status);
        Assert.Equal(1.0, (center.Center.X / center.Center.W).Approx, 9);
        Assert.Equal(1.0, (center.Center.Y / center.Center.W).Approx, 9);
        Assert.Equal(2.0, center.RadiusSquared.Approx, 9);
    }

    [Fact]
    public void Question3_TwoFixedPointsWithNDG()    {
        var results = InverseExamSolver.SolveFixedPointsForSlopeSum(Circle, MovingLine, "k");

        Assert.Equal(2, results.Count);

        bool HasPoint(double x, double y) => results.Any(r =>
            System.Math.Abs((r.Point.X / r.Point.W).Approx - x) < 1e-9 &&
            System.Math.Abs((r.Point.Y / r.Point.W).Approx - y) < 1e-9);

        Assert.True(HasPoint(0, 0), "应找到 E=(0,0)");
        Assert.True(HasPoint(2, -2), "应找到 E=(2,-2)");

        // NDG 是每个解各自的：把所有解的排除集合并起来，应含 k=0 与 k=-2
        var ndg = results.Aggregate(RealSets.Empty, (acc, r) => RealSets.Union(acc, r.ExcludedParams));
        Assert.Equal(StrictResult.StrictlyTrue, RealSets.Contains(ndg, ExactScalar.FromInt(0)));
        Assert.Equal(StrictResult.StrictlyTrue, RealSets.Contains(ndg, ExactScalar.FromInt(-2)));

        // 定值：E=(0,0) 对应 2，E=(2,-2) 对应 -2
        var e00 = results.First(r => System.Math.Abs((r.Point.X / r.Point.W).Approx) < 1e-9);
        Assert.Equal(2.0, e00.ConstantValue.Approx, 9);
    }

    // ---------- 2022 全国乙卷理科 Q20(2)：直接用原始构造也能符号证明 ----------
    // 本测试回归"齐次坐标归一化"：构造里不做任何手工归一化，
    // 由 Conic.Intersect / OtherIntersection 在出口归一化，发现器应直接给出 SymbolicProved。
    [Fact]
    public void Question2022_NationalB_Q20_2_SymbolicProofWithoutManualNormalization()
    {
        var e = Conic.Ellipse(ExactScalar.FromFormula("sqrt(3)"), 2);
        var a = HPoint.FromCartesian(0, -2);
        var b = HPoint.FromFormula("3/2", "-1");
        var p = HPoint.FromCartesian(1, -2);
        var ab = HLine.FromPoints(a, b);

        HLine BuildHN(ExactScalar t)
        {
            var m = e.OtherIntersection(a, HLine.FromPointAndSlope(a, t));
            var n = e.OtherIntersection(m, HLine.FromPoints(p, m));

            var horizontal = new HLine(0, m.W, -m.Y);
            var tt = HLine.Intersect(horizontal, ab);
            var h = new HPoint(2 * tt.X * m.W - m.X * tt.W, 2 * tt.Y * m.W - m.Y * tt.W, tt.W * m.W);

            return HLine.FromPoints(h, n);
        }

        var finding = DiscoveryEngine.DiscoverFixedPoint(BuildHN);
        Assert.NotNull(finding);
        Assert.Equal(ProofStatus.SymbolicProved, finding!.Status);
    }

    // ---------- 2022 全国乙卷理科 Q20(2)：HN 过定点 ----------
    // 说明：完整版会用 DiscoveryEngine.DiscoverFixedPoint 对参数 t 做符号恒等证明
    // （SymbolicProved，约 2 分钟），太重不适合放进默认测试；这里用精确有理实例做快速回归，
    // 完整符号证明的结论记录在 docs/exams.md。
    [Fact]
    public void Question2022_NationalB_Q20_2_LineHNPassesFixedPoint()
    {
        var e = Conic.Ellipse(ExactScalar.FromFormula("sqrt(3)"), 2);   // x²/3 + y²/4 = 1
        var a = HPoint.FromCartesian(0, -2);
        var b = HPoint.FromFormula("3/2", "-1");
        var p = HPoint.FromCartesian(1, -2);
        var ab = HLine.FromPoints(a, b);

        static HPoint Norm(HPoint q) => new(q.X / q.W, q.Y / q.W, 1);

        // 用"椭圆上的点 M(t)"参数化：M 由过 A 的割线取另一交点；
        // N 为弦 PM 的另一交点；T = 过 M 的水平线与 AB 的交点；H = 2T − M。
        HLine BuildHN(ExactScalar t)
        {
            var m = Norm(e.OtherIntersection(a, HLine.FromPointAndSlope(a, t)));
            var n = Norm(e.OtherIntersection(m, HLine.FromPoints(p, m)));

            var horizontal = new HLine(0, 1, -m.Y);
            var tt = Norm(HLine.Intersect(horizontal, ab));
            var h = new HPoint(2 * tt.X - m.X, 2 * tt.Y - m.Y, 1);

            return HLine.FromPoints(h, n);
        }

        // 定点应为 A=(0,-2)：每条 HN 都严格过 A
        foreach (int t in new[] { 1, 2, -1, 3 })
            Assert.Equal(StrictResult.StrictlyTrue, BuildHN(ExactScalar.FromInt(t)).ContainsStrictly(a));

        // 独立核对：两条不同参数的 HN 严格交于 A
        var l1 = BuildHN(ExactScalar.FromInt(1));
        var l2 = BuildHN(ExactScalar.FromInt(2));
        var intersection = HLine.Intersect(l1, l2);
        Assert.Equal(StrictResult.StrictlyTrue, PointTools.SameStrictly(intersection, a));
    }
}
