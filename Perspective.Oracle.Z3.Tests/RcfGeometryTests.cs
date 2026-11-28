using Perspective.Experiment.Z3;
using Perspective.Oracle.Z3;
using Xunit;

namespace Perspective.Oracle.Z3.Tests;

public sealed class RcfGeometryTests
{
    [Fact]
    public void UnitCircle_Line_ThenConstructCircleFromPoint()
    {
        using var ctx = new Z3RcfContext();
        var circle = RcfGeometry.UnitCircle(ctx);
        var line = RcfGeometry.Line(ctx, 0, 2, -1); // y=1/2

        var points = RcfGeometry.Intersect(line, circle);
        Assert.Equal(2, points.Count);

        foreach (var point in points)
        {
            Assert.Equal(0, RcfGeometry.Eval(circle, point).Sign());
            Assert.Equal(0, RcfGeometry.Pair(line, point).Sign());
        }

        // 用其中一个交点作圆心，构造新圆。
        var constructed = RcfGeometry.Circle(points[0], ctx.FromInt(1));
        Assert.Equal(-1, RcfGeometry.Eval(constructed, points[0]).Sign());

        var chord = RcfGeometry.Join(points[0], points[1]);
        Assert.Equal(0, RcfGeometry.Pair(chord, points[0]).Sign());
        Assert.Equal(0, RcfGeometry.Pair(chord, points[1]).Sign());
    }
}
