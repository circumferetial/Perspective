using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

/// <summary>三值真值（用于"能否判定"，不静默猜测）。</summary>
public enum TruthValue
{
    True,
    False,
    Unknown
}

/// <summary>量词种类。</summary>
public enum QuantifierKind
{
    ForAll,
    Exists
}

public enum RelationKind
{
    Equal,
    NotEqual,
    LessThan,
    GreaterThan,
    LessOrEqual,
    GreaterOrEqual
}

/// <summary>
/// 问题陈述的语法树：**量词节点 + 逻辑连接 + 原子关系**。
/// <para>
/// 【设计意图】把"题目"表示成
/// <c>Q₁x₁…Qₙxₙ. φ(x₁…xₙ)</c> 的形式（Q 为 ∀/∃，φ 为无量词矩阵），
/// 再按量词前缀的形状分派到不同求解器（恒等式验证／方程求根／范围求解）。
/// </para>
/// <para>
/// 【理论背景】一阶逻辑中任何公式都可化为**前束范式**（量词全部前置），所以"以 ∀/∃ 开头、
/// 内部嵌套"的表达能力是足够描述一阶陈述的；但"能表达"不等于"能判定"：
/// 量词交替、高阶量词（"存在一种构造"）、以及实数理论的完整 QE 都超出当前能力。
/// </para>
/// </summary>
public abstract record Statement
{
    // ----- 原子关系 -----
    public static Statement Equal(ExactScalar left, ExactScalar right) =>
        new RelationStatement(RelationKind.Equal, left, right);

    public static Statement NotEqual(ExactScalar left, ExactScalar right) =>
        new RelationStatement(RelationKind.NotEqual, left, right);

    public static Statement LessThan(ExactScalar left, ExactScalar right) =>
        new RelationStatement(RelationKind.LessThan, left, right);

    public static Statement GreaterThan(ExactScalar left, ExactScalar right) =>
        new RelationStatement(RelationKind.GreaterThan, left, right);

    public static Statement LessOrEqual(ExactScalar left, ExactScalar right) =>
        new RelationStatement(RelationKind.LessOrEqual, left, right);

    public static Statement GreaterOrEqual(ExactScalar left, ExactScalar right) =>
        new RelationStatement(RelationKind.GreaterOrEqual, left, right);

    // ----- 逻辑连接 -----
    public static Statement And(params Statement[] items) => new JunctionStatement(true, items);

    public static Statement Or(params Statement[] items) => new JunctionStatement(false, items);

    public static Statement Not(Statement body) => new NotStatement(body);

    // ----- 量词 -----
    public static Statement ForAll(IReadOnlyList<string> variables, Statement body) =>
        new QuantifiedStatement(QuantifierKind.ForAll, variables, body);

    public static Statement Exists(IReadOnlyList<string> variables, Statement body) =>
        new QuantifiedStatement(QuantifierKind.Exists, variables, body);

    public static Statement ForAll(Statement body, params string[] variables) =>
        ForAll(variables, body);

    public static Statement Exists(Statement body, params string[] variables) =>
        Exists(variables, body);
}

/// <summary>原子关系 left op right。</summary>
public sealed record RelationStatement(RelationKind Kind, ExactScalar Left, ExactScalar Right) : Statement;

/// <summary>合取 / 析取。</summary>
public sealed record JunctionStatement(bool IsAnd, IReadOnlyList<Statement> Items) : Statement;

/// <summary>否定。</summary>
public sealed record NotStatement(Statement Body) : Statement;

/// <summary>量词节点：kind 个变量 + 体。</summary>
public sealed record QuantifiedStatement(
    QuantifierKind Kind,
    IReadOnlyList<string> Variables,
    Statement Body) : Statement;
