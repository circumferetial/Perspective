using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// 共享场景构造器：把此前散落在临时脚本里的"脚手架"集中到仓库内。
/// </summary>
internal static class Scenes
{
    /// <summary>单位圆 x²+y²=1。</summary>
    public static Conic UnitCircle => Conic.FromGeneralEquation(1, 0, 1, 0, 0, -1);

    /// <summary>圆 x²+y²=4。</summary>
    public static Conic Circle4 => Conic.FromGeneralEquation(1, 0, 1, 0, 0, -4);

    /// <summary>椭圆 x²/4+y²/3=1（矩阵形式 3x²+4y²=12）。</summary>
    public static Conic Ellipse4x3 => Conic.FromGeneralEquation(3, 0, 4, 0, 0, -12);

    /// <summary>旋转椭圆 x²+xy+y²=1。</summary>
    public static Conic RotatedEllipse => Conic.FromGeneralEquation(1, 1, 1, 0, 0, -1);

    /// <summary>命题裁决演示场景（椭圆 + 定点 P + 切线 + 割线 + 中点）。</summary>
    public static FactBase DemoScene()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        var ellipse = Conic.Ellipse(2, ExactScalar.FromFormula("sqrt(3)"));
        facts.AddConic("E", ellipse);
        facts.AddPoint("O", HPoint.FromCartesian(0, 0));
        facts.AddFact(new CenterOfConicFact("O", "E"), "初始");

        var p = HPoint.FromCartesian(0, 3);
        facts.AddPoint("P", p);
        facts.AddLine("L_P", ellipse.PolarLine(p));
        facts.AddFact(new PolarOfFact("L_P", "P", "E"), "初始");

        var q = HPoint.FromCartesian(2, 1);
        var a = HPoint.FromFormula("1", "3/2");
        var b = HPoint.FromCartesian(2, 0);
        facts.AddPoint("Q", q);
        facts.AddPoint("A", a);
        facts.AddPoint("B", b);
        facts.AddFact(new OnLineFact("Q", "L_P"), "初始");
        facts.AddFact(new OnConicFact("A", "E"), "初始");
        facts.AddFact(new OnConicFact("B", "E"), "初始");

        facts.AddLine("T1", ellipse.PolarLine(a));
        facts.AddLine("T2", ellipse.PolarLine(b));
        facts.AddFact(new TangentFact("T1", "E", "A"), "初始");
        facts.AddFact(new TangentFact("T2", "E", "B"), "初始");
        facts.AddFact(new OnLineFact("Q", "T1"), "初始");
        facts.AddFact(new OnLineFact("Q", "T2"), "初始");

        var sec = new HLine(1, -1, -1); // y = x - 1
        var mn = ellipse.Intersect(sec);
        var lAB = HLine.FromPoints(a, b);
        var r = HLine.Intersect(sec, lAB);
        facts.AddLine("L_sec", sec);
        facts.AddPoint("M", mn[0]);
        facts.AddPoint("N", mn[1]);
        facts.AddPoint("R", r);
        facts.AddLine("L_AB", lAB);
        foreach (var (pid, lid) in new[]
                 {
                     ("A", "L_AB"), ("B", "L_AB"), ("Q", "L_sec"),
                     ("M", "L_sec"), ("N", "L_sec"), ("R", "L_sec"), ("R", "L_AB")
                 })
            facts.AddFact(new OnLineFact(pid, lid), "初始");
        facts.AddFact(new OnConicFact("M", "E"), "初始");
        facts.AddFact(new OnConicFact("N", "E"), "初始");

        facts.AddPoint("Mid", HPoint.FromFormula("3/2", "3/4"));
        facts.AddFact(new MidpointFact("Mid", "A", "B"), "初始");
        return facts;
    }

    /// <summary>单位圆上 6 个精确有理点（Pascal 用）。</summary>
    public static (string Id, HPoint P)[] SixConicPoints =>
    [
        ("A", HPoint.FromCartesian(1, 0)),
        ("B", HPoint.FromCartesian(0, 1)),
        ("C", HPoint.FromCartesian(-1, 0)),
        ("D", HPoint.FromCartesian(0, -1)),
        ("E", HPoint.FromFormula("3/5", "4/5")),
        ("F", HPoint.FromFormula("-4/5", "3/5")),
    ];

    /// <summary>单位圆上 6 条精确切线（Brianchon 用）。</summary>
    public static (string Id, HPoint Touch, HLine Line)[] SixTangents =>
    [
        ("P1", HPoint.FromCartesian(1, 0), new HLine(1, 0, -1)),
        ("P2", HPoint.FromCartesian(0, 1), new HLine(0, 1, -1)),
        ("P3", HPoint.FromCartesian(-1, 0), new HLine(1, 0, 1)),
        ("P4", HPoint.FromCartesian(0, -1), new HLine(0, 1, 1)),
        ("P5", HPoint.FromFormula("3/5", "4/5"), new HLine(3, 4, -5)),
        ("P6", HPoint.FromFormula("4/5", "-3/5"), new HLine(4, -3, -5)),
    ];

    /// <summary>一般位置四点形 A(0,0) B(1,0) C(2,1) D(0,2)。</summary>
    public static FactBase QuadrangleScene()
    {
        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddPoint("A", HPoint.FromCartesian(0, 0));
        facts.AddPoint("B", HPoint.FromCartesian(1, 0));
        facts.AddPoint("C", HPoint.FromCartesian(2, 1));
        facts.AddPoint("D", HPoint.FromCartesian(0, 2));
        return facts;
    }
}
