using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// 问题陈述的量词树（Statement）与按形状分派的求解器（ProblemSpec）。
/// </summary>
public class ProblemSpecTests
{
    [Fact]
    public void UniversalIdentity_IsProved()
    {
        var x = ExactScalar.FromSymbol("x");
        var statement = Statement.ForAll(["x"],
            Statement.Equal((x + 1) * (x + 1), x * x + 2 * x + 1));

        var report = ProblemSpec.Evaluate(statement);

        Assert.Equal(ProblemShape.Universal, report.Shape);
        Assert.Equal(TruthValue.True, report.Truth);
    }

    [Fact]
    public void UniversalFalse_IsRefuted()
    {
        var x = ExactScalar.FromSymbol("x");
        var statement = Statement.ForAll(["x"], Statement.Equal(x * x, 1));

        var report = ProblemSpec.Evaluate(statement);

        Assert.Equal(TruthValue.False, report.Truth);
    }

    [Fact]
    public void Existential_IsSolved()
    {
        var x = ExactScalar.FromSymbol("x");
        var statement = Statement.Exists(["x"], Statement.Equal(x * x, 4));

        var report = ProblemSpec.Evaluate(statement);

        Assert.Equal(ProblemShape.Existential, report.Shape);
        Assert.Equal(TruthValue.True, report.Truth);
        Assert.NotNull(report.Solutions);
        Assert.Equal(StrictResult.StrictlyTrue, RealSets.Contains(report.Solutions!, ExactScalar.FromInt(2)));
        Assert.Equal(StrictResult.StrictlyTrue, RealSets.Contains(report.Solutions!, ExactScalar.FromInt(-2)));
    }

    [Fact]
    public void ExistentialWithoutSolution_IsFalse()
    {
        var x = ExactScalar.FromSymbol("x");
        var statement = Statement.Exists(["x"], Statement.Equal(x * x + 1, 0));

        var report = ProblemSpec.Evaluate(statement);

        Assert.Equal(TruthValue.False, report.Truth);
    }

    [Fact]
    public void QuantifierAlternation_IsReportedUnsupported()
    {
        var x = ExactScalar.FromSymbol("x");
        var y = ExactScalar.FromSymbol("y");
        var statement = Statement.ForAll(["x"],
            Statement.Exists(["y"], Statement.Equal(x * y, 1)));

        var report = ProblemSpec.Evaluate(statement);

        Assert.Equal(ProblemShape.Mixed, report.Shape);
        Assert.NotNull(report.UnsupportedReason);
        Assert.Equal(TruthValue.Unknown, report.Truth);
    }

    [Fact]
    public void QuantifierInsideJunction_NeedsPrenexForm()
    {
        var x = ExactScalar.FromSymbol("x");
        var statement = Statement.And(
            Statement.ForAll(["x"], Statement.Equal(x, x)),
            Statement.Equal(ExactScalar.FromInt(1), ExactScalar.FromInt(1)));

        var report = ProblemSpec.Evaluate(statement);

        Assert.NotNull(report.UnsupportedReason);
        Assert.Contains("前束", report.UnsupportedReason);
    }

    [Fact]
    public void KleeneJunctions_AreHandled()
    {
        var trueStmt = Statement.Equal(ExactScalar.FromInt(1), ExactScalar.FromInt(1));
        var falseStmt = Statement.Equal(ExactScalar.FromInt(1), ExactScalar.FromInt(2));

        Assert.Equal(TruthValue.False, ProblemSpec.Evaluate(Statement.And(trueStmt, falseStmt)).Truth);
        Assert.Equal(TruthValue.True, ProblemSpec.Evaluate(Statement.Or(trueStmt, falseStmt)).Truth);
    }

    /// <summary>几何参数恒等式：期中题第 2 问的"∀k"版本，走有理函数通道。</summary>
    [Fact]
    public void GeometricParametricIdentity_IsProved()
    {
        var circle = Conic.FromGeneralEquation(1, 0, 1, -4, 0, 0);
        var k = ExactScalar.FromSymbol("k");
        var mid = circle.ChordMidpoint(new HLine(k, -1, 2));
        var dx = mid.X / mid.W;
        var dy = mid.Y / mid.W;
        var diff = (dx - 1) * (dx - 1) + (dy - 1) * (dy - 1) - 2;

        var report = ProblemSpec.Evaluate(Statement.ForAll(["k"], Statement.Equal(diff, ExactScalar.FromInt(0))));

        Assert.Equal(ProblemShape.Universal, report.Shape);
        Assert.Equal(TruthValue.True, report.Truth);
    }
}
