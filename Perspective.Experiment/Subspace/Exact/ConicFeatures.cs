using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

/// <summary>
///     轴对齐椭圆/双曲线的焦点、中心、离心率。
///     只处理 Q01=0 的 conic；旋转 conic 返回失败。
/// </summary>
public readonly record struct ConicFoci(
    ConicKind Kind,
    Primal3<ExactScalar> Center,
    Primal3<ExactScalar> Focus1,
    Primal3<ExactScalar> Focus2,
    ExactScalar Eccentricity);

public static class ConicFeatures
{
    public static Result<ConicFoci, GeometryError> Foci(this BilinearForm3<ExactScalar> q)
    {
        var zero = ExactScalar.Zero;
        if (q.Q01.VerifyStrictZero() != StrictResult.StrictlyTrue)
            return Result<ConicFoci, GeometryError>.Fail(GeometryError.UndefinedParameter);

        // 抛物线：Q00=0（水平轴）或 Q11=0（竖直轴）。
        if (q.Q00.VerifyStrictZero() == StrictResult.StrictlyTrue
            && q.Q11.VerifyStrictZero() != StrictResult.StrictlyTrue)
            return ParabolaFoci(q, horizontalAxis: true);
        if (q.Q11.VerifyStrictZero() == StrictResult.StrictlyTrue
            && q.Q00.VerifyStrictZero() != StrictResult.StrictlyTrue)
            return ParabolaFoci(q, horizontalAxis: false);

        if (q.Q00.VerifyStrictZero() == StrictResult.StrictlyTrue
            || q.Q11.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<ConicFoci, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var h = -q.Q02 / q.Q00;
        var k = -q.Q12 / q.Q11;
        var center = new Primal3<ExactScalar>(h, k, ExactScalar.One);
        var constant = -q.Eval(center);
        if (Sign(constant) == 0)
            return Result<ConicFoci, GeometryError>.Fail(GeometryError.DegenerateLine);

        var a = q.Q00;
        var b = q.Q11;
        var sa = Sign(a);
        var sb = Sign(b);
        var sc = Sign(constant);

        var isCircle = ExactScalar.AreStrictlyEqual(a, b) == StrictResult.StrictlyTrue;
        if (sa == sb)
        {
            var a2 = constant / a;
            var b2 = constant / b;
            if (a2.Approx <= 0 || b2.Approx <= 0)
                return Result<ConicFoci, GeometryError>.Fail(GeometryError.UndefinedParameter);

            if (isCircle)
            {
                return Result<ConicFoci, GeometryError>.Ok(new ConicFoci(
                    ConicKind.Circle, center, center, center, ExactScalar.Zero));
            }

            var majorY = b2.Approx > a2.Approx;
            var major2 = majorY ? b2 : a2;
            var minor2 = majorY ? a2 : b2;
            var c2 = major2 - minor2;
            if (c2.Approx < 0)
                return Result<ConicFoci, GeometryError>.Fail(GeometryError.UndefinedParameter);

            var c = ExactScalar.Sqrt(c2);
            var major = ExactScalar.Sqrt(major2);
            if (major.VerifyStrictZero() == StrictResult.StrictlyTrue)
                return Result<ConicFoci, GeometryError>.Fail(GeometryError.UndefinedParameter);

            var f1 = majorY
                ? new Primal3<ExactScalar>(h, k + c, ExactScalar.One)
                : new Primal3<ExactScalar>(h + c, k, ExactScalar.One);
            var f2 = majorY
                ? new Primal3<ExactScalar>(h, k - c, ExactScalar.One)
                : new Primal3<ExactScalar>(h - c, k, ExactScalar.One);

            return Result<ConicFoci, GeometryError>.Ok(new ConicFoci(
                ConicKind.Ellipse, center, f1, f2, c / major));
        }

        // 双曲线：常数项/系数同号的那一轴是实轴。
        if (sc != sa && sc != sb)
            return Result<ConicFoci, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var transverseX = sc == sa;
        var ta2 = constant / (transverseX ? a : b);
        var other2 = -constant / (transverseX ? b : a);
        if (ta2.Approx <= 0 || other2.Approx <= 0)
            return Result<ConicFoci, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var tc2 = ta2 + other2;
        var tc = ExactScalar.Sqrt(tc2);
        var ta = ExactScalar.Sqrt(ta2);
        if (ta.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<ConicFoci, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var h1 = transverseX
            ? new Primal3<ExactScalar>(h + tc, k, ExactScalar.One)
            : new Primal3<ExactScalar>(h, k + tc, ExactScalar.One);
        var h2 = transverseX
            ? new Primal3<ExactScalar>(h - tc, k, ExactScalar.One)
            : new Primal3<ExactScalar>(h, k - tc, ExactScalar.One);

        return Result<ConicFoci, GeometryError>.Ok(new ConicFoci(
            ConicKind.Hyperbola, center, h1, h2, tc / ta));
    }

    /// <summary>椭圆/双曲线两条准线；抛物线一条；圆没有有限准线。</summary>
    public static Result<IReadOnlyList<Dual3<ExactScalar>>, GeometryError> Directrices(
        this BilinearForm3<ExactScalar> q)
    {
        var fociResult = q.Foci();
        if (!fociResult.TryGet(out var data))
            return Result<IReadOnlyList<Dual3<ExactScalar>>, GeometryError>.Fail(fociResult.Error);

        if (data.Kind == ConicKind.Circle)
            return Result<IReadOnlyList<Dual3<ExactScalar>>, GeometryError>.Ok([]);

        if (data.Kind == ConicKind.Parabola)
        {
            var dx = data.Focus1.X - data.Center.X;
            var dy = data.Focus1.Y - data.Center.Y;
            var line = Math.Abs(dx.Approx) >= Math.Abs(dy.Approx)
                ? new Dual3<ExactScalar>(ExactScalar.One, ExactScalar.Zero,
                    -(data.Center.X - dx))
                : new Dual3<ExactScalar>(ExactScalar.Zero, ExactScalar.One,
                    -(data.Center.Y - dy));
            return Result<IReadOnlyList<Dual3<ExactScalar>>, GeometryError>.Ok([line]);
        }

        var e = data.Eccentricity;
        if (e.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<IReadOnlyList<Dual3<ExactScalar>>, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var fx = data.Focus1.X - data.Center.X;
        var fy = data.Focus1.Y - data.Center.Y;
        var c = ExactScalar.Sqrt(fx * fx + fy * fy);
        var distance = c / (e * e);

        var lines = Math.Abs(fx.Approx) >= Math.Abs(fy.Approx)
            ? new[]
            {
                new Dual3<ExactScalar>(ExactScalar.One, ExactScalar.Zero, -(data.Center.X + distance)),
                new Dual3<ExactScalar>(ExactScalar.One, ExactScalar.Zero, -(data.Center.X - distance))
            }
            : new[]
            {
                new Dual3<ExactScalar>(ExactScalar.Zero, ExactScalar.One, -(data.Center.Y + distance)),
                new Dual3<ExactScalar>(ExactScalar.Zero, ExactScalar.One, -(data.Center.Y - distance))
            };

        return Result<IReadOnlyList<Dual3<ExactScalar>>, GeometryError>.Ok(lines);
    }

    private static Result<ConicFoci, GeometryError> ParabolaFoci(
        BilinearForm3<ExactScalar> q,
        bool horizontalAxis)
    {
        if (horizontalAxis)
        {
            // A y² + 2B x + 2C y + D = 0
            var a = q.Q11;
            var b = q.Q02;
            var cc = q.Q12;
            var d = q.Q22;
            if (b.VerifyStrictZero() == StrictResult.StrictlyTrue)
                return Result<ConicFoci, GeometryError>.Fail(GeometryError.UndefinedParameter);

            var k = -cc / a;
            var h = (cc * cc - a * d) / (ExactScalar.FromInt(2) * a * b);
            var p = -b / (ExactScalar.FromInt(2) * a);
            var vertex = new Primal3<ExactScalar>(h, k, ExactScalar.One);
            var focus = new Primal3<ExactScalar>(h + p, k, ExactScalar.One);
            return Result<ConicFoci, GeometryError>.Ok(new ConicFoci(
                ConicKind.Parabola, vertex, focus, focus, ExactScalar.One));
        }

        // A x² + 2B x + 2C y + D = 0
        var aa = q.Q00;
        var bb = q.Q02;
        var c2 = q.Q12;
        var dd = q.Q22;
        if (c2.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<ConicFoci, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var hh = -bb / aa;
        var kk = (bb * bb - aa * dd) / (ExactScalar.FromInt(2) * aa * c2);
        var pp = -c2 / (ExactScalar.FromInt(2) * aa);
        var vertex2 = new Primal3<ExactScalar>(hh, kk, ExactScalar.One);
        var focus2 = new Primal3<ExactScalar>(hh, kk + pp, ExactScalar.One);
        return Result<ConicFoci, GeometryError>.Ok(new ConicFoci(
            ConicKind.Parabola, vertex2, focus2, focus2, ExactScalar.One));
    }

    private static int Sign(ExactScalar x)
    {
        if (x.VerifyStrictZero() == StrictResult.StrictlyTrue) return 0;
        return x.Approx >= 0 ? 1 : -1;
    }
}
