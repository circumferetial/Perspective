using System.Numerics;
using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

/// <summary>
/// 1) 精确复数标量（C）：只做代数标量，不当 2D 几何点使用。
/// </summary>
public readonly record struct ExactComplex(ExactScalar Re, ExactScalar Im) : IExactRing<ExactComplex>
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

    public override string ToString() => $"{Re.ToSimplifiedString()} + ({Im.ToSimplifiedString()})i";
}

/// <summary>
/// 2) 单位相位算子 e^(iθ) ∈ U(1) ≅ SO(2)：
/// 只支持旋转乘法与反相共轭（不支持加法，因为单位圆对加法不封闭）。
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
    public ExactComplex ToComplex() => new(Cos, Sin);

    /// <summary>相位复合：e^(iα) · e^(iβ) = e^(i(α+β))。</summary>
    public static UnitPhase operator *(UnitPhase a, UnitPhase b) =>
        new(a.Cos * b.Cos - a.Sin * b.Sin, a.Sin * b.Cos + a.Cos * b.Sin);

    /// <summary>极坐标定位：r · e^(iθ) → 复坐标。</summary>
    public ExactComplex Scale(ExactScalar radius) => new(radius * Cos, radius * Sin);

    /// <summary>直接旋转一个齐次共形点（绕原点旋转 θ）。</summary>
    public HComplex Rotate(HComplex pt) => new(ToComplex() * pt.Z, pt.W);
}

/// <summary>
/// 3) 黎曼球面 CP¹ 上的齐次复点 (z = Z / W)：
/// 唯一的 2D 共形/反演/共圆几何点类型（全程零除法，天然包含无穷远点 ∞ = (1, 0)）。
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
        if (den.VerifyStrictZero() == StrictResult.StrictlyTrue) return StrictResult.StrictlyFalse;
        return (num * den.Conjugate()).Im.VerifyStrictZero();
    }

    /// <summary>仿射中点（假定两点均非无穷远 W != 0）。</summary>
    public static HComplex Midpoint(HComplex a, HComplex b) =>
        new(a.Z * b.W + b.Z * a.W, 2 * a.W * b.W);
}

public static class ComplexGeometryExperiment
{
    public static void Run()
    {
        Console.WriteLine("\n================ 复扩展整理版：共形 CP¹ + e^(iθ) + 射影 CP² ================");

        // [1] 零除法反演 + 共圆（全部 StrictlyTrue）
        Console.WriteLine("\n[1] 齐次 CP¹ 严证圆反演保角题（零除法）：");
        var u = ExactScalar.FromSymbol("u", 0.3);
        var e = HComplex.From(ExactComplex.Zero);
        var p = HComplex.From(new ExactComplex(0, 2));
        var pPrime = HComplex.Invert(p, ExactComplex.Zero, 16);

        var a = new HComplex(4, new ExactComplex(1, -u));
        var b = new HComplex(4, new ExactComplex(1, -(2 - u)));
        var aPrime = HComplex.Invert(a, ExactComplex.Zero, 16);
        var bPrime = HComplex.Invert(b, ExactComplex.Zero, 16);
        var midPrime = HComplex.Midpoint(aPrime, bPrime);

        Console.WriteLine($"  P、A(u)、B(2-u) 共线？{HComplex.AreCollinear(p, a, b)}");
        Console.WriteLine($"  反演后 E、P'、A'、B' 严格共圆？{HComplex.AreConcyclicOrCollinear(e, pPrime, aPrime, bPrime)}");
        Console.WriteLine($"  A'B' 中点纵坐标恒为 4？" +
                          $"{((midPrime.Z * midPrime.W.Conjugate()).Im - 4 * midPrime.W.NormSquared()).VerifyStrictZero()}");

        // [2] e^(iθ) 相位算子与极坐标反演
        Console.WriteLine("\n[2] 单位相位 e^(iθ)（特殊角复合 + 半角极坐标反演）：");
        var pi = ExactScalar.FromFormula("pi");
        var rotSum = UnitPhase.FromAngle(pi / 6) * UnitPhase.FromAngle(pi / 3);
        var rot90 = UnitPhase.FromAngle(pi / 2);
        Console.WriteLine($"  e^(i·π/6) · e^(i·π/3) ≡ e^(i·π/2)？{(rotSum.ToComplex() - rot90.ToComplex()).VerifyStrictZero()}");

        var t = ExactScalar.FromSymbol("t", 0.7);
        var r = ExactScalar.FromSymbol("r", 5.0);
        var polarPt = HComplex.FromPolarHalfAngle(r, t);                 // r · e^(iθ)
        var polarInv = HComplex.Invert(polarPt, ExactComplex.Zero, 100); // 反演：极角不变，半径变 100/r
        Console.WriteLine($"  极坐标点与反演点共射线（零三角函数）？{HComplex.AreCollinear(e, polarPt, polarInv)}");

        // [3] CP² 复射影平面（依然是同一套 Primal3<T> / BilinearForm3<T>）
        Console.WriteLine("\n[3] 复射影 CP²：圆环点与 Plücker 焦点定理");
        var ptI = new Primal3<ExactComplex>(1, ExactComplex.I, 0);
        var ptJ = new Primal3<ExactComplex>(1, -ExactComplex.I, 0);
        var anyCircle = new BilinearForm3<ExactComplex>(1, 0, -3, 1, 5, 27);
        Console.WriteLine($"  任意圆过 I, J？{anyCircle.Eval(ptI).VerifyStrictZero()} / {anyCircle.Eval(ptJ).VerifyStrictZero()}");

        var ellipse = new BilinearForm3<ExactComplex>(9, 0, 0, 25, 0, -225);
        var isotropicLine = SubspaceAlgebra.Join(Primal3<ExactComplex>.Fixed(4, 0), ptI);
        Console.WriteLine($"  焦点 F=(4,0) 连向 I 的直线相切于椭圆？{ellipse.DualForm().Eval(isotropicLine).VerifyStrictZero()}");
    }
}