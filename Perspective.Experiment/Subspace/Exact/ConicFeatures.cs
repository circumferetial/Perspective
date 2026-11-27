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

    private static int Sign(ExactScalar x)
    {
        if (x.VerifyStrictZero() == StrictResult.StrictlyTrue) return 0;
        return x.Approx >= 0 ? 1 : -1;
    }
}
