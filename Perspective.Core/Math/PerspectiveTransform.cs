using Perspective.Core.Geometry;

namespace Perspective.Core.Math;

/// <summary>
/// 几何变换层级（从严格到自由，层级越靠下，保持的性质越少）
/// </summary>
public enum TransformLevel
{
    Isometry,   // 等距变换（平移/旋转/翻折）：保持长度、角度、垂直、面积、圆
    Similarity, // 相似变换（等比缩放）：保持角度、垂直、线段比、圆；不保绝对长度
    Affine,     // 仿射变换（椭圆压成圆/错切）：保持平行、中点、面积比；不保垂直、角度、焦点
    Projective, // 射影变换（透视/四点映射）：只保共线、相切、交比、极点极线；不保平行与中点
    Singular    // 退化/不可逆变换
}

public static class PerspectiveTransform
{
    /// <summary>
    /// 自动鉴定一个 3x3 变换矩阵属于哪一级几何视角
    /// </summary>
    public static TransformLevel Classify(Matrix3x3 m)
    {
        if (m.Determinant().IsNumericallyZero())
            return TransformLevel.Singular;

        // 第三行前两项不为 0，说明无穷远直线被拉到了有限处 -> 射影变换
        if (!m.M20.IsNumericallyZero() || !m.M21.IsNumericallyZero())
            return TransformLevel.Projective;

        // 否则是仿射及以上。把左上角 2x2 矩阵除以 m.M22 归一化
        var w = m.M22;
        var a = m.M00 / w; var b = m.M01 / w;
        var c = m.M10 / w; var d = m.M11 / w;

        // 检查两列是否正交 (ab + cd == 0) 且长度相等 (a^2 + c^2 == b^2 + d^2)
        bool orthogonal = (a * b + c * d).IsNumericallyZero();
        var lenSq1 = a * a + c * c;
        var lenSq2 = b * b + d * d;
        bool equalScale = (lenSq1 - lenSq2).IsNumericallyZero();

        if (!orthogonal || !equalScale)
            return TransformLevel.Affine;

        // 若两列长度都严格等于 1，则是等距变换，否则是相似变换
        return (lenSq1 - 1).IsNumericallyZero()
            ? TransformLevel.Isometry
            : TransformLevel.Similarity;
    }

    /// <summary>
    /// 【仿射三点法】把任意不共线的 3 个点 (p1, p2, p3) 映射到目标 3 点 (q1, q2, q3)
    /// 常见用途：把斜坐标系、平行四边形直接摆正成直角坐标轴！
    /// </summary>
    public static Matrix3x3 FromAffine3Points(
        HPoint p1, HPoint p2, HPoint p3,
        HPoint q1, HPoint q2, HPoint q3)
    {
        // 先把点规范到 W = 1 的仿射平面（不能拿无穷远点做仿射三点基）
        static (ExactScalar x, ExactScalar y) Norm(HPoint pt) =>
            pt.IsAtInfinity
                ? throw new ArgumentException("仿射三点映射不能使用无穷远点。")
                : (pt.X / pt.W, pt.Y / pt.W);

        var (px1, py1) = Norm(p1); var (px2, py2) = Norm(p2); var (px3, py3) = Norm(p3);
        var (qx1, qy1) = Norm(q1); var (qx2, qy2) = Norm(q2); var (qx3, qy3) = Norm(q3);

        var P = new Matrix3x3(px1, px2, px3, py1, py2, py3, 1, 1, 1);
        var Q = new Matrix3x3(qx1, qx2, qx3, qy1, qy2, qy3, 1, 1, 1);
        return (Q * P.Inverse()).Simplified();
    }

    /// <summary>
    /// 【射影四点法】把任意 4 个点（无三点共线）映射到目标 4 个点
    /// 常见用途：把任意四边形拉成正方形，或把特定交点送到无穷远处！
    /// </summary>
    public static Matrix3x3 FromProjective4Points(
        HPoint p1, HPoint p2, HPoint p3, HPoint p4,
        HPoint q1, HPoint q2, HPoint q3, HPoint q4)
    {
        static Matrix3x3 MapFromStandardBasis(HPoint a, HPoint b, HPoint c, HPoint d)
        {
            var basis = new Matrix3x3(
                a.X, b.X, c.X,
                a.Y, b.Y, c.Y,
                a.W, b.W, c.W);

            // 求解 basis * (l1, l2, l3)^T = d，用 3x3 逆矩阵即可，完全避免解 8x8 方程组！
            var scales = basis.Inverse() * d;
            return new Matrix3x3(
                scales.X * a.X, scales.Y * b.X, scales.W * c.X,
                scales.X * a.Y, scales.Y * b.Y, scales.W * c.Y,
                scales.X * a.W, scales.Y * b.W, scales.W * c.W);
        }

        var stdToP = MapFromStandardBasis(p1, p2, p3, p4);
        var stdToQ = MapFromStandardBasis(q1, q2, q3, q4);
        return (stdToQ * stdToP.Inverse()).Simplified();
    }
}