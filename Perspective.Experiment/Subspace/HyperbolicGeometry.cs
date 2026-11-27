// ============================================================================
// HyperbolicGeometry：split-complex（j² = 1）——Cayley–Klein 的“双曲列”
//
// 三写法（照 ComplexGeometry / ParabolicGeometry 的模板）：
//   1) 标量：SplitComplex<T>（实现 IExactRing；注意 (1+j)(1-j)=0，有零因子，不是域）
//   2) 算子：UnitBoost（Lorentz boost，cosh² − sinh² = 1；半快度参数化避开双曲函数）
//   3) 空间：Klein 模型——已有的 GeometryLens<ExactScalar>.Hyperbolic（绝对形 x²+y²−w²=0），
//            boost 就是保持该绝对形的线性变换，不需要新类型。
//
// 与另外两列的对应：
//   | 椭圆 | UnitPhase  e^{iθ} | CP¹            |
//   | 抛物 | UnitTranslation 1+εt | 切丛（点+速度） |
//   | 双曲 | UnitBoost  e^{jt} | Klein 模型     |
// ============================================================================
using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

/// <summary>
///     split-complex 数 a + bj（j² = 1）。
///     不是域：1±j 满足 (1+j)(1-j)=0，所以本模块不提供除法（IExactRing 也不需要）。
/// </summary>
public readonly record struct SplitComplex<T>(T Real, T Hyper) : IExactRing<SplitComplex<T>>
    where T : IExactRing<T>
{
    public static SplitComplex<T> Zero => new(T.Zero, T.Zero);
    public static SplitComplex<T> One => new(T.One, T.Zero);

    public static SplitComplex<T> FromLong(long value) => new(T.FromLong(value), T.Zero);

    public static SplitComplex<T> FromReal(T value) => new(value, T.Zero);
    public static SplitComplex<T> FromSplit(T real, T hyper) => new(real, hyper);

    public static implicit operator SplitComplex<T>(T value) => new(value, T.Zero);

    public static SplitComplex<T> operator +(SplitComplex<T> a, SplitComplex<T> b) =>
        new(a.Real + b.Real, a.Hyper + b.Hyper);

    public static SplitComplex<T> operator -(SplitComplex<T> a, SplitComplex<T> b) =>
        new(a.Real - b.Real, a.Hyper - b.Hyper);

    public static SplitComplex<T> operator -(SplitComplex<T> a) => new(-a.Real, -a.Hyper);

    /// <summary>(a + bj)(c + dj) = (ac + bd) + (ad + bc)j，因为 j² = 1。</summary>
    public static SplitComplex<T> operator *(SplitComplex<T> a, SplitComplex<T> b) =>
        new(a.Real * b.Real + a.Hyper * b.Hyper, a.Real * b.Hyper + a.Hyper * b.Real);

    /// <summary>“模长平方” a² − b²：类光元素（1±j）的 Norm 为 0，但自身非零。</summary>
    public T Norm() => Real * Real - Hyper * Hyper;

    public StrictResult VerifyZero() => Combine(T.VerifyZero(Real), T.VerifyZero(Hyper));

    static StrictResult IExactRing<SplitComplex<T>>.VerifyZero(SplitComplex<T> a) => a.VerifyZero();

    static bool IExactRing<SplitComplex<T>>.IsZeroFast(SplitComplex<T> a) =>
        T.IsZeroFast(a.Real) && T.IsZeroFast(a.Hyper);

    static StrictVerdict IExactRing<SplitComplex<T>>.IsUnit(SplitComplex<T> a) =>
        T.IsUnit(a.Norm());

    public override string ToString() => $"{Real} + {Hyper}j";

    private static StrictResult Combine(StrictResult a, StrictResult b)
    {
        if (a == StrictResult.StrictlyFalse || b == StrictResult.StrictlyFalse) return StrictResult.StrictlyFalse;
        if (a == StrictResult.StrictlyTrue && b == StrictResult.StrictlyTrue) return StrictResult.StrictlyTrue;
        if (a == StrictResult.NoSymbolicData || b == StrictResult.NoSymbolicData) return StrictResult.NoSymbolicData;
        return StrictResult.NearZeroUnproven;
    }
}

/// <summary>
///     双曲单位算子 e^{jt} = cosh t + j·sinh t（Lorentz boost）。
///     半快度参数化 u = tanh(t/2)：cosh t = (1+u²)/(1−u²)，sinh t = 2u/(1−u²)，
///     这样不需要 cosh/sinh 的符号基因。
/// </summary>
public readonly record struct UnitBoost(ExactScalar Cosh, ExactScalar Sinh)
{
    public static UnitBoost Identity => new(ExactScalar.One, ExactScalar.Zero);

    /// <summary>由半快度构造；u = ±1 时 1−u² 为零，返回失败。</summary>
    public static Result<UnitBoost, GeometryError> FromHalfRapidity(ExactScalar u)
    {
        var u2 = u * u;
        var den = ExactScalar.One - u2;
        if (den.IsZeroWithFallback(treatUnprovenAsZero: true))
            return Result<UnitBoost, GeometryError>.Fail(GeometryError.UndefinedParameter);

        return Result<UnitBoost, GeometryError>.Ok(new UnitBoost((1 + u2) / den, (2 * u) / den));
    }

    public UnitBoost Inverse() => new(Cosh, -Sinh);

    /// <summary>快度相加：B(t₁)·B(t₂) = B(t₁+t₂)。</summary>
    public static UnitBoost operator *(UnitBoost a, UnitBoost b) => new(
        a.Cosh * b.Cosh + a.Sinh * b.Sinh,
        a.Cosh * b.Sinh + a.Sinh * b.Cosh);

    /// <summary>沿 x 轴的 boost（保持 x² + y² − w²）。</summary>
    /// <remarks>只混合 x/w；任意方向需先 UnitPhase 旋转，再沿 x 轴 boost，最后反向旋转。</remarks>
    public LinearMap3<ExactScalar> ToMap() => new(
        Cosh, ExactScalar.Zero, Sinh,
        ExactScalar.Zero, ExactScalar.One, ExactScalar.Zero,
        Sinh, ExactScalar.Zero, Cosh);

    public Primal3<ExactScalar> Apply(Primal3<ExactScalar> p) => ToMap().Apply(p);

    public override string ToString() => $"boost(cosh={Cosh}, sinh={Sinh})";
}
