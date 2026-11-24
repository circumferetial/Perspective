using AngouriMath;
using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// P0 守卫回归：退化极线、退化角、重合点共线、负/无穷远线段比。
/// </summary>
public class RuleGuardsTests
{
    private static ExactScalar F(string s) => ExactScalar.FromFormula(s);
    private static ExactScalar I(int v) => ExactScalar.FromInt(v);

    private static Conic DegeneratePairOfLines() =>
        // x·y = 0：两条相交直线，奇点在原点
        new(new Matrix3x3(I(0), F("1/2"), I(0), F("1/2"), I(0), I(0), I(0), I(0), I(0)));

    [Fact]
    public void AreAngleEqual_DegenerateLines_AreRejected()
    {
        var lInf = new HLine(0, 0, 1);        // 无穷远线：A=B=0
        var zero = new HLine(0, 0, 0);
        var scaled = new HLine(0, 0, 5);      // 仍是 A=B=0
        var lx = new HLine(1, 0, 0);
        var ly = new HLine(0, 1, 0);

        Assert.Equal(StrictResult.StrictlyFalse, GeometryAudit.AreAngleEqual(lInf, lx, ly, ly, 1));
        Assert.Equal(StrictResult.StrictlyFalse, GeometryAudit.AreAngleEqual(lx, ly, zero, lx, 1));
        Assert.Equal(StrictResult.StrictlyFalse, GeometryAudit.AreAngleEqual(scaled, lx, ly, lx, 1));
    }

    [Fact]
    public void AreAngleEqual_RightAngle_BothSignsAreTrue_ExpectedBehavior()
    {
        var lx = new HLine(1, 0, 0);
        var ly = new HLine(0, 1, 0);
        var l1 = new HLine(1, 1, 0);
        var l2 = new HLine(1, -1, 0);

        // 直角时 sign=+1/-1 都满足（tan 奇函数在 dot=0 处退化）；锁定当前语义，防止未来无意改变
        Assert.Equal(StrictResult.StrictlyTrue, GeometryAudit.AreAngleEqual(lx, ly, l1, l2, 1));
        Assert.Equal(StrictResult.StrictlyTrue, GeometryAudit.AreAngleEqual(lx, ly, l1, l2, -1));
    }

    [Fact]
    public void CollinearFromCommonLine_RejectsCoincidentCoordinates()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddLine("L", new HLine(1, -1, 0));   // x − y = 0
        facts.AddPoint("P1", HPoint.FromCartesian(0, 0));
        facts.AddPoint("P2", HPoint.FromCartesian(0, 0));   // 不同 id、相同坐标
        facts.AddPoint("Q", HPoint.FromCartesian(1, 1));
        facts.AddFact(new OnLineFact("P1", "L"), "测试");
        facts.AddFact(new OnLineFact("P2", "L"), "测试");
        facts.AddFact(new OnLineFact("Q", "L"), "测试");

        var derived = CollinearFromCommonLineRule.Apply(facts);

