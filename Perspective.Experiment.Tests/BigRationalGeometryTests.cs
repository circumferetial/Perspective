using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Xunit;

namespace Perspective.Experiment.Tests;

public sealed class BigRationalGeometryTests
{
    private static BigRational Q(long num, long den) => new(num, den);

    [Fact]
    public void ProjectiveIncidence_IsExactOverBigRational()
    {
        var a = Primal3<BigRational>.Fixed(0, 0);
        var b = Primal3<BigRational>.Fixed(1, 1);
        var c = Primal3<BigRational>.Fixed(2, 2);

        Assert.Equal(StrictResult.StrictlyTrue,
            BigRational.VerifyZero(SubspaceAlgebra.Det(a, b, c)));

        var line = SubspaceAlgebra.Join(a, b);
        Assert.Equal(StrictResult.StrictlyTrue,
            BigRational.VerifyZero(SubspaceAlgebra.Pair(line, b)));

        var other = SubspaceAlgebra.Join(
            Primal3<BigRational>.Fixed(0, 3),
            Primal3<BigRational>.Fixed(3, 0));
        var meet = SubspaceAlgebra.Meet(line, other);
        var expected = new Primal3<BigRational>(Q(3, 2), Q(3, 2), BigRational.One);

        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.SameSubspace(meet, expected).Value);
    }

    [Fact]
    public void RationalCircle_And_LinearMap_Work()
    {
        var circle = new BilinearForm3<BigRational>(
            BigRational.One, BigRational.Zero, BigRational.Zero,
            BigRational.One, BigRational.Zero, -BigRational.One);

        var point = new Primal3<BigRational>(Q(3, 5), Q(4, 5), BigRational.One);
        Assert.Equal(StrictResult.StrictlyTrue,
            BigRational.VerifyZero(circle.Eval(point)));

        var map = new LinearMap3<BigRational>(
            BigRational.FromLong(2), BigRational.Zero, BigRational.Zero,
            BigRational.Zero, BigRational.FromLong(2), BigRational.Zero,
            BigRational.Zero, BigRational.Zero, BigRational.One);

        Assert.Equal(StrictVerdict.StrictlyTrue, map.IsInvertible());
        var image = map.Apply(point);
        var expected = new Primal3<BigRational>(Q(6, 5), Q(8, 5), BigRational.One);
        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.SameSubspace(image, expected).Value);
    }
}
