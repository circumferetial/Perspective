using System.Numerics;
using AngouriMath;
using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Experiment;

/// <summary>
///     实验 P2：Cayley-Klein 视角——从射影平面"自上而下"造出欧氏几何。
///     <para>
///         在无穷远直线 ℓ∞ 上钦定一对复共轭点（圆环点）I=(1,i,0)、J=(1,−i,0)，
///         欧氏几何里"天经地义"的概念全部塌缩成纯射影关系：
///     </para>
///     <list type="number">
///         <item>圆 = 恰好过 I、J 的普通圆锥曲线；</item>
///         <item>垂直 = 两线无穷远点 P,Q 满足交比 (P,Q;I,J) = −1（调和共轭）；</item>
///         <item>角度 = Laguerre 公式 θ = (1/2i)·ln(l₁,l₂;l_I,l_J)，交比的对数；</item>
///         <item>根轴 = 两圆方程相减（消去 x²+y²）剩下的直线；</item>
///         <item>度量 = 绝对形 Ω=diag(1,1,0) 的二次型，距离平方 = (P−Q)ᵀΩ(P−Q)。</item>
///     </list>
///     <para>全程不调用 Sqrt / 点积公式，只用交比、圆锥曲线矩阵与 diag(1,1,0)。</para>
/// </summary>
public static class CayleyKlein
{
    /// <summary>虚数单位 i。AngouriMath 的字符串 "I" 是符号而非虚数单位，必须用 Complex 隐式转换。</summary>
    private static readonly ExactScalar I = new(0.0, new Complex(0, 1));

    /// <summary>圆环点 I = (1, i, 0)。</summary>
    private static readonly HPoint Ipt = new(ExactScalar.FromInt(1), I, ExactScalar.FromInt(0));

    /// <summary>圆环点 J = (1, −i, 0)。</summary>
    private static readonly HPoint Jpt = new(ExactScalar.FromInt(1), -I, ExactScalar.FromInt(0));

    public static void Run()
    {
        Console.WriteLine("\n=== P2：Cayley-Klein——用圆环点 I,J 与绝对形 Ω 造欧氏几何 ===");

        Console.WriteLine($"  圆环点 I = {Fmt(Ipt)}，J = {Fmt(Jpt)}（ℓ∞ 上的一对复共轭点）");

        CircleAsConicThroughCircularPoints();
        PerpendicularityViaCrossRatio();
        LaguerreAngle();
        RadicalAxisBySubtraction();
        AbsoluteAndMetric();
    }

    // ---------- 1) 圆 = 过 I, J 的圆锥曲线 ----------

    private static void CircleAsConicThroughCircularPoints()
    {
        Console.WriteLine("\n--- 1) 圆 = 过 I, J 的圆锥曲线 ---");

        var circle = Conic.Circle(0, 0, ExactScalar.FromInt(1));
        var vI = ExperimentMath.QuadraticValue(circle, Ipt);
        var vJ = ExperimentMath.QuadraticValue(circle, Jpt);
        Console.WriteLine($"  单位圆在 I 处取值 = {vI.Simplified()}（符号判零？{IsZeroSymbolic(vI)}）；" +
                          $"在 J 处取值 = {vJ.Simplified()}（符号判零？{IsZeroSymbolic(vJ)}）");

        // 一般圆锥曲线在 I 处的取值：M00 + 2i·M01 − M11
        // 注意：符号名不能用 "m00" 之类（会被解析成 m^00），用单字母。
        var u = ExactScalar.FromSymbol("u");// M00
        var v = ExactScalar.FromSymbol("v");// M01
        var w = ExactScalar.FromSymbol("w");// M11
        var val = u + 2 * v * I + w * I * I;// = u − w + 2i·v
        Console.WriteLine($"  一般圆锥曲线在 I 处取值 = {val.Simplified()}");
        Console.WriteLine($"    实部 = {MathS.Re(val.Exact!).Simplify()}，虚部 = {MathS.Im(val.Exact!).Simplify()}");
        Console.WriteLine("  → 过 I,J ⟺ M00 = M11 且 M01 = 0，正是圆的方程条件。");

        // 两圆交点：2 个实交点 + I, J（Bézout 2×2=4）
        var c1 = Conic.Circle(0, 0, ExactScalar.FromInt(1));
        var c2 = Conic.Circle(ExactScalar.FromInt(1), ExactScalar.FromInt(0), ExactScalar.FromInt(1));
        Console.WriteLine("\n  两圆 C1(原点,1)、C2((1,0),1)：");
        Console.WriteLine($"    C1 在 I 处 = {ExperimentMath.QuadraticValue(c1, Ipt).Simplified()}，" +
                          $"C2 在 I 处 = {ExperimentMath.QuadraticValue(c2, Ipt).Simplified()}（均符号为 0）");
        var ra = Conic.RadicalAxis(c1, c2);
        Console.WriteLine($"    根轴（相减所得直线）= {ExperimentMath.Fmt(ra)}");
        Console.WriteLine("    → 两圆至多 2 个实交点，另外 2 个交点恒为 I、J（共 4 个）。");
    }

