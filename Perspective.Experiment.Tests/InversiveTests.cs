using Perspective.Core.Math;
using Perspective.Experiment.Inversive;
using Perspective.Experiment.Subspace;
using Xunit;

namespace Perspective.Experiment.Tests;

public sealed class InversiveTests
{
    private static ExactScalar S(long n) => ExactScalar.FromInt(n);

    [Fact]
    public void GeneralizedCircle_RecognizesCircleLinePoint()
    {
        var circle = GeneralizedCircle<ExactScalar>.FromCircle(
            new Vec2<ExactScalar>(S(0), S(0)), S(1));
        Assert.Equal(GeneralizedCircleKind.Circle, circle.Classify());
        Assert.Equal(StrictResult.StrictlyTrue, circle.Eval(new Vec2<ExactScalar>(S(1), S(0))).VerifyStrictZero());

        var line = GeneralizedCircle<ExactScalar>.FromLine(S(1), S(0), S(-1));
        Assert.Equal(GeneralizedCircleKind.Line, line.Classify());
        Assert.Equal(StrictResult.StrictlyTrue, line.Eval(new Vec2<ExactScalar>(S(1), S(3))).VerifyStrictZero());

        var point = GeneralizedCircle<ExactScalar>.FromPoint(new Vec2<ExactScalar>(S(2), S(3)));
        Assert.Equal(GeneralizedCircleKind.Point, point.Classify());
        Assert.Equal(StrictResult.StrictlyTrue, point.Eval(new Vec2<ExactScalar>(S(2), S(3))).VerifyStrictZero());
    }

    [Fact]
    public void GeneralizedCircle_UnitInversion()
    {
        var unit = GeneralizedCircle<ExactScalar>.FromCircle(Vec2<ExactScalar>.Zero, S(1));
        var fixedCircle = unit.InvertInUnitCircle();
        Assert.Equal(GeneralizedCircleKind.Circle, fixedCircle.Classify());
        Assert.Equal(StrictResult.StrictlyTrue,
            fixedCircle.Eval(new Vec2<ExactScalar>(S(1), S(0))).VerifyStrictZero());

        // x=1 不过原点，反演成过原点的圆 x²+y²−x=0。
        var line = GeneralizedCircle<ExactScalar>.FromLine(S(1), S(0), S(-1));
        var inverted = line.InvertInUnitCircle();
        Assert.Equal(GeneralizedCircleKind.Circle, inverted.Classify());
        Assert.Equal(StrictResult.StrictlyTrue,
            inverted.Eval(new Vec2<ExactScalar>(S(0), S(0))).VerifyStrictZero());
        Assert.Equal(StrictResult.StrictlyTrue,
            inverted.Eval(new Vec2<ExactScalar>(S(1), S(0))).VerifyStrictZero());
    }

    [Fact]
    public void GeneralizedCircle_GeneralInversion()
    {
        // 以 (1,0) 为心、半径 1 反演直线 x=2：
        // 直线距心 1，切于 (2,0)，反演成以 (1,0),(2,0) 为直径的圆。
        var line = GeneralizedCircle<ExactScalar>.FromLine(S(1), S(0), S(-2));
        var inverted = line.InvertInCircle(
            new Vec2<ExactScalar>(S(1), S(0)), S(1)).Value;

        Assert.Equal(GeneralizedCircleKind.Circle, inverted.Classify());
        Assert.Equal(StrictResult.StrictlyTrue,
            inverted.Eval(new Vec2<ExactScalar>(S(1), S(0))).VerifyStrictZero());
        Assert.Equal(StrictResult.StrictlyTrue,
            inverted.Eval(new Vec2<ExactScalar>(S(2), S(0))).VerifyStrictZero());
        Assert.Equal(StrictResult.StrictlyTrue,
            inverted.Eval(new Vec2<ExactScalar>(S(3) / S(2), S(1) / S(2)))
                .VerifyStrictZero());
    }

    [Fact]
    public void ComplexMobius_TransformsCircle()
    {
        var unit = new GeneralizedCircle<ExactComplex>(
            ExactComplex.One, ExactComplex.Zero,
            ExactComplex.Zero, -ExactComplex.One);
        var translation = new MobiusMap(
            ExactComplex.One, ExactComplex.One,
            ExactComplex.Zero, ExactComplex.One);

        var image = translation.Apply(unit).Value;

        // z ↦ z+1 把单位圆变成 |w−1|=1，即 w²−w−w̄=0。
        foreach (var z in new[]
                 {
                     ExactComplex.Zero,
                     ExactComplex.FromLong(2),
                     ExactComplex.One + ExactComplex.I
                 })
            Assert.Equal(StrictResult.StrictlyTrue,
                image.EvalAt(HComplex.From(z)).VerifyStrictZero());
    }

    [Fact]
    public void GeneralizedCircle_Orthogonality()
    {
        var unit = GeneralizedCircle<ExactScalar>.FromCircle(Vec2<ExactScalar>.Zero, S(1));
        var other = GeneralizedCircle<ExactScalar>.FromCircle(new Vec2<ExactScalar>(S(2), S(0)), S(3));

        Assert.Equal(
            StrictResult.StrictlyTrue,
            unit.InnerProduct(other).VerifyStrictZero());

        var concentric = GeneralizedCircle<ExactScalar>.FromCircle(Vec2<ExactScalar>.Zero, S(9));
        Assert.False(unit.IsOrthogonal(concentric));
    }
}
