// ============================================================================
// ParabolicGeometry：对偶数（dual numbers, ε² = 0）——Cayley–Klein 的“抛物列”
//
// 三写法（照 ComplexGeometry 的模板）：
//   1) 标量：DualNumber<T>（实现 IExactRing，可直接替换骨架里的 T）
//   2) 算子：平移 e^{εt} = 1 + εt（UnitTranslation；抛物 Möbius 变换 z ↦ z + t）
//   3) 空间：Primal3<DualNumber<T>> 本身就是“点 + 速度”（P² 的切丛），
//            泛型骨架会自动把 Join/Meet/Det 的导数算出来（乘积法则在环里）。
//
// 为什么有用：
//   · “相切”从判别式变成约束：Q(p) = 0 同时表示“点在曲线上”和“速度沿切线”；
//   · 任何多项式构造的导数自动传播，不用另写微分；
//   · 例：Join(P + εv, P) = ε·(v × P)，ε 部分就是“过 P、方向 v 的直线”。
// ============================================================================
using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

/// <summary>
///     对偶数 a + bε（ε² = 0）。
///     注意：这不是对偶空间；命名用 DualNumber 以免和 <see cref="Dual3{T}" /> 混淆。
/// </summary>
public readonly record struct DualNumber<T>(T Real, T Eps) : IExactRing<DualNumber<T>>
    where T : IExactRing<T>
{
    public static DualNumber<T> Zero => new(T.Zero, T.Zero);
    public static DualNumber<T> One => new(T.One, T.Zero);

    public static DualNumber<T> FromLong(long value) => new(T.FromLong(value), T.Zero);

    /// <summary>常数（无 ε 项）。</summary>
    public static DualNumber<T> FromReal(T value) => new(value, T.Zero);

    /// <summary>位置 + 一阶速度。</summary>
    public static DualNumber<T> FromJet(T real, T eps) => new(real, eps);

    public static implicit operator DualNumber<T>(T value) => new(value, T.Zero);

    public static DualNumber<T> operator +(DualNumber<T> a, DualNumber<T> b) =>
        new(a.Real + b.Real, a.Eps + b.Eps);

    public static DualNumber<T> operator -(DualNumber<T> a, DualNumber<T> b) =>
        new(a.Real - b.Real, a.Eps - b.Eps);

    public static DualNumber<T> operator -(DualNumber<T> a) => new(-a.Real, -a.Eps);

    /// <summary>(a + bε)(c + dε) = ac + (ad + bc)ε，因为 ε² = 0。</summary>
    public static DualNumber<T> operator *(DualNumber<T> a, DualNumber<T> b) =>
        new(a.Real * b.Real, a.Real * b.Eps + a.Eps * b.Real);

    /// <summary>对偶数等于 0 ⇔ 实部与 ε 部都为 0。</summary>
    public StrictResult VerifyZero() => Combine(T.VerifyZero(Real), T.VerifyZero(Eps));

    static StrictResult IExactRing<DualNumber<T>>.VerifyZero(DualNumber<T> a) => a.VerifyZero();

    static bool IExactRing<DualNumber<T>>.IsZeroFast(DualNumber<T> a) =>
        T.IsZeroFast(a.Real) && T.IsZeroFast(a.Eps);

    static StrictVerdict IExactRing<DualNumber<T>>.IsUnit(DualNumber<T> a) =>
        T.IsUnit(a.Real);

    public override string ToString() => $"{Real} + {Eps}ε";

    private static StrictResult Combine(StrictResult a, StrictResult b)
    {
        if (a == StrictResult.StrictlyFalse || b == StrictResult.StrictlyFalse) return StrictResult.StrictlyFalse;
        if (a == StrictResult.StrictlyTrue && b == StrictResult.StrictlyTrue) return StrictResult.StrictlyTrue;
        if (a == StrictResult.NoSymbolicData || b == StrictResult.NoSymbolicData) return StrictResult.NoSymbolicData;
        return StrictResult.NearZeroUnproven;
    }
}

/// <summary>
///     平移算子 e^{εt} = 1 + εt 的 2D 版本（对偶单位群，抛物 Möbius 变换 z ↦ z + b）。
///     作用在齐次点上就是 (x, y, w) ↦ (x + dx·w, y + dy·w, w)。
/// </summary>
public readonly record struct UnitTranslation<T>(T Dx, T Dy) where T : IExactRing<T>
{
    public static UnitTranslation<T> Identity => new(T.Zero, T.Zero);

    public UnitTranslation<T> Compose(UnitTranslation<T> other) => new(Dx + other.Dx, Dy + other.Dy);

    public UnitTranslation<T> Inverse() => new(-Dx, -Dy);

    public static UnitTranslation<T> operator *(UnitTranslation<T> a, UnitTranslation<T> b) => a.Compose(b);

    public Primal3<T> Apply(Primal3<T> p) => new(p.X + Dx * p.W, p.Y + Dy * p.W, p.W);

    public LinearMap3<T> ToMap() => new(
        T.One, T.Zero, Dx,
        T.Zero, T.One, Dy,
        T.Zero, T.Zero, T.One);

    public override string ToString() => $"translate({Dx}, {Dy})";
}
