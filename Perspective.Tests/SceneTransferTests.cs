using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// M5：视角门控的定理搬运——把场景按变换搬走，逐条严通道回验事实；
/// 非不变关系（如射影变换下的中点）会被丢弃并记录在报告里。
/// </summary>
public class SceneTransferTests
{
    private static FactBase LaHireScene(TransformLevel level = TransformLevel.Projective)
    {
        var circle = Conic.Circle(0, 0, ExactScalar.FromInt(2));
        var facts = new FactBase { CurrentLevel = level };
        facts.AddConic("C", circle);

        var p = HPoint.FromCartesian(0, 3);
        var q = HPoint.FromCartesian(1, ExactScalar.FromFormula("4/3"));   // 在 P 的极线 y=4/3 上
        facts.AddPoint("P", p);
        facts.AddPoint("Q", q);
        facts.AddLine("LP", circle.PolarLine(p));
        facts.AddLine("LQ", circle.PolarLine(q));
        facts.AddFact(new PolarOfFact("LP", "P", "C"), "初始");
        facts.AddFact(new PolarOfFact("LQ", "Q", "C"), "初始");
        facts.AddFact(new OnLineFact("Q", "LP"), "初始");

        // 再加一条"中点"事实（仿射不变量），用于观察射影搬运时被丢弃
        var a = HPoint.FromCartesian(0, 0);
        var b = HPoint.FromCartesian(2, 0);
        facts.AddPoint("A", a);
        facts.AddPoint("B", b);
        facts.AddPoint("M", HPoint.FromCartesian(1, 0));
        facts.AddFact(new MidpointFact("M", "A", "B"), "初始");
        return facts;
    }

    [Fact]
    public void SimilarityTransfer_KeepsAllFacts()
    {
        var source = LaHireScene(TransformLevel.Similarity);
        var similarity = Matrix3x3.Rotation(ExactScalar.FromFormula("sqrt(2)/2"), ExactScalar.FromFormula("sqrt(2)/2"))
                       * Matrix3x3.Scale(ExactScalar.FromInt(2), ExactScalar.FromInt(2));

        var (transformed, report) = SceneTransfer.Apply(source, similarity);

        Assert.Equal(TransformLevel.Similarity, report.TargetLevel);
        Assert.Empty(report.Dropped);
        Assert.Equal(source.Facts.Count, report.Kept.Count);

        // 原命题（Q 在 P 的极线上）搬运后仍成立
        var claim = new ClaimOnLine("Q", "LP");
        var judge = ProofJudge.Evaluate(transformed, new TheoremEngine(), claim);
        Assert.Equal(ClaimVerdict.ProvedByTheoremChain, judge.Verdict);
    }

    [Fact]
    public void ProjectiveTransfer_DropsAffineOnlyFacts()
    {
        var source = LaHireScene();
        // 射影变换（保持直线/相切/配极，破坏中点）
        var projective = new Matrix3x3(
            ExactScalar.FromInt(1), ExactScalar.FromInt(0), ExactScalar.FromInt(0),
            ExactScalar.FromInt(0), ExactScalar.FromInt(1), ExactScalar.FromInt(0),
            ExactScalar.FromInt(1), ExactScalar.FromInt(0), ExactScalar.FromInt(1));

        var (_, report) = SceneTransfer.Apply(source, projective);

        Assert.Equal(TransformLevel.Projective, report.TargetLevel);
        Assert.Contains(report.Dropped, f => f is MidpointFact);
        // 配极/在线上这类射影事实应保留
        Assert.Contains(report.Kept, f => f is PolarOfFact);
        Assert.Contains(report.Kept, f => f is OnLineFact);
    }
}
