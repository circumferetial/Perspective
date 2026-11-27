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
/// <summary>通用谓词判定：True/False 为已证，Unknown 为未证，NoSymbolicData 为含近似数据。</summary>
public enum StrictVerdict
{
    StrictlyTrue,
    StrictlyFalse,
    Unknown,
    NoSymbolicData
}

/// <summary>
///     交换环的计算签名（不表达也无法表达结合律/分配律/交换律，这些靠文档、测试和分层守住）。
///     <para>
///         约定：<c>FromLong</c> 是 \(\mathbb Z\)-代数同态（0/1、加法、乘法保持）；
///         <c>VerifyZero</c> 是 sound 但不完备的判定；<c>IsZeroFast</c>/<c>IsUnit</c>
///         只用于 sound 守卫。需要除法/唯一性/平均值时，额外要求 2 是单位。
///     </para>
/// </summary>
public interface IExactRing<T> where T : IExactRing<T>
{
    abstract static T Zero { get; }
    abstract static T One { get; }

    /// <summary>\(\mathbb Z\)-代数同态：FromLong(0)=Zero、FromLong(1)=One，并保持加法/乘法。</summary>
    abstract static T FromLong(long value);

    abstract static T operator +(T a, T b);
    abstract static T operator -(T a, T b);
    abstract static T operator -(T a);
    abstract static T operator *(T a, T b);

    /// <summary>sound 但不完备的零判定：StrictlyFalse 才表示已证非零。</summary>
    abstract static StrictResult VerifyZero(T a);

    /// <summary>只用于“前置条件守卫”的快速零判定，不是判定器。</summary>
    /// <remarks>
    ///     契约：
    ///     1. 返回 true ⇒ 数学上确为零；实现不得用 Approx 近零来返回 true，
    ///        否则大尺度坐标消去会把有效点误杀成 ZeroVector。
    ///     2. 返回 false 不表示非零；未化简的代数零可能漏判（PolyExpr 只认结构零）。
    ///     3. 只用于入口守卫；最终 StrictResult 判定仍用 <see cref="VerifyZero" />。
    /// </remarks>
    static virtual bool IsZeroFast(T a) => T.VerifyZero(a) == StrictResult.StrictlyTrue;

    /// <summary>
    ///     是否为环中的单位：StrictlyTrue ⇒ 确有逆元；StrictlyFalse ⇒ 确无逆元；
    ///     Unknown ⇒ 未证明。默认 Unknown，绝不能用“非零”代替。
    /// </summary>
    static virtual StrictVerdict IsUnit(T a) => StrictVerdict.Unknown;
}

/// <summary>
///     整环标记：1≠0 且无零因子。C# 无法强制定律，只能作为语义分层与测试标记。
/// </summary>
public interface IIntegralDomain<T> : IExactRing<T> where T : IIntegralDomain<T>
{
}

/// <summary>
///     可计算域：提供三态 <see cref="TryInverse" />。这不是“所有非零元都能判定”的数学域，
///     而是带 Unknown / NoSymbolicData 的计算契约。
/// </summary>
public interface IComputableField<T> : IIntegralDomain<T> where T : IComputableField<T>
{
    /// <summary>StrictlyTrue 时 inverse 满足 a·inverse=One；StrictlyFalse/Unknown 时 inverse 无意义。</summary>
    static abstract StrictVerdict TryInverse(T a, out T inverse);
}

/// <summary>可判定符号的环：只要求三态符号，不要求全序/可计算比较。</summary>
public interface IOrderedRing<T> : IExactRing<T> where T : IOrderedRing<T>
{
    /// <summary>-1 / 0 / +1；返回 0 必须已有零证明。</summary>
    static abstract int Sign(T value);
}

/// <summary>可计算域 + 符号，组合成“实域”语义。</summary>
public interface IRealField<T> : IComputableField<T>, IOrderedRing<T>
    where T : IRealField<T>
{
}

/// <summary>可开方：StrictlyTrue 时 root²=value 已确认。</summary>
public interface ISqrtable<T> : IExactRing<T> where T : ISqrtable<T>
{
    static abstract StrictVerdict TrySqrt(T value, out T root);
}

/// <summary>实域 + 开方，组合成“实闭/可开方实域”语义。</summary>
public interface IRealSqrtField<T> : IRealField<T>, ISqrtable<T>
    where T : IRealSqrtField<T>
{
}

