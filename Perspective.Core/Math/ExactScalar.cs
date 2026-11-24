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
/// 精确算术域契约（**不是**严格代数意义上的环）：
/// 只承诺 0/1、加/减/乘（含一元负号）与三态判零决策过程；
/// 不承诺除法、序、以及 Approx 轨上的代数封闭性（Exact 可为空）。
/// 用途：让"同一套几何/消元代码"跑在 ExactScalar、PolyExpr、复数等不同系数域上。
/// </summary>
public interface IExactArithmetic<T> where T : IExactArithmetic<T>
{
    abstract static T Zero { get; }
    abstract static T One { get; }
    abstract static T FromInt(long value);
    abstract static T operator +(T a, T b);
    abstract static T operator -(T a, T b);
    abstract static T operator -(T a);
    abstract static T operator *(T a, T b);

    /// <summary>三态判零：真 / 假 / 未证（不得把未证当零）。</summary>
    abstract static StrictResult DecideZero(T value);
}
/// <summary>
/// 双轨数字：同时携带“double 近似值”和“AngouriMath 精确代数基因”。
/// 注意：record struct 默认的 == 为表达式树结构相等，判数学相等请使用
/// <see cref="AreStrictlyEqual"/> 或 <c>(a - b).VerifyStrictZero()</c>。
/// </summary>
public readonly record struct ExactScalar(double Approx, Entity? Exact) : IExactArithmetic<ExactScalar>
{
    public static ExactScalar Zero => FromInt(0);
    public static ExactScalar One => FromInt(1);
    static ExactScalar IExactArithmetic<ExactScalar>.FromInt(long value) => FromInt(value);
    static StrictResult IExactArithmetic<ExactScalar>.DecideZero(ExactScalar a) => a.VerifyStrictZero();
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

    /// <summary>创建一个符号参数（如 "t"），同时带一个用于数值探测的测试值</summary>
    public static ExactScalar FromSymbol(string symbolName, double probeValue = 1.7320508) =>
        new(probeValue, symbolName);

    public static implicit operator ExactScalar(int value) => FromInt(value);
    public static implicit operator ExactScalar(long value) => FromInt(value);

    // --- 内部零/一快速检测 ---

    private static bool IsExactZero(Entity? e) => e is not null && e.Equals(Entity.Number.Integer.Zero);
    private static bool IsExactOne(Entity? e) => e is not null && e.Equals(Entity.Number.Integer.One);

    /// <summary>在关键节点折叠表达式树，防止雪球膨胀</summary>
    public ExactScalar Simplified() => Exact is null ? this : this with { Exact = Exact.Simplify() };

    // --- 四则运算：自带 0/1 零开销剪枝 ---
    // 注意：只要有一方是纯近似数 (Exact == null)，结果就不再假装自己精确

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
        // 除数为精确 0（含未折叠但恒等于 0 的表达式）时显式报错，避免 AngouriMath 静默给出 ComplexInfinity
        if (IsExactZero(b.Exact) ||
            (b.IsNumericallyZero() && b.VerifyStrictZero(allowFastNumericReject: false) == StrictResult.StrictlyTrue))
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
        if (x.Exact is not null)
        {
            var exact = x.Exact.Simplify();
            if (IsExactZero(exact))
                return FromInt(0);

            if (exact.FreeVariables.Count == 0)
            {
                // 常量表达式：严格判断符号，确实为负才抛
                double val = (double)exact.EvalNumerical();
                if (val < -1e-9)
                    throw new ArgumentOutOfRangeException(nameof(x), "实数范围内无法对负数开平方。");

                if (val < 0)
                {
                    var zeroCheck = new ExactScalar(val, exact).VerifyStrictZero(allowFastNumericReject: false);
                    if (zeroCheck == StrictResult.StrictlyTrue)
                        return FromInt(0);
                    if (zeroCheck == StrictResult.StrictlyFalse)
                        throw new ArgumentOutOfRangeException(nameof(x), "实数范围内无法对负数开平方。");

                    // NearZeroUnproven：可能是符号恒零但 EvalNumerical 产生极小负噪，标记探针无效
                    return new ExactScalar(double.NaN, MathS.Sqrt(exact));
                }

                return new ExactScalar(System.Math.Sqrt(val), MathS.Sqrt(exact));
            }

            // 含自由符号：无法在参数全域判定符号；
            // 当前探针落在定义域外 (x.Approx < 0) 时必须置为 NaN，防止下游误触 VerifyStrictZero 快判短路。
            double approxSym = (double.IsNaN(x.Approx) || x.Approx < 0)
                ? double.NaN
                : System.Math.Sqrt(x.Approx);
            return new ExactScalar(approxSym, MathS.Sqrt(exact));
        }

        // 纯近似输入：没有精确基因，只能按数值判断
        if (x.Approx < -1e-9)
            throw new ArgumentOutOfRangeException(nameof(x), "实数范围内无法对负数开平方。");

        double approx = x.Approx <= 0 ? 0 : System.Math.Sqrt(x.Approx);
        return new ExactScalar(approx, null);
    }

    /// <summary>精确余弦（保留符号基因，如 cos(pi/4) → sqrt(2)/2）</summary>
    public static ExactScalar Cos(ExactScalar x) =>
        new(System.Math.Cos(x.Approx), x.Exact is not null ? MathS.Cos(x.Exact) : null);

    /// <summary>精确正弦（保留符号基因，如 sin(pi/6) → 1/2）</summary>
    public static ExactScalar Sin(ExactScalar x) =>
        new(System.Math.Sin(x.Approx), x.Exact is not null ? MathS.Sin(x.Exact) : null);

    /// <summary>绝对值（保留符号基因）。</summary>
    public ExactScalar Abs() =>
        new(System.Math.Abs(Approx), Exact is not null ? MathS.Abs(Exact) : null);

    // --- 快通道与严通道 ---

    /// <summary>【快通道】仅用于画图渲染、判断无穷远、初步吸附筛选</summary>
    public bool IsNumericallyZero(double tol = 1e-9) => System.Math.Abs(Approx) < tol;

    /// <summary>【严通道】向用户宣布相切、共线、过定点等结论前，必须过这一关！</summary>
    public StrictResult VerifyStrictZero() => VerifyStrictZero(allowFastNumericReject: true);

    /// <summary>
    /// 【严通道重载】<paramref name="allowFastNumericReject"/> 为 false 时跳过
    /// <c>|Approx| &gt; 1e-4</c> 的数值快判（用于大数抵消风险高的场景或内部保底检验）。
    /// </summary>
    public StrictResult VerifyStrictZero(bool allowFastNumericReject)
    {
        if (Exact is null)
            return StrictResult.NoSymbolicData;

        if (IsExactZero(Exact))
            return StrictResult.StrictlyTrue;

        if (allowFastNumericReject && double.IsFinite(Approx) && System.Math.Abs(Approx) > 1e-4)
            return StrictResult.StrictlyFalse;

        // 1) 【有理函数通道优先】通分为单分式后证"分子 ≡ 0"。
        if (ContainsDivision(Exact) && TryRationalZero(Exact) is { } rational)
            return rational;

        // 2) 直接化简
        if (DecideZero(Exact.Simplify()) is { } decided)
            return decided;

        // 3) 展开括号后再深层化简
        if (DecideZero(Exact.Simplify().Expand(4).Simplify(6)) is { } decided2)
            return decided2;

        return StrictResult.NearZeroUnproven;
    }

    /// <summary>
    /// 统一判零语义：
    /// 有符号基因时严格遵守严通道（不会用浮点容差推翻 StrictlyFalse）；
    /// 仅在纯近似数据 (<see cref="StrictResult.NoSymbolicData"/>) 时回退到数值容差。
    /// </summary>
    public bool IsZeroWithFallback(double approxTol = 1e-9, bool treatUnprovenAsZero = false) =>
        VerifyStrictZero() switch
        {
            StrictResult.StrictlyTrue => true,
            StrictResult.StrictlyFalse => false,
            StrictResult.NoSymbolicData => IsNumericallyZero(approxTol),
            StrictResult.NearZeroUnproven => treatUnprovenAsZero,
            _ => false
        };

    /// <summary>数学相等性严格判定（验证 a - b ≡ 0）。</summary>
    public static StrictResult AreStrictlyEqual(ExactScalar a, ExactScalar b) =>
        (a - b).VerifyStrictZero();

    private static bool ContainsDivision(Entity entity)
    {
        if (entity is Entity.Divf) return true;
        foreach (var node in entity.Nodes)
            if (node is Entity.Divf) return true;
        return false;
    }

    private static StrictResult? TryRationalZero(Entity entity)
    {
        try
        {
            var fraction = UnwrapProvided(entity).AsSingleFraction();
            if (UnwrapProvided(fraction) is not Entity.Divf divf) return null;
            return DecideZero(divf.Dividend.Expand(4).Simplify(6));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static StrictResult? DecideZero(Entity entity)
    {
        entity = UnwrapProvided(entity);

        if (entity.Equals(Entity.Number.Integer.Zero))
            return StrictResult.StrictlyTrue;
        if (entity is Entity.Number)
            return StrictResult.StrictlyFalse;
        return null;
    }

    private static Entity UnwrapProvided(Entity entity) =>
        entity is Entity.Providedf provided ? provided.Expression : entity;

    /// <summary>调试显示不触发昂贵的 Simplify，避免在 IDE 悬停查看大表达式时卡死。</summary>
    public override string ToString() =>
        Exact is not null ? $"{Approx:G6} [精确: {Exact}]" : $"{Approx:G6} [近似]";

    /// <summary>按需输出化简后的表达式字符串。</summary>
    public string ToSimplifiedString() =>
        Exact is not null ? $"{Approx:G6} [精确: {Exact.Simplify()}]" : $"{Approx:G6} [近似]";
}