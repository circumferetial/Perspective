using Perspective.Core.Reasoning;
using Perspective.Sympy;

namespace Perspective.Sympy.Tests;

/// <summary>
/// SymPy 后端接入测试（子进程 + JSON 协议，零 CMake/native 构建）。
/// 依赖本机已安装 SymPy（python -m pip install sympy）。
/// </summary>
public class SympyBackendTests
{
    private static readonly SympyBackend Backend = new(timeoutMs: 30_000);

    [Fact]
    public void Simplify_IdentityToZero()
    {
        var result = Backend.Simplify("(x + 1) ^ 2 - (x ^ 2 + 2*x + 1)");

        Assert.Equal(SymbolicResultStatus.Ok, result.Status);
        Assert.Equal("0", result.Output);
    }

    [Fact]
    public void Expand_ProducesCanonicalForm()
    {
        var result = Backend.Expand("(x + 1) ^ 2");

        Assert.Equal(SymbolicResultStatus.Ok, result.Status);
        Assert.Contains("2*x", result.Output);
    }

    [Fact]
    public void Factor_RecoversFactors()
    {
        var result = Backend.Factor("x ^ 2 - 1");

        Assert.Equal(SymbolicResultStatus.Ok, result.Status);
        Assert.Contains("x - 1", result.Output);
        Assert.Contains("x + 1", result.Output);
    }

    [Fact]
    public void Solve_FindsBothRoots()
    {
        var result = Backend.Solve("x ^ 2 - 4", "x");

        Assert.Equal(SymbolicResultStatus.Ok, result.Status);
        Assert.Contains("-2", result.Output);
        Assert.Contains("2", result.Output);
    }

    [Fact]
    public void Resultant_EliminatesVariable()
    {
        // resultant(x²−2, y−x, x) = y²−2
        var result = Backend.Resultant("x ^ 2 - 2", "y - x", "x");

        Assert.Equal(SymbolicResultStatus.Ok, result.Status);
        Assert.Contains("y", result.Output);
        Assert.Contains("2", result.Output);
    }

    [Fact]
    public void InvalidExpression_IsReportedAsError()
    {
        var result = Backend.Simplify("1 +* 2");

        Assert.Equal(SymbolicResultStatus.Error, result.Status);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }
}
