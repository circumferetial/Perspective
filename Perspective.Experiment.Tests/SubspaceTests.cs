using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Perspective.Experiment.Subspace.Benchmarks;
using Poly = Perspective.Experiment.Subspace.PolyExpr;

namespace Perspective.Experiment.Tests;

public class SubspaceTests
{
    private static ExactScalar S(long v) => ExactScalar.FromInt(v);

    private static StrictVerdict UnitOf<T>(T value) where T : IExactRing<T> => T.IsUnit(value);

    private static StrictVerdict TryInverseOf<T>(T value, out T inverse)
        where T : IComputableField<T> => T.TryInverse(value, out inverse);

    private static void AssertFromLongLaws<T>(Func<long, T> fromLong) where T : IExactRing<T>
    {
        Assert.Equal(StrictResult.StrictlyTrue, T.VerifyZero(fromLong(0) - T.Zero));
        Assert.Equal(StrictResult.StrictlyTrue, T.VerifyZero(fromLong(1) - T.One));

        var two = fromLong(2);
        Assert.Equal(StrictResult.StrictlyTrue, T.VerifyZero(two - (T.One + T.One)));

        var three = fromLong(3);
        Assert.Equal(StrictResult.StrictlyTrue, T.VerifyZero(three - (two + T.One)));
        Assert.Equal(StrictResult.StrictlyTrue, T.VerifyZero(fromLong(-1) + T.One));
    }

    private static void AssertZero<T>(T value) where T : IExactRing<T> =>
        Assert.Equal(StrictResult.StrictlyTrue, T.VerifyZero(value));

    /// <summary>随机测试只能找反例，不能证明定律；这里用固定种子锁回归。</summary>
    private static void AssertRingLaws<T>(Func<Random, T> next, int iterations = 25)
        where T : IExactRing<T>
    {
        var rng = new Random(20261128);
        for (var i = 0; i < iterations; i++)
        {
            var a = next(rng);
            var b = next(rng);
            var c = next(rng);

            AssertZero(a + b - (b + a));
            AssertZero((a + b) + c - (a + (b + c)));
            AssertZero(a * b - b * a);
            AssertZero(a * b * c - a * (b * c));
            AssertZero(a * (b + c) - (a * b + a * c));
            AssertZero(a * T.One - a);
            AssertZero(a + T.Zero - a);
        }
    }

    private static void AssertFromLongRandom<T>(Func<long, T> fromLong, int iterations = 40)
        where T : IExactRing<T>
    {
        var rng = new Random(20261129);
        for (var i = 0; i < iterations; i++)
        {
            long a = rng.Next(-100, 101);
            long b = rng.Next(-100, 101);

            AssertZero(fromLong(a + b) - (fromLong(a) + fromLong(b)));
            AssertZero(fromLong(a * b) - fromLong(a) * fromLong(b));
            AssertZero(fromLong(-a) + fromLong(a));
        }
    }

    private static PolyExpr NextPoly(Random rng)
    {
        var x = PolyExpr.Var(0);
        return PolyExpr.FromLong(rng.Next(-4, 5)) * x * x
               + PolyExpr.FromLong(rng.Next(-4, 5)) * x
               + PolyExpr.FromLong(rng.Next(-4, 5));
    }

    private static void AssertMaps(MobiusMap map, HComplex from, HComplex to)
    {
        var image = map.Apply(from).Value;
        var cross = image.Z * to.W - to.Z * image.W;
        Assert.Equal(StrictResult.StrictlyTrue, cross.VerifyStrictZero());
    }

    private static bool IsZeroFast<T>(T value) where T : IExactRing<T> => T.IsZeroFast(value);

    [Fact]
    public void Join_And_Incidence()
    {
        var a = Primal3<ExactScalar>.Fixed(0, 0);
        var b = Primal3<ExactScalar>.Fixed(1, 0);
        var line = SubspaceAlgebra.Join(a, b); // y = 0

        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.IsIncident(line, Primal3<ExactScalar>.Fixed(2, 0)).Value);
        Assert.Equal(StrictResult.StrictlyFalse,
            SubspaceAlgebra.IsIncident(line, Primal3<ExactScalar>.Fixed(0, 1)).Value);

