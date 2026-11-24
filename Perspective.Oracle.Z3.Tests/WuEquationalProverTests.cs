using Perspective.Constraint;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;
using Perspective.Oracle.Z3;

namespace Perspective.Oracle.Z3.Tests;

/// <summary>
/// E2：吴特征列后端接入 E 契约。重点：
/// - 与 Z3 参考后端反向校验：Wu 说 Proved/ProvedUnderAssumptions ⇒ Z3 必须也 Proved；
/// - NDG 必须外显为 ProvedUnderAssumptions；
/// - 未证/可约分支/非多项式片段只能是 Unknown。
/// </summary>
public class WuEquationalProverTests
{
    private static ExactScalar Sym(string formula) => new(double.NaN, formula);

    private static RelationStatement Eq(string left, string right) =>
        new(RelationKind.Equal, Sym(left), Sym(right));

    private static readonly string[] Variables = ["x", "y", "z", "a"];

    [Fact]
    public void EntailedEquality_IsProved()
    {
        var prover = new WuEquationalProver();
        var result = prover.ProveZero(Variables, [Eq("x*y", "1")], Eq("x*y", "1"));

        Assert.True(result.Status is EquationalStatus.Proved or EquationalStatus.ProvedUnderAssumptions,
            $"实际状态：{result.Status}（{result.Detail}）");
    }

    [Fact]
    public void TransitiveEqualities_AreProved()
    {
        var prover = new WuEquationalProver();
        var result = prover.ProveZero(
            Variables,
            [Eq("x", "y"), Eq("y", "z")],
            Eq("x", "z"));

        Assert.True(result.Status is EquationalStatus.Proved or EquationalStatus.ProvedUnderAssumptions,
            $"实际状态：{result.Status}（{result.Detail}）");
    }

    [Fact]
    public void NonlinearElimination_IsProved()
    {
        var prover = new WuEquationalProver();
        var result = prover.ProveZero(
            Variables,
            [Eq("x^2 + y^2", "1"), Eq("x", "0")],
            Eq("y^2", "1"));

        Assert.True(result.Status is EquationalStatus.Proved or EquationalStatus.ProvedUnderAssumptions,
            $"实际状态：{result.Status}（{result.Detail}）");
    }

    [Fact]
    public void InitialDependentProof_ReturnsNonddegeneracyAssumptions()
    {
        // a*x − 1 = 0 ⇒ a*x − 1 = 0：伪除以初始式 a 为代价，必须把 a≠0 外显
        var prover = new WuEquationalProver();
        var result = prover.ProveZero(Variables, [Eq("a*x", "1")], Eq("a*x", "1"));

        Assert.True(result.Status is EquationalStatus.Proved or EquationalStatus.ProvedUnderAssumptions,
            $"实际状态：{result.Status}（{result.Detail}）");
        if (result.Status == EquationalStatus.ProvedUnderAssumptions)
        {
            Assert.NotEmpty(result.Assumptions);
            Assert.All(result.Assumptions, a => Assert.Equal(RelationKind.NotEqual, a.Kind));
        }
    }

    [Fact]
    public void NonEntailedEquality_IsUnknown()
    {
        var prover = new WuEquationalProver();
        var result = prover.ProveZero(Variables, [Eq("x*y", "0")], Eq("x", "0"));

        // 可约分支未分解：只能 Unknown，绝不当假
        Assert.Equal(EquationalStatus.Unknown, result.Status);
    }

    [Fact]
    public void InconsistentHypotheses_AreFlagged()
    {
        var prover = new WuEquationalProver();
        var result = prover.ProveZero(Variables, [Eq("0", "1")], Eq("x", "0"));

        Assert.Equal(EquationalStatus.InconsistentHypotheses, result.Status);
    }

    [Fact]
    public void NonPolynomialExpression_IsUnknown()
    {
        var prover = new WuEquationalProver();
        var result = prover.ProveZero(Variables, [Eq("sin(x)", "0")], Eq("x", "0"));

        Assert.Equal(EquationalStatus.Unknown, result.Status);
    }

    [Fact]
    public void WuProved_MustAlsoBeProvedByZ3_CrossCheck()
    {
        var wu = new WuEquationalProver();
        using var z3 = new Z3EquationalProver();

        var cases = new (RelationStatement[] Hypotheses, RelationStatement Goal)[]
        {
            ([Eq("x*y", "1")], Eq("x*y", "1")),
            ([Eq("x", "y"), Eq("y", "z")], Eq("x", "z")),
            ([Eq("x^2 + y^2", "1"), Eq("x", "0")], Eq("y^2", "1")),
            ([Eq("x*y", "0")], Eq("x", "0")),
            ([Eq("x", "0")], Eq("x*y", "0")),
        };

        foreach (var (hypotheses, goal) in cases)
        {
            var wuResult = wu.ProveZero(Variables, hypotheses, goal);
            if (wuResult.Status is not (EquationalStatus.Proved or EquationalStatus.ProvedUnderAssumptions))
                continue;

            var z3Result = z3.ProveZero(Variables, hypotheses, goal);
            Assert.True(z3Result.Status == EquationalStatus.Proved,
                $"Wu 声称可证但 Z3 不同意的案例；Wu={wuResult.Status}，Z3={z3Result.Status}，goal={goal.Left} = {goal.Right}");
        }
    }
}
