namespace Perspective.Core.Reasoning;

/// <summary>符号后端调用结果的状态。</summary>
public enum SymbolicResultStatus
{
    /// <summary>成功；<see cref="SymbolicResult.Output"/> 为结果字符串。</summary>
    Ok,

    /// <summary>当前后端不支持该操作/表达式形态。</summary>
    Unsupported,

    /// <summary>后端调用失败（异常、超时、环境缺失）。</summary>
    Error
}

public sealed record SymbolicResult(SymbolicResultStatus Status, string? Output, string? Error = null)
{
    public static SymbolicResult Ok(string output) => new(SymbolicResultStatus.Ok, output);

    public static SymbolicResult Unsupported(string reason) => new(SymbolicResultStatus.Unsupported, null, reason);

    public static SymbolicResult Failure(string reason) => new(SymbolicResultStatus.Error, null, reason);
}

/// <summary>
/// 【符号后端契约】重计算（化简/展开/因式分解/求解/结式）交给外部强库，
/// 本项目只负责契约、能力声明、结果校验与可解释记录（见 architecture.md §4i）。
/// <para>
/// 【关于表达式语法】输入输出都是**表达式字符串**（跨库边界只认字符串，
/// 不让任何第三方类型进入 Core 的接口签名）。调用方负责在语法之间转换。
/// </para>
/// </summary>
public interface ISymbolicBackend
{
    /// <summary>后端名称（记录到 OracleStep/Derivation 用）。</summary>
    string Name { get; }

    SymbolicResult Simplify(string expression);

    SymbolicResult Expand(string expression);

    SymbolicResult Factor(string expression);

    /// <summary>解方程 expression = 0（对指定变量）。</summary>
    SymbolicResult Solve(string expression, string variable);

    /// <summary>结式 resultant(a, b, variable)。</summary>
    SymbolicResult Resultant(string left, string right, string variable);
}