/// <summary>近似值能力；只供数值守卫/候选选择，不进入正确性判定。</summary>
public interface IApproxable<T> : IExactRing<T> where T : IApproxable<T>
{
    static abstract double Approx(T value);
}
/// <summary>
/// 双轨数字：同时携带“double 近似值”和“AngouriMath 精确代数基因”。
/// 注意：record struct 默认的 == 为表达式树结构相等，判数学相等请使用
/// <see cref="AreStrictlyEqual"/> 或 <c>(a - b).VerifyStrictZero()</c>。
/// </summary>
public readonly record struct ExactScalar(double Approx, Entity? Exact) : IExactRing<ExactScalar>, IComputableField<ExactScalar>, IRealSqrtField<ExactScalar>, IApproxable<ExactScalar>
{
    public static ExactScalar Zero => FromInt(0);
    public static ExactScalar One => FromInt(1);
    static ExactScalar IExactRing<ExactScalar>.FromLong(long value) => FromInt(value);
    static StrictResult IExactRing<ExactScalar>.VerifyZero(ExactScalar a) => a.VerifyStrictZero();
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

    private static bool IsExactZero(Entity? e) =>
        e is not null && e.Equals(Entity.Number.Integer.Zero);

    private static bool IsNumericZero(Entity? e) =>
        e is Entity.Number.Complex number && number.IsZero;

    private static bool IsExactOne(Entity? e) => e is not null && e.Equals(Entity.Number.Integer.One);

    /// <summary>供其它环转发的快速零守卫；只认数值零，不做化简。</summary>
    public static bool IsZeroFast(ExactScalar a) => IsNumericZero(a.Exact);

    static bool IExactRing<ExactScalar>.IsZeroFast(ExactScalar a) => IsZeroFast(a);

    static StrictVerdict IExactRing<ExactScalar>.IsUnit(ExactScalar a) =>
        a.VerifyStrictZero() switch
        {
            StrictResult.StrictlyTrue => StrictVerdict.StrictlyFalse,
            StrictResult.StrictlyFalse => StrictVerdict.StrictlyTrue,
            StrictResult.NearZeroUnproven => StrictVerdict.Unknown,
            _ => StrictVerdict.NoSymbolicData
        };

    static StrictVerdict IComputableField<ExactScalar>.TryInverse(ExactScalar a, out ExactScalar inverse)
    {
        switch (a.VerifyStrictZero())
        {
            case StrictResult.StrictlyFalse:
                inverse = One / a;
                return StrictVerdict.StrictlyTrue;
            case StrictResult.StrictlyTrue:
                inverse = default;
                return StrictVerdict.StrictlyFalse;
            case StrictResult.NearZeroUnproven:
                inverse = default;
                return StrictVerdict.Unknown;
            default:
                inverse = default;
                return StrictVerdict.NoSymbolicData;
        }
    }

    static int IOrderedRing<ExactScalar>.Sign(ExactScalar value)
    {
        if (value.VerifyStrictZero() == StrictResult.StrictlyTrue) return 0;
        return value.Approx >= 0 ? 1 : -1;
    }

    static StrictVerdict ISqrtable<ExactScalar>.TrySqrt(ExactScalar value, out ExactScalar root)
    {
        var sign = value.VerifyStrictZero() == StrictResult.StrictlyTrue
            ? 0
            : value.Approx >= 0 ? 1 : -1;

        if (sign < 0)
        {
            root = default;
            return StrictVerdict.StrictlyFalse;
        }

        root = sign == 0 ? Zero : Sqrt(value);
        return StrictVerdict.StrictlyTrue;
    }

    static double IApproxable<ExactScalar>.Approx(ExactScalar value) => value.Approx;

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
        if (IsExactZero(b.Exact)
            || (!double.IsFinite(b.Approx)
                && b.VerifyStrictZero(allowFastNumericReject: false) == StrictResult.StrictlyTrue)
            || (b.IsNumericallyZero()
                && b.VerifyStrictZero(allowFastNumericReject: false) == StrictResult.StrictlyTrue))
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

    /// <summary>
    ///     【严通道】向用户宣布相切、共线、过定点等结论前，必须过这一关！
    ///     <para>
    ///         注意：默认会用 <c>|Approx| &gt; 1e-4</c> 做“显然非零”的快判。若表达式本身因整体缩放
    ///         或相消导致 Approx 不可靠（典型：齐次坐标下的行列式/关联式，坐标尺度很大时），
    ///         可能把恒为零的式子误报成 <see cref="StrictResult.StrictlyFalse" />。
    ///         这类场景请先规范化代表元，或改用 <see cref="VerifyStrictZero(bool)" /> 传
    ///         <c>allowFastNumericReject: false</c>。
    ///     </para>
    /// </summary>
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

        // 数值节点（Integer / Rational / Real / Complex）统一用 IsZero：
        // 不要用 Equals(Integer.Zero)，否则 Real/Complex 的零会被误判成 StrictlyFalse。
        if (IsNumericZero(entity))
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