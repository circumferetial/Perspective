using AngouriMath;
using Perspective.Core.Math;

namespace Perspective.Core.Geometry;

/// <summary>
/// 二次曲线的仿射/相似分类。判别只依赖矩阵不变量，不依赖坐标表示。
/// </summary>
public enum ConicKind
{
    Ellipse,                // 实椭圆（非圆）
    Circle,                 // 圆（λ₁ = λ₂）
    Hyperbola,              // 双曲线
    Parabola,               // 抛物线
    Point,                  // 退化为一个实点
    TwoIntersectingLines,   // 退化为两条相交实直线
    TwoParallelLines,       // 退化为两条平行实直线
    DoubleLine,             // 退化为一条重直线
    Empty,                  // 无实点（虚椭圆 / 两虚平行线）
    Unknown                 // 含近似数据或符号表达式无法严格定号
}

/// <summary>
/// 分类结果。<see cref="MeaningfulAt"/> 表示该分类断言在哪个视角层级仍有意义：
/// 椭圆/双曲/抛物只在 Affine 群内可区分（射影视角下它们同构），圆需要 Similarity，
/// 退化判定在 Projective 下也成立。
/// </summary>
public readonly record struct ConicClassification(ConicKind Kind, TransformLevel MeaningfulAt);

/// <summary>
/// 主轴与半轴。半轴以**平方**给出，因为双曲线的"半轴"可能是虚的；
/// 方向用齐次无穷远点（W=0）表示，避免引入浮点角度。
/// </summary>
public readonly record struct ConicAxes(
    HPoint Center,
    HPoint Axis1Direction,
    HPoint Axis2Direction,
    ExactScalar SemiAxis1Squared,
    ExactScalar SemiAxis2Squared,
    bool IsCircle);

/// <summary>
/// 圆锥曲线相关的通用判定工具（严格通道）。
/// 点是否在曲线上已有 <see cref="Conic.ContainsStrictly"/>，本类补"曲线—直线"的组合谓词
/// 与曲线分类，供定理规则、裁决器与发现引擎共用，避免同一条判据多处实现而漂移。
/// </summary>
public static class ConicTools
{
    /// <summary>
    /// 直线是否严格切于圆锥曲线。
    /// 【判据】判别式 lᵀ·adj(C)·l 严格为零 ⟺ 相切（符号约定见 <see cref="Conic.CheckLineRelation"/>）。
    /// 【返回】相切 → StrictlyTrue；确定为相交/相离 → StrictlyFalse；判别式无法严格化简 → 原样降级。
    /// </summary>
    public static StrictResult IsTangent(Conic conic, HLine line)
    {
        var check = conic.CheckLineRelation(line);
        if (check.Relation == LineConicRelation.DegenerateConic)
            return StrictResult.StrictlyFalse;
        if (check.Certainty == StrictResult.StrictlyTrue)
            return check.Relation == LineConicRelation.Tangent
                ? StrictResult.StrictlyTrue
                : StrictResult.StrictlyFalse; // 确知是相交/相离 => 确定不相切
        if (check.Certainty == StrictResult.StrictlyFalse)
            return StrictResult.StrictlyFalse;
        return check.Certainty; // NearZeroUnproven / NoSymbolicData
    }

    /// <summary>
    /// 直线 line 是否恰为点 pole 关于 conic 的极线。
    /// 【判据】line 与 C·pole 成比例 ⟺ 两者叉积全为零（<see cref="LineTools.Same"/>）。
    /// 【用途】定理规则回验 PolarOfFact 标签、裁决器验证 ClaimPolarOf，共用同一判据。
    /// </summary>
    public static StrictResult IsPolarLine(Conic conic, HPoint pole, HLine line) =>
        LineTools.Same(conic.PolarLine(pole), line);

