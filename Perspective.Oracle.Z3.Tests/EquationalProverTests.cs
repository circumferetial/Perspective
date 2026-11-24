using Perspective.Core.Math;
using Perspective.Core.Reasoning;
using Perspective.Oracle.Z3;

namespace Perspective.Oracle.Z3.Tests;

/// <summary>
/// E 契约：等式后端的 soundness 状态映射（参考后端 = Z3）。
/// 重点锁死：NotZero 只能是 Unknown、矛盾假设显式标记、非等式片段拒绝。
/// </summary>
public class EquationalProverTests
{
    // 含自由变量的表达式不能用 FromFormula（EvalNumerical 会抛）；Approx=NaN 明确"无数值"，只走符号通道
    private static ExactScalar Sym(string formula) => new(double.NaN, formula);

    private static RelationStatement Eq(string left, string right) =>
        new(RelationKind.Equal, Sym(left), Sym(right));

    private static RelationStatement NotEq(string left, string right) =>
        new(RelationKind.NotEqual, Sym(left), Sym(right));

    [Fact]
    public void EntailedEquality_IsProved()
    {
        using var prover = new Z3EquationalProver();
        var result = prover.ProveZero(
            ["x", "y"],
            [Eq("x*y", "1")],
            Eq("x*y", "1"));

        Assert.Equal(EquationalStatus.Proved, result.Status);
        Assert.Empty(result.Assumptions);
    }

    [Fact]
    public void TransitiveEqualities_AreProved()
    {
        using var prover = new Z3EquationalProver();
        var result = prover.ProveZero(
            ["x", "y", "z"],
            [Eq("x", "y"), Eq("y", "z")],
            Eq("x", "z"));

        Assert.Equal(EquationalStatus.Proved, result.Status);
    }

    [Fact]
    public void NonlinearElimination_IsProved()
    {
        using var prover = new Z3EquationalProver();
        var result = prover.ProveZero(
            ["x", "y"],
            [Eq("x^2 + y^2", "1"), Eq("x", "0")],
            Eq("y^2", "1"));

        Assert.Equal(EquationalStatus.Proved, result.Status);
    }

    [Fact]
    public void NonEntailedEquality_IsUnknown_NotFalse()
    {
        using var prover = new Z3EquationalProver();
        var result = prover.ProveZero(
            ["x", "y"],
            [Eq("x*y", "0")],
            Eq("x", "0"));

        // 存在反例：只能 Unknown，E 契约没有"假"这种状态
        Assert.Equal(EquationalStatus.Unknown, result.Status);
    }

    [Fact]
    public void InconsistentHypotheses_AreFlagged()
    {
        using var prover = new Z3EquationalProver();
        var result = prover.ProveZero(
            ["x"],
            [Eq("0", "1")],
            Eq("x", "0"));

        Assert.Equal(EquationalStatus.InconsistentHypotheses, result.Status);
    }

    [Fact]
    public void InequalityHypothesis_IsRejectedAsUnknown()
    {
        using var prover = new Z3EquationalProver();
        var result = prover.ProveZero(
            ["x"],
            [new RelationStatement(RelationKind.GreaterOrEqual,
                Sym("x"), Sym("0"))],
            Eq("x", "0"));

        Assert.Equal(EquationalStatus.Unknown, result.Status);
    }

    [Fact]
    public void InequalityGoal_IsRejectedAsUnknown()
    {
        using var prover = new Z3EquationalProver();
        var result = prover.ProveZero(
            ["x"],
            [Eq("x", "1")],
            NotEq("x", "0"));

        Assert.Equal(EquationalStatus.Unknown, result.Status);
    }
}