        Assert.Equal(0, derived);
        Assert.Empty(facts.Facts.OfType<CollinearFact>());
    }

    [Fact]
    public void Collinear_PurePredicate_StillAcceptsCoincidentPoints()
    {
        // 纯代数判据保持"行列式为零"语义；互异性守卫由规则层负责，API 语义不被悄悄改变
        var p = HPoint.FromCartesian(1, 2);
        var q = HPoint.FromCartesian(3, 4);
        Assert.Equal(StrictResult.StrictlyTrue, PointTools.Collinear(p, p, q));
    }

    [Fact]
    public void IsSegmentRatio_RejectsNegativeConstant()
    {
        var a1 = HPoint.FromCartesian(0, 0);
        var a2 = HPoint.FromCartesian(1, 0);
        var b1 = HPoint.FromCartesian(0, 0);
        var b2 = HPoint.FromCartesian(2, 0);

        Assert.Equal(StrictResult.StrictlyFalse, GeometryAudit.IsSegmentRatio(a1, a2, b1, b2, F("-1/2")));
        Assert.Equal(StrictResult.StrictlyTrue, GeometryAudit.IsSegmentRatio(a1, a2, b1, b2, F("1/2")));
    }

    [Fact]
    public void IsSegmentRatio_RejectsPointsAtInfinity_WithoutThrowing()
    {
        var finite = HPoint.FromCartesian(0, 0);
        var infinity = new HPoint(1, 0, 0);

        var result = GeometryAudit.IsSegmentRatio(finite, infinity, finite, finite, I(1));
        Assert.Equal(StrictResult.StrictlyFalse, result);
    }

    [Fact]
    public void IsSegmentRatio_DoesNotKillSymbolicSign()
    {
        // 线段长度² = t²，value = −t：平方判据恒成立（t² = (−t)²），但 value 的符号未定且探针为负。
        // 正确行为：符号守卫不误杀，判据应给出 StrictlyTrue（而不是因 Approx<0 直接拒绝）。
        Entity t = MathS.Var("t");
        var ts = new ExactScalar(2.0, t);

        var a1 = HPoint.FromCartesian(ts, I(0));
        var a2 = HPoint.FromCartesian(I(0), I(0));
        var b1 = HPoint.FromCartesian(I(0), I(0));
        var b2 = HPoint.FromCartesian(I(1), I(0));

        var value = I(0) - ts;   // −t，探针值为 −2（负数）
        var result = GeometryAudit.IsSegmentRatio(a1, a2, b1, b2, value);

        Assert.NotEqual(StrictResult.StrictlyFalse, result);
        Assert.Equal(StrictResult.StrictlyTrue, result);
    }

    [Fact]
    public void IsProperPolarLine_RejectsDegenerateConicAndZeroPolar()
    {
        var degenerate = DegeneratePairOfLines();
        var origin = HPoint.FromCartesian(0, 0);
        var zeroLine = new HLine(0, 0, 0);

        Assert.Equal(StrictResult.StrictlyFalse,
            ConicTools.IsProperPolarLine(degenerate, origin, zeroLine));
        // 定义性判据本身保持原语义（供对照/测试用）
        Assert.Equal(StrictResult.StrictlyTrue,
            ConicTools.IsPolarLine(degenerate, origin, zeroLine));

        var circle = Conic.Circle(0, 0, I(2));
        var p = HPoint.FromCartesian(0, 3);
        Assert.Equal(StrictResult.StrictlyTrue,
            ConicTools.IsProperPolarLine(circle, p, circle.PolarLine(p)));
    }

    private static FactBase EllipseMidpointScene(Matrix3x3 transform)
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Affine };
        var ellipse = Conic.Ellipse(F("2"), I(1));          // x²/4 + y² = 1（轴对齐）
        var conic = transform.Transform(ellipse);

        var a = transform * HPoint.FromCartesian(2, 0);
        var b = transform * HPoint.FromCartesian(0, 1);
        var m = transform * HPoint.FromCartesian(1, F("1/2"));
        var o = transform * HPoint.FromCartesian(0, 0);

        facts.AddConic("C", conic);
        facts.AddPoint("A", a);
        facts.AddPoint("B", b);
        facts.AddPoint("M", m);
        facts.AddPoint("O", o);
        facts.AddFact(new CenterOfConicFact("O", "C"), "测试");
        facts.AddFact(new MidpointFact("M", "A", "B"), "测试");
        facts.AddFact(new OnConicFact("A", "C"), "测试");
        facts.AddFact(new OnConicFact("B", "C"), "测试");
        return facts;
    }

    [Fact]
    public void SlopeProduct_IsOnlyEmittedInAxisAlignedFrame()
    {
        // 轴对齐：斜率积显示性推论生成
        var aligned = EllipseMidpointScene(Matrix3x3.Identity);
        ConicMidpointSlopeRule.Apply(aligned);
        Assert.NotEmpty(aligned.SlopeProducts);

        // 旋转 30°：斜率积定值不再生成（M01≠0）
        var rotation = Matrix3x3.Rotation(F("sqrt(3)/2"), F("1/2"));
        var rotated = EllipseMidpointScene(rotation);
        ConicMidpointSlopeRule.Apply(rotated);
        Assert.Empty(rotated.SlopeProducts);
    }

    [Fact]
    public void PolarFundamentalRule_DoesNotProduceTangentFactForDegenerateConic()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        var degenerate = DegeneratePairOfLines();
        var origin = HPoint.FromCartesian(0, 0);
        facts.AddConic("C", degenerate);
        facts.AddPoint("O", origin);
        facts.AddLine("Z", new HLine(0, 0, 0));
        facts.AddFact(new OnConicFact("O", "C"), "奇点在退化曲线上");
        facts.AddFact(new PolarOfFact("Z", "O", "C"), "测试");

        var derived = PolarFundamentalRule.Apply(facts);

        Assert.Equal(0, derived);
        Assert.Empty(facts.Tangents);
    }
}
