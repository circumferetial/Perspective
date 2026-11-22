namespace Perspective.Core.Reasoning;

/// <summary>实代数判定结果（三值：不可判定时不猜测）。</summary>
public enum OracleVerdictKind
{
    /// <summary>约束无实解（对偶地：全称命题无反正例）。</summary>
    Unsat,

    /// <summary>约束有实解（附一个模型，可用于定位漏掉的约束）。</summary>
    Sat,

    /// <summary>判定器超时/超出可编译片段。</summary>
    Unknown
}

/// <summary>判定器返回值。<see cref="Model"/> 仅在 <see cref="OracleVerdictKind.Sat"/> 时非空。</summary>
public sealed record OracleVerdict(OracleVerdictKind Kind, string? Model = null, string? Reason = null)
{
    public static OracleVerdict Unsat { get; } = new(OracleVerdictKind.Unsat);

    public static OracleVerdict Sat(string model) => new(OracleVerdictKind.Sat, model);

    public static OracleVerdict Unknown(string? reason = null) => new(OracleVerdictKind.Unknown, null, reason);
}

/// <summary>
/// 实代数判定器（外部神谕）接口。
/// <para>
/// 【定位】只做"判定"，不生成证明步骤——主证明仍由符号推导与规则链产生；
/// 神谕只负责闭合符号通道化不动的部分（不等式、根式比较等），
/// 因此结果是 <see cref="OracleVerdictKind"/> 而不是证明文本。
/// </para>
/// <para>
/// 【契约】<see cref="DecideExists"/> 判定 <c>∃(vars) [constraints 的合取]</c> 在实数中是否可满足。
/// 全称命题通过取反归结为存在命题（见 <see cref="DecideUniversal"/>）。
/// </para>
/// </summary>
public interface IAlgebraicOracle
{
    OracleVerdict DecideExists(IReadOnlyList<string> variables, IReadOnlyList<RelationStatement> constraints);
}

/// <summary>把全称判定归结为"存在反正例"的取反检查。</summary>
public static class AlgebraicOracleExtensions
{
    /// <summary>
    /// 判定 <c>∀(vars) [hypotheses → conclusion]</c>：
    /// 等价于检查 <c>∃(vars) [hypotheses ∧ ¬conclusion]</c> 是否**不可满足**。
    /// </summary>
    public static OracleVerdict DecideUniversal(
        this IAlgebraicOracle oracle,
        IReadOnlyList<string> variables,
        IReadOnlyList<RelationStatement> hypotheses,
        RelationStatement conclusion)
    {
        var constraints = new List<RelationStatement>(hypotheses) { Negate(conclusion) };
        return oracle.DecideExists(variables, constraints);
    }

    /// <summary>关系的否定（用于全称命题的反正例检查）。</summary>
    public static RelationStatement Negate(this RelationStatement relation) => relation.Kind switch
    {
        RelationKind.Equal => relation with { Kind = RelationKind.NotEqual },
        RelationKind.NotEqual => relation with { Kind = RelationKind.Equal },
        RelationKind.LessThan => relation with { Kind = RelationKind.GreaterOrEqual },
        RelationKind.GreaterThan => relation with { Kind = RelationKind.LessOrEqual },
        RelationKind.LessOrEqual => relation with { Kind = RelationKind.GreaterThan },
        RelationKind.GreaterOrEqual => relation with { Kind = RelationKind.LessThan },
        _ => throw new InvalidOperationException("未知关系")
    };
}
