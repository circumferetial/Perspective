using Perspective.Core.Math;

namespace Perspective.Core.Geometry;

/// <summary>
/// 极坐标 → 齐次点的**边界适配器**。
/// <para>
/// 【定位】极坐标不是线性坐标卡：二次曲线在 (r, θ) 下含根号与三角函数，不再是多项式，
/// 因此内部一律使用齐次坐标与矩阵；极坐标只用于输入/输出边界，转换后立刻丢弃角度。
/// </para>
/// <para>
/// 【结论】P(r, θ) = (r·cosθ, r·sinθ, 1)，其中 r 与三角函数都保留精确基因
/// （例如 r = 2, θ = π/4 得到 (√2, √2)，而不是 1.4142…）。
/// </para>
/// <para>
/// 【为什么不做 (x,y) → (r,θ) 的反向精确转换】反向需要 atan2，一般无闭式精确值，
/// 且角度多值（θ 与 θ+2π 表示同一点）、原点处角度未定义——这些正是极坐标不适合
/// 进入推理内环的原因。需要展示时用 <see cref="HPoint.ToCartesianApprox"/> 即可。
/// </para>
/// </summary>
public static class PolarCoordinates
{
    /// <summary>极坐标点 (r, θ) → 齐次点；θ 为精确弧度公式（如 "pi/4"）或精确数值。</summary>
    public static HPoint ToPoint(ExactScalar radius, ExactScalar theta) =>
        HPoint.FromCartesian(radius * ExactScalar.Cos(theta), radius * ExactScalar.Sin(theta));

    /// <summary>
    /// 已知精确 cosθ、sinθ 时用这个重载（特角场景），避免三角化简开销：
    /// 例如 θ = π/4 直接传 cos = sin = sqrt(2)/2。
    /// </summary>
    public static HPoint ToPoint(ExactScalar radius, ExactScalar cosTheta, ExactScalar sinTheta) =>
        HPoint.FromCartesian(radius * cosTheta, radius * sinTheta);
}
