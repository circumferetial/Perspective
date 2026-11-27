// ============================================================================
// ComplexGeometry：复标量 + 单位相位 + CP¹ 齐次复点（共形/反演/共圆）
//
// 三者都不属于"纯射影骨架"：ExactComplex 是另一种 IExactRing 实现，
// UnitPhase 是只支持乘法的相位算子，HComplex 是 CP¹ 上的齐次复点。
// 它们只做代数，全部零除法（需要除法的地方用共轭放大），因此也不需要 Approx 挑基点。
// ============================================================================
using System.Numerics;
using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

/// <summary>
///     1) 精确复数标量（C）：只做代数标量，不当 2D 几何点使用。
/// </summary>
public readonly record struct ExactComplex(ExactScalar Re, ExactScalar Im) : IExactRing<ExactComplex>, IComputableField<ExactComplex>
{
    public static ExactComplex Zero => new(ExactScalar.Zero, ExactScalar.Zero);
    public static ExactComplex One => new(ExactScalar.One, ExactScalar.Zero);
    public static ExactComplex I => new(ExactScalar.Zero, ExactScalar.One);

    public static ExactComplex FromLong(long value) => new(ExactScalar.FromInt(value), ExactScalar.Zero);
    public static ExactComplex FromReal(ExactScalar r) => new(r, ExactScalar.Zero);

    public static implicit operator ExactComplex(int v) => FromLong(v);
    public static implicit operator ExactComplex(long v) => FromLong(v);
    public static implicit operator ExactComplex(ExactScalar r) => FromReal(r);

    public Complex Approx => new(Re.Approx, Im.Approx);

    public ExactComplex Conjugate() => new(Re, -Im);
    public ExactScalar NormSquared() => Re * Re + Im * Im;
    public ExactComplex Simplified() => new(Re.Simplified(), Im.Simplified());

    public static ExactComplex operator +(ExactComplex a, ExactComplex b) => new(a.Re + b.Re, a.Im + b.Im);
    public static ExactComplex operator -(ExactComplex a, ExactComplex b) => new(a.Re - b.Re, a.Im - b.Im);
    public static ExactComplex operator -(ExactComplex a) => new(-a.Re, -a.Im);

    public static ExactComplex operator *(ExactComplex a, ExactComplex b) =>
        new(a.Re * b.Re - a.Im * b.Im, a.Re * b.Im + a.Im * b.Re);

    public static ExactComplex operator /(ExactComplex a, ExactComplex b)
    {
        var den = b.NormSquared();
        if (den.IsZeroWithFallback(treatUnprovenAsZero: true))
            throw new DivideByZeroException("精确复数除法：模长平方为 0。");
        var num = a * b.Conjugate();
        return new(num.Re / den, num.Im / den);
    }

    public StrictResult VerifyStrictZero()
    {
        var r = Re.VerifyStrictZero();
        var i = Im.VerifyStrictZero();
        if (r == StrictResult.StrictlyFalse || i == StrictResult.StrictlyFalse) return StrictResult.StrictlyFalse;
        if (r == StrictResult.StrictlyTrue && i == StrictResult.StrictlyTrue) return StrictResult.StrictlyTrue;
        if (r == StrictResult.NoSymbolicData || i == StrictResult.NoSymbolicData) return StrictResult.NoSymbolicData;
        return StrictResult.NearZeroUnproven;
    }

    static StrictResult IExactRing<ExactComplex>.VerifyZero(ExactComplex a) => a.VerifyStrictZero();

    static bool IExactRing<ExactComplex>.IsZeroFast(ExactComplex a) =>
        ExactScalar.IsZeroFast(a.Re) && ExactScalar.IsZeroFast(a.Im);

    static StrictVerdict IExactRing<ExactComplex>.IsUnit(ExactComplex a) =>
        a.VerifyStrictZero() switch
        {
            StrictResult.StrictlyTrue => StrictVerdict.StrictlyFalse,
            StrictResult.StrictlyFalse => StrictVerdict.StrictlyTrue,
            StrictResult.NearZeroUnproven => StrictVerdict.Unknown,
            _ => StrictVerdict.NoSymbolicData
        };

    static StrictVerdict IMultiplicativeGroup<ExactComplex>.TryInverse(ExactComplex a, out ExactComplex inverse)
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

    public override string ToString() => $"{Re.ToSimplifiedString()} + ({Im.ToSimplifiedString()})i";
}

