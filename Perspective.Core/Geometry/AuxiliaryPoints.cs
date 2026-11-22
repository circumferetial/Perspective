using Perspective.Core.Math;

namespace Perspective.Core.Geometry;

/// <summary>
/// 辅助点构造：几何证明里的"辅助点"按自由度分三类，统一用符号坐标精确表示。
/// <para>
/// 【三类】固定点（0 自由度，具体坐标）／线上点（1 自由度，P₀ + t·d）／
/// 一般点（2 自由度，(s,t)）。曲线上点用有理参数化实现（过已知点作割线）。
/// </para>
/// <para>
/// 【语义】符号坐标 = "对参数恒成立（∀）"。因此用恒等式证出的结论对一般点成立
/// （genericity：成立在一个稠密开集上）。而"某一般点不在某条线上"这类**否定性前提
/// 不可证明**，必须作为非退化假设登记（见 <see cref="FactBase.AssumeNonZero"/>）。
/// </para>
/// </summary>
public static class AuxiliaryPoints
{
    /// <summary>固定点（0 自由度）。</summary>
    public static HPoint Fixed(ExactScalar x, ExactScalar y) => HPoint.FromCartesian(x, y);

    /// <summary>
    /// 一般点（2 自由度）：坐标取两个独立符号，"任意取一个点"。
    /// 对它的结论通过恒等式证明；"不在某线上"需另行假设。
    /// </summary>
    public static HPoint Generic(string tag, double probeX = 1.234567, double probeY = 2.345678) =>
        new(ExactScalar.FromSymbol($"x_{tag}", probeX), ExactScalar.FromSymbol($"y_{tag}", probeY), 1);

    /// <summary>
    /// 直线上的任意点（1 自由度）：P(t) = P₀ + t·d，其中 P₀ 是直线上离原点最近的点、
    /// d = (−B, A) 是方向。得到的点对 t 恒在直线上（ContainsStrictly 可严格验证）。
    /// </summary>
    public static HPoint OnLine(HLine line, string tag, double probeT = 0.731) =>
        OnLine(line, tag, probeT, out _);

    /// <summary>同上，并返回参数符号（供后续 NDG 或显式表达式使用）。</summary>
    public static HPoint OnLine(HLine line, string tag, double probeT, out ExactScalar parameter)
    {
        parameter = ExactScalar.FromSymbol($"t_{tag}", probeT);

        var n2 = line.A * line.A + line.B * line.B;
        // 直线上离原点最近的点（齐次）：(-A·C, -B·C, A²+B²)
        var p0 = new HPoint(-line.A * line.C, -line.B * line.C, n2);
        var direction = new HPoint(-line.B, line.A, 0);

        return new HPoint(
            p0.X + parameter * direction.X,
            p0.Y + parameter * direction.Y,
            p0.W + parameter * direction.W);
    }

    /// <summary>
    /// 圆锥曲线上的任意点（1 自由度）：以已知曲线点 knownPoint 为基点，
    /// 作过它的割线（斜率取符号 t），取另一个交点。要求 knownPoint 在曲线上。
    /// </summary>
    public static HPoint OnConic(Conic conic, HPoint knownPoint, string tag, double probeT = 0.618)
    {
        var t = ExactScalar.FromSymbol($"t_{tag}", probeT);
        var secant = HLine.FromPointAndSlope(knownPoint, t);
        return conic.OtherIntersection(knownPoint, secant);
    }
}
