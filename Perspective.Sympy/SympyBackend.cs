using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Perspective.Core.Reasoning;

namespace Perspective.Sympy;

/// <summary>
/// SymPy 后端（纯 Python，子进程 + JSON 协议）。
/// <para>
/// 【为什么不做 CMake/native】SymPy 本体是纯 Python；它的"C 内核"对应物是 SymEngine（C++），
/// 需要 vcpkg/CMake 构建。先用零构建的子进程方式接入，把契约与流程跑通；
/// 将来需要性能时再换 SymEngine C API（契约不变，见 architecture.md §4i）。
/// </para>
/// <para>
/// 【进程模型】每次调用启动 `python -c &lt;脚本&gt;`，请求经 stdin JSON 传入、结果从 stdout 读回。
/// 可通过环境变量 `PERSPECTIVE_PYTHON` 指定解释器路径。
/// </para>
/// </summary>
public sealed class SympyBackend : ISymbolicBackend
{
    private readonly string _pythonExecutable;
    private readonly int _timeoutMs;

    public SympyBackend(string? pythonExecutable = null, int timeoutMs = 60_000)
    {
        _pythonExecutable = pythonExecutable
            ?? Environment.GetEnvironmentVariable("PERSPECTIVE_PYTHON")
            ?? "python";
        _timeoutMs = timeoutMs;
    }

    public string Name => "SymPy";

    public SymbolicResult Simplify(string expression) => Run("simplify", [expression]);

    public SymbolicResult Expand(string expression) => Run("expand", [expression]);

    public SymbolicResult Factor(string expression) => Run("factor", [expression]);

    public SymbolicResult Solve(string expression, string variable) =>
        Run("solve", [expression], variable);

    public SymbolicResult Resultant(string left, string right, string variable) =>
        Run("resultant", [left, right], variable);

    private SymbolicResult Run(string operation, string[] arguments, string? variable = null)
    {
        try
        {
            var request = JsonSerializer.Serialize(new
            {
                op = operation,
                args = arguments,
                var = variable ?? "x"
            });

            var startInfo = new ProcessStartInfo(_pythonExecutable)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(WorkerScript);

            using var process = Process.Start(startInfo);
            if (process is null) return SymbolicResult.Failure("无法启动 Python 进程");

            process.StandardInput.Write(request);
            process.StandardInput.Close();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(_timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* 忽略 */ }
                return SymbolicResult.Failure($"SymPy 调用超时（>{_timeoutMs}ms）");
            }

            if (process.ExitCode != 0)
                return SymbolicResult.Failure($"Python 退出码 {process.ExitCode}：{Truncate(error)}");

            return ParseResponse(output);
        }
        catch (Exception exception)
        {
            return SymbolicResult.Failure("SymPy 调用异常：" + exception.Message);
        }
    }

    private static SymbolicResult ParseResponse(string output)
    {
        string trimmed = output.Trim();
        if (trimmed.Length == 0)
            return SymbolicResult.Failure("SymPy 无输出（可能未安装：python -m pip install sympy）");

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;
            if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True)
                return SymbolicResult.Ok(root.GetProperty("result").GetString() ?? string.Empty);

            string message = root.TryGetProperty("error", out var error) ? error.GetString() ?? "未知错误" : "未知错误";
            return SymbolicResult.Failure(message);
        }
        catch (JsonException)
        {
            return SymbolicResult.Failure("SymPy 返回非 JSON：" + Truncate(trimmed));
        }
    }

    private static string Truncate(string text) =>
        text.Length <= 400 ? text : text[..400] + "…";

    /// <summary>内嵌的 Python 工作脚本（经 stdin 读 JSON 请求，stdout 写 JSON 结果）。</summary>
    private const string WorkerScript = """
        import sys, json
        import sympy as sp
        from sympy.parsing.sympy_parser import parse_expr, standard_transformations, convert_xor

        TRANSFORMS = standard_transformations + (convert_xor,)
        LOCALS = {"sqrt": sp.sqrt, "pi": sp.pi, "E": sp.E}

        def parse(text):
            return parse_expr(text, transformations=TRANSFORMS, local_dict=LOCALS)

        def main():
            request = json.load(sys.stdin)
            op = request["op"]
            args = request.get("args", [])
            variable = sp.Symbol(request.get("var", "x"))

            if op == "simplify":
                result = sp.simplify(parse(args[0]))
            elif op == "expand":
                result = sp.expand(parse(args[0]))
            elif op == "factor":
                result = sp.factor(parse(args[0]))
            elif op == "solve":
                result = sp.solve(sp.Eq(parse(args[0]), 0), variable)
            elif op == "resultant":
                result = sp.resultant(parse(args[0]), parse(args[1]), variable)
            else:
                raise ValueError("unknown op: " + op)

            print(json.dumps({"ok": True, "result": str(result)}))

        try:
            main()
        except Exception as exc:
            print(json.dumps({"ok": False, "error": str(exc)}))
        """;
}
