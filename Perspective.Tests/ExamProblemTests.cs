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
    public void Question3_TwoFixedPointsWithNDG()
    {
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
}
