namespace Perspective.Core.Reasoning;

/// <summary>
/// 【E：等式约束后端契约】证明"在 hypotheses 下 goal 恒等于 0"。
/// <para>
/// 与 <see cref="IAlgebraicOracle"/> 的分工：
///  oracle 判断"∃ 实解"（反例判定）；本接口判断"generic 等式蕴含"（推导），
///  两者方向不同，不得互相替代（见 docs/architecture.md §4k）。
/// </para>
/// <para>
/// 【Soundness 契约】后端返回 <see cref="EquationalStatus.Proved"/> 时，
/// 只有在 hypotheses 与 goal 是**忠实编码的多项式等式**时成立；
/// 若证明依赖非退化假设（伪除的初始式 ≠ 0），必须返回
/// <see cref="EquationalStatus.ProvedUnderAssumptions"/> 并列出这些假设，
/// 调用方不得把它当无条件真。
/// </para>
/// </summary>
public enum EquationalStatus
{
    /// <summary>在给定 hypotheses 下，goal ≡ 0（无附加假设）。</summary>
    Proved,

    /// <summary>仅当 <see cref="EquationalProofResult.Assumptions"/> 全部成立时，goal ≡ 0。</summary>
    ProvedUnderAssumptions,

    /// <summary>未证明（可能是假、可能是后端能力不足）。**永远不得当作假**。</summary>
    Unknown,

    /// <summary>hypotheses 本身矛盾：蕴含空洞成立，但必须显式标记，不能静默当证明。</summary>
    InconsistentHypotheses
}

/// <summary>等式后端的证明结果。<see cref="Assumptions"/> 为 NDG（非退化）条件：多项式 ≠ 0。</summary>
public sealed record EquationalProofResult(
    EquationalStatus Status,
    IReadOnlyList<RelationStatement> Assumptions,
    string? Detail = null)
{
    public static EquationalProofResult Proved(string? detail = null) =>
        new(EquationalStatus.Proved, [], detail);

    public static EquationalProofResult ProvedUnderAssumptions(
        IReadOnlyList<RelationStatement> assumptions, string? detail = null) =>
        new(EquationalStatus.ProvedUnderAssumptions, assumptions, detail);

    public static EquationalProofResult Unknown(string? detail = null) =>
        new(EquationalStatus.Unknown, [], detail);

    public static EquationalProofResult InconsistentHypotheses(string? detail = null) =>
        new(EquationalStatus.InconsistentHypotheses, [], detail);
}

/// <summary>
/// 等式蕴含后端（E 契约）。当前参考实现见 <c>Perspective.Oracle.Z3.Z3EquationalProver</c>；
/// 吴特征列后端将以同一契约接入（NDG 通过 <see cref="EquationalProofResult.Assumptions"/> 外显）。
/// </summary>
public interface IEquationalProver
{
    string Name { get; }

    /// <summary>证明 hypotheses（等式合取）⇒ goal 左侧 − 右侧 ≡ 0。</summary>
    EquationalProofResult ProveZero(
        IReadOnlyList<string> variables,
        IReadOnlyList<RelationStatement> hypotheses,
        RelationStatement goal);
}
