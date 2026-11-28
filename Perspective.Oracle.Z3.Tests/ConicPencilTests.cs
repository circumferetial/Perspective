using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Perspective.Experiment.Z3;
using Perspective.Oracle.Z3;
using Xunit;

namespace Perspective.Oracle.Z3.Tests;

public sealed class ConicPencilTests
{
    [Fact]
    public void Pencil_FindsKnownDegenerateMember()
    {
        var circle = new BilinearForm3<BigRational>(
            BigRational.One, BigRational.Zero, BigRational.Zero,
            BigRational.One, BigRational.Zero, -BigRational.One);

        // N = x²−w²（两条直线），Q2 = N − circle，故 λ=1 退化。
        var pairOfLines = new BilinearForm3<BigRational>(
            BigRational.One, BigRational.Zero, BigRational.Zero,
            BigRational.Zero, BigRational.Zero, -BigRational.One);
        var q2 = new BilinearForm3<BigRational>(
            pairOfLines.Q00 - circle.Q00,
            pairOfLines.Q01 - circle.Q01,
            pairOfLines.Q02 - circle.Q02,
            pairOfLines.Q11 - circle.Q11,
            pairOfLines.Q12 - circle.Q12,
            pairOfLines.Q22 - circle.Q22);

        var pencil = ConicPencil.FromConics(circle, q2);
        using var ctx = new Z3RcfContext();
        var roots = pencil.FindDegenerateLambdas(ctx);
        try
        {
            using var one = ctx.FromInt(1);
            Assert.Contains(roots, root => ctx.Sign(ctx.Sub(root, one)) == 0);
        }
        finally
        {
            foreach (var root in roots) root.Dispose();
        }
    }
}