    // ========================================================================
    // 分类：δ = M₀₀M₁₁ − M₀₁²，Δ = det C
    // 【前提】C 为实二次曲线矩阵（可含退化）。
    // 【结论】按 sign(δ)、sign(Δ) 与秩联合判类，见下方推导。
    // 【推导】
    //   非退化（Δ≠0）：δ>0 椭圆型（Δ<0 实椭圆；δ>0 且 tr²=4δ 时为圆）；
    //                 δ<0 双曲线；δ=0 抛物线。
    //   退化（Δ=0）：秩 1 → 重线；秩 2 且 δ>0 → 一点；δ<0 → 两条相交实直线；
    //                 δ=0 → 平行线族，看 T = (D u+E v)² − F(a+c)²：
    //                 T>0 两实平行线，T=0 重线，T<0 空（两虚平行线），
    //                 其中 Q=(u x+v y)²（a=u²,b=uv,c=v²），已符号验证。
    // 【等级】椭圆/双曲/抛物 = Affine；圆 = Similarity；退化 = Projective。
    // ========================================================================
    public static ConicClassification Classify(Conic conic)
    {
        var m = conic.Matrix;
        var delta = (m.M00 * m.M11 - m.M01 * m.M01).Simplified();
        var det = m.Determinant().Simplified();

        int? sDelta = TrySign(delta);
        int? sDet = TrySign(det);
        if (sDelta is null || sDet is null)
            return new ConicClassification(ConicKind.Unknown, TransformLevel.Projective);

        if (sDet != 0)
        {
            if (sDelta == 0)
                return new ConicClassification(ConicKind.Parabola, TransformLevel.Affine);

            if (sDelta > 0)
            {
                var trace = m.M00 + m.M11;
                bool isCircle = (trace * trace - 4 * delta).VerifyStrictZero() == StrictResult.StrictlyTrue;
                return isCircle
                    ? new ConicClassification(ConicKind.Circle, TransformLevel.Similarity)
                    : new ConicClassification(ConicKind.Ellipse, TransformLevel.Affine);
            }

            return new ConicClassification(ConicKind.Hyperbola, TransformLevel.Affine);
        }

        // Δ = 0：退化。先看秩（adj 是否全零 ⇒ 秩 ≤ 1）
        var adj = m.Adjugate();
        bool rankOne = AllStrictlyZero(
            adj.M00, adj.M01, adj.M02, adj.M10, adj.M11, adj.M12, adj.M20, adj.M21, adj.M22);
        if (rankOne)
        {
            bool zeroMatrix = AllStrictlyZero(
                m.M00, m.M01, m.M02, m.M10, m.M11, m.M12, m.M20, m.M21, m.M22);
            return zeroMatrix
                ? new ConicClassification(ConicKind.Unknown, TransformLevel.Projective)
                : new ConicClassification(ConicKind.DoubleLine, TransformLevel.Projective);
        }

        if (sDelta > 0)
            return new ConicClassification(ConicKind.Point, TransformLevel.Projective);
        if (sDelta < 0)
            return new ConicClassification(ConicKind.TwoIntersectingLines, TransformLevel.Projective);

        // δ = 0 且秩 2：平行线族，T = (M₀₂²M₀₀ + 2M₀₂M₁₂M₀₁ + M₁₂²M₁₁) − M₂₂(M₀₀+M₁₁)²
        var t = (m.M02 * m.M02 * m.M00
               + 2 * m.M02 * m.M12 * m.M01
               + m.M12 * m.M12 * m.M11
               - m.M22 * (m.M00 + m.M11) * (m.M00 + m.M11)).Simplified();

        int? sT = TrySign(t);
        return sT switch
        {
            > 0 => new ConicClassification(ConicKind.TwoParallelLines, TransformLevel.Projective),
            0 => new ConicClassification(ConicKind.DoubleLine, TransformLevel.Projective),
            < 0 => new ConicClassification(ConicKind.Empty, TransformLevel.Projective),
            _ => new ConicClassification(ConicKind.Unknown, TransformLevel.Projective)
        };
    }

