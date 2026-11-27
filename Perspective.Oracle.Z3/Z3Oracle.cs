using AngouriMath;
using Microsoft.Z3;
using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Oracle.Z3;

/// <summary>
/// 基于 Z3（nlsat）的实代数判定器。
/// <para>
/// 【能力】无量词非线性实多项式约束的可满足性（含严格/非严格不等式）。
/// 根式/分母等非多项式节点在 M1 阶段返回 <see cref="OracleVerdictKind.Unknown"/>，
/// 后续通过"引入新变量 + 平方约束"正则化。
/// </para>
/// <para>
/// 【定位】只回答"有没有实解"，不输出证明步骤；调用方把结论记成
/// <see cref="OracleStep"/>，解释层诚实描述"经实代数判定无反例"。
/// </para>
/// </summary>
public sealed class Z3Oracle : IAlgebraicOracle, IDisposable
{
    private const int MaxPowExponent = 64;

    private readonly Context _context = new();
    private readonly uint _timeoutMs;

    public Z3Oracle(uint timeoutMs = 5000) => _timeoutMs = timeoutMs;

    public OracleVerdict DecideExists(
        IReadOnlyList<string> variables,
        IReadOnlyList<RelationStatement> constraints)
    {        try
        {
            _sqrtCounter = 0;
            var env = variables.ToDictionary(v => v, v => (ArithExpr)_context.MkRealConst(v));

            var assertions = new List<BoolExpr>(constraints.Count);
            var sideConstraints = new List<BoolExpr>();
            foreach (var constraint in constraints)
            {
                var compiled = CompileRelation(constraint, env, sideConstraints);
                if (compiled is null)
                    return OracleVerdict.Unknown("约束含不可编译节点（根式/超越函数/符号分母）");
                assertions.Add(compiled);
            }
            assertions.AddRange(sideConstraints);

            var solver = _context.MkSolver();
            solver.Set("timeout", _timeoutMs);
            foreach (var assertion in assertions) solver.Assert(assertion);

            return solver.Check() switch
            {
                Status.UNSATISFIABLE => OracleVerdict.Unsat,
                Status.SATISFIABLE => OracleVerdict.Sat(solver.Model.ToString()),
                _ => OracleVerdict.Unknown("Z3 返回 unknown（超时或超出判定片段）")
            };
        }
        catch (Exception ex)
        {
            return OracleVerdict.Unknown("Z3 异常：" + ex.Message);
        }
    }

    /// <summary>
    ///     闭式表达式的零判定：查询“expression != 0 是否有反例”。
    ///     UNSAT ⇒ StrictlyTrue（依赖 Z3 可信）；SAT ⇒ StrictlyFalse；否则 Unknown。
    /// </summary>
    public StrictVerdict TryZero(ExactScalar expression)
    {
        var verdict = DecideExists(
            [],
            [new RelationStatement(RelationKind.NotEqual, expression, ExactScalar.Zero)]);

        return verdict.Kind switch
        {
            OracleVerdictKind.Unsat => StrictVerdict.StrictlyTrue,
            OracleVerdictKind.Sat => StrictVerdict.StrictlyFalse,
            _ => StrictVerdict.Unknown
        };
    }

    /// <summary>
    ///     参数化恒等式：∀vars, expression=0。
    ///     通过“是否存在 expression≠0 的反例”判定。
    /// </summary>
    public StrictVerdict TryProveZero(ExactScalar expression, IReadOnlyList<string> variables)
    {
        var verdict = DecideExists(
            variables,
            [new RelationStatement(RelationKind.NotEqual, expression, ExactScalar.Zero)]);

        return verdict.Kind switch
        {
            OracleVerdictKind.Unsat => StrictVerdict.StrictlyTrue,
            OracleVerdictKind.Sat => StrictVerdict.StrictlyFalse,
            _ => StrictVerdict.Unknown
        };
    }

    private BoolExpr? CompileRelation(
        RelationStatement relation,
        Dictionary<string, ArithExpr> env,
        List<BoolExpr> sideConstraints)
    {
        var left = Compile(relation.Left, env, sideConstraints);
        var right = Compile(relation.Right, env, sideConstraints);
        if (left is null || right is null) return null;

        return relation.Kind switch
        {
            RelationKind.Equal => _context.MkEq(left, right),
            RelationKind.NotEqual => _context.MkNot(_context.MkEq(left, right)),
            RelationKind.LessThan => _context.MkLt(left, right),
            RelationKind.GreaterThan => _context.MkGt(left, right),
            RelationKind.LessOrEqual => _context.MkLe(left, right),
            RelationKind.GreaterOrEqual => _context.MkGe(left, right),
            _ => null
        };
    }