        var origin = SubspaceAlgebra.Meet(
            new Dual3<ExactScalar>(S(1), S(0), S(0)),
            new Dual3<ExactScalar>(S(0), S(1), S(0)));
        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.SameSubspace(origin, Primal3<ExactScalar>.Fixed(0, 0)).Value);
    }

    [Fact]
    public void Det_Collinearity()
    {
        var a = Primal3<ExactScalar>.Fixed(0, 0);
        var b = Primal3<ExactScalar>.Fixed(1, 1);
        var c = Primal3<ExactScalar>.Fixed(2, 2);
        Assert.Equal(StrictResult.StrictlyTrue, SubspaceAlgebra.AreCollinear(a, b, c).Value);

        var d = Primal3<ExactScalar>.Fixed(0, 1);
        Assert.Equal(StrictResult.StrictlyFalse, SubspaceAlgebra.AreCollinear(a, b, d).Value);
    }

    [Fact]
    public void Veronese_Dot_Matches_ConicEval()
    {
        var conic = new BilinearForm3<ExactScalar>(S(1), S(0), S(0), S(2), S(0), S(-8));
        var p = Primal3<ExactScalar>.Fixed(1, 1);

        var dot = Veronese.Dot(Veronese.Row(p), Veronese.Coefficients(conic));
        Assert.Equal(conic.Eval(p).Approx, dot.Approx, 12);
    }

    [Fact]
    public void ConicParametrization_StaysOnConic()
    {
        var conic = new BilinearForm3<ExactScalar>(S(1), S(0), S(0), S(1), S(0), S(-1));
        var parametrization = new ConicParametrization<ExactScalar>(
            conic, Primal3<ExactScalar>.Fixed(1, 0));

        foreach (var t in new[] { 0L, 1L, 2L })
        {
            var p = parametrization.At(S(t)).Value;
            Assert.Equal(StrictResult.StrictlyTrue, conic.Eval(p).VerifyStrictZero());
        }
    }

    [Fact]
    public void RationalRange_EvenQuadratic()
    {
        var u = Poly.Var(0);
        var s = u * u;
        var n = Poly.FromLong(3) * (Poly.FromLong(9) * s * s - Poly.FromLong(18) * s + Poly.One);
        var d = Poly.FromLong(4) * (Poly.FromLong(9) * s * s + Poly.FromLong(10) * s + Poly.One);

        var range = RationalRange.OfPositive(n, d, 0, S(1) / S(3));

        Assert.Equal(-0.5625, range.Lower.Value.Approx, 8);
        Assert.False(range.Lower.Closed);
        Assert.Equal(0.75, range.Upper.Value.Approx, 8);
        Assert.False(range.Upper.Closed);
    }

    [Fact]
    public void RationalRange_Certificate()
    {
        var u = Poly.Var(0);
        var s = u * u;
        var n = Poly.FromLong(3) * (Poly.FromLong(9) * s * s - Poly.FromLong(18) * s + Poly.One);
        var d = Poly.FromLong(4) * (Poly.FromLong(9) * s * s + Poly.FromLong(10) * s + Poly.One);

        var certificate = RationalRange.Certify(n, d, 0, S(1) / S(3));

        Assert.True(certificate.Lower.Valid);
        Assert.True(certificate.Upper.Valid);
        Assert.Equal(-0.5625, certificate.Lower.Bound.Approx, 8);
        Assert.Equal(0.75, certificate.Upper.Bound.Approx, 8);
    }

    [Fact]
    public void ApproxRange_WithPoles_AndTwoIntervals()
    {
        var u = Poly.Var(0);
        var range = RationalRangeApprox.OfReal(Poly.One, u * u - Poly.One, 0);

        Assert.Equal(2, range.Intervals.Count);

        Assert.True(double.IsNegativeInfinity(range.Intervals[0].Lo));
        Assert.Equal(-1.0, range.Intervals[0].Hi, 8);
        Assert.True(range.Intervals[0].HiClosed);

        Assert.Equal(0.0, range.Intervals[1].Lo, 8);
        Assert.False(range.Intervals[1].LoClosed);
        Assert.True(double.IsPositiveInfinity(range.Intervals[1].Hi));
    }

    [Fact]
    public void ProblemSet5_AllPass()
    {
        var results = ProblemSet5.RunAll();
        Assert.Equal(7, results.Count);
        Assert.All(results, r => Assert.True(r.Passed, $"{r.Id}: {r.Detail}"));
    }

    [Fact]
    public void ZeroVector_IsRejected_ByGeometryPredicates()
    {
        var zeroPoint = new Primal3<ExactScalar>(S(0), S(0), S(0));
        var zeroLine = new Dual3<ExactScalar>(S(0), S(0), S(0));
        var finite = Primal3<ExactScalar>.Fixed(1, 2);

        var same = SubspaceAlgebra.SameSubspace(zeroPoint, finite);
        Assert.True(same.IsFailure);
        Assert.Equal(GeometryError.ZeroVector, same.Error);

        var incident = SubspaceAlgebra.IsIncident(zeroLine, finite);
        Assert.True(incident.IsFailure);
        Assert.Equal(GeometryError.ZeroVector, incident.Error);
    }

    [Fact]
    public void AffineMidpoint_RejectsPointAtInfinity()
    {
        var lens = GeometryLens<ExactScalar>.EuclideanXoy;
        var atInfinity = new Primal3<ExactScalar>(S(1), S(0), S(0));
        var finite = Primal3<ExactScalar>.Fixed(0, 0);

        var result = lens.Affine.Midpoint(atInfinity, finite);
        Assert.True(result.IsFailure);
        Assert.Equal(GeometryError.AtInfinity, result.Error);
    }

    [Fact]
    public void HyperbolicPerpendicularThrough_PoleIsDegenerate()
    {
        var metric = GeometryLens<ExactScalar>.Hyperbolic.Metric;
        var line = new Dual3<ExactScalar>(S(1), S(0), S(-2));
        var pole = metric.OmegaStar.Pole(line);

        var result = metric.PerpendicularThroughStrict(pole, line);
        Assert.True(result.IsFailure);
        Assert.Equal(GeometryError.UndefinedParameter, result.Error);
    }

    [Fact]
    public void ConicParametrization_RejectsDirectionEqualKnownInfinityPoint()
    {
        // 抛物线 x² − y w = 0，已知无穷远点 [0,1,0]；再取同一方向 (0,1,0) 线未定义。
        var parabola = new BilinearForm3<ExactScalar>(
            S(1), S(0), S(0), S(0), -S(1) / S(2), S(0));
        var known = new Primal3<ExactScalar>(S(0), S(1), S(0));
        var parametrization = new ConicParametrization<ExactScalar>(parabola, known);

        var degenerate = parametrization.At(S(0), S(1));
        Assert.True(degenerate.IsFailure);
        Assert.Equal(GeometryError.UndefinedParameter, degenerate.Error);

        var horizontal = parametrization.At(S(1), S(0));
        Assert.True(horizontal.IsOk);
    }

    [Fact]
    public void LinearMap_ApplyDualConic_ScalesByMSMT()
    {
        var map = new LinearMap3<ExactScalar>(
            S(2), S(0), S(0),
            S(0), S(3), S(0),
            S(0), S(0), S(1));
        var dual = DualBilinearForm3<ExactScalar>.Fixed(1, 0, 0, 1, 0, 1);

        var image = map.Apply(dual);

        Assert.Equal(4, image.S00.Approx, 10);
        Assert.Equal(0, image.S01.Approx, 10);
        Assert.Equal(9, image.S11.Approx, 10);
        Assert.Equal(1, image.S22.Approx, 10);
    }

    [Fact]
    public void Veronese_LineRow_MatchesDualConicEval()
    {
        var line = new Dual3<ExactScalar>(S(1), S(2), S(3));
        var dual = new DualBilinearForm3<ExactScalar>(S(2), S(1), S(0), S(3), S(1), S(4));

        var dot = Veronese.Dot(Veronese.LineRow(line), Veronese.DualCoefficients(dual));
        Assert.Equal(dual.Eval(line).Approx, dot.Approx, 10);
    }

    [Fact]
    public void AreParallel_Coincident_Fails_And_InfinityLine_IsTrue()
    {
        var lens = GeometryLens<ExactScalar>.EuclideanXoy;
        var line = new Dual3<ExactScalar>(S(1), S(1), S(-1));

        var coincident = lens.Affine.AreParallelStrict(line, line);
        Assert.True(coincident.IsFailure);
        Assert.Equal(GeometryError.UndefinedParameter, coincident.Error);

        var finiteLine = new Dual3<ExactScalar>(S(0), S(1), S(-1)); // y = 1
        var withInfinity = lens.Affine.AreParallel(lens.InfinityLine, finiteLine);
        Assert.True(withInfinity.IsOk);
        Assert.Equal(StrictResult.StrictlyTrue, withInfinity.Value);
    }

    [Fact]
    public void Veronese_DualMatrix_MatchesLineRows()
    {
        var lines = new[]
        {
            new Dual3<ExactScalar>(S(1), S(0), S(0)),
            new Dual3<ExactScalar>(S(0), S(1), S(0)),
            new Dual3<ExactScalar>(S(1), S(1), S(-1)),
        };
        var dual = new DualBilinearForm3<ExactScalar>(S(2), S(1), S(0), S(3), S(1), S(4));
        var matrix = Veronese.DualMatrix(lines);

        Assert.Equal(lines.Length, matrix.GetLength(0));
        Assert.Equal(6, matrix.GetLength(1));

        for (var i = 0; i < lines.Length; i++)
        {
            var row = new ExactScalar[6];
            for (var j = 0; j < 6; j++) row[j] = matrix[i, j];

            Assert.Equal(dual.Eval(lines[i]).Approx,
                Veronese.Dot(row, Veronese.DualCoefficients(dual)).Approx, 10);
        }
    }

    [Fact]
    public void TwiceSignedArea_IsInvariantUnderHomogeneousScaling()
    {
        var a = Primal3<ExactScalar>.Fixed(0, 0);
        var b = Primal3<ExactScalar>.Fixed(1, 0);
        var c = Primal3<ExactScalar>.Fixed(0, 1);
        var area = ExactGeometryOps.TwiceSignedArea(a, b, c).Value;

        var aScaled = new Primal3<ExactScalar>(S(0), S(0), S(2));
        var bScaled = new Primal3<ExactScalar>(S(2), S(0), S(2));
        var cScaled = new Primal3<ExactScalar>(S(0), S(2), S(2));
        var scaledArea = ExactGeometryOps.TwiceSignedArea(aScaled, bScaled, cScaled).Value;

        Assert.Equal(area.Approx, scaledArea.Approx, 10);
    }

    [Fact]
    public void IsZeroVector_HandlesMixedSymbolicAndNumericComponents()
    {
        var mixed = new Primal3<ExactScalar>(
            ExactScalar.Zero,
            ExactScalar.FromApprox(1e-12),
            ExactScalar.Zero);

        Assert.True(mixed.IsZeroVector());
    }

    [Fact]
    public void Normalized_DividesTinyButExactVectors()
    {
        var trillion = ExactScalar.FromInt(1_000_000_000_000);
        var tiny = new Primal3<ExactScalar>(
            ExactScalar.One / trillion,
            ExactScalar.FromInt(2) / trillion,
            ExactScalar.One / trillion);

        var normalized = tiny.Normalized();
        Assert.Equal(1.0, normalized.Y.Approx, 6);
        Assert.True(normalized.X.Approx < 1);
    }

    [Fact]
    public void EuclideanDistance_RejectsHyperbolicLens()
    {
        var lens = GeometryLens<ExactScalar>.Hyperbolic;
        var result = lens.EuclideanDistanceSquared(
            Primal3<ExactScalar>.Fixed(0, 0),
            Primal3<ExactScalar>.Fixed(1, 0));

        Assert.True(result.IsFailure);
        Assert.Equal(GeometryError.UndefinedParameter, result.Error);
    }

    [Fact]
    public void CartesianCoordinates_RejectNonStandardInfinityLine()
    {
        var lens = new GeometryLens<ExactScalar>(
            new AffineLens<ExactScalar>(new Dual3<ExactScalar>(S(1), S(0), S(0))),
            CayleyKleinMetric<ExactScalar>.Euclidean);

        Assert.False(lens.TryToCartesian(Primal3<ExactScalar>.Fixed(1, 2), out _, out _));
    }

    [Fact]
    public void ExactScalar_IsZeroFast_HandlesNonIntegerZero()
    {
        Assert.True(IsZeroFast(ExactScalar.Zero));
        Assert.False(IsZeroFast(ExactScalar.One));

        var realZero = ExactScalar.FromFormula("0.0");
        Assert.Equal(StrictResult.StrictlyTrue, realZero.VerifyStrictZero(allowFastNumericReject: false));
        Assert.True(IsZeroFast(realZero));
    }

    [Fact]
    public void Normalized_HandlesNaNProbe()
    {
        var nanProbe = ExactScalar.FromInt(1) with { Approx = double.NaN };
        var p = new Primal3<ExactScalar>(nanProbe, ExactScalar.One, ExactScalar.One);

        var normalized = p.Normalized();
        Assert.Equal(1.0, normalized.Y.Approx, 10);
        Assert.Equal(1.0, normalized.W.Approx, 10);
    }

    [Fact]
    public void ApproxRange_DoesNotMergeOpenIntervalsWithGapPoint()
    {
        var u = Poly.Var(0);
        // f(x)=(x−1)/(x+1)，值域是 R\{1}：两段 (1,+∞) 与 (−∞,1) 不能在 1 处合并。
        var range = RationalRangeApprox.OfReal(u - Poly.One, u + Poly.One, 0);

        Assert.Equal(2, range.Intervals.Count);
    }

    [Fact]
    public void ApproxRange_HandlesRepeatedCriticalRoot()
    {
        var u = Poly.Var(0);
        var n = (u - Poly.One) * (u - Poly.One) * (u - Poly.One);
        var d = u * u + Poly.One;

        var range = RationalRangeApprox.OfReal(n, d, 0);
        Assert.NotEmpty(range.Intervals);
    }

    [Fact]
    public void ComposedRings_IsZeroFast_ForwardsToComponents()
    {
        Assert.True(IsZeroFast(new DualNumber<ExactScalar>(ExactScalar.Zero, ExactScalar.Zero)));
        Assert.False(IsZeroFast(new DualNumber<ExactScalar>(ExactScalar.One, ExactScalar.Zero)));
        Assert.True(IsZeroFast(new ExactComplex(ExactScalar.Zero, ExactScalar.Zero)));
        Assert.True(IsZeroFast(new SplitComplex<ExactScalar>(ExactScalar.Zero, ExactScalar.Zero)));
    }

    [Fact]
    public void HComplex_DuplicatePoint_IsStillConcyclic()
    {
        var a = HComplex.From(new ExactComplex(ExactScalar.Zero, ExactScalar.Zero));
        var b = HComplex.From(new ExactComplex(ExactScalar.One, ExactScalar.Zero));
        var c = HComplex.From(new ExactComplex(ExactScalar.Zero, ExactScalar.One));

        Assert.Equal(StrictResult.StrictlyTrue, HComplex.AreConcyclicOrCollinear(a, b, c, a));
    }

    [Fact]
    public void UnifiedGeoSpace_RequireNonZero_EliminatesDegenerateBranch()
    {
        var space = new UnifiedGeoSpace();
        var x = space.NewSymbol("x");
        var y = space.NewSymbol("y");

        space.RequireZero(x * y);
        space.RequireNonZero(y);

        var outcome = space.ProveZero(x);
        Assert.Equal(UnifiedProofStatus.Proved, outcome.Status);
    }

    [Fact]
    public void UnifiedGeoSpace_WithoutNonZero_DoesNotProveDegenerateBranch()
    {
        var space = new UnifiedGeoSpace();
        var x = space.NewSymbol("x");
        var y = space.NewSymbol("y");

        space.RequireZero(x * y);

        var outcome = space.ProveZero(x);
        Assert.NotEqual(UnifiedProofStatus.Proved, outcome.Status);
    }

    [Fact]
    public void ConicFromFivePoints_RecoversEllipse()
    {
        var points = new[]
        {
            Primal3<ExactScalar>.Fixed(2, 0),
            Primal3<ExactScalar>.Fixed(0, 1),
            Primal3<ExactScalar>.Fixed(-2, 0),
            Primal3<ExactScalar>.Fixed(0, -1),
            new Primal3<ExactScalar>(S(8) / S(5), S(3) / S(5), S(1)),
        };

        var conic = ExactGeometryOps.ConicFromFivePoints(points).Value;
        foreach (var p in points)
            Assert.Equal(StrictResult.StrictlyTrue, conic.Eval(p).VerifyStrictZero());
    }

    [Fact]
    public void TangentsFromPoint_CountsOutsideOnAndInside()
    {
        var circle = new BilinearForm3<ExactScalar>(S(1), S(0), S(0), S(1), S(0), S(-1));

        var outside = ExactGeometryOps.TangentsFromPoint(Primal3<ExactScalar>.Fixed(2, 0), circle).Value;
        Assert.Equal(2, outside.Count);
        foreach (var t in outside)
            Assert.Equal(StrictResult.StrictlyTrue, circle.Eval(t).VerifyStrictZero());

        var onCircle = ExactGeometryOps.TangentsFromPoint(Primal3<ExactScalar>.Fixed(1, 0), circle).Value;
        Assert.Single(onCircle);

        var inside = ExactGeometryOps.TangentsFromPoint(Primal3<ExactScalar>.Fixed(0, 0), circle).Value;
        Assert.Empty(inside);
    }

    [Fact]
    public void CircleCircleIntersection_UsesRadicalAxis()
    {
        var c1 = new BilinearForm3<ExactScalar>(S(1), S(0), S(0), S(1), S(0), S(-1));
        var c2 = new BilinearForm3<ExactScalar>(S(1), S(0), S(-1), S(1), S(0), S(0)); // x²+y²−2x=0
        var c3 = new BilinearForm3<ExactScalar>(S(1), S(0), S(-3), S(1), S(0), S(8)); // x²+y²−6x+8=0

        Assert.Equal(LineConicKind.TwoPoints, ExactGeometryOps.CircleCircleIntersection(c1, c2).Value.Kind);
        Assert.Equal(LineConicKind.NoRealIntersection, ExactGeometryOps.CircleCircleIntersection(c1, c3).Value.Kind);

        var same = ExactGeometryOps.CircleCircleIntersection(c1, c1);
        Assert.True(same.IsFailure);
        Assert.Equal(GeometryError.UndefinedParameter, same.Error);
    }

    [Fact]
    public void UnitPhase_ToMap_IsRotation()
    {
        var quarterTurn = UnitPhase.FromHalfAngle(ExactScalar.One); // cos=0, sin=1
        var p = quarterTurn.Apply(Primal3<ExactScalar>.Fixed(1, 0));

        Assert.Equal(0.0, p.X.Approx, 10);
        Assert.Equal(1.0, p.Y.Approx, 10);
        Assert.Equal(1.0, p.W.Approx, 10);
    }

    [Fact]
    public void LinearMap_FromFourPoints_MatchesKnownAffineMap()
    {
        var p1 = Primal3<ExactScalar>.Fixed(1, 0);
        var p2 = Primal3<ExactScalar>.Fixed(0, 1);
        var p3 = Primal3<ExactScalar>.Fixed(0, 0);
        var p4 = Primal3<ExactScalar>.Fixed(2, 3);

        var known = new LinearMap3<ExactScalar>(
            S(2), S(0), S(1),
            S(0), S(3), S(2),
            S(0), S(0), S(1));
        var q1 = known.Apply(p1);
        var q2 = known.Apply(p2);
        var q3 = known.Apply(p3);
        var q4 = known.Apply(p4);

        var map = LinearMap3<ExactScalar>.FromFourPoints(p1, p2, p3, p4, q1, q2, q3, q4).Value;

        foreach (var (p, q) in new[] { (p1, q1), (p2, q2), (p3, q3), (p4, q4) })
            Assert.Equal(StrictResult.StrictlyTrue,
                SubspaceAlgebra.SameSubspace(map.Apply(p), q).Value);
    }

    [Fact]
    public void OtherTangent_ReturnsOtherTangentThroughPoint()
    {
        var circle = new BilinearForm3<ExactScalar>(S(1), S(0), S(0), S(1), S(0), S(-1));
        var dual = circle.DualForm();
        var known = new Dual3<ExactScalar>(S(1), S(0), S(-1)); // x = 1
        var throughPoint = new Dual3<ExactScalar>(S(1), S(1), S(-2)); // x + y = 2, P=(1,1)

        var other = dual.OtherTangent(known, throughPoint).Value;
        var p = Primal3<ExactScalar>.Fixed(1, 1);

        Assert.Equal(StrictResult.StrictlyTrue, dual.Eval(other).VerifyStrictZero());
        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.Pair(other, p).VerifyStrictZero());
    }

    [Fact]
    public void HComplex_Primal3_RoundTrip_OnFinitePoints()
    {
        var p = new Primal3<ExactScalar>(S(2), S(4), S(1));
        var h = p.ToHComplex().Value;
        var back = h.ToPrimal3().Value;

        Assert.Equal(StrictResult.StrictlyTrue, SubspaceAlgebra.SameSubspace(back, p).Value);

        var scaled = new Primal3<ExactScalar>(S(4), S(8), S(2));
        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.SameSubspace(scaled.ToHComplex().Value.ToPrimal3().Value, p).Value);
    }

    [Fact]
    public void HComplex_Primal3_RoundTrip_WithComplexW()
    {
        var h = new HComplex(
            new ExactComplex(S(2), S(4)),
            new ExactComplex(S(1), S(1)));

        var back = h.ToPrimal3().Value.ToHComplex().Value;
        var cross = h.Z * back.W - back.Z * h.W;

        Assert.Equal(StrictResult.StrictlyTrue, cross.VerifyStrictZero());
    }

    [Fact]
    public void HComplex_Primal3_InfinityEdges()
    {
        Assert.True(HComplex.Infinity.ToPrimal3().IsFailure);
        Assert.Equal(GeometryError.AtInfinity, HComplex.Infinity.ToPrimal3().Error);

        var direction = new Primal3<ExactScalar>(S(1), S(2), S(0));
        Assert.True(direction.ToHComplex().IsFailure);
        Assert.Equal(GeometryError.AtInfinity, direction.ToHComplex().Error);

        var projected = direction.ProjectToRiemannSphere().Value;
        Assert.Equal(HComplex.Infinity, projected);
    }

    [Fact]
    public void HComplex_Primal3_ZeroVectors()
    {
        var zeroH = new HComplex(ExactComplex.Zero, ExactComplex.Zero);
        Assert.True(zeroH.ToPrimal3().IsFailure);
        Assert.Equal(GeometryError.ZeroVector, zeroH.ToPrimal3().Error);

        var zeroP = new Primal3<ExactScalar>(S(0), S(0), S(0));
        Assert.True(zeroP.ToHComplex().IsFailure);
        Assert.Equal(GeometryError.ZeroVector, zeroP.ToHComplex().Error);
        Assert.True(zeroP.ProjectToRiemannSphere().IsFailure);
    }

    [Fact]
    public void MobiusMap_FromThreePoints_MapsTriples()
    {
        var p1 = HComplex.From(new ExactComplex(S(0), S(0)));
        var p2 = HComplex.From(new ExactComplex(S(1), S(0)));
        var p3 = HComplex.Infinity;
        var q1 = HComplex.From(new ExactComplex(S(2), S(1)));
        var q2 = HComplex.From(new ExactComplex(S(-1), S(3)));
        var q3 = HComplex.From(new ExactComplex(S(4), S(-2)));

        var map = MobiusMap.FromThreePoints(p1, p2, p3, q1, q2, q3).Value;
        AssertMaps(map, p1, q1);
        AssertMaps(map, p2, q2);
        AssertMaps(map, p3, q3);
    }

    [Fact]
    public void MobiusMap_ComposeAndInverse_AreConsistent()
    {
        var p1 = HComplex.From(new ExactComplex(S(0), S(0)));
        var p2 = HComplex.From(new ExactComplex(S(1), S(0)));
        var p3 = HComplex.Infinity;
        var q1 = HComplex.From(new ExactComplex(S(2), S(1)));
        var q2 = HComplex.From(new ExactComplex(S(-1), S(3)));
        var q3 = HComplex.From(new ExactComplex(S(4), S(-2)));

        var map = MobiusMap.FromThreePoints(p1, p2, p3, q1, q2, q3).Value;
        var roundTrip = map.Inverse().Value.Compose(map).Value;

        AssertMaps(roundTrip, p1, p1);
        AssertMaps(roundTrip, p2, p2);
        AssertMaps(roundTrip, p3, p3);
    }

    [Fact]
    public void MobiusMap_Singular_And_DuplicatePoints_Fail()
    {
        var singular = new MobiusMap(
            ExactComplex.One, ExactComplex.Zero,
            ExactComplex.Zero, ExactComplex.Zero);
        var p = HComplex.From(new ExactComplex(S(1), S(0)));

        Assert.True(singular.Apply(p).IsFailure);
        Assert.Equal(GeometryError.UndefinedParameter, singular.Apply(p).Error);
        Assert.True(singular.Inverse().IsFailure);

        var duplicate = MobiusMap.FromThreePoints(p, p, HComplex.Infinity, p, p, HComplex.Infinity);
        Assert.True(duplicate.IsFailure);
    }

    [Fact]
    public void AntiMobius_CircleInversion_MatchesHComplex()
    {
        var center = new ExactComplex(S(1), S(0));
        var radiusSq = S(4);
        var p = HComplex.From(new ExactComplex(S(2), S(1)));

        var anti = AntiMobiusMap.FromCircleInversion(center, radiusSq).Value;
        var got = anti.Apply(p).Value;
        var expected = HComplex.Invert(p, center, radiusSq);

        var cross = got.Z * expected.W - expected.Z * got.W;
        Assert.Equal(StrictResult.StrictlyTrue, cross.VerifyStrictZero());
    }

    [Fact]
    public void AntiMobius_Inverse_ComposesBackToMobius()
    {
        var center = new ExactComplex(S(1), S(0));
        var anti = AntiMobiusMap.FromCircleInversion(center, S(4)).Value;
        var identity = anti.Inverse().Value.Compose(anti).Value;

        var p = HComplex.From(new ExactComplex(S(2), S(1)));
        AssertMaps(identity, p, p);
    }

    [Fact]
    public void ConicClassification_DistinguishesCircleFromEllipse()
    {
        var circle = new BilinearForm3<ExactScalar>(S(1), S(0), S(0), S(1), S(0), S(-1));
        var ellipse = new BilinearForm3<ExactScalar>(S(1), S(0), S(0), S(3), S(0), S(-12)); // x²+3y²=12
        var parabola = new BilinearForm3<ExactScalar>(S(0), S(0), S(-2), S(1), S(0), S(0)); // y²=4x
        var hyperbola = new BilinearForm3<ExactScalar>(S(3), S(0), S(0), S(-4), S(0), S(-12)); // 3x²−4y²=12

        Assert.True(circle.IsCircle());
        Assert.Equal(ConicKind.Circle, circle.Classify());

        Assert.False(ellipse.IsCircle());
        Assert.Equal(ConicKind.Ellipse, ellipse.Classify());
        Assert.Equal(ConicKind.Parabola, parabola.Classify());
        Assert.Equal(ConicKind.Hyperbola, hyperbola.Classify());
    }

    [Fact]
    public void PerpendicularFoot_And_Reflection_Work()
    {
        var lens = GeometryLens<ExactScalar>.EuclideanXoy;
        var xAxis = new Dual3<ExactScalar>(S(0), S(1), S(0));
        var p = Primal3<ExactScalar>.Fixed(1, 1);

        var foot = lens.PerpendicularFoot(p, xAxis).Value;
        Assert.True(lens.TryToCartesian(foot, out var fx, out var fy));
        Assert.Equal(1.0, fx.Approx, 10);
        Assert.Equal(0.0, fy.Approx, 10);

        var reflected = lens.ReflectPointAcrossLine(p, xAxis).Value;
        Assert.True(lens.TryToCartesian(reflected, out var rx, out var ry));
        Assert.Equal(1.0, rx.Approx, 10);
        Assert.Equal(-1.0, ry.Approx, 10);
    }

    [Fact]
    public void Circumcircle_OfThreePoints_IsCircle()
    {
        var circle = ExactGeometryOps.Circumcircle(
            Primal3<ExactScalar>.Fixed(1, 0),
            Primal3<ExactScalar>.Fixed(0, 1),
            Primal3<ExactScalar>.Fixed(-1, 0)).Value;

        Assert.True(circle.IsCircle());
        Assert.Equal(ConicKind.Circle, circle.Classify());
        Assert.Equal(StrictResult.StrictlyTrue,
            circle.Eval(Primal3<ExactScalar>.Fixed(0, -1)).VerifyStrictZero());
    }

    [Fact]
    public void HyperbolicCoshSquared_FromCenterToHalfRadius()
    {
        var half = new Primal3<ExactScalar>(S(1) / S(2), S(0), S(1));
        var value = ExactGeometryOps.HyperbolicCoshSquared(Primal3<ExactScalar>.Fixed(0, 0), half).Value;

        Assert.Equal(4.0 / 3.0, value.Approx, 10);
    }

    [Fact]
    public void HyperbolicReflection_AcrossVerticalLine()
    {
        var lens = GeometryLens<ExactScalar>.Hyperbolic;
        var line = new Dual3<ExactScalar>(S(1), S(0), S(0)); // x = 0
        var p = Primal3<ExactScalar>.Fixed(1, 1);

        var reflected = lens.ReflectPointAcrossLine(p, line).Value;
        Assert.True(lens.TryToCartesian(reflected, out var x, out var y));
        Assert.Equal(-1.0, x.Approx, 10);
        Assert.Equal(1.0, y.Approx, 10);
    }

    [Fact]
    public void ConicClassification_IsInvariantUnderGlobalSignFlip()
    {
        var circle = new BilinearForm3<ExactScalar>(-S(1), S(0), S(0), -S(1), S(0), S(1));
        var ellipse = new BilinearForm3<ExactScalar>(-S(1), S(0), S(0), -S(3), S(0), S(12));

        Assert.Equal(ConicKind.Circle, circle.Classify());
        Assert.Equal(ConicKind.Ellipse, ellipse.Classify());
    }

    [Fact]
    public void Circumcircle_OfCollinearPoints_ReportsCollinear()
    {
        var result = ExactGeometryOps.Circumcircle(
            Primal3<ExactScalar>.Fixed(0, 0),
            Primal3<ExactScalar>.Fixed(1, 1),
            Primal3<ExactScalar>.Fixed(2, 2));

        Assert.True(result.IsFailure);
        Assert.Equal(GeometryError.CollinearPoints, result.Error);
    }

    [Fact]
    public void Vec2_DotDeterminantAndPerp_AreConsistent()
    {
        var a = new Vec2<ExactScalar>(S(1), S(2));
        var b = new Vec2<ExactScalar>(S(3), S(4));

        Assert.Equal(11.0, a.Dot(b).Approx, 10);
        Assert.Equal(-2.0, a.Determinant(b).Approx, 10);
        Assert.Equal(5.0, a.NormSquared.Approx, 10);

        var perpendicular = a.Perp;
        Assert.Equal(0.0, a.Dot(perpendicular).Approx, 10);
        Assert.Equal(5.0, a.SignedArea(perpendicular).Approx, 10);
    }

    [Fact]
    public void Vec2_Primal3_RoundTrip()
    {
        var p = Primal3<ExactScalar>.Fixed(3, 4);
        var v = p.ToVector().Value;
        Assert.Equal(3.0, v.X.Approx, 10);
        Assert.Equal(4.0, v.Y.Approx, 10);

        var back = v.ToPrimal3();
        Assert.Equal(StrictResult.StrictlyTrue, SubspaceAlgebra.SameSubspace(back, p).Value);
    }

    [Fact]
    public void ComplexVectorBridge_IsRealLinearOnly()
    {
        var z = new ExactComplex(S(3), S(4));
        var v = z.ToVector();
        Assert.Equal(3.0, v.X.Approx, 10);
        Assert.Equal(4.0, v.Y.Approx, 10);

        var back = v.ToComplex();
        Assert.Equal(3.0, back.Re.Approx, 10);
        Assert.Equal(4.0, back.Im.Approx, 10);
    }

    [Fact]
    public void LinearMap_IsInvertible_ChecksUnitsNotJustNonzero()
    {
        var identity = new LinearMap3<ExactScalar>(
            S(1), S(0), S(0),
            S(0), S(1), S(0),
            S(0), S(0), S(1));
        Assert.Equal(StrictVerdict.StrictlyTrue, identity.IsInvertible());

        var dual = new LinearMap3<DualNumber<ExactScalar>>(
            new DualNumber<ExactScalar>(S(0), S(2)), DualNumber<ExactScalar>.Zero, DualNumber<ExactScalar>.Zero,
            DualNumber<ExactScalar>.Zero, DualNumber<ExactScalar>.One, DualNumber<ExactScalar>.Zero,
            DualNumber<ExactScalar>.Zero, DualNumber<ExactScalar>.Zero, DualNumber<ExactScalar>.One);
        Assert.Equal(StrictVerdict.StrictlyFalse, dual.IsInvertible());

        var split = new LinearMap3<SplitComplex<ExactScalar>>(
            new SplitComplex<ExactScalar>(S(1), S(1)), SplitComplex<ExactScalar>.Zero, SplitComplex<ExactScalar>.Zero,
            SplitComplex<ExactScalar>.Zero, SplitComplex<ExactScalar>.One, SplitComplex<ExactScalar>.Zero,
            SplitComplex<ExactScalar>.Zero, SplitComplex<ExactScalar>.Zero, SplitComplex<ExactScalar>.One);
        Assert.Equal(StrictVerdict.StrictlyFalse, split.IsInvertible());
    }

    [Fact]
    public void HyperDual_NestedDual_ComputesSecondDerivative()
    {
        var x = new DualNumber<DualNumber<ExactScalar>>(
            new DualNumber<ExactScalar>(S(0), S(1)),
            new DualNumber<ExactScalar>(S(1), S(0)));

        var square = x * x;
        Assert.Equal(2.0, square.Eps.Eps.Approx, 10);
    }

    [Fact]
    public void ComplexCircularPoints_LieOnEveryCircle()
    {
        var i = new ExactComplex(S(0), S(1));
        var minusI = new ExactComplex(S(0), S(-1));
        var circularI = new Primal3<ExactComplex>(ExactComplex.One, i, ExactComplex.Zero);
        var circularJ = new Primal3<ExactComplex>(ExactComplex.One, minusI, ExactComplex.Zero);
        var circle = new BilinearForm3<ExactComplex>(
            ExactComplex.One, ExactComplex.Zero, ExactComplex.Zero,
            ExactComplex.One, ExactComplex.Zero, -ExactComplex.One);

        Assert.Equal(StrictResult.StrictlyTrue, circle.Eval(circularI).VerifyStrictZero());
        Assert.Equal(StrictResult.StrictlyTrue, circle.Eval(circularJ).VerifyStrictZero());
    }

    [Fact]
    public void FromLong_IsZAlgebraHomomorphism()
    {
        AssertFromLongLaws<ExactScalar>(ExactScalar.FromInt);
        AssertFromLongLaws<ExactComplex>(ExactComplex.FromLong);
        AssertFromLongLaws<DualNumber<ExactScalar>>(DualNumber<ExactScalar>.FromLong);
        AssertFromLongLaws<SplitComplex<ExactScalar>>(SplitComplex<ExactScalar>.FromLong);
        AssertFromLongLaws<PolyExpr>(PolyExpr.FromLong);
    }

    [Fact]
    public void RandomizedRingLaws_AreFalsificationChecks()
    {
        AssertRingLaws<ExactScalar>(r => ExactScalar.FromInt(r.Next(-10, 11)));
        AssertRingLaws<ExactComplex>(r => new ExactComplex(
            ExactScalar.FromInt(r.Next(-5, 6)),
            ExactScalar.FromInt(r.Next(-5, 6))));
        AssertRingLaws<DualNumber<ExactScalar>>(r => new DualNumber<ExactScalar>(
            ExactScalar.FromInt(r.Next(-5, 6)),
            ExactScalar.FromInt(r.Next(-5, 6))));
        AssertRingLaws<SplitComplex<ExactScalar>>(r => new SplitComplex<ExactScalar>(
            ExactScalar.FromInt(r.Next(-5, 6)),
            ExactScalar.FromInt(r.Next(-5, 6))));
        AssertRingLaws<PolyExpr>(NextPoly);
    }

    [Fact]
    public void RandomizedFromLongLaws_AreFalsificationChecks()
    {
        AssertFromLongRandom<ExactScalar>(ExactScalar.FromInt);
        AssertFromLongRandom<ExactComplex>(ExactComplex.FromLong);
        AssertFromLongRandom<DualNumber<ExactScalar>>(DualNumber<ExactScalar>.FromLong);
        AssertFromLongRandom<SplitComplex<ExactScalar>>(SplitComplex<ExactScalar>.FromLong);
        AssertFromLongRandom<PolyExpr>(PolyExpr.FromLong);
    }

    [Fact]
    public void IsUnit_IsProvenUnit_NotJustNonzero()
    {
        Assert.Equal(StrictVerdict.StrictlyFalse, UnitOf(S(0)));
        Assert.Equal(StrictVerdict.StrictlyTrue, UnitOf(S(2)));

        Assert.Equal(StrictVerdict.StrictlyFalse,
            UnitOf(new DualNumber<ExactScalar>(S(0), S(1))));
        Assert.Equal(StrictVerdict.StrictlyTrue,
            UnitOf(new DualNumber<ExactScalar>(S(2), S(0))));

        Assert.Equal(StrictVerdict.StrictlyFalse,
            UnitOf(new SplitComplex<ExactScalar>(S(1), S(1))));
        Assert.Equal(StrictVerdict.StrictlyTrue,
            UnitOf(new SplitComplex<ExactScalar>(S(2), S(0))));

        Assert.Equal(StrictVerdict.StrictlyTrue, UnitOf(PolyExpr.One));
        Assert.Equal(StrictVerdict.StrictlyFalse, UnitOf(PolyExpr.Var(0)));
    }

    [Fact]
    public void Primal3_IsUnimodular_IsConservative()
    {
        Assert.Equal(StrictVerdict.StrictlyTrue,
            Primal3<ExactScalar>.Fixed(1, 0).IsUnimodular());
        Assert.Equal(StrictVerdict.StrictlyFalse,
            Primal3<ExactScalar>.Fixed(0, 0, 0).IsUnimodular());

        Assert.Equal(StrictVerdict.Unknown,
            new Primal3<DualNumber<ExactScalar>>(
                new DualNumber<ExactScalar>(S(0), S(1)),
                DualNumber<ExactScalar>.Zero,
                DualNumber<ExactScalar>.Zero).IsUnimodular());
        Assert.Equal(StrictVerdict.StrictlyTrue,
            new Primal3<DualNumber<ExactScalar>>(
                new DualNumber<ExactScalar>(S(1), S(1)),
                DualNumber<ExactScalar>.Zero,
                DualNumber<ExactScalar>.Zero).IsUnimodular());

        Assert.Equal(StrictVerdict.StrictlyTrue,
            new Primal3<PolyExpr>(PolyExpr.One, PolyExpr.Zero, PolyExpr.Zero).IsUnimodular());
        Assert.Equal(StrictVerdict.Unknown,
            new Primal3<PolyExpr>(PolyExpr.Var(0), PolyExpr.Var(1), PolyExpr.Zero).IsUnimodular());
    }

    [Fact]
    public void IComputableField_TryInverse_IsSoundAndTriState()
    {
        Assert.Equal(StrictVerdict.StrictlyTrue, TryInverseOf(S(2), out var half));
        AssertZero(S(2) * half - S(1));

        Assert.Equal(StrictVerdict.StrictlyFalse, TryInverseOf(S(0), out _));

        var z = new ExactComplex(S(1), S(1));
        Assert.Equal(StrictVerdict.StrictlyTrue, TryInverseOf(z, out var zInv));
        AssertZero(z * zInv - ExactComplex.One);

        Assert.Equal(StrictVerdict.StrictlyFalse, TryInverseOf(ExactComplex.Zero, out _));
    }

    [Fact]
    public void SubspaceAlgebra_IsHarmonic_IsDivisionFreeOverIntegralDomain()
    {
        var a = Primal3<ExactScalar>.Fixed(0, 0);
        var b = Primal3<ExactScalar>.Fixed(4, 0);
        var c = Primal3<ExactScalar>.Fixed(1, 0);
        var d = Primal3<ExactScalar>.Fixed(-2, 0);

        Assert.Equal(StrictResult.StrictlyTrue, SubspaceAlgebra.IsHarmonic(a, b, c, d));
        Assert.Equal(StrictResult.StrictlyFalse,
            SubspaceAlgebra.IsHarmonic(a, b, c, Primal3<ExactScalar>.Fixed(2, 0)));
        Assert.Equal(StrictResult.StrictlyFalse,
            SubspaceAlgebra.IsHarmonic(a, b, c, Primal3<ExactScalar>.Fixed(0, 1)));

        var pa = Primal3<PolyExpr>.Fixed(0, 0);
        var pb = Primal3<PolyExpr>.Fixed(4, 0);
        var pc = Primal3<PolyExpr>.Fixed(1, 0);
        var pd = Primal3<PolyExpr>.Fixed(-2, 0);
        Assert.Equal(StrictResult.StrictlyTrue, SubspaceAlgebra.IsHarmonic(pa, pb, pc, pd));
    }

    [Fact]
    public void PolyExpr_ParabolaPerpendicularChord_PassesFixedPoint()
    {
        var p = PolyExpr.Var(0);
        var a = PolyExpr.Var(1);
        var b = PolyExpr.Var(2);
        var two = PolyExpr.FromLong(2);

        // y² = 2px 的参数化：P(t) = (2p t², 2p t, 1)
        var pa = new Primal3<PolyExpr>(two * p * a * a, two * p * a, PolyExpr.One);
        var pb = new Primal3<PolyExpr>(two * p * b * b, two * p * b, PolyExpr.One);
        var fixedPoint = new Primal3<PolyExpr>(two * p, PolyExpr.Zero, PolyExpr.One);

        var space = new UnifiedGeoSpace();
        space.RequireZero(a * b + PolyExpr.One); // OA ⟂ OB

        var incidence = SubspaceAlgebra.Pair(SubspaceAlgebra.Join(pa, pb), fixedPoint);
        var outcome = space.ProveZero(incidence);

        Assert.Equal(UnifiedProofStatus.Proved, outcome.Status);
    }

    [Fact]
    public void ExactScalar_EllipsePerpendicularChord_FactorsToFixedPoint()
    {
        var k = ExactScalar.FromSymbol("k");
        var m = ExactScalar.FromSymbol("m");
        var one = ExactScalar.One;
        var lc = one + 4 * k * k;
        var sum = -8 * k * m / lc;
        var product = 4 * (m * m - one) / lc;

        var perpendicular = (one + k * k) * product
                            + k * (m - one) * sum
                            + (m - one) * (m - one);
        var identity = perpendicular * lc - (m - one) * (5 * m + 3);

        Assert.Equal(StrictResult.StrictlyTrue, identity.VerifyStrictZero());

        var mFixed = ExactScalar.FromInt(-3) / ExactScalar.FromInt(5);
        var sumFixed = -8 * k * mFixed / lc;
        var productFixed = 4 * (mFixed * mFixed - one) / lc;
        var perpendicularAtFixed = (one + k * k) * productFixed
                                   + k * (mFixed - one) * sumFixed
                                   + (mFixed - one) * (mFixed - one);
        Assert.Equal(StrictResult.StrictlyTrue, perpendicularAtFixed.VerifyStrictZero());
    }

    [Fact]
    public void PolyExpr_EllipseFixedFamily_SatisfiesPointLineCondition()
    {
        var k = PolyExpr.Var(0);
        var x1 = PolyExpr.Var(1);
        var x2 = PolyExpr.Var(2);
        var one = PolyExpr.One;
        var half = PolyExpr.FromConst(ExactScalar.FromInt(1) / ExactScalar.FromInt(2));
        var m = half - k;
        var lc = one + 4 * k * k;

        var space = new UnifiedGeoSpace();
        space.RequireZero(lc * (x1 + x2) + 8 * k * m);
        space.RequireZero(lc * x1 * x2 - 4 * (m * m - one));

        var a = new Primal3<PolyExpr>(PolyExpr.FromLong(2), PolyExpr.Zero, one);
        var b = new Primal3<PolyExpr>(PolyExpr.Zero, one, one);
        var mm = new Primal3<PolyExpr>(x1, k * x1 + m, one);
        var nn = new Primal3<PolyExpr>(x2, k * x2 + m, one);
        var p = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(a, mm), SubspaceAlgebra.Join(b, nn));
        var target = new Dual3<PolyExpr>(one, PolyExpr.FromLong(2), -PolyExpr.FromLong(4));

        var outcome = space.ProveZero(SubspaceAlgebra.Pair(target, p));
        Assert.Equal(UnifiedProofStatus.Proved, outcome.Status);
    }

    [Fact]
    public void Symbolic_HarmonicCrossRatio_OnParabola()
    {
        var a = PolyExpr.Var(0);
        var p = PolyExpr.Var(1);
        var one = PolyExpr.One;
        var zero = PolyExpr.Zero;

        var u = new Primal3<PolyExpr>(zero, zero, one);
        var v = new Primal3<PolyExpr>(one, one, one);
        var aa = new Primal3<PolyExpr>(a * a, a, one);

        // U=P(0), V=P(1)，条件 t_U∩t_V∈AB 化为 2ab=a+b，取 b=a/(2a−1)。
        var den = 2 * a - one;
        var bb = new Primal3<PolyExpr>(a * a, a * den, den * den);
        var point = new Primal3<PolyExpr>(p * p, p, one);

        var uv = SubspaceAlgebra.Join(u, v);
        var x = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(point, aa), uv);
        var y = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(point, bb), uv);
        var o = new Primal3<PolyExpr>(one, zero, zero);

        var target = SubspaceAlgebra.Det(o, u, x) * SubspaceAlgebra.Det(o, v, y)
                     + SubspaceAlgebra.Det(o, u, y) * SubspaceAlgebra.Det(o, v, x);

        var outcome = new UnifiedGeoSpace().ProveZero(target);
        Assert.Equal(UnifiedProofStatus.Proved, outcome.Status);
    }

    [Fact]
    public void Symbolic_DegeneratePascal_OnParabola()
    {
        var m = PolyExpr.Var(0);
        var one = PolyExpr.One;
        var zero = PolyExpr.Zero;
        var two = PolyExpr.FromLong(2);

        var a = new Primal3<PolyExpr>(zero, zero, one);
        var b = new Primal3<PolyExpr>(one, one, one);
        var mm = new Primal3<PolyExpr>(m * m, m, one);
        var den = two * m - one;
        var nn = new Primal3<PolyExpr>(m * m, m * den, den * den);

        var ta = new Dual3<PolyExpr>(one, zero, zero);       // tangent at A=P(0)
        var tb = new Dual3<PolyExpr>(one, -two, one);        // tangent at B=P(1)

        var x1 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(a, mm), tb);
        var y1 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(b, mm), ta);
        var x2 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(a, nn), tb);
        var y2 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(b, nn), ta);

        var l1 = SubspaceAlgebra.Join(x1, y1);
        var l2 = SubspaceAlgebra.Join(x2, y2);
        var ab = SubspaceAlgebra.Join(a, b);
        var target = SubspaceAlgebra.Det(l1, l2, ab);

        var outcome = new UnifiedGeoSpace().ProveZero(target);
        Assert.Equal(UnifiedProofStatus.Proved, outcome.Status);
    }

    [Fact]
    public void Symbolic_DegenerateBrianchon_OnParabola()
    {
        var h = PolyExpr.Var(0);
        var one = PolyExpr.One;
        var zero = PolyExpr.Zero;
        var two = PolyExpr.FromLong(2);
        var four = PolyExpr.FromLong(4);

        var e = new Primal3<PolyExpr>(zero, zero, one);
        var f = new Primal3<PolyExpr>(one, one, one);
        var g = new Primal3<PolyExpr>(four, two, one);
        var hh = new Primal3<PolyExpr>(h * h, h, one);

        var te = new Dual3<PolyExpr>(one, zero, zero);
        var tf = new Dual3<PolyExpr>(one, -two, one);
        var tg = new Dual3<PolyExpr>(one, -four, four);
        var th = new Dual3<PolyExpr>(one, -two * h, h * h);

        var r = SubspaceAlgebra.Meet(te, tg);
        var s = SubspaceAlgebra.Meet(th, tf);
        var m = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(e, f), SubspaceAlgebra.Join(g, hh));
        var n = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(f, g), SubspaceAlgebra.Join(hh, e));

        var target1 = SubspaceAlgebra.Det(r, s, m);
        var target2 = SubspaceAlgebra.Det(r, s, n);

        var space = new UnifiedGeoSpace();
        Assert.Equal(UnifiedProofStatus.Proved, space.ProveZero(target1).Status);
        Assert.Equal(UnifiedProofStatus.Proved, space.ProveZero(target2).Status);
    }

    [Fact]
    public void ConicFrame_NormalizesThreeParameters()
    {
        var frame = ConicFrame.FromThreeParameters(S(2), S(3), S(5)).Value;

        Assert.Equal(StrictResult.StrictlyTrue,
            frame.ApplyParameter(S(2)).Value.VerifyStrictZero());
        Assert.Equal(StrictResult.StrictlyTrue,
            (frame.ApplyParameter(S(3)).Value - S(1)).VerifyStrictZero());

        var atInfinity = frame.ApplyParameter(S(5));
        Assert.True(atInfinity.IsFailure);
        Assert.Equal(GeometryError.AtInfinity, atInfinity.Error);

        var p2 = frame.ApplyPoint(Primal3<ExactScalar>.Fixed(4, 2)).Value;
        var p3 = frame.ApplyPoint(Primal3<ExactScalar>.Fixed(9, 3)).Value;
        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.SameSubspace(p2, Primal3<ExactScalar>.Fixed(0, 0)).Value);
        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.SameSubspace(p3, Primal3<ExactScalar>.Fixed(1, 1)).Value);

        var inverse = frame.Inverse().Value;
        var identity = frame.Compose(inverse).Value;
        var back = identity.ApplyParameter(S(7)).Value;
        Assert.Equal(StrictResult.StrictlyTrue, (back - S(7)).VerifyStrictZero());
    }

    [Fact]
    public void ConicFoci_ExtractsEllipseHyperbolaCircle()
    {
        var ellipse = new BilinearForm3<ExactScalar>(
            S(1) / S(4), S(0), S(0),
            S(1) / S(3), S(0), S(-1));
        var ef = ellipse.Foci().Value;
        Assert.Equal(ConicKind.Ellipse, ef.Kind);
        Assert.Equal(0.5, ef.Eccentricity.Approx, 10);
        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.SameSubspace(ef.Focus1, Primal3<ExactScalar>.Fixed(1, 0)).Value);
        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.SameSubspace(ef.Focus2, Primal3<ExactScalar>.Fixed(-1, 0)).Value);

        var hyperbola = new BilinearForm3<ExactScalar>(
            S(1), S(0), S(0),
            S(-1) / S(3), S(0), S(-1));
        var hf = hyperbola.Foci().Value;
        Assert.Equal(ConicKind.Hyperbola, hf.Kind);
        Assert.Equal(2.0, hf.Eccentricity.Approx, 10);
        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.SameSubspace(hf.Focus1, Primal3<ExactScalar>.Fixed(2, 0)).Value);

        var circle = new BilinearForm3<ExactScalar>(
            S(1), S(0), S(0),
            S(1), S(0), S(-1));
        var cf = circle.Foci().Value;
        Assert.Equal(ConicKind.Circle, cf.Kind);
        Assert.Equal(0.0, cf.Eccentricity.Approx, 10);

        var rotated = new BilinearForm3<ExactScalar>(
            S(1), S(1) / S(2), S(0),
            S(1), S(0), S(-1));
        Assert.True(rotated.Foci().IsFailure);
    }

    [Fact]
    public void ConicFoci_And_Directrices_Parabola()
    {
        // y²=4x
        var y2 = new BilinearForm3<ExactScalar>(
            S(0), S(0), S(-2),
            S(1), S(0), S(0));
        var yf = y2.Foci().Value;
        Assert.Equal(ConicKind.Parabola, yf.Kind);
        Assert.Equal(1.0, yf.Focus1.X.Approx, 10);
        Assert.Equal(0.0, yf.Focus1.Y.Approx, 10);

        var yd = y2.Directrices().Value;
        Assert.Single(yd);
        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.Pair(yd[0], Primal3<ExactScalar>.Fixed(-1, 0)).VerifyStrictZero());

        // x²=4y
        var x2 = new BilinearForm3<ExactScalar>(
            S(1), S(0), S(0),
            S(0), S(-2), S(0));
        var xf = x2.Foci().Value;
        Assert.Equal(0.0, xf.Focus1.X.Approx, 10);
        Assert.Equal(1.0, xf.Focus1.Y.Approx, 10);

        var xd = x2.Directrices().Value;
        Assert.Single(xd);
        Assert.Equal(StrictResult.StrictlyTrue,
            SubspaceAlgebra.Pair(xd[0], Primal3<ExactScalar>.Fixed(0, -1)).VerifyStrictZero());

        // 椭圆 x²/4+y²/3=1 的准线 x=±4
        var ellipse = new BilinearForm3<ExactScalar>(
            S(1) / S(4), S(0), S(0),
            S(1) / S(3), S(0), S(-1));
        var ed = ellipse.Directrices().Value;
        Assert.Equal(2, ed.Count);
        Assert.True(
            SubspaceAlgebra.Pair(ed[0], Primal3<ExactScalar>.Fixed(4, 0)).VerifyStrictZero()
                == StrictResult.StrictlyTrue
            || SubspaceAlgebra.Pair(ed[0], Primal3<ExactScalar>.Fixed(-4, 0)).VerifyStrictZero()
                == StrictResult.StrictlyTrue);
        Assert.True(
            SubspaceAlgebra.Pair(ed[1], Primal3<ExactScalar>.Fixed(4, 0)).VerifyStrictZero()
                == StrictResult.StrictlyTrue
            || SubspaceAlgebra.Pair(ed[1], Primal3<ExactScalar>.Fixed(-4, 0)).VerifyStrictZero()
                == StrictResult.StrictlyTrue);
    }

    [Fact]
    public void Result_DefaultIsFailure()
    {
        var failed = default(Result<int, GeometryError>);
        Assert.False(failed.IsOk);
        Assert.Equal(GeometryError.Unknown, failed.Error);

        var ok = Result<int, GeometryError>.Ok(42);
        Assert.True(ok.IsOk);
        Assert.Equal(42, ok.Value);
    }
}
