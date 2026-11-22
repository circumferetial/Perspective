using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Tests;

/// <summary>
/// P1-4b：代数关系事实——长尾"定值组合"的兜底载体。
/// 表达式本身作为事实内容，严格验证后规范化存储；只作终端结论。
/// </summary>
public class AlgebraicRelationTests
{
    [Fact]
    public void SumToConstant_IsStoredAsFact()
    {
        // 模拟"某组合相加为定值"：(k+1)² − (k²+2k) ≡ 1
        var k = ExactScalar.FromSymbol("k");
        var left = (k + ExactScalar.FromInt(1)) * (k + ExactScalar.FromInt(1));
        var right = k * k + ExactScalar.FromInt(2) * k + ExactScalar.FromInt(1);

        var fact = AlgebraicRelationFact.Create(left, right);

        Assert.NotNull(fact);
        Assert.True(fact!.IsEquality);

        var facts = new FactBase();
        Assert.True(facts.AddFact(fact, "题目给定：组合为定值"));
        Assert.Single(facts.AlgebraicRelations);
        Assert.Contains(facts.DerivationLog, d => d.Fact == fact && d.Note!.Contains("组合为定值"));
    }

    [Fact]
    public void EquivalentForms_DeduplicateToSameFact()
    {
        var k = ExactScalar.FromSymbol("k");
        var left1 = (k + ExactScalar.FromInt(1)) * (k + ExactScalar.FromInt(1));
        var right1 = k * k + ExactScalar.FromInt(2) * k + ExactScalar.FromInt(1);
        var left2 = k * k + ExactScalar.FromInt(2) * k + ExactScalar.FromInt(1);
        var right2 = (k + ExactScalar.FromInt(1)) * (k + ExactScalar.FromInt(1));

        var f1 = AlgebraicRelationFact.Create(left1, right1);
        var f2 = AlgebraicRelationFact.Create(left2, right2);

        Assert.NotNull(f1);
        Assert.NotNull(f2);
        // 两侧都规约到同一形态后，两个写法应归一到同一事实（至少不会因左右顺序不同而不同）
        Assert.Equal(f1, f2);

        var facts = new FactBase();
        facts.AddFact(f1!, "a");
        facts.AddFact(f2!, "b");
        Assert.Single(facts.AlgebraicRelations);
    }

    [Fact]
    public void DivisionToConstant_IsVerifiedByStrictChannel()
    {
        // "除以一个系数仍是定值"：(k²+1)/(k²+1) + 1 ≡ 2（有理函数通道）
        var k = ExactScalar.FromSymbol("k");
        var quotient = (k * k + ExactScalar.FromInt(1)) / (k * k + ExactScalar.FromInt(1));
        var left = quotient + ExactScalar.FromInt(1);
        var right = ExactScalar.FromInt(2);

        var fact = AlgebraicRelationFact.Create(left, right);

        Assert.NotNull(fact);
    }

    [Fact]
    public void FalseEquality_IsRejected()
    {
        var x = ExactScalar.FromSymbol("x");
        var fact = AlgebraicRelationFact.Create(x, ExactScalar.FromInt(1));

        Assert.Null(fact);   // 不恒等 → 拒绝写成事实
    }

    [Fact]
    public void ApproximateInput_IsRejected()
    {
        // 纯近似输入（无符号基因）不能成为严格事实
        var approximate = new ExactScalar(3.14159, null);
        var fact = AlgebraicRelationFact.Create(approximate, ExactScalar.FromInt(3));

        Assert.Null(fact);
    }
}