    /// <summary>
    /// 主轴与半轴（仅对有唯一中心且非退化的二次曲线有定义）。
    /// 【前提】δ ≠ 0（有心圆锥曲线；抛物线无有限中心）且 Δ ≠ 0（非退化）。
    /// 【结论】中心 O = adj(C)·(0,0,1)/δ；二次部分特征值 λ± = (tr ± √(tr²−4δ))/2；
    ///         半轴平方 = −c'/λ±，其中 c' = OᵀCO；主轴方向取特征向量 (M₀₁, λ−M₀₀)。
    /// 【推导】把原点平移到中心后，方程化为 Xᵀ C₂ X + c' = 0；C₂ 对称可正交对角化，
    ///         沿主轴方向化为 λ₁u² + λ₂v² + c' = 0 ⇒ 半轴² = −c'/λ。
    ///         方向：C₂ v = λv ⇒ (M₀₀−λ)x + M₀₁y = 0，取 v = (M₀₁, λ−M₀₀)。
    /// 【说明】半轴以平方返回（双曲线有一支为虚）；方向为齐次无穷远点，避免浮点角度。
    ///         以上各式均已符号验证（旋转椭圆与平移椭圆各一例）。
    /// </summary>
    public static ConicAxes? PrincipalAxes(Conic conic)
    {
        var m = conic.Matrix;
        var delta = (m.M00 * m.M11 - m.M01 * m.M01).Simplified();

        if (TrySign(delta) is null or 0) return null;                 // 抛物线 / 退化
        if (TrySign(m.Determinant().Simplified()) is null or 0) return null;

        // 中心 = adj(C)·(0,0,1)，其 W 分量恰为 δ
        var adj = m.Adjugate();
        var center = HPoint.FromCartesian(
            (adj.M02 / delta).Simplified(),
            (adj.M12 / delta).Simplified());

        // c' = OᵀCO
        var cPrime = (center.X * (m.M00 * center.X + m.M01 * center.Y + m.M02 * center.W)
                    + center.Y * (m.M10 * center.X + m.M11 * center.Y + m.M12 * center.W)
                    + center.W * (m.M20 * center.X + m.M21 * center.Y + m.M22 * center.W)).Simplified();

        var trace = (m.M00 + m.M11).Simplified();
        var disc = (trace * trace - 4 * delta).Simplified();
        var root = ExactScalar.Sqrt(disc);                            // δ≠0 且非退化 ⇒ 实特征值
        var lambda1 = ((trace + root) / 2).Simplified();
        var lambda2 = ((trace - root) / 2).Simplified();

        var semi1Sq = ((-cPrime) / lambda1).Simplified();
        var semi2Sq = ((-cPrime) / lambda2).Simplified();

        var dir1 = EigenDirection(m, lambda1);
        var dir2 = EigenDirection(m, lambda2);
        bool isCircle = disc.VerifyStrictZero() == StrictResult.StrictlyTrue;

        // 圆的两个特征值相同、特征向量不唯一：给一组标准正交方向，避免两个方向重复
        if (isCircle)
        {
            dir1 = new HPoint(1, 0, 0);
            dir2 = new HPoint(0, 1, 0);
        }

        return new ConicAxes(center, dir1, dir2, semi1Sq, semi2Sq, isCircle);
    }

    /// <summary>特征向量方向（齐次无穷远点）：C₂ = [[M₀₀,M₀₁],[M₀₁,M₁₁]]，v = (M₀₁, λ−M₀₀)。</summary>
    private static HPoint EigenDirection(Matrix3x3 m, ExactScalar lambda)
    {
        var vx = m.M01;
        var vy = (lambda - m.M00).Simplified();

        // M₀₁ = 0 时上式退化为零向量，此时主轴就是坐标轴：λ = M₀₀ → x 轴，否则 y 轴
        if (vx.VerifyStrictZero() == StrictResult.StrictlyTrue
            && vy.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return (lambda - m.M00).VerifyStrictZero() == StrictResult.StrictlyTrue
                ? new HPoint(1, 0, 0)
                : new HPoint(0, 1, 0);

        return new HPoint(vx, vy, 0);
    }

    /// <summary>严格判定一组数是否全为零（用于矩阵秩/零矩阵判定）。</summary>
    private static bool AllStrictlyZero(params ExactScalar[] values)
    {
        foreach (var v in values)
            if (v.VerifyStrictZero() != StrictResult.StrictlyTrue)
                return false;
        return true;
    }

    /// <summary>
    /// 严格定号：0 / +1 / −1；无法严格判定时返回 null（绝不猜）。
    /// 只有能化简为具体实数时才定号，复杂符号表达式一律判"未知"。
    /// </summary>
    private static int? TrySign(ExactScalar value)
    {
        if (value.VerifyStrictZero() == StrictResult.StrictlyTrue) return 0;
        if (value.Exact is null) return null;          // 纯近似数据：不判

        var simplified = value.Exact.Simplify();
        if (simplified is not Entity.Number number) return null;

        double v = (double)number.EvalNumerical();
        return v > 0 ? 1 : (v < 0 ? -1 : 0);
    }
}
