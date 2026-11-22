using AngouriMath;
using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

/// <summary>一"问"的答案（模板输出）。</summary>
/// <param name="PartId">题号（如 "20(1)"）。</param>
/// <param name="Kind">答案类型：SlopeSet / Area / Extremum / Verdict / ...</param>
/// <param name="Summary">人类可读摘要。</param>
/// <param name="Numeric">数值型结论（可被后续小题注入为参数）。</param>
/// <param name="Solutions">解集型结论（如斜率集合）。</param>
/// <param name="Attained">最值/端点是否可达。</param>
/// <param name="Strict">结论是否为严格不等式。</param>
/// <param name="Steps">步骤链（Derivation/OracleStep/验证结论摘要）。</param>
public sealed record ProblemAnswer(
    string PartId,
    string Kind,
    string Summary,
    ExactScalar? Numeric = null,
    Entity.Set? Solutions = null,
    bool? Attained = null,
    bool? Strict = null,
    IReadOnlyList<string>? Steps = null);

/// <summary>
/// 【陈设】一道题/一串小题共享的上下文：事实库 + 参数表 + 已得答案。
/// <para>
/// 多问依赖就是这么实现的：上一问的数值答案经 <see cref="ProblemBoard.UseAnswerAsParameter"/>
/// 注入参数表；已证明的几何事实直接留在 <see cref="Facts"/> 里，下一问模板照常读取。
/// </para>
/// </summary>
public sealed class ProblemScene
{
    public FactBase Facts { get; set; } = new();

    /// <summary>参数表（如 b、m、k），可由前几问的答案注入。</summary>
    public Dictionary<string, ExactScalar> Parameters { get; } = [];

    /// <summary>已答各问（按顺序）。</summary>
    public List<ProblemAnswer> Answers { get; } = [];

    public ExactScalar GetParameter(string name) =>
        Parameters.TryGetValue(name, out var value)
            ? value
            : throw new InvalidOperationException(
                $"参数 '{name}' 未设置：前几问是否漏了 UseAnswerAsParameter？");
}

/// <summary>
/// 【题板】把多问串起来：每问调用一个模板，答案累积进 <see cref="ProblemScene"/>；
/// 后续小题可以消费前几问的数值结论（参数注入）与已证明事实（Facts）。
/// </summary>
public sealed class ProblemBoard
{
    public ProblemScene Scene { get; } = new();

    public ProblemAnswer Solve<TInput>(IProblemTemplate<TInput> template, TInput input, string partId)
    {
        var answer = template.Solve(Scene, input);
        Scene.Answers.Add(answer with { PartId = partId });
        return Scene.Answers[^1];
    }

    /// <summary>把某问的数值结论注入参数表（前几问 ⇒ 后几问的依赖通道）。</summary>
    public void UseAnswerAsParameter(string parameterName, ProblemAnswer answer)
    {
        if (answer.Numeric is null)
            throw new InvalidOperationException(
                $"答案 {answer.PartId} 没有数值结论，不能注入为参数 '{parameterName}'");
        Scene.Parameters[parameterName] = answer.Numeric.Value;
    }
}

/// <summary>题型模板：输入 = 陈设 + 槽位，输出 = 一问的答案。</summary>
public interface IProblemTemplate<in TInput>
{
    string Name { get; }

    ProblemAnswer Solve(ProblemScene scene, TInput input);
}