/// <summary>
///     2) 单位相位算子 e^(iθ) ∈ U(1) ≅ SO(2)：
///     只支持旋转乘法与反相共轭（不支持加法，因为单位圆对加法不封闭）。
/// </summary>
public readonly record struct UnitPhase(ExactScalar Cos, ExactScalar Sin)
{
    public static UnitPhase Identity => new(ExactScalar.One, ExactScalar.Zero);

    /// <summary>由具体角度构造（适合 π/6、π/4 等特殊角）。</summary>
    public static UnitPhase FromAngle(ExactScalar theta) =>
        new(ExactScalar.Cos(theta), ExactScalar.Sin(theta));

    /// <summary>由半角正切 t = tan(θ/2) 构造有理相位（彻底避开三角函数，适合一般符号推理）。</summary>
    public static UnitPhase FromHalfAngle(ExactScalar t)
    {
        var tt = t * t;
        var den = 1 + tt;
        return new((1 - tt) / den, (2 * t) / den);
    }

    public UnitPhase Conjugate() => new(Cos, -Sin);

    public UnitPhase Inverse() => Conjugate();

    public ExactComplex ToComplex() => new(Cos, Sin);

    /// <summary>相位复合：e^(iα) · e^(iβ) = e^(i(α+β))。</summary>
    public static UnitPhase operator *(UnitPhase a, UnitPhase b) =>
        new(a.Cos * b.Cos - a.Sin * b.Sin, a.Sin * b.Cos + a.Cos * b.Sin);

    /// <summary>极坐标定位：r · e^(iθ) → 复坐标。</summary>
    public ExactComplex Scale(ExactScalar radius) => new(radius * Cos, radius * Sin);

    /// <summary>旋转对应的 3×3 齐次矩阵（绕原点；w 不变）。</summary>
    public LinearMap3<ExactScalar> ToMap() => new(
        Cos, -Sin, ExactScalar.Zero,
        Sin, Cos, ExactScalar.Zero,
        ExactScalar.Zero, ExactScalar.Zero, ExactScalar.One);

    public Primal3<ExactScalar> Apply(Primal3<ExactScalar> p) => ToMap().Apply(p);

    /// <summary>直接旋转一个齐次共形点（绕原点旋转 θ）。</summary>
    public HComplex Rotate(HComplex pt) => new(ToComplex() * pt.Z, pt.W);
}

