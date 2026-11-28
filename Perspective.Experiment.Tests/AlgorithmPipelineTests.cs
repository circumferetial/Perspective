using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Xunit;

namespace Perspective.Experiment.Tests;

public sealed class AlgorithmPipelineTests
{
    [Fact]
    public void HS3_ConicIntersection_FocusChord()
    {
        // 真正跑 ConicIntersection：y²=4x 与焦点弦 x=1。
        var parabola = new BilinearForm3<ExactScalar>(
            ExactScalar.Zero, ExactScalar.Zero, -ExactScalar.FromInt(2),
            ExactScalar.One, ExactScalar.Zero, ExactScalar.Zero);
        var chord = new Dual3<ExactScalar>(
            ExactScalar.One, ExactScalar.Zero, -ExactScalar.One);

        var result = ConicIntersection.Intersect(chord, parabola);
        Assert.Equal(LineConicKind.TwoPoints, result.Kind);
        Assert.NotNull(result.P1);
        Assert.NotNull(result.P2);

        var focus = Primal3<ExactScalar>.Fixed(1, 0);
        var sum = InverseDistance(result.P1.Value, focus)
                  + InverseDistance(result.P2.Value, focus);

        Assert.Equal(StrictResult.StrictlyTrue,
            (sum - ExactScalar.One).VerifyStrictZero());
    }

    private static ExactScalar InverseDistance(
        Primal3<ExactScalar> point,
        Primal3<ExactScalar> focus)
    {
        var dx = point.X / point.W - focus.X;
        var dy = point.Y / point.W - focus.Y;
        var distance = ExactScalar.Sqrt(dx * dx + dy * dy);
        return ExactScalar.One / distance;
    }
}
