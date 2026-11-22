using AngouriMath;

namespace Perspective.Core.Math;

/// <summary>
/// 严格检验结果：绝不靠 double 容差糊弄用户
/// </summary>
public enum StrictResult
{
    StrictlyTrue,     // 代数严格化简为 0（100% 确定成立）
    StrictlyFalse,    // 确定不等于 0（哪怕只差 1e-12 也能抓出来）
    NearZeroUnproven, // 数值极接近 0，但含复杂超越/根式，符号引擎暂未能化简为 0
    NoSymbolicData    // 来源包含纯近似浮点数（如鼠标自由拖拽），无法做严格证明
}

/// <summary>
/// 双轨数字：同时携带“double 近似值”和“AngouriMath 精确代数基因”
/// </summary>
public readonly record struct ExactScalar(double Approx, Entity? Exact)
{
    // --- 几种创建方式 ---

    /// <summary>从精确整数创建（自带精确基因）</summary>
    public static ExactScalar FromInt(long value) => new(value, value);

    /// <summary>从数学公式创建，例如 "sqrt(3)", "pi / 2", "1/3"（自带精确基因）</summary>
    public static ExactScalar FromFormula(string formula)
    {
        Entity exact = formula;
        double approx = (double)exact.EvalNumerical();
        return new ExactScalar(approx, exact);
    }

    /// <summary>从纯近似浮点数创建（没有精确基因，诚实标记为近似值）</summary>
    public static ExactScalar FromApprox(double value) => new(value, null);

    public static implicit operator ExactScalar(int value) => FromInt(value);
    public static implicit operator ExactScalar(long value) => FromInt(value);

    // --- 四则运算：同时更新 double 和代数基因 ---
    // 注意：只要有一方是纯近似数 (Exact == null)，结果就不再假装自己精确
    private static bool IsExactZero(Entity? e) => e is not null && e.Equals(Entity.Number.Integer.Zero);
    private static bool IsExactOne(Entity? e) => e is not null && e.Equals(Entity.Number.Integer.One);

    /// <summary>在关键节点（如矩阵求逆后）折叠表达式树，防止雪球膨胀</summary>
    public ExactScalar Simplified() => Exact is null ? this : new(Approx, Exact.Simplify());

    // --- 四则运算：自带 0/1 零开销剪枝 ---

    public static ExactScalar operator +(ExactScalar a, ExactScalar b)
    {
        if (a.Exact is null || b.Exact is null) return new(a.Approx + b.Approx, null);
        if (IsExactZero(a.Exact)) return b;
        if (IsExactZero(b.Exact)) return a;
        return new(a.Approx + b.Approx, a.Exact + b.Exact);
    }

    public static ExactScalar operator -(ExactScalar a, ExactScalar b)
    {
        if (a.Exact is null || b.Exact is null) return new(a.Approx - b.Approx, null);
        if (IsExactZero(b.Exact)) return a;
        if (IsExactZero(a.Exact)) return -b;
        return new(a.Approx - b.Approx, a.Exact - b.Exact);
    }

    public static ExactScalar operator *(ExactScalar a, ExactScalar b)
    {
        if (a.Exact is null || b.Exact is null) return new(a.Approx * b.Approx, null);
        if (IsExactZero(a.Exact) || IsExactZero(b.Exact)) return FromInt(0);
        if (IsExactOne(a.Exact)) return b;
        if (IsExactOne(b.Exact)) return a;
        return new(a.Approx * b.Approx, a.Exact * b.Exact);
    }

    public static ExactScalar operator /(ExactScalar a, ExactScalar b)
    {
        if (a.Exact is null || b.Exact is null) return new(a.Approx / b.Approx, null);
        if (IsExactOne(b.Exact)) return a;
        // 除数为精确 0 时，AngouriMath 会静默给出 ComplexInfinity，这里显式报错，避免污染下游结论
        if (IsExactZero(b.Exact))
            throw new DivideByZeroException("精确除法：除数为 0，结果无定义。");
        if (IsExactZero(a.Exact)) return FromInt(0);
        return new(a.Approx / b.Approx, a.Exact / b.Exact);
    }

    public static ExactScalar operator -(ExactScalar a)
    {
        if (a.Exact is null) return new(-a.Approx, null);
        if (IsExactZero(a.Exact)) return FromInt(0);
        return new(-a.Approx, -a.Exact);
    }
    /// <summary>精确开平方（保留根号基因，如 sqrt(3) 或 sqrt(4 - t^2)）</summary>
    public static ExactScalar Sqrt(ExactScalar x)
    {
        // 有精确基因时，实数性判定必须基于精确表达式，而不是探针值 Approx：
        // Approx 只是参数域上的一个采样点，用它判负会误伤“探针为负、但表达式在别处为正”的符号量。
        if (x.Exact is not null)
        {
            var exact = x.Exact.Simplify();

            if (exact.FreeVariables.Count == 0)
            {
                // 常量表达式：严格判断符号，确实为负才抛
                double val = (double)exact.EvalNumerical();
                if (val < -1e-9)
                    throw new ArgumentOutOfRangeException(nameof(x), "实数范围内无法对负数开平方。");
                return new ExactScalar(val <= 0 ? 0 : System.Math.Sqrt(val), AngouriMath.MathS.Sqrt(exact));
            }

            // 含自由符号：无法在参数全域判定符号，不再依据探针值抛异常；
            // 调用方应在使用前用严通道验证根号内表达式非负。
            return new ExactScalar(
                x.Approx < 0 ? 0 : System.Math.Sqrt(x.Approx),
                AngouriMath.MathS.Sqrt(exact));
        }

        // 纯近似输入：没有精确基因，只能按数值判断
        if (x.Approx < -1e-9)
            throw new ArgumentOutOfRangeException(nameof(x), "实数范围内无法对负数开平方。");

        double approx = x.Approx <= 0 ? 0 : System.Math.Sqrt(x.Approx);
        return new ExactScalar(approx, null);
    }
    /// <summary>精确余弦（保留符号基因，如 cos(pi/4) → sqrt(2)/2）</summary>
    public static ExactScalar Cos(ExactScalar x) =>
        new(System.Math.Cos(x.Approx), x.Exact is not null ? AngouriMath.MathS.Cos(x.Exact) : null);

    /// <summary>精确正弦（保留符号基因，如 sin(pi/6) → 1/2）</summary>
    public static ExactScalar Sin(ExactScalar x) =>
        new(System.Math.Sin(x.Approx), x.Exact is not null ? AngouriMath.MathS.Sin(x.Exact) : null);

    // --- 快通道与严通道 ---

    /// <summary>【快通道】仅用于画图渲染、判断无穷远、初步吸附筛选</summary>
    public bool IsNumericallyZero(double tol = 1e-9) => System.Math.Abs(Approx) < tol;

    /// <summary>【严通道】向用户宣布相切、共线、过定点等结论前，必须过这一关！</summary>
    public StrictResult VerifyStrictZero()
    {
        if (Exact is null)
            return StrictResult.NoSymbolicData;

        if (double.IsFinite(Approx) && System.Math.Abs(Approx) > 1e-4)
            return StrictResult.StrictlyFalse;

        Entity simplified = Exact.Simplify();
        if (simplified.Equals(Entity.Number.Integer.Zero))
            return StrictResult.StrictlyTrue;

        if (simplified is Entity.Number)
            return StrictResult.StrictlyFalse;

        // 如果带符号的分式默认 4 轮没化完，追加到 6 轮再试一次
// 如果带符号的分式没化完，先 Expand 展开括号再做深层 Simplify
        simplified = simplified.Expand(4).Simplify(6);
        if (simplified.Equals(Entity.Number.Integer.Zero))
            return StrictResult.StrictlyTrue;

        return simplified is Entity.Number ? StrictResult.StrictlyFalse : StrictResult.NearZeroUnproven;
    }

    public override string ToString() =>
        Exact is not null ? $"{Approx:G6} [精确: {Exact.Simplify()}]" : $"{Approx:G6} [近似]";
    
    /// <summary>创建一个符号参数（如 "t"），同时带一个用于数值探测的测试值</summary>
    public static ExactScalar FromSymbol(string symbolName, double probeValue = 1.7320508) =>
        new(probeValue, symbolName);
}