/// <summary>
///     3) 黎曼球面 CP¹ 上的齐次复点 (z = Z / W)：
///     唯一的 2D 共形/反演/共圆几何点类型（全程零除法，天然包含无穷远点 ∞ = (1, 0)）。
/// </summary>
public readonly record struct HComplex(ExactComplex Z, ExactComplex W)
{
    public static HComplex Infinity => new(ExactComplex.One, ExactComplex.Zero);
    public static HComplex From(ExactComplex z) => new(z, ExactComplex.One);

    /// <summary>用极径 r 和半角参数 t = tan(θ/2) 零除法构造极坐标点：r·(1+it)/(1-it)。</summary>
    public static HComplex FromPolarHalfAngle(ExactScalar radius, ExactScalar halfAngleT) =>
        new(radius * new ExactComplex(1, halfAngleT), new ExactComplex(1, -halfAngleT));

    public static HComplex FromPolar(ExactScalar radius, UnitPhase phase) =>
        From(phase.Scale(radius));

    /// <summary>零除法圆反演：z' = z0 + R² / conj(z - z0)。</summary>
    public static HComplex Invert(HComplex pt, ExactComplex center, ExactScalar radiusSq)
    {
        var d = pt.Z - center * pt.W;
        var dConj = d.Conjugate();
        var wConj = pt.W.Conjugate();
        return new(center * dConj + radiusSq * wConj, dConj);
    }

    /// <summary>三点共线 ⇔ 与无穷远点 ∞=(1,0) 共圆。</summary>
    public static StrictResult AreCollinear(HComplex a, HComplex b, HComplex c) =>
        AreConcyclicOrCollinear(a, b, c, Infinity);

    /// <summary>零除法四点共圆或共线判定：复交比虚部为 0。</summary>
    public static StrictResult AreConcyclicOrCollinear(HComplex a, HComplex b, HComplex c, HComplex d)
    {
        var d13 = a.Z * c.W - c.Z * a.W;
        var d24 = b.Z * d.W - d.Z * b.W;
        var d14 = a.Z * d.W - d.Z * a.W;
        var d23 = b.Z * c.W - c.Z * b.W;

        var num = d13 * d24;
        var den = d14 * d23;

        // 不做除零拦截：den == 0（含重合点）时 num·conj(den) 也为 0，交比虚部为 0。
        // 这符合“任意三点共圆/共线”的退化语义；若调用方要求四点互异，应自己先查重。
        return (num * den.Conjugate()).Im.VerifyStrictZero();
    }

    /// <summary>仿射中点（假定两点均非无穷远 W != 0）。</summary>
    public static HComplex Midpoint(HComplex a, HComplex b) =>
        new(a.Z * b.W + b.Z * a.W, 2 * a.W * b.W);
}

