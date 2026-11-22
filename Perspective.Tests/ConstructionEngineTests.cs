using Perspective.Core.Geometry;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// 【外部构造注入】人/LLM 提出辅助对象，机器严格验收：
/// 合格则登记并参与证明；不合格则拒绝并说明哪条关系不成立。
/// </summary>
public class ConstructionEngineTests
{
    private static (FactBase Facts, Conic Conic, HPoint A, HPoint B, HLine T1, HLine T2) TangentScene()
    {
        var conic = Conic.Ellipse(ExactScalar.FromInt(2), ExactScalar.FromFormula("sqrt(3)"));
        var a = HPoint.FromFormula("1", "3/2");    // 在椭圆 x²/4+y²/3=1 上
        var b = HPoint.FromCartesian(2, 0);
        var t1 = conic.PolarLine(a);
        var t2 = conic.PolarLine(b);

        var facts = new FactBase { CurrentLevel = TransformLevel.Projective };
        facts.AddConic("C", conic);
        facts.AddPoint("A", a);
        facts.AddPoint("B", b);
        facts.AddLine("T1", t1);
        facts.AddLine("T2", t2);
        facts.AddFact(new TangentFact("T1", "C", "A"), "已知");
        facts.AddFact(new TangentFact("T2", "C", "B"), "已知");
        return (facts, conic, a, b, t1, t2);
    }

    [Fact]
    public void AcceptedConstruction_EnablesTheoremChain()
    {
        var (facts, _, _, _, t1, t2) = TangentScene();
        var q = HLine.Intersect(t1, t2);

        // 外部（用户/LLM）提出：Q = T1 ∩ T2，并声称它同时在两条切线上
        var result = ConstructionEngine.ProposePoint(facts, "Q", q,
            [new ClaimPointOnLine("Q", "T1"), new ClaimPointOnLine("Q", "T2")],
            proposedBy: "user");

        Assert.True(result.Accepted, string.Join("；", result.FailedClaims));
        Assert.Equal(2, result.VerifiedClaims.Count);
        Assert.Empty(result.FailedClaims);

        // 构造被验收后，定理链照常工作：切点弦 AB 是 Q 的极线
        new TheoremEngine().Register<ChordOfContactIsPolarRule>().RunToFixedPoint(facts);
        Assert.Contains(facts.Polars, p => p.PoleId == "Q" && p.ConicId == "C");

        // 来源可追溯：构造步骤写入 Derivation.Note，而不是伪造规则推导
        Assert.Contains(facts.DerivationLog, d =>
            d.RuleId == RuleIds.ExternalConstruction &&
            d.Fact is OnLineFact ol && ol.PointId == "Q" &&
            d.Note is not null && d.Note.Contains("外部构造"));
    }

    [Fact]
    public void RejectedConstruction_DoesNotPolluteFactBase()
    {
        var (facts, _, _, _, _, _) = TangentScene();
        int before = facts.Facts.Count;

        // 谎称一个明显不在切线上的点在 T1 上
        var result = ConstructionEngine.ProposePoint(facts, "X", HPoint.FromCartesian(10, 10),
            [new ClaimPointOnLine("X", "T1")], proposedBy: "llm");

        Assert.False(result.Accepted);
        Assert.Single(result.FailedClaims);
        Assert.NotNull(result.RejectReason);
        Assert.DoesNotContain("X", facts.Points.Keys);
        Assert.Equal(before, facts.Facts.Count);
    }

    [Fact]
    public void MidpointConstruction_IsVerifiedAndRecorded()
    {
        var (facts, _, a, b, _, _) = TangentScene();
        var mid = new HPoint((a.X / a.W + b.X / b.W) / 2, (a.Y / a.W + b.Y / b.W) / 2, 1);

        var result = ConstructionEngine.ProposePoint(facts, "M", mid,
            [new ClaimMidpoint("M", "A", "B")], proposedBy: "search");

        Assert.True(result.Accepted);
        Assert.Contains(facts.Midpoints, m => m.MidId == "M" && m.AId == "A" && m.BId == "B");
        Assert.Contains(facts.DerivationLog, d =>
            d.RuleId == RuleIds.ExternalConstruction &&
            d.Fact is MidpointFact mf && mf.MidId == "M");
    }

    [Fact]
    public void ValidConstruction_DoesNotGuaranteeProof()
    {
        var (facts, _, a, b, _, _) = TangentScene();
        var mid = new HPoint((a.X / a.W + b.X / b.W) / 2, (a.Y / a.W + b.Y / b.W) / 2, 1);

        // 构造合法，但用它去证一个无关错误命题：必须如实失败
        var constructed = ConstructionEngine.ProposePoint(facts, "M", mid,
            [new ClaimMidpoint("M", "A", "B")], proposedBy: "user");
        Assert.True(constructed.Accepted);

        var engine = new TheoremEngine().Register<MidpointGroundRule>();
        var attempt = engine.Prove(facts, typeof(OnConicFact),
            f => f.Facts.Contains(new OnConicFact("M", "C")) ? "错误命中" : null);
        Assert.False(attempt.Proved);
    }

    /// <summary>测试用的极简规则：把"中点"这一事实本身作为可证明目标。</summary>
    private readonly struct MidpointGroundRule : IFactRule<MidpointGroundRule>
    {
        public static string Title => "测试：中点事实";
        public static TransformLevel RequiredLevel => TransformLevel.Affine;
        public static Type[] ConclusionTypes => [typeof(MidpointFact)];
        public static Type[] PremiseTypes => [];

        public static int Apply(FactBase facts) => 0;
    }
}
