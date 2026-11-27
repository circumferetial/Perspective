using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Xunit;

namespace Perspective.Experiment.Tests;

public sealed class QuadScalarGeometryTests
{
    private static QuadScalar Q(long n) => QuadScalar.FromLong(n);
    private static QuadScalar Sqrt(long n) => QuadScalar.FromSqrt(new BigRational(n, 1));

    [Fact]
    public void UnitCircle_Accepts_QuadraticPoint()
    {
        var sqrt2 = Sqrt(2);
        var half = Q(1) / Q(2);
        var point = new Primal3<QuadScalar>(sqrt2 * half, sqrt2 * half, QuadScalar.One);

        var circle = new BilinearForm3<QuadScalar>(
            QuadScalar.One, QuadScalar.Zero, QuadScalar.Zero,
            QuadScalar.One, QuadScalar.Zero, -QuadScalar.One);

        Assert.Equal(StrictResult.StrictlyTrue,
            QuadScalar.VerifyZero(circle.Eval(point)));
    }

    [Fact]
    public void JoinAndMeet_WorkOverQuadraticExtension()
    {
        var sqrt2 = Sqrt(2);
        var a = new Primal3<QuadScalar>(QuadScalar.Zero, QuadScalar.Zero, QuadScalar.One);
        var b = new Primal3<QuadScalar>(sqrt2, sqrt2, QuadScalar.One);
        var line = SubspaceAlgebra.Join(a, b);

        var c = new Primal3<QuadScalar>(sqrt2, QuadScalar.Zero, QuadScalar.One);
        var d = new Primal3<QuadScalar>(QuadScalar.Zero, sqrt2, QuadScalar.One);
        var other = SubspaceAlgebra.Join(c, d);
        var meet = SubspaceAlgebra.Meet(line, other);

        var expected = new Primal3<QuadScalar>(sqrt2 / Q(2), sqrt2 / Q(2), QuadScalar.One);
        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.SameSubspace(meet, expected).Value);
    }
}
