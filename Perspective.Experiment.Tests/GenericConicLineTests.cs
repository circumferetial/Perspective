using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Xunit;

namespace Perspective.Experiment.Tests;

public sealed class GenericConicLineTests
{
    private static BigRational Q(long num, long den = 1) => new(num, den);

    [Fact]
    public void BigRational_RationalIntersections_RunAlgorithm()
    {
        var parabola = new BilinearForm3<BigRational>(
            BigRational.Zero, BigRational.Zero, -BigRational.FromLong(2),
            BigRational.One, BigRational.Zero, BigRational.Zero);
        var chord = new Dual3<BigRational>(
            BigRational.One, BigRational.Zero, -BigRational.One);

        var hit = GenericConicLine.Intersect(chord, parabola);
        Assert.True(hit.TryGet(out var result));
        Assert.Equal(LineConicKind.TwoPoints, result.Kind);
        Assert.NotNull(result.P1);
        Assert.NotNull(result.P2);

        foreach (var point in new[] { result.P1.Value, result.P2.Value })
        {
            Assert.Equal(StrictResult.StrictlyTrue,
                BigRational.VerifyZero(parabola.Eval(point)));
            Assert.Equal(StrictResult.StrictlyTrue,
                BigRational.VerifyZero(SubspaceAlgebra.Pair(chord, point)));
        }
    }

    [Fact]
    public void QuadScalar_IrrationalIntersections_RunAlgorithm()
    {
        var circle = new BilinearForm3<QuadScalar>(
            QuadScalar.One, QuadScalar.Zero, QuadScalar.Zero,
            QuadScalar.One, QuadScalar.Zero, -QuadScalar.One);
        var line = new Dual3<QuadScalar>(
            QuadScalar.Zero, QuadScalar.One, -QuadScalar.FromLong(1) / QuadScalar.FromLong(2));

        var hit = GenericConicLine.Intersect(line, circle);
        Assert.True(hit.TryGet(out var result));
        Assert.Equal(LineConicKind.TwoPoints, result.Kind);
        Assert.NotNull(result.P1);
        Assert.NotNull(result.P2);

        foreach (var point in new[] { result.P1.Value, result.P2.Value })
        {
            Assert.Equal(StrictResult.StrictlyTrue,
                QuadScalar.VerifyZero(circle.Eval(point)));
            Assert.Equal(StrictResult.StrictlyTrue,
                QuadScalar.VerifyZero(SubspaceAlgebra.Pair(line, point)));
        }
    }

    [Fact]
    public void BigRational_NonSquareRoot_IsUnsupported_NotException()
    {
        var circle = new BilinearForm3<BigRational>(
            BigRational.One, BigRational.Zero, BigRational.Zero,
            BigRational.One, BigRational.Zero, -BigRational.One);
        var line = new Dual3<BigRational>(
            BigRational.Zero, BigRational.One, Q(-1, 2));

        var hit = GenericConicLine.Intersect(line, circle);
        Assert.True(hit.IsFailure);
        Assert.Equal(GeometryError.Unsupported, hit.Error);
    }
}