    private ArithExpr? Compile(
        ExactScalar scalar,
        Dictionary<string, ArithExpr> env,
        List<BoolExpr> sideConstraints) =>
        scalar.Exact is null ? null : CompileEntity(scalar.Exact, env, sideConstraints);

    /// <summary>
    /// 只编译"有理系数多项式 + 可正则化根式"片段：
    /// 整数/有理数、变量、+−×、整数幂、除以有理常数；
    /// sqrt(e) 引入 s，并加 s²=e、s≥0。
    /// 其余（三角、变量在分母、超越）返回 null → 上层报 Unknown。
    /// </summary>
    private ArithExpr? CompileEntity(
        Entity entity,
        Dictionary<string, ArithExpr> env,
        List<BoolExpr> sideConstraints)
    {
        switch (entity)
        {
            case Entity.Number.Integer integer:
                return _context.MkReal(integer.ToString());

            case Entity.Number.Rational rational:
                return _context.MkDiv(
                    _context.MkReal(rational.Numerator.ToString()),
                    _context.MkReal(rational.Denominator.ToString()));

            case Entity.Variable variable:
                return env.TryGetValue(variable.Name, out var value) ? value : null;

            case Entity.Sumf sum:
            {
                var a = CompileEntity(sum.NodeFirstChild, env, sideConstraints);
                var b = CompileEntity(sum.NodeSecondChild, env, sideConstraints);
                return a is null || b is null ? null : _context.MkAdd(a, b);
            }

            case Entity.Minusf difference:
            {
                var a = CompileEntity(difference.NodeFirstChild, env, sideConstraints);
                var b = CompileEntity(difference.NodeSecondChild, env, sideConstraints);
                return a is null || b is null ? null : _context.MkSub(a, b);
            }

            case Entity.Mulf product:
            {
                var a = CompileEntity(product.NodeFirstChild, env, sideConstraints);
                var b = CompileEntity(product.NodeSecondChild, env, sideConstraints);
                return a is null || b is null ? null : _context.MkMul(a, b);
            }

            case Entity.Divf quotient:
            {
                var numerator = CompileEntity(quotient.NodeFirstChild, env, sideConstraints);
                if (numerator is null) return null;

                // 只允许除以有理常数（变量/根式在分母 → Unknown，等后续正则化）
                return quotient.NodeSecondChild switch
                {
                    Entity.Number.Integer denominator =>
                        _context.MkDiv(numerator, _context.MkReal(denominator.ToString())),
                    Entity.Number.Rational denominator =>
                        _context.MkMul(
                            numerator,
                            _context.MkDiv(
                                _context.MkReal(denominator.Denominator.ToString()),
                                _context.MkReal(denominator.Numerator.ToString()))),
                    _ => null
                };
            }

            case Entity.Powf power when IsHalfExponent(power.Exponent):
            {
                var radicand = CompileEntity(power.Base, env, sideConstraints);
                if (radicand is null) return null;

                var root = (ArithExpr)_context.MkRealConst($"sqrt_{_sqrtCounter++}");
                sideConstraints.Add(_context.MkEq(_context.MkMul(root, root), radicand));
                sideConstraints.Add(_context.MkGe(root, _context.MkReal(0)));
                return root;
            }

            case Entity.Powf power:
            {
                if (power.Exponent is not Entity.Number.Integer exponentEntity) return null;
                if (!int.TryParse(exponentEntity.ToString(), out int exponent)) return null;
                if (exponent is < 0 or > MaxPowExponent) return null;

                var @base = CompileEntity(power.Base, env, sideConstraints);
                if (@base is null) return null;

                ArithExpr result = _context.MkReal(1);
                for (int i = 0; i < exponent; i++)
                    result = _context.MkMul(result, @base);
                return result;
            }

            default:
                return null;
        }
    }

    private int _sqrtCounter;

    private static bool IsHalfExponent(Entity exponent) =>
        exponent is Entity.Number.Rational rational
        && rational.Numerator.ToString() == "1"
        && rational.Denominator.ToString() == "2";

    public void Dispose() => _context.Dispose();
}