/// <summary>
///     PGL(2,ℂ) 的 2×2 复矩阵作用在 CP¹ 上：z ↦ (Az+B)/(Cz+D)。
///     全部用齐次坐标乘法实现，不需要除法；矩阵可逆时是双射。
/// </summary>
public readonly record struct MobiusMap(ExactComplex A, ExactComplex B, ExactComplex C, ExactComplex D)
{
    public ExactComplex Determinant => A * D - B * C;

    public bool IsInvertible => Determinant.VerifyStrictZero() != StrictResult.StrictlyTrue;

    public Result<HComplex, GeometryError> Apply(HComplex p)
    {
        if (!IsInvertible)
            return Result<HComplex, GeometryError>.Fail(GeometryError.UndefinedParameter);
        if (IsZero(p))
            return Result<HComplex, GeometryError>.Fail(GeometryError.ZeroVector);

        return Result<HComplex, GeometryError>.Ok(
            new HComplex(A * p.Z + B * p.W, C * p.Z + D * p.W));
    }

    /// <summary>this ∘ other：先作用 other，再作用 this。</summary>
    public Result<MobiusMap, GeometryError> Compose(MobiusMap other)
    {
        if (!IsInvertible || !other.IsInvertible)
            return Result<MobiusMap, GeometryError>.Fail(GeometryError.UndefinedParameter);

        return Result<MobiusMap, GeometryError>.Ok(new MobiusMap(
            A * other.A + B * other.C,
            A * other.B + B * other.D,
            C * other.A + D * other.C,
            C * other.B + D * other.D));
    }

    /// <summary>莫比乌斯 ∘ 反莫比乌斯 = 反莫比乌斯。</summary>
    public Result<AntiMobiusMap, GeometryError> Compose(AntiMobiusMap other)
    {
        var composed = Compose(other.Map);
        return composed.TryGet(out var map)
            ? Result<AntiMobiusMap, GeometryError>.Ok(new AntiMobiusMap(map))
            : Result<AntiMobiusMap, GeometryError>.Fail(composed.Error);
    }

    public Result<MobiusMap, GeometryError> Inverse()
    {
        if (!IsInvertible)
            return Result<MobiusMap, GeometryError>.Fail(GeometryError.UndefinedParameter);

        return Result<MobiusMap, GeometryError>.Ok(new MobiusMap(D, -B, -C, A));
    }

    /// <summary>三点定一个莫比乌斯变换（三点必须互异）。</summary>
    public static Result<MobiusMap, GeometryError> FromThreePoints(
        HComplex p1, HComplex p2, HComplex p3,
        HComplex q1, HComplex q2, HComplex q3)
    {
        if (IsZero(p1) || IsZero(p2) || IsZero(p3)
            || IsZero(q1) || IsZero(q2) || IsZero(q3))
            return Result<MobiusMap, GeometryError>.Fail(GeometryError.ZeroVector);

        var source = ToStandard(p1, p2, p3);
        if (!source.TryGet(out var s))
            return Result<MobiusMap, GeometryError>.Fail(source.Error);

        var target = ToStandard(q1, q2, q3);
        if (!target.TryGet(out var t))
            return Result<MobiusMap, GeometryError>.Fail(target.Error);

        var inverse = t.Inverse();
        if (!inverse.TryGet(out var tInv))
            return Result<MobiusMap, GeometryError>.Fail(inverse.Error);

        return tInv.Compose(s);
    }

    /// <summary>把 p1→0、p2→1、p3→∞ 的标准矩阵。</summary>
    private static Result<MobiusMap, GeometryError> ToStandard(HComplex p1, HComplex p2, HComplex p3)
    {
        var s = p1.W * p2.Z - p1.Z * p2.W; // r1·p2
        var u = p3.W * p2.Z - p3.Z * p2.W; // r2_raw·p2
        var d = p1.W * p3.Z - p1.Z * p3.W; // p1 与 p3 的叉积

        if (s.VerifyStrictZero() == StrictResult.StrictlyTrue
            || u.VerifyStrictZero() == StrictResult.StrictlyTrue
            || d.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<MobiusMap, GeometryError>.Fail(GeometryError.CoincidentPoints);

        return Result<MobiusMap, GeometryError>.Ok(new MobiusMap(
            p1.W / s, -p1.Z / s,
            p3.W / u, -p3.Z / u));
    }

    internal static bool IsZero(HComplex p) =>
        p.Z.VerifyStrictZero() == StrictResult.StrictlyTrue
        && p.W.VerifyStrictZero() == StrictResult.StrictlyTrue;
}

/// <summary>
///     反莫比乌斯：z ↦ M(conj(z))。圆反演是它的标准例子；
///     与莫比乌斯复合两次会回到莫比乌斯。
/// </summary>
public readonly record struct AntiMobiusMap(MobiusMap Map)
{
    public Result<HComplex, GeometryError> Apply(HComplex p)
    {
        if (MobiusMap.IsZero(p))
            return Result<HComplex, GeometryError>.Fail(GeometryError.ZeroVector);

        var conjugated = new HComplex(p.Z.Conjugate(), p.W.Conjugate());
        return Map.Apply(conjugated);
    }

    public Result<AntiMobiusMap, GeometryError> Inverse()
    {
        var inv = Map.Inverse();
        if (!inv.TryGet(out var m))
            return Result<AntiMobiusMap, GeometryError>.Fail(inv.Error);

        return Result<AntiMobiusMap, GeometryError>.Ok(new AntiMobiusMap(
            new MobiusMap(m.A.Conjugate(), m.B.Conjugate(), m.C.Conjugate(), m.D.Conjugate())));
    }

    /// <summary>圆反演 z ↦ c + R² / conj(z−c)，写成反莫比乌斯矩阵。</summary>
    public static Result<AntiMobiusMap, GeometryError> FromCircleInversion(
        ExactComplex center, ExactScalar radiusSquared)
    {
        if (radiusSquared.VerifyStrictZero() == StrictResult.StrictlyTrue
            || radiusSquared.Approx <= 0)
            return Result<AntiMobiusMap, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var cConj = center.Conjugate();
        var b = new ExactComplex(radiusSquared, ExactScalar.Zero) - center * cConj;
        var map = new MobiusMap(center, b, ExactComplex.One, -cConj);
        return map.IsInvertible
            ? Result<AntiMobiusMap, GeometryError>.Ok(new AntiMobiusMap(map))
            : Result<AntiMobiusMap, GeometryError>.Fail(GeometryError.UndefinedParameter);
    }

    /// <summary>反莫比乌斯 ∘ 莫比乌斯 = 反莫比乌斯。</summary>
    public Result<AntiMobiusMap, GeometryError> Compose(MobiusMap other)
    {
        var conjugated = new MobiusMap(
            other.A.Conjugate(), other.B.Conjugate(),
            other.C.Conjugate(), other.D.Conjugate());
        var composed = Map.Compose(conjugated);
        return composed.TryGet(out var map)
            ? Result<AntiMobiusMap, GeometryError>.Ok(new AntiMobiusMap(map))
            : Result<AntiMobiusMap, GeometryError>.Fail(composed.Error);
    }

    /// <summary>反莫比乌斯 ∘ 反莫比乌斯 = 莫比乌斯。</summary>
    public Result<MobiusMap, GeometryError> Compose(AntiMobiusMap other)
    {
        var conjugated = new MobiusMap(
            other.Map.A.Conjugate(), other.Map.B.Conjugate(),
            other.Map.C.Conjugate(), other.Map.D.Conjugate());
        return Map.Compose(conjugated);
    }
}

/// <summary>
///     复数 ↔ 实二维向量：只保留 Re/Im 的实线性结构；
///     这不是环同构（复数乘法在向量侧没有对应）。
/// </summary>
public static class ComplexVectorBridge
{
    public static Vec2<ExactScalar> ToVector(this ExactComplex z) => new(z.Re, z.Im);

    public static ExactComplex ToComplex(this Vec2<ExactScalar> v) => new(v.X, v.Y);
}

/// <summary>
///     CP¹（HComplex）与 RP²（Primal3）的桥：只在有限平面上可逆；
///     W=0 的无穷远边界两边拓扑不同，必须显式失败或选择“捏扁”投影。
/// </summary>
public static class HComplexBridge
{    /// <summary>HComplex → Primal3；只接受有限点（W≠0）。</summary>
    public static Result<Primal3<ExactScalar>, GeometryError> ToPrimal3(this HComplex h)
    {
        var zZero = h.Z.VerifyStrictZero() == StrictResult.StrictlyTrue;
        var wZero = h.W.VerifyStrictZero() == StrictResult.StrictlyTrue;

        if (zZero && wZero)
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.ZeroVector);
        if (wZero)
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.AtInfinity);

        // z = Z/W = Z·conj(W) / |W|²，零除法地把分母变成实数。
        var z = h.Z * h.W.Conjugate();
        var wNorm = h.W.NormSquared();
        if (wNorm.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.AtInfinity);

        return Result<Primal3<ExactScalar>, GeometryError>.Ok(
            new Primal3<ExactScalar>(z.Re, z.Im, wNorm));
    }

    /// <summary>Primal3 → HComplex；只接受有限点（W≠0），保证与 <see cref="ToPrimal3" /> 可逆。</summary>
    public static Result<HComplex, GeometryError> ToHComplex(this Primal3<ExactScalar> p)
    {
        if (p.VerifyZero() == StrictResult.StrictlyTrue)
            return Result<HComplex, GeometryError>.Fail(GeometryError.ZeroVector);
        if (p.W.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<HComplex, GeometryError>.Fail(GeometryError.AtInfinity);

        return Result<HComplex, GeometryError>.Ok(new HComplex(
            new ExactComplex(p.X, p.Y),
            new ExactComplex(p.W, ExactScalar.Zero)));
    }

    /// <summary>
    ///     显式的“捏扁”投影：把整条 l∞ 压成 CP¹ 的北极点 ∞。
    ///     这一步不可逆；需要可逆往返时用 <see cref="ToHComplex" />。
    /// </summary>
    public static Result<HComplex, GeometryError> ProjectToRiemannSphere(this Primal3<ExactScalar> p)
    {
        if (p.VerifyZero() == StrictResult.StrictlyTrue)
            return Result<HComplex, GeometryError>.Fail(GeometryError.ZeroVector);
        if (p.W.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<HComplex, GeometryError>.Ok(HComplex.Infinity);

        return Result<HComplex, GeometryError>.Ok(new HComplex(
            new ExactComplex(p.X, p.Y),
            new ExactComplex(p.W, ExactScalar.Zero)));
    }
}
