using Perspective.Core.Geometry;

namespace Perspective.Core.Math;

/// <summary>
/// 3x3 射影/仿射变换与二次型矩阵（全程携带双轨 ExactScalar）
/// </summary>
public readonly record struct Matrix3x3(
    ExactScalar M00, ExactScalar M01, ExactScalar M02,
    ExactScalar M10, ExactScalar M11, ExactScalar M12,
    ExactScalar M20, ExactScalar M21, ExactScalar M22)
{
    public static Matrix3x3 Identity => new(
        1, 0, 0,
        0, 1, 0,
        0, 0, 1);

    // --- 常用构造工厂 ---

    public static Matrix3x3 Translation(ExactScalar dx, ExactScalar dy) => new(
        1, 0, dx,
        0, 1, dy,
        0, 0, 1);

    /// <summary>缩放/仿射压缩矩阵（例如把椭圆 x^2/a^2 + y^2/b^2 = 1 压成单位圆）</summary>
    public static Matrix3x3 Scale(ExactScalar sx, ExactScalar sy) => new(
        sx, 0, 0,
        0, sy, 0,
        0, 0, 1);

    /// <summary>精确角度旋转（传入 cos(theta), sin(theta) 的精确公式，例如 "sqrt(3)/2", "1/2"）</summary>
    public static Matrix3x3 Rotation(ExactScalar cosTheta, ExactScalar sinTheta) => new(
        cosTheta, -sinTheta, 0,
        sinTheta,  cosTheta, 0,
        0,         0,        1);

    // --- 基础矩阵代数 ---

    public Matrix3x3 Transpose() => new(
        M00, M10, M20,
        M01, M11, M21,
        M02, M12, M22);

    /// <summary>伴随矩阵 adj(M)：代数余子式的转置</summary>
    public Matrix3x3 Adjugate() => new(
        M11 * M22 - M12 * M21, M02 * M21 - M01 * M22, M01 * M12 - M02 * M11,
        M12 * M20 - M10 * M22, M00 * M22 - M02 * M20, M02 * M10 - M00 * M12,
        M10 * M21 - M11 * M20, M01 * M20 - M00 * M21, M00 * M11 - M01 * M10);

    public ExactScalar Determinant() =>
        M00 * (M11 * M22 - M12 * M21)
      + M01 * (M12 * M20 - M10 * M22)
      + M02 * (M10 * M21 - M11 * M20);

    public Matrix3x3 Inverse()
    {
        var det = Determinant();
        // 奇异判定优先走严通道：行列式符号上为 0 时必定不可逆。
        // 数值容差仅作为无符号数据时的兜底，避免漏判“符号为 0 但探针值非零”的退化矩阵。
        if (det.VerifyStrictZero() == StrictResult.StrictlyTrue || det.IsNumericallyZero())
            throw new InvalidOperationException("变换矩阵奇异（行列式为 0），不可逆！");

        var adj = Adjugate();
        return new Matrix3x3(
            adj.M00 / det, adj.M01 / det, adj.M02 / det,
            adj.M10 / det, adj.M11 / det, adj.M12 / det,
            adj.M20 / det, adj.M21 / det, adj.M22 / det).Simplified();
    }

    // --- 矩阵乘法重载 ---

    public static Matrix3x3 operator *(Matrix3x3 a, Matrix3x3 b) => new(
        a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20,
        a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21,
        a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22,

        a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20,
        a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21,
        a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22,

        a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20,
        a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21,
        a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22);

    public static HPoint operator *(Matrix3x3 m, HPoint p) => new(
        m.M00 * p.X + m.M01 * p.Y + m.M02 * p.W,
        m.M10 * p.X + m.M11 * p.Y + m.M12 * p.W,
        m.M20 * p.X + m.M21 * p.Y + m.M22 * p.W);

    public static HLine operator *(Matrix3x3 m, HLine l) => new(
        m.M00 * l.A + m.M01 * l.B + m.M02 * l.C,
        m.M10 * l.A + m.M11 * l.B + m.M12 * l.C,
        m.M20 * l.A + m.M21 * l.B + m.M22 * l.C);

    // --- 视角变换核心接口 ---

    /// <summary>把点变换到新视角：p' = M * p</summary>
    public HPoint Transform(HPoint p) => this * p;

    /// <summary>把直线变换到新视角：l' = (M^-1)^T * l</summary>
    public HLine Transform(HLine l) => Inverse().Transpose() * l;

    /// <summary>把圆锥曲线变换到新视角：C' = (M^-1)^T * C * M^-1</summary>
    public Conic Transform(Conic c)
    {
        var inv = Inverse();
        return new Conic(inv.Transpose() * c.Matrix * inv);
    }

    /// <summary>判断当前变换是否属于仿射变换（第三行为 [0, 0, c]）</summary>
    public bool IsAffine() => M20.IsNumericallyZero() && M21.IsNumericallyZero() && !M22.IsNumericallyZero();
    
    /// <summary>对矩阵的 9 个元素做一次化简瘦身</summary>
    public Matrix3x3 Simplified() => new(
        M00.Simplified(), M01.Simplified(), M02.Simplified(),
        M10.Simplified(), M11.Simplified(), M12.Simplified(),
        M20.Simplified(), M21.Simplified(), M22.Simplified());
}