    // ---------- 2) 垂直 = 交比 (P,Q;I,J) = −1 ----------

    private static void PerpendicularityViaCrossRatio()
    {
        Console.WriteLine("\n--- 2) 垂直 = 交比 (P,Q;I,J) = −1 ---");

        var Omega = new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 0);// 绝对形 diag(1,1,0)
        var cases = new (string Label, HLine L1, HLine L2)[]
        {
            ("x=0 与 y=0", new HLine(1, 0, 0), new HLine(0, 1, 0)),
            ("x=0 与 x+y=0", new HLine(1, 0, 0), new HLine(1, 1, 0)),
            ("x+y=0 与 x−y=0", new HLine(1, 1, 0), new HLine(1, -1, 0)),
            ("2x+3y=0 与 3x−2y=0", new HLine(2, 3, 0), new HLine(3, -2, 0))
        };

        foreach (var (label, l1, l2) in cases)
        {
            var p1 = new HPoint(-l1.B, l1.A, ExactScalar.FromInt(0));
            var p2 = new HPoint(-l2.B, l2.A, ExactScalar.FromInt(0));
            var cr = GeometryAudit.CrossRatio(p1, p2, Ipt, Jpt);
            var harmonic = IsZeroSymbolic(cr + ExactScalar.FromInt(1));

            // 用绝对形直接判：l1ᵀΩl2 = A1A2 + B1B2
            var viaOmega = l1.A * l2.A + l1.B * l2.B;

            Console.WriteLine($"  {label}：");
            Console.WriteLine($"    交比 (P1,P2;I,J) = {cr.Simplified()}，+1 严格为 0？{harmonic}");
            Console.WriteLine($"    l1ᵀΩl2 = {viaOmega.Simplified()}（Ω=diag(1,1,0)）");
            Console.WriteLine($"    GeometryAudit.ArePerpendicular = {GeometryAudit.ArePerpendicular(l1, l2)}");
        }
        Console.WriteLine("  → 交比 = −1、l1ᵀΩl2 = 0、点积判据三者完全等价。");
    }

    // ---------- 3) 角度 = Laguerre 公式 ----------

    private static void LaguerreAngle()
    {
        Console.WriteLine("\n--- 3) 角度 = Laguerre 公式 θ = (1/2i)·ln(l₁,l₂;l_I,l_J) ---");

        // 交比 (l1,l2;lI,lJ) = (P1,P2;I,J)，其中 P1,P2 为两线方向。
        // 对斜率 m1,m2 的两线：CR = (m1−i)(m2+i) / ((m2−i)(m1+i))，是单位复数，幅角 = ±2θ。
        ExactScalar Cr(ExactScalar m1, ExactScalar m2)
        {
            return (m1 - I) * (m2 + I) / ((m2 - I) * (m1 + I));
        }

        var cases = new (string Label, ExactScalar Slope1, ExactScalar Slope2, double ExpectedDeg)[]
        {
            ("x 轴 与 y=x", ExactScalar.FromInt(0), ExactScalar.FromInt(1), 45),
            ("x 轴 与 30° 线", ExactScalar.FromInt(0), ExactScalar.FromFormula("1/sqrt(3)"), 30),
            ("y=x 与 y=−x", ExactScalar.FromInt(1), ExactScalar.FromInt(-1), 90),
            ("x 轴 与 60° 线", ExactScalar.FromInt(0), ExactScalar.FromFormula("sqrt(3)"), 60)
        };

        foreach (var (label, m1, m2, expected) in cases)
        {
            var cr = Cr(m1, m2);
            var crEntity = cr.Exact!;
            var re = (double)MathS.Re(crEntity).EvalNumerical();
            var im = (double)MathS.Im(crEntity).EvalNumerical();
            var thetaDeg = Math.Abs(Math.Atan2(im, re)) / 2.0 * 180.0 / Math.PI;

            Console.WriteLine($"  {label}（斜率 {m1:G4} 与 {m2:G4}）：");
            Console.WriteLine($"    交比 = {cr.Simplified()}，|幅角|/2 = {thetaDeg:F4}°（期望 {expected}°）");
        }
        Console.WriteLine("  → 角度是交比（射影不变量）的对数，连角度都是射影量的马甲。");
    }

    // ---------- 4) 根轴 = 两圆相减 ----------

    private static void RadicalAxisBySubtraction()
    {
        Console.WriteLine("\n--- 4) 根轴 = 两圆方程相减（消去 x²+y²）---");

        var c1 = Conic.Circle(0, 0, ExactScalar.FromInt(1));
        var c2 = Conic.Circle(ExactScalar.FromInt(1), ExactScalar.FromInt(0), ExactScalar.FromInt(1));
        var ra = Conic.RadicalAxis(c1, c2);
        Console.WriteLine($"  C1 = {c1.ToEquationString()}");
        Console.WriteLine($"  C2 = {c2.ToEquationString()}");
        Console.WriteLine($"  根轴 = {ExperimentMath.Fmt(ra)}");

        // 验证：根轴上的点到两圆的幂相等（用圆锥曲线二次型值比较）
        var test = HPoint.FromCartesian(ExactScalar.FromFormula("1/2"), ExactScalar.FromFormula("0"));
        var v1 = ExperimentMath.QuadraticValue(c1, test);
        var v2 = ExperimentMath.QuadraticValue(c2, test);
        Console.WriteLine($"  根轴上点 (1/2, 0)：C1 取值 = {v1.Simplified()}，C2 取值 = {v2.Simplified()}" +
                          $"（相等？{IsZeroSymbolic(v1 - v2)}）");
        Console.WriteLine("  → 两圆相减消去二次项，剩下的线性方程就是根轴，无需开方。");
    }

    // ---------- 5) 绝对形 Ω 与度量 ----------

    private static void AbsoluteAndMetric()
    {
        Console.WriteLine("\n--- 5) 绝对形 Ω 与对偶 Ω*，度量来自 Ω 的二次型 ---");

        var Omega = new Matrix3x3(1, 0, 0, 0, 1, 0, 0, 0, 0);
        var OmegaStar = Omega.Adjugate();
        Console.WriteLine("  Ω = diag(1,1,0)（圆环点对，即绝对形）");
        Console.WriteLine($"  Ω* = adj(Ω) = diag({OmegaStar.M00}, {OmegaStar.M11}, {OmegaStar.M22})" +
                          "（对偶绝对形 = 无穷远直线）");

        // 距离平方 = (P−Q)ᵀΩ(P−Q)，不调用 Sqrt / 点积
        var p = HPoint.FromCartesian(ExactScalar.FromInt(1), ExactScalar.FromInt(2));
        var q = HPoint.FromCartesian(ExactScalar.FromInt(4), ExactScalar.FromInt(6));
        var dx = p.X - q.X;
        var dy = p.Y - q.Y;
        var d2 = Omega.M00 * dx * dx + 2 * Omega.M01 * dx * dy + Omega.M11 * dy * dy;

        Console.WriteLine("  P=(1,2)、Q=(4,6)：");
        Console.WriteLine($"    (P−Q)ᵀΩ(P−Q) = {d2.Simplified()}");
        Console.WriteLine($"    Metric.DistanceSquared = {Metric.DistanceSquared(p, q).Simplified()}（对照）");
        Console.WriteLine("  → 欧氏度量不是额外结构，而是绝对形 Ω 的二次型。");
    }

    // ---------- 工具 ----------

    /// <summary>复数/符号表达式判零：直接走符号通道（Approx 门控对复数不可靠）。</summary>
    private static bool IsZeroSymbolic(ExactScalar s)
    {
        if (s.Exact is null) return false;
        return s.Exact.Simplify().Equals(Entity.Number.Integer.Zero);
    }

    private static string Fmt(HPoint p)
    {
        static string S(ExactScalar s)
        {
            return s.Exact is not null ? s.Exact.Simplify().ToString() : s.Approx.ToString("G6");
        }

        return $"({S(p.X)}, {S(p.Y)}, {S(p.W)})";
    }
}