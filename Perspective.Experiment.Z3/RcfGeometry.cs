using Perspective.Oracle.Z3;

namespace Perspective.Experiment.Z3;

public sealed class RcfPoint
{
    public RcfPoint(RcfNum x, RcfNum y, RcfNum w)
    {
        X = x;
        Y = y;
        W = w;
    }

    public RcfNum X { get; }
    public RcfNum Y { get; }
    public RcfNum W { get; }
}

public sealed class RcfLine
{
    public RcfLine(RcfNum a, RcfNum b, RcfNum c)
    {
        A = a;
        B = b;
        C = c;
    }

    public RcfNum A { get; }
    public RcfNum B { get; }
    public RcfNum C { get; }
}

public sealed class RcfConic
{
    public RcfConic(
        RcfNum q00, RcfNum q01, RcfNum q02,
        RcfNum q11, RcfNum q12, RcfNum q22)
    {
        Q00 = q00;
        Q01 = q01;
        Q02 = q02;
        Q11 = q11;
        Q12 = q12;
        Q22 = q22;
    }

    public RcfNum Q00 { get; }
    public RcfNum Q01 { get; }
    public RcfNum Q02 { get; }
    public RcfNum Q11 { get; }
    public RcfNum Q12 { get; }
    public RcfNum Q22 { get; }
}

/// <summary>RCF 上的点/线/conic 与“直线 ∩ conic”最小层。</summary>
public static class RcfGeometry
{
    public static RcfPoint Point(Z3RcfContext ctx, int x, int y, int w = 1) =>
        new(ctx.FromInt(x), ctx.FromInt(y), ctx.FromInt(w));

    public static RcfLine Line(Z3RcfContext ctx, int a, int b, int c) =>
        new(ctx.FromInt(a), ctx.FromInt(b), ctx.FromInt(c));

    public static RcfConic UnitCircle(Z3RcfContext ctx) =>
        new(ctx.FromInt(1), ctx.FromInt(0), ctx.FromInt(0),
            ctx.FromInt(1), ctx.FromInt(0), ctx.FromInt(-1));

    /// <summary>以有限点 center 为圆心、radiusSquared 为半径² 的圆。</summary>
    public static RcfConic Circle(RcfPoint center, RcfNum radiusSquared)
    {
        var cx = center.X / center.W;
        var cy = center.Y / center.W;
        var ctx = center.X.Context;
        return new RcfConic(
            ctx.FromInt(1), ctx.FromInt(0), -cx,
            ctx.FromInt(1), -cy,
            cx * cx + cy * cy - radiusSquared);
    }

    public static RcfLine Join(RcfPoint a, RcfPoint b) =>
        new(
            a.Y * b.W - a.W * b.Y,
            a.W * b.X - a.X * b.W,
            a.X * b.Y - a.Y * b.X);

    public static RcfPoint Meet(RcfLine l1, RcfLine l2) =>
        new(
            l1.B * l2.C - l1.C * l2.B,
            l1.C * l2.A - l1.A * l2.C,
            l1.A * l2.B - l1.B * l2.A);

    public static RcfNum Pair(RcfLine line, RcfPoint point) =>
        line.A * point.X + line.B * point.Y + line.C * point.W;

    public static RcfNum Eval(RcfConic conic, RcfPoint point)
    {
        var two = point.X.Context.FromInt(2);
        var xx = point.X * point.X;
        var yy = point.Y * point.Y;
        var ww = point.W * point.W;

        return conic.Q00 * xx
               + two * conic.Q01 * point.X * point.Y
               + conic.Q11 * yy
               + two * conic.Q02 * point.X * point.W
               + two * conic.Q12 * point.Y * point.W
               + conic.Q22 * ww;
    }

    public static RcfLine Polar(RcfConic conic, RcfPoint point) =>
        new(
            conic.Q00 * point.X + conic.Q01 * point.Y + conic.Q02 * point.W,
            conic.Q01 * point.X + conic.Q11 * point.Y + conic.Q12 * point.W,
            conic.Q02 * point.X + conic.Q12 * point.Y + conic.Q22 * point.W);

    public static IReadOnlyList<RcfPoint> Intersect(RcfLine line, RcfConic conic)
    {
        var ctx = ContextOf(line.A);
        var zero = ctx.FromInt(0);
        if (line.A.Sign() == 0 && line.B.Sign() == 0)
            return [];

        var u = new RcfPoint(-line.B, line.A, zero);
        var v = new RcfPoint(
            -line.A * line.C,
            -line.B * line.C,
            line.A * line.A + line.B * line.B);

        var a = Eval(conic, u);
        var b = Pair(Polar(conic, u), v);
        var c = Eval(conic, v);

        if (a.Sign() == 0)
        {
            if (b.Sign() == 0)
                return [];

            var other = new RcfPoint(
                c * u.X - ctx.FromInt(2) * b * v.X,
                c * u.Y - ctx.FromInt(2) * b * v.Y,
                c * u.W - ctx.FromInt(2) * b * v.W);
            return [u, other];
        }

        var disc = b * b - a * c;
        var sign = disc.Sign();
        if (sign < 0) return [];

        if (sign == 0)
        {
            var touch = new RcfPoint(
                a * v.X - b * u.X,
                a * v.Y - b * u.Y,
                a * v.W - b * u.W);
            return [touch];
        }

        var sqrt = disc.Sqrt();
        var p1 = new RcfPoint(
            (-b + sqrt) * u.X + a * v.X,
            (-b + sqrt) * u.Y + a * v.Y,
            (-b + sqrt) * u.W + a * v.W);
        var p2 = new RcfPoint(
            (-b - sqrt) * u.X + a * v.X,
            (-b - sqrt) * u.Y + a * v.Y,
            (-b - sqrt) * u.W + a * v.W);
        return [p1, p2];
    }

    private static Z3RcfContext ContextOf(RcfNum value) =>
        value.Context;
}
