using AngouriMath;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Constraint;

/// <summary>
/// E2：吴特征列等式后端（PolyExpr 引擎原样移植 + 薄适配）。
/// <para>
/// 【能力】多项式等式蕴含：H ⇒ goal=0。可约约束/非多项式/超时/未知均为 Unknown；
/// 依赖伪除初始式的证明返回 ProvedUnderAssumptions（NDG 外显）。
/// 【不做】不等式、根式、分母含变量、超越函数——这些转 IAlgebraicOracle/证书层。
/// </para>
/// </summary>
public sealed class WuEquationalProver : IEquationalProver
{
    public string Name => "Wu 特征列（等式）";

    public EquationalProofResult ProveZero(
        IReadOnlyList<string> variables,
        IReadOnlyList<RelationStatement> hypotheses,
        RelationStatement goal)
    {
        if (goal.Kind != RelationKind.Equal)
            return EquationalProofResult.Unknown("E 后端只接受等式目标");
        if (hypotheses.Any(h => h.Kind != RelationKind.Equal))
            return EquationalProofResult.Unknown("E 后端只接受等式假设（不等式请走 IAlgebraicOracle/证书层）");

        try
        {
            var space = new ConstraintSpace();
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var name in variables)
            {
                ids[name] = ids.Count;
                space.NewSymbol(name);
            }

            PolyExpr Parse(ExactScalar scalar)
            {
                if (scalar.Exact is null)
                    throw new NotSupportedException("表达式缺少精确基因（纯近似数据）");
                return ParseEntity(scalar.Exact, ids);
            }

            foreach (var hypothesis in hypotheses)
                space.RequireZero(Parse(hypothesis.Left) - Parse(hypothesis.Right));

            var target = Parse(goal.Left) - Parse(goal.Right);
            var outcome = space.ProveZero(target);

            return outcome.Status switch
            {
                ConstraintProofStatus.Proved when outcome.NdgConditions.Count == 0 =>
                    EquationalProofResult.Proved("特征列约化余式为零"),
                ConstraintProofStatus.Proved =>
                    EquationalProofResult.ProvedUnderAssumptions(
                        outcome.NdgConditions.Select(p => NonZeroRelation(p, variables)).ToList(),
                        "仅在 NDG 条件下成立"),
                ConstraintProofStatus.NotZero =>
                    EquationalProofResult.Unknown("特征列约化余式非零（未证，且不得当假）"),
                ConstraintProofStatus.Inconsistent =>
                    EquationalProofResult.InconsistentHypotheses("约束集中出现非零常数"),
                _ => EquationalProofResult.Unknown("未知状态")
            };
        }
        catch (Exception ex)
        {
            return EquationalProofResult.Unknown($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static RelationStatement NonZeroRelation(PolyExpr polynomial, IReadOnlyList<string> variables)
    {
        var text = polynomial.Format(id => id >= 0 && id < variables.Count ? variables[id] : $"v{id}");
        Entity expression = text;
        return new RelationStatement(
            RelationKind.NotEqual,
            new ExactScalar(double.NaN, expression),
            ExactScalar.FromInt(0));
    }

    /// <summary>把 AngouriMath 表达式解析成多项式；超出多项式片段一律抛 NotSupportedException（→ Unknown）。</summary>
    private static PolyExpr ParseEntity(Entity entity, IReadOnlyDictionary<string, int> ids)
    {
        if (entity is Entity.Providedf provided)
            return ParseEntity(provided.Expression, ids);

        if (entity is Entity.Variable variable)
        {
            var name = variable.Name;
            if (!ids.TryGetValue(name, out var id))
                throw new NotSupportedException($"未知变量 {name}");
            return PolyExpr.Var(id);
        }

        if (entity is Entity.Number number)
        {
            var value = (double)number.EvalNumerical();
            if (!double.IsFinite(value))
                throw new NotSupportedException("非实/非有限常数");
            return PolyExpr.FromConst(new ExactScalar(value, number));
        }

        if (entity is Entity.Minusf subtraction)
        {
            var children = subtraction.DirectChildren;
            if (children.Count == 1) return -ParseEntity(children[0], ids);
            if (children.Count == 2) return ParseEntity(children[0], ids) - ParseEntity(children[1], ids);
            throw new NotSupportedException("减法节点参数个数异常");
        }

        if (entity is Entity.Sumf additive)
        {
            var sum = PolyExpr.Zero;
            foreach (var node in additive.DirectChildren)
                sum += ParseEntity(node, ids);
            return sum;
        }

        if (entity is Entity.Mulf multiplicative)
        {
            var product = PolyExpr.One;
            foreach (var node in multiplicative.DirectChildren)
                product *= ParseEntity(node, ids);
            return product;
        }

        if (entity is Entity.Powf power)
        {
            var nodes = power.DirectChildren.ToList();
            if (nodes.Count != 2 || nodes[1] is not Entity.Number exponent)
                throw new NotSupportedException("幂只支持整数指数");
            var k = (int)(double)exponent.EvalNumerical();
            if (k < 0 || System.Math.Abs(k - (double)exponent.EvalNumerical()) > 1e-12)
                throw new NotSupportedException("幂只支持非负整数指数");

            var basis = ParseEntity(nodes[0], ids);
            var result = PolyExpr.One;
            for (var i = 0; i < k; i++) result *= basis;
            return result;
        }

        if (entity is Entity.Divf division)
        {
            var nodes = division.DirectChildren.ToList();
            if (nodes.Count != 2 || nodes[1] is not Entity.Number denominator)
                throw new NotSupportedException("分母必须是数值常数");
            var d = (double)denominator.EvalNumerical();
            if (d == 0) throw new NotSupportedException("分母为零");
            var reciprocal = new ExactScalar(1.0 / d, Entity.Number.Integer.One / denominator);
            return PolyExpr.FromConst(reciprocal) * ParseEntity(nodes[0], ids);
        }

        throw new NotSupportedException($"不在多项式片段内：{entity.GetType().Name}");
    }
}
