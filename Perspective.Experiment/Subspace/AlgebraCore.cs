// ============================================================================
// AlgebraCore：唯一的一套泛型几何骨架
//
// 只允许依赖 IExactRing<T> 的 +、-、*（含一元 -）和 VerifyZero——
// 也就是"纯射影代数"能表达的全部东西。凡是要除法 /、Sqrt、或靠 Approx
// 挑数值稳定基点的操作，都不属于这里，见 ExactGeometryOps.cs。
//
// 注：IExactRing<T> 定义在 Perspective.Core.Math（ExactScalar.cs）。
// 不能把接口搬到本文件：ExactScalar 属于被引用的 Perspective.Core 程序集，
// 而 Core 不能反向引用 Experiment，接口只能留在 Core 侧。
// ============================================================================
using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

/// <summary>
///     原空间 V 中的向量（射影点 = 一维子空间，不带方向）。
///     <para>
///         它只是坐标三元组，不自动等于 <c>P²(T)</c> 的点。两种语义必须显式区分：
///         Path A（整环，例如 PolyExpr）：按 <c>P²(Frac(T))</c> 的代表元使用，
///         <c>Join == 0</c> 表示分式域上的比例等价，退化零点由证明层的
///         <c>RequireNonZero</c> 排除；Path B（严格 <c>P²(T)</c>）：要求 T 是域，
///         或先通过 <see cref="IsUnimodular" />/IsProjectivePoint 守卫。
///     </para>
/// </summary>
public readonly record struct Primal3<T>(T X, T Y, T W) where T : IExactRing<T>
{
    public static Primal3<T> Fixed(long x, long y, long w = 1) =>
        new(T.FromLong(x), T.FromLong(y), T.FromLong(w));

    public static Primal3<T> operator +(Primal3<T> a, Primal3<T> b) => new(a.X + b.X, a.Y + b.Y, a.W + b.W);
    public static Primal3<T> operator -(Primal3<T> a, Primal3<T> b) => new(a.X - b.X, a.Y - b.Y, a.W - b.W);
    public static Primal3<T> operator -(Primal3<T> a) => new(-a.X, -a.Y, -a.W);
    public static Primal3<T> operator *(T k, Primal3<T> v) => new(k * v.X, k * v.Y, k * v.W);
    public static Primal3<T> operator *(Primal3<T> v, T k) => k * v;

    public StrictResult VerifyZero() => SubspaceAlgebra.AllZero(X, Y, W);

    /// <summary>
    ///     幺模性的 sound 近似：至少一个坐标是已证单位 ⇒ StrictlyTrue；
    ///     零向量 ⇒ StrictlyFalse；其余 ⇒ Unknown。
    ///     <para>
    ///         在域上这等价于非零；在局部环上“无单位坐标”其实就非幺模，但通用环上
    ///         判定理想 (X,Y,W)=R 需要理想成员判定，不能在这里 O(1) 完成。
    ///         Path A 的 PolyExpr 代表元不应依赖此判定。
    ///     </para>
    /// </summary>
    public StrictVerdict IsUnimodular()
    {
        if (VerifyZero() == StrictResult.StrictlyTrue)
            return StrictVerdict.StrictlyFalse;

        if (T.IsUnit(X) == StrictVerdict.StrictlyTrue
            || T.IsUnit(Y) == StrictVerdict.StrictlyTrue
            || T.IsUnit(W) == StrictVerdict.StrictlyTrue)
            return StrictVerdict.StrictlyTrue;

        return StrictVerdict.Unknown;
    }

    /// <summary>严格 P²(T) 的入口守卫；Path A 请直接用分式域语义，不必调用。</summary>
    public StrictVerdict IsProjectivePoint() => IsUnimodular();

    public override string ToString() => $"[{X}, {Y}, {W}]";
}

/// <summary>对偶空间 V* 中的余向量（射影直线 = ker(l) 二维子空间）。</summary>
public readonly record struct Dual3<T>(T A, T B, T C) where T : IExactRing<T>
{
    public static Dual3<T> Fixed(long a, long b, long c) =>
        new(T.FromLong(a), T.FromLong(b), T.FromLong(c));

    public static Dual3<T> operator +(Dual3<T> a, Dual3<T> b) => new(a.A + b.A, a.B + b.B, a.C + b.C);
    public static Dual3<T> operator -(Dual3<T> a, Dual3<T> b) => new(a.A - b.A, a.B - b.B, a.C - b.C);
    public static Dual3<T> operator -(Dual3<T> a) => new(-a.A, -a.B, -a.C);
    public static Dual3<T> operator *(T k, Dual3<T> v) => new(k * v.A, k * v.B, k * v.C);
    public static Dual3<T> operator *(Dual3<T> v, T k) => k * v;

    public StrictResult VerifyZero() => SubspaceAlgebra.AllZero(A, B, C);

    public override string ToString() => $"({A}·X + {B}·Y + {C}·W = 0)";
}

/// <summary>射影线性映射 V → V（严格区分原始矩阵乘 Multiply 与几何作用 Apply）。</summary>
public readonly record struct LinearMap3<T>(
    T M00,
    T M01,
    T M02,
    T M10,
    T M11,
    T M12,
    T M20,
    T M21,
    T M22) where T : IExactRing<T>
{
    public static LinearMap3<T> Fixed(
        long m00, long m01, long m02,
        long m10, long m11, long m12,
        long m20, long m21, long m22) => new(
        T.FromLong(m00), T.FromLong(m01), T.FromLong(m02),
        T.FromLong(m10), T.FromLong(m11), T.FromLong(m12),
        T.FromLong(m20), T.FromLong(m21), T.FromLong(m22));

    public T Determinant() =>
        M00 * (M11 * M22 - M12 * M21)
        - M01 * (M10 * M22 - M12 * M20)
        + M02 * (M10 * M21 - M11 * M20);

    /// <summary>可逆 ⇔ 行列式是环中的单位（不仅是“非零”）；零因子环上不能只看 VerifyZero。</summary>
    public StrictVerdict IsInvertible() => T.IsUnit(Determinant());

    public Primal3<T> Multiply(Primal3<T> p) => new(
        M00 * p.X + M01 * p.Y + M02 * p.W,
        M10 * p.X + M11 * p.Y + M12 * p.W,
        M20 * p.X + M21 * p.Y + M22 * p.W);

    public Dual3<T> Multiply(Dual3<T> l) => new(
        M00 * l.A + M01 * l.B + M02 * l.C,
        M10 * l.A + M11 * l.B + M12 * l.C,
        M20 * l.A + M21 * l.B + M22 * l.C);

    public LinearMap3<T> Multiply(LinearMap3<T> r) => new(
        M00 * r.M00 + M01 * r.M10 + M02 * r.M20, M00 * r.M01 + M01 * r.M11 + M02 * r.M21,
        M00 * r.M02 + M01 * r.M12 + M02 * r.M22,
        M10 * r.M00 + M11 * r.M10 + M12 * r.M20, M10 * r.M01 + M11 * r.M11 + M12 * r.M21,
        M10 * r.M02 + M11 * r.M12 + M12 * r.M22,
        M20 * r.M00 + M21 * r.M10 + M22 * r.M20, M20 * r.M01 + M21 * r.M11 + M22 * r.M21,
        M20 * r.M02 + M21 * r.M12 + M22 * r.M22);

    public LinearMap3<T> Transpose() => new(
        M00, M10, M20,
        M01, M11, M21,
        M02, M12, M22);

    /// <summary>余子式矩阵 C = (adj M)^T。</summary>
    public LinearMap3<T> CofactorMatrix() => new(
        M11 * M22 - M12 * M21, M12 * M20 - M10 * M22, M10 * M21 - M11 * M20,
        M02 * M21 - M01 * M22, M00 * M22 - M02 * M20, M01 * M20 - M00 * M21,
        M01 * M12 - M02 * M11, M02 * M10 - M00 * M12, M00 * M11 - M01 * M10);

    public Primal3<T> Apply(Primal3<T> p) => Multiply(p);
    public Dual3<T> Apply(Dual3<T> l) => CofactorMatrix().Multiply(l);

    /// <summary>把射影变换作用到二次曲线矩阵上：Q ↦ C·Q·Cᵀ（零除法）。</summary>
    /// <remarks>次数：对 M 的元素总 4 次（C 是 2 次）；ExactScalar 长链迭代时先 Normalized。</remarks>
    public BilinearForm3<T> Apply(BilinearForm3<T> q)
    {
        var c = CofactorMatrix();
        var qMat = new LinearMap3<T>(
            q.Q00, q.Q01, q.Q02,
            q.Q01, q.Q11, q.Q12,
            q.Q02, q.Q12, q.Q22);

        var res = c.Multiply(qMat).Multiply(c.Transpose());
        return new BilinearForm3<T>(res.M00, res.M01, res.M02, res.M11, res.M12, res.M22);
    }

    /// <summary>
    ///     对偶二次型（切二次曲线 S: V* → V）的协变变换：S ↦ M·S·Mᵀ。
    ///     与点的 C·Q·Cᵀ 对偶；只相差 det² 的整体尺度（零除法）。
    /// </summary>
    public DualBilinearForm3<T> Apply(DualBilinearForm3<T> s)
    {
        var sMat = new LinearMap3<T>(
            s.S00, s.S01, s.S02,
            s.S01, s.S11, s.S12,
            s.S02, s.S12, s.S22);

        var res = Multiply(sMat).Multiply(Transpose());
        return new DualBilinearForm3<T>(res.M00, res.M01, res.M02, res.M11, res.M12, res.M22);
    }

    /// <summary>
    ///     四点定射影变换（无三点共线）。纯 +、-、*，用伴随矩阵清掉所有分母：
    ///     M = T·diag(β_i·∏_{j≠i} detS_j)·adj(S)。
    /// </summary>
    public static Result<LinearMap3<T>, GeometryError> FromFourPoints(
        Primal3<T> p1, Primal3<T> p2, Primal3<T> p3, Primal3<T> p4,
        Primal3<T> q1, Primal3<T> q2, Primal3<T> q3, Primal3<T> q4)
    {
        var detS = SubspaceAlgebra.Det(p1, p2, p3);
        var detT = SubspaceAlgebra.Det(q1, q2, q3);
        var s0 = SubspaceAlgebra.Det(p4, p2, p3);
        var s1 = SubspaceAlgebra.Det(p1, p4, p3);
        var s2 = SubspaceAlgebra.Det(p1, p2, p4);
        var t0 = SubspaceAlgebra.Det(q4, q2, q3);
        var t1 = SubspaceAlgebra.Det(q1, q4, q3);
        var t2 = SubspaceAlgebra.Det(q1, q2, q4);

        if (T.VerifyZero(detS) == StrictResult.StrictlyTrue
            || T.VerifyZero(detT) == StrictResult.StrictlyTrue
            || T.VerifyZero(s0) == StrictResult.StrictlyTrue
            || T.VerifyZero(s1) == StrictResult.StrictlyTrue
            || T.VerifyZero(s2) == StrictResult.StrictlyTrue
            || T.VerifyZero(t0) == StrictResult.StrictlyTrue
            || T.VerifyZero(t1) == StrictResult.StrictlyTrue
            || T.VerifyZero(t2) == StrictResult.StrictlyTrue)
            return Result<LinearMap3<T>, GeometryError>.Fail(GeometryError.UndefinedParameter);

        // 列向量为 p1,p2,p3 / q1,q2,q3
        var s = new LinearMap3<T>(
            p1.X, p2.X, p3.X,
            p1.Y, p2.Y, p3.Y,
            p1.W, p2.W, p3.W);
        var t = new LinearMap3<T>(
            q1.X, q2.X, q3.X,
            q1.Y, q2.Y, q3.Y,
            q1.W, q2.W, q3.W);

        var diag = new LinearMap3<T>(
            t0 * s1 * s2, T.Zero, T.Zero,
            T.Zero, t1 * s0 * s2, T.Zero,
            T.Zero, T.Zero, t2 * s0 * s1);

        var adjS = s.CofactorMatrix().Transpose();
        return Result<LinearMap3<T>, GeometryError>.Ok(t.Multiply(diag).Multiply(adjS));
    }
}

/// <summary>对称双线性型 Q: V → V*（纯射影二次曲线 / 配极映射，无任何度量假设）。</summary>
public readonly record struct BilinearForm3<T>(
    T Q00,
    T Q01,
    T Q02,
    T Q11,
    T Q12,
    T Q22) where T : IExactRing<T>
{
    public static BilinearForm3<T> Fixed(long q00, long q01, long q02, long q11, long q12, long q22) => new(
        T.FromLong(q00), T.FromLong(q01), T.FromLong(q02), T.FromLong(q11), T.FromLong(q12), T.FromLong(q22));

    public Dual3<T> Polar(Primal3<T> p) => new(
        Q00 * p.X + Q01 * p.Y + Q02 * p.W,
        Q01 * p.X + Q11 * p.Y + Q12 * p.W,
        Q02 * p.X + Q12 * p.Y + Q22 * p.W);

    public T Eval(Primal3<T> p) => SubspaceAlgebra.Pair(Polar(p), p);

    /// <summary>
    ///     已知直线与二次曲线交于 <paramref name="known" />，且 <paramref name="otherOnLine" /> 是这条直线上
    ///     另一点（不必在曲线上）：返回另一个交点（纯 +、-、*，零除法）。
    ///     <para>
    ///         退化：输入为零向量，或 <paramref name="otherOnLine" /> 与 <paramref name="known" /> 代表同一点
    ///         （线未定义）时返回 <see cref="GeometryError.ZeroVector" /> /
    ///         <see cref="GeometryError.UndefinedParameter" />，不再返回零向量。
    ///     </para>
    ///     <para>次数：对 otherOnLine 2 次、对 known 1 次，总次数 3；ExactScalar 长链迭代时先 Normalized。</para>
    /// </summary>
    public Result<Primal3<T>, GeometryError> OtherIntersection(Primal3<T> known, Primal3<T> otherOnLine)
    {
        if (IsZeroPoint(known) || IsZeroPoint(otherOnLine))
            return Result<Primal3<T>, GeometryError>.Fail(GeometryError.ZeroVector);

        var a = Eval(otherOnLine);
        var b = SubspaceAlgebra.Pair(Polar(otherOnLine), known);
        var result = a * known - T.FromLong(2) * b * otherOnLine;

        if (IsZeroPoint(result))
            return Result<Primal3<T>, GeometryError>.Fail(GeometryError.UndefinedParameter);
        return Result<Primal3<T>, GeometryError>.Ok(result);
    }

    private static bool IsZeroPoint(Primal3<T> p) =>
        T.IsZeroFast(p.X) && T.IsZeroFast(p.Y) && T.IsZeroFast(p.W);

    public DualBilinearForm3<T> DualForm() => new(
        Q11 * Q22 - Q12 * Q12, Q02 * Q12 - Q01 * Q22, Q01 * Q12 - Q02 * Q11,
        Q00 * Q22 - Q02 * Q02, Q01 * Q02 - Q00 * Q12,
        Q00 * Q11 - Q01 * Q01);
}

/// <summary>对偶空间上的对称双线性型 S: V* → V（切线二次曲线 / 绝对形）。</summary>
public readonly record struct DualBilinearForm3<T>(
    T S00,
    T S01,
    T S02,
    T S11,
    T S12,
    T S22) where T : IExactRing<T>
{
    public static DualBilinearForm3<T> Fixed(long s00, long s01, long s02, long s11, long s12, long s22) => new(
        T.FromLong(s00), T.FromLong(s01), T.FromLong(s02), T.FromLong(s11), T.FromLong(s12), T.FromLong(s22));

    public Primal3<T> Pole(Dual3<T> l) => new(
        S00 * l.A + S01 * l.B + S02 * l.C,
        S01 * l.A + S11 * l.B + S12 * l.C,
        S02 * l.A + S12 * l.B + S22 * l.C);

    public T Eval(Dual3<T> l) => SubspaceAlgebra.Pair(l, Pole(l));

    public Result<StrictResult, GeometryError> IsTangent(Dual3<T> l)
    {
        if (T.IsZeroFast(l.A) && T.IsZeroFast(l.B) && T.IsZeroFast(l.C))
            return Result<StrictResult, GeometryError>.Fail(GeometryError.ZeroVector);
        return Result<StrictResult, GeometryError>.Ok(T.VerifyZero(Eval(l)));
    }

    /// <summary>
    ///     已知一条切线 <paramref name="knownTangent" />，再给一条过切点外某点 P 的直线
    ///     <paramref name="otherLine" />，求过 P 的另一条切线（对偶版 OtherIntersection）。
    /// </summary>
    public Result<Dual3<T>, GeometryError> OtherTangent(Dual3<T> knownTangent, Dual3<T> otherLine)
    {
        if (T.IsZeroFast(knownTangent.A) && T.IsZeroFast(knownTangent.B) && T.IsZeroFast(knownTangent.C))
            return Result<Dual3<T>, GeometryError>.Fail(GeometryError.ZeroVector);
        if (T.IsZeroFast(otherLine.A) && T.IsZeroFast(otherLine.B) && T.IsZeroFast(otherLine.C))
            return Result<Dual3<T>, GeometryError>.Fail(GeometryError.ZeroVector);

        var a = Eval(otherLine);
        var b = SubspaceAlgebra.Pair(knownTangent, Pole(otherLine));
        var result = a * knownTangent - T.FromLong(2) * b * otherLine;

        if (T.IsZeroFast(result.A) && T.IsZeroFast(result.B) && T.IsZeroFast(result.C))
            return Result<Dual3<T>, GeometryError>.Fail(GeometryError.UndefinedParameter);
        return Result<Dual3<T>, GeometryError>.Ok(result);
    }
}

/// <summary>纯向量空间/外代数运算：关联、楔积、退化判定（全部只用 +、-、* 与 VerifyZero）。</summary>
public static class SubspaceAlgebra
{
    public static T Pair<T>(Dual3<T> l, Primal3<T> p) where T : IExactRing<T> =>
        l.A * p.X + l.B * p.Y + l.C * p.W;

    /// <summary>关联判定；任一侧为零向量时失败，避免零向量“关联一切”。</summary>
    public static Result<StrictResult, GeometryError> IsIncident<T>(Dual3<T> l, Primal3<T> p)
        where T : IExactRing<T>
    {
        if (IsZeroTriple(l.A, l.B, l.C) || IsZeroTriple(p.X, p.Y, p.W))
            return Result<StrictResult, GeometryError>.Fail(GeometryError.ZeroVector);
        return Result<StrictResult, GeometryError>.Ok(T.VerifyZero(Pair(l, p)));
    }

    /// <summary>两点张成直线（代数上与 Meet 同为三维叉积，方向从 V×V 到 V*）。</summary>
    public static Dual3<T> Join<T>(Primal3<T> p, Primal3<T> q) where T : IExactRing<T> => new(
        p.Y * q.W - p.W * q.Y,
        p.W * q.X - p.X * q.W,
        p.X * q.Y - p.Y * q.X);

    /// <summary>两线求交（代数上与 Join 同为三维叉积，方向从 V*×V* 到 V）。</summary>
    public static Primal3<T> Meet<T>(Dual3<T> l1, Dual3<T> l2) where T : IExactRing<T> => new(
        l1.B * l2.C - l1.C * l2.B,
        l1.C * l2.A - l1.A * l2.C,
        l1.A * l2.B - l1.B * l2.A);

    /// <summary>
    ///     三点退化行列式（与三点的齐次尺度成正比，零点判定与尺度无关）。
    ///     <para>
    ///         注意（ExactScalar）：坐标整体尺度很大时，<c>Approx</c> 会发生灾难性消去，
    ///         导致 <c>VerifyStrictZero()</c> 的数值快判把恒为零的式子误报成 StrictlyFalse。
    ///         严格判定前请先用 <c>ExactGeometryOps.Normalized</c> 规范化代表元，
    ///         或改用 <c>VerifyStrictZero(allowFastNumericReject: false)</c>。
    ///     </para>
    /// </summary>
    public static T Det<T>(Primal3<T> a, Primal3<T> b, Primal3<T> c) where T : IExactRing<T> =>
        Pair(Join(b, c), a);

    /// <summary>三线退化行列式（同 <see cref="Det{T}(Primal3{T}, Primal3{T}, Primal3{T})" /> 的尺度注意）。</summary>
    public static T Det<T>(Dual3<T> l1, Dual3<T> l2, Dual3<T> l3) where T : IExactRing<T> =>
        Pair(l1, Meet(l2, l3));

    /// <summary>三点共线；输入零向量时失败。</summary>
    public static Result<StrictResult, GeometryError> AreCollinear<T>(Primal3<T> a, Primal3<T> b, Primal3<T> c)
        where T : IExactRing<T>
    {
        if (IsZeroTriple(a.X, a.Y, a.W) || IsZeroTriple(b.X, b.Y, b.W) || IsZeroTriple(c.X, c.Y, c.W))
            return Result<StrictResult, GeometryError>.Fail(GeometryError.ZeroVector);
        return Result<StrictResult, GeometryError>.Ok(T.VerifyZero(Det(a, b, c)));
    }

    /// <summary>
    ///     无除法调和判定：Det(o,a,c)·Det(o,b,d) + Det(o,a,d)·Det(o,b,c) = 0。
    ///     只用 +、-、* 与 VerifyZero；整环上按分式域解释（Path A）。
    /// </summary>
    public static StrictResult IsHarmonic<T>(
        this Primal3<T> a,
        Primal3<T> b,
        Primal3<T> c,
        Primal3<T> d)
        where T : IIntegralDomain<T>
    {
        if (a.VerifyZero() == StrictResult.StrictlyTrue
            || b.VerifyZero() == StrictResult.StrictlyTrue
            || c.VerifyZero() == StrictResult.StrictlyTrue
            || d.VerifyZero() == StrictResult.StrictlyTrue)
            return StrictResult.StrictlyFalse;

        var join = Join(a, b);
        var joinZero = join.VerifyZero();
        if (joinZero == StrictResult.StrictlyTrue)
            return StrictResult.StrictlyFalse;
        if (joinZero != StrictResult.StrictlyFalse)
            return joinZero;

        var cOnLine = T.VerifyZero(Det(a, b, c));
        var dOnLine = T.VerifyZero(Det(a, b, d));
        if (cOnLine == StrictResult.StrictlyFalse || dOnLine == StrictResult.StrictlyFalse)
            return StrictResult.StrictlyFalse;
        if (cOnLine != StrictResult.StrictlyTrue || dOnLine != StrictResult.StrictlyTrue)
            return cOnLine != StrictResult.StrictlyTrue ? cOnLine : dOnLine;

        Primal3<T>? origin = null;
        var unknown = StrictResult.StrictlyFalse;
        var candidates = new[]
        {
            new Primal3<T>(T.One, T.Zero, T.Zero),
            new Primal3<T>(T.Zero, T.One, T.Zero),
            new Primal3<T>(T.Zero, T.Zero, T.One)
        };

        foreach (var candidate in candidates)
        {
            var incidence = T.VerifyZero(Pair(join, candidate));
            if (incidence == StrictResult.StrictlyFalse)
            {
                origin = candidate;
                break;
            }

            if (incidence != StrictResult.StrictlyTrue)
                unknown = incidence;
        }

        if (origin is null)
            return unknown == StrictResult.StrictlyFalse
                ? StrictResult.StrictlyFalse
                : unknown;

        var o = origin.Value;
        var numerator = Det(o, a, c) * Det(o, b, d);
        var denominator = Det(o, a, d) * Det(o, b, c);

        var denominatorZero = T.VerifyZero(denominator);
        if (denominatorZero == StrictResult.StrictlyTrue)
            return StrictResult.StrictlyFalse;
        if (denominatorZero != StrictResult.StrictlyFalse)
            return denominatorZero;

        return T.VerifyZero(numerator + denominator);
    }

    /// <summary>三线共点；输入零向量时失败。</summary>
    public static Result<StrictResult, GeometryError> AreConcurrent<T>(Dual3<T> l1, Dual3<T> l2, Dual3<T> l3)
        where T : IExactRing<T>
    {
        if (IsZeroTriple(l1.A, l1.B, l1.C) || IsZeroTriple(l2.A, l2.B, l2.C) || IsZeroTriple(l3.A, l3.B, l3.C))
            return Result<StrictResult, GeometryError>.Fail(GeometryError.ZeroVector);
        return Result<StrictResult, GeometryError>.Ok(T.VerifyZero(Det(l1, l2, l3)));
    }

    /// <summary>同一射影点；零向量输入返回 <see cref="GeometryError.ZeroVector" />，不再被当成“相同”。</summary>
    public static Result<StrictResult, GeometryError> SameSubspace<T>(Primal3<T> a, Primal3<T> b)
        where T : IExactRing<T>
    {
        if (IsZeroTriple(a.X, a.Y, a.W) || IsZeroTriple(b.X, b.Y, b.W))
            return Result<StrictResult, GeometryError>.Fail(GeometryError.ZeroVector);
        var w = Join(a, b);
        return Result<StrictResult, GeometryError>.Ok(AllZero(w.A, w.B, w.C));
    }

    /// <summary>同一射影线；零向量输入返回 <see cref="GeometryError.ZeroVector" />。</summary>
    public static Result<StrictResult, GeometryError> SameSubspace<T>(Dual3<T> a, Dual3<T> b)
        where T : IExactRing<T>
    {
        if (IsZeroTriple(a.A, a.B, a.C) || IsZeroTriple(b.A, b.B, b.C))
            return Result<StrictResult, GeometryError>.Fail(GeometryError.ZeroVector);
        var m = Meet(a, b);
        return Result<StrictResult, GeometryError>.Ok(AllZero(m.X, m.Y, m.W));
    }

    private static bool IsZeroTriple<T>(T x, T y, T z) where T : IExactRing<T> =>
        T.IsZeroFast(x) && T.IsZeroFast(y) && T.IsZeroFast(z);

    /// <summary>逐个 VerifyZero 的三元合取：任一 StrictlyFalse 即 False，全 True 才 True。</summary>
    /// <remarks>
    ///     优先级：StrictlyFalse &gt; 全 True &gt; NoSymbolicData &gt; NearZeroUnproven。
    ///     混有 NoSymbolicData 时返回它（比 NearZeroUnproven 更弱：连符号数据都没有）。
    /// </remarks>
    public static StrictResult AllZero<T>(T x, T y, T z) where T : IExactRing<T>
    {
        var rx = T.VerifyZero(x);
        var ry = T.VerifyZero(y);
        var rz = T.VerifyZero(z);

        if (rx == StrictResult.StrictlyFalse || ry == StrictResult.StrictlyFalse || rz == StrictResult.StrictlyFalse)
            return StrictResult.StrictlyFalse;
        if (rx == StrictResult.StrictlyTrue && ry == StrictResult.StrictlyTrue && rz == StrictResult.StrictlyTrue)
            return StrictResult.StrictlyTrue;
        if (rx == StrictResult.NoSymbolicData || ry == StrictResult.NoSymbolicData || rz == StrictResult.NoSymbolicData)
            return StrictResult.NoSymbolicData;
        return StrictResult.NearZeroUnproven;
    }
}

/// --外挂结构层：往纯射影骨架上叠放无穷远线 l∞ 与绝对二次型 Ω*--
/// <summary>
///     仿射结构：只依赖无穷远线 l∞。与度量结构分开，
///     避免把 Klein 图卡上的仿射中点/欧氏平行误当成双曲几何的中点/平行。
/// </summary>
public readonly record struct AffineLens<T>(Dual3<T> InfinityLine) where T : IExactRing<T>
{
    public static AffineLens<T> EuclideanXoy { get; } = new(new Dual3<T>(T.Zero, T.Zero, T.One));

    public Result<StrictResult, GeometryError> IsAtInfinity(Primal3<T> p)
    {
        if (IsZeroPoint(p)) return Result<StrictResult, GeometryError>.Fail(GeometryError.ZeroVector);
        return Result<StrictResult, GeometryError>.Ok(
            T.VerifyZero(SubspaceAlgebra.Pair(InfinityLine, p)));
    }

    /// <summary>射影仿射意义：两线交于 l∞ 上（快速守卫；未化简的等价代表元可能漏判）。</summary>
    /// <remarks>
    ///     · 显式数值零重合 → <see cref="GeometryError.UndefinedParameter" />；
    ///     未化简的代数重合可能被严格 <see cref="IsAtInfinity" /> 判成 Ok(StrictlyTrue)。<br />
    ///     · 其中一条恰为 l∞ 且另一条有限 → Ok(StrictlyTrue)（射影意义）；<br />
    ///     · 需要“未化简重合也 Fail”的严格语义时用 <see cref="AreParallelStrict" />。
    /// </remarks>
    public Result<StrictResult, GeometryError> AreParallel(Dual3<T> l1, Dual3<T> l2)
    {
        if (IsZeroLine(l1) || IsZeroLine(l2))
            return Result<StrictResult, GeometryError>.Fail(GeometryError.ZeroVector);

        var meet = SubspaceAlgebra.Meet(l1, l2);
        if (IsZeroPoint(meet))
            return Result<StrictResult, GeometryError>.Fail(GeometryError.UndefinedParameter);

        return IsAtInfinity(meet);
    }

    /// <summary>严格版：用 SameSubspace 的完整判定兜住未化简的等价代表元。</summary>
    public Result<StrictResult, GeometryError> AreParallelStrict(Dual3<T> l1, Dual3<T> l2)
    {
        if (IsZeroLine(l1) || IsZeroLine(l2))
            return Result<StrictResult, GeometryError>.Fail(GeometryError.ZeroVector);

        var same = SubspaceAlgebra.SameSubspace(l1, l2);
        if (!same.TryGet(out var sameVerdict))
            return Result<StrictResult, GeometryError>.Fail(same.Error);
        if (sameVerdict == StrictResult.StrictlyTrue)
            return Result<StrictResult, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var meet = SubspaceAlgebra.Meet(l1, l2);
        if (IsZeroPoint(meet))
            return Result<StrictResult, GeometryError>.Fail(GeometryError.UndefinedParameter);
        return IsAtInfinity(meet);
    }

    /// <summary>
    ///     仿射图卡的中点（只对有限点有定义）。双曲中点不在这个函数里；
    ///     它需要交比与开方，属于 <see cref="CayleyKleinMetric{T}" /> 之外的另一层。
    /// </summary>
    /// <remarks>
    ///     快速守卫只捕获结构零/显式无穷远；由复杂构造得到但尚未化简的无穷远点可能漏过，
    ///     此时会继续计算并返回 Ok，而不是 Fail(AtInfinity)。严格调用方应先用
    ///     <see cref="IsAtInfinity" /> + VerifyZero（或先 Normalized）确认输入有限。
    /// </remarks>
    public Result<Primal3<T>, GeometryError> Midpoint(Primal3<T> a, Primal3<T> b)
    {
        if (IsZeroPoint(a) || IsZeroPoint(b))
            return Result<Primal3<T>, GeometryError>.Fail(GeometryError.ZeroVector);

        var wa = SubspaceAlgebra.Pair(InfinityLine, a);
        var wb = SubspaceAlgebra.Pair(InfinityLine, b);
        if (T.IsZeroFast(wa) || T.IsZeroFast(wb))
            return Result<Primal3<T>, GeometryError>.Fail(GeometryError.AtInfinity);

        var midpoint = wb * a + wa * b;
        if (IsZeroPoint(midpoint))
            return Result<Primal3<T>, GeometryError>.Fail(GeometryError.UndefinedParameter);
        return Result<Primal3<T>, GeometryError>.Ok(midpoint);
    }

    /// <summary>三点有向面积的两倍；要求三个输入都是非零射影点。</summary>
    /// <remarks>
    ///     次数：对三个点合计 6 次（Det 3 次 × 三个 w 各 1 次）。<br />
    ///     快速守卫可能漏过未化简的无穷远点，此时返回 Ok(0) 而不是 Fail(AtInfinity)；
    ///     严格调用方应先用 <see cref="IsAtInfinity" /> + VerifyZero 确认输入有限。
    /// </remarks>
    public Result<T, GeometryError> OrientSigned(Primal3<T> a, Primal3<T> b, Primal3<T> p)
    {
        if (IsZeroPoint(a) || IsZeroPoint(b) || IsZeroPoint(p))
            return Result<T, GeometryError>.Fail(GeometryError.ZeroVector);

        var wa = SubspaceAlgebra.Pair(InfinityLine, a);
        var wb = SubspaceAlgebra.Pair(InfinityLine, b);
        var wp = SubspaceAlgebra.Pair(InfinityLine, p);
        if (T.IsZeroFast(wa) || T.IsZeroFast(wb) || T.IsZeroFast(wp))
            return Result<T, GeometryError>.Fail(GeometryError.AtInfinity);

        return Result<T, GeometryError>.Ok(SubspaceAlgebra.Det(a, b, p) * wa * wb * wp);
    }

    private static bool IsZeroPoint(Primal3<T> p) =>
        T.IsZeroFast(p.X) && T.IsZeroFast(p.Y) && T.IsZeroFast(p.W);

    private static bool IsZeroLine(Dual3<T> l) =>
        T.IsZeroFast(l.A) && T.IsZeroFast(l.B) && T.IsZeroFast(l.C);
}

/// <summary>度量结构：只依赖绝对二次型 Ω*（Cayley–Klein 的极点-极线垂直）。</summary>
public readonly record struct CayleyKleinMetric<T>(DualBilinearForm3<T> OmegaStar) where T : IExactRing<T>
{
    public static CayleyKleinMetric<T> Euclidean { get; } = new(
        new DualBilinearForm3<T>(T.One, T.Zero, T.Zero, T.One, T.Zero, T.Zero));

    /// <summary>双曲几何视角（Klein 模型，Minkowski 符号差 (+, +, -)）。</summary>
    public static CayleyKleinMetric<T> Hyperbolic { get; } = new(
        new DualBilinearForm3<T>(T.One, T.Zero, T.Zero, T.One, T.Zero, -T.One));

    public Result<T, GeometryError> PerpendicularCond(Dual3<T> l1, Dual3<T> l2)
    {
        if (IsZeroLine(l1) || IsZeroLine(l2))
            return Result<T, GeometryError>.Fail(GeometryError.ZeroVector);
        return Result<T, GeometryError>.Ok(SubspaceAlgebra.Pair(l1, OmegaStar.Pole(l2)));
    }

    public Result<StrictResult, GeometryError> ArePerpendicular(Dual3<T> l1, Dual3<T> l2)
    {
        var condition = PerpendicularCond(l1, l2);
        if (!condition.TryGet(out var value))
            return Result<StrictResult, GeometryError>.Fail(condition.Error);
        return Result<StrictResult, GeometryError>.Ok(T.VerifyZero(value));
    }

    /// <summary>过 p 且垂直于 l 的直线（快速守卫；未化简的极点关系可能漏判）。</summary>
    /// <remarks>
    ///     若 p 恰好是 l 的极点（双曲几何里有限点可以做到），则“过 p 的每条线都垂直于 l”，
    ///     垂线不唯一，应为 <see cref="GeometryError.UndefinedParameter" />。
    ///     默认快速守卫可能漏过未化简的 Join；严格语义用 <see cref="PerpendicularThroughStrict" />。
    /// </remarks>
    public Result<Dual3<T>, GeometryError> PerpendicularThrough(Primal3<T> p, Dual3<T> l)
    {
        if (IsZeroPoint(p) || IsZeroLine(l))
            return Result<Dual3<T>, GeometryError>.Fail(GeometryError.ZeroVector);

        var pole = OmegaStar.Pole(l);
        if (IsZeroPoint(pole))
            return Result<Dual3<T>, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var line = SubspaceAlgebra.Join(p, pole);
        if (IsZeroLine(line))
            return Result<Dual3<T>, GeometryError>.Fail(GeometryError.UndefinedParameter);

        return Result<Dual3<T>, GeometryError>.Ok(line);
    }

    /// <summary>严格版：用 AllZero 的完整判定识别未化简的极点退化。</summary>
    public Result<Dual3<T>, GeometryError> PerpendicularThroughStrict(Primal3<T> p, Dual3<T> l)
    {
        if (IsZeroPoint(p) || IsZeroLine(l))
            return Result<Dual3<T>, GeometryError>.Fail(GeometryError.ZeroVector);

        var pole = OmegaStar.Pole(l);
        if (IsZeroPoint(pole))
            return Result<Dual3<T>, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var line = SubspaceAlgebra.Join(p, pole);
        if (IsZeroLine(line)
            || SubspaceAlgebra.AllZero(line.A, line.B, line.C) == StrictResult.StrictlyTrue)
            return Result<Dual3<T>, GeometryError>.Fail(GeometryError.UndefinedParameter);

        return Result<Dual3<T>, GeometryError>.Ok(line);
    }

    private static bool IsZeroPoint(Primal3<T> p) =>
        T.IsZeroFast(p.X) && T.IsZeroFast(p.Y) && T.IsZeroFast(p.W);

    private static bool IsZeroLine(Dual3<T> l) =>
        T.IsZeroFast(l.A) && T.IsZeroFast(l.B) && T.IsZeroFast(l.C);
}

/// <summary>把仿射结构与度量结构组合起来；具体方法在 <see cref="Affine" /> / <see cref="Metric" /> 上。</summary>
public readonly record struct GeometryLens<T>(AffineLens<T> Affine, CayleyKleinMetric<T> Metric)
    where T : IExactRing<T>
{
    public static GeometryLens<T> EuclideanXoy { get; } = new(
        AffineLens<T>.EuclideanXoy, CayleyKleinMetric<T>.Euclidean);

    public static GeometryLens<T> Hyperbolic { get; } = new(
        AffineLens<T>.EuclideanXoy, CayleyKleinMetric<T>.Hyperbolic);

    public Dual3<T> InfinityLine => Affine.InfinityLine;
    public DualBilinearForm3<T> OmegaStar => Metric.OmegaStar;
}

/// <summary>
///     二次曲线的 Veronese 线性化：把点 p 映成六维向量
///     <c>[x², 2xy, 2xw, y², 2yw, w²]</c>，于是
///     <c>Q(p) = ⟨V(p), (Q00,Q01,Q02,Q11,Q12,Q22)⟩</c>。
///     “五点定圆锥曲线 / 轨迹拟合”因此变成一次线性零空间问题。
/// </summary>
public static class Veronese
{
    /// <summary>点 → 二次曲线系数空间的行向量。</summary>
    public static T[] Row<T>(Primal3<T> p) where T : IExactRing<T>
    {
        var two = T.FromLong(2);
        return
        [
            p.X * p.X,
            two * p.X * p.Y,
            two * p.X * p.W,
            p.Y * p.Y,
            two * p.Y * p.W,
            p.W * p.W
        ];
    }

    public static T[,] Matrix<T>(IReadOnlyList<Primal3<T>> points) where T : IExactRing<T>
    {
        var m = new T[points.Count, 6];
        for (var i = 0; i < points.Count; i++)
        {
            var row = Row(points[i]);
            for (var j = 0; j < 6; j++) m[i, j] = row[j];
        }
        return m;
    }

    /// <summary>一批切线的对偶 Veronese 矩阵（每行 = <see cref="LineRow{T}" />）。</summary>
    public static T[,] DualMatrix<T>(IReadOnlyList<Dual3<T>> lines) where T : IExactRing<T>
    {
        var m = new T[lines.Count, 6];
        for (var i = 0; i < lines.Count; i++)
        {
            var row = LineRow(lines[i]);
            for (var j = 0; j < 6; j++) m[i, j] = row[j];
        }
        return m;
    }

    /// <summary>行向量与系数向量的配对（等于 Q(p)）。</summary>
    public static T Dot<T>(T[] row, T[] coefficients) where T : IExactRing<T>
    {
        var sum = T.Zero;
        for (var i = 0; i < 6; i++) sum = sum + row[i] * coefficients[i];
        return sum;
    }

    public static T[] Coefficients<T>(BilinearForm3<T> q) where T : IExactRing<T> =>
        [q.Q00, q.Q01, q.Q02, q.Q11, q.Q12, q.Q22];

    public static BilinearForm3<T> ToConic<T>(T[] coefficients) where T : IExactRing<T> =>
        new(coefficients[0], coefficients[1], coefficients[2], coefficients[3], coefficients[4], coefficients[5]);

    /// <summary>线 l=(A,B,C) 的对偶 Veronese 行：[A²,2AB,2AC,B²,2BC,C²]。</summary>
    public static T[] LineRow<T>(Dual3<T> l) where T : IExactRing<T>
    {
        var two = T.FromLong(2);
        return
        [
            l.A * l.A,
            two * l.A * l.B,
            two * l.A * l.C,
            l.B * l.B,
            two * l.B * l.C,
            l.C * l.C
        ];
    }

    public static T[] DualCoefficients<T>(DualBilinearForm3<T> s) where T : IExactRing<T> =>
        [s.S00, s.S01, s.S02, s.S11, s.S12, s.S22];

    public static DualBilinearForm3<T> ToDualConic<T>(T[] coefficients) where T : IExactRing<T> =>
        new(coefficients[0], coefficients[1], coefficients[2], coefficients[3], coefficients[4], coefficients[5]);

    // --- 数值采样版（同一约定，给“采样 → 零空间”的发现管线用） ---

    public static double[] Row(double x, double y, double w = 1) =>
        [x * x, 2 * x * y, 2 * x * w, y * y, 2 * y * w, w * w];

    /// <summary>线 (A,B,C) 的对偶 Veronese 行（对偶二次型用）。</summary>
    public static double[] LineRow(double a, double b, double c) =>
        [a * a, 2 * a * b, 2 * a * c, b * b, 2 * b * c, c * c];

    public static double[,] Matrix(IReadOnlyList<double[]> rows)
    {
        var m = new double[rows.Count, 6];
        for (var i = 0; i < rows.Count; i++)
        for (var j = 0; j < 6; j++)
            m[i, j] = rows[i][j];
        return m;
    }
}

/// <summary>
///     圆锥曲线的有理参数化：给定曲线上一点 <paramref name="knownOnConic" />，
///     用过该点的线束（方向点 (u,v,0)）与曲线的另一个交点给出参数化。
///     只用到 +、-、*，因此 T 可以是任何交换环（ExactScalar / PolyExpr / …）。
///     <para>
///         <c>At(t)</c> 等价于 <c>At(1,t)</c>；<c>At(u,v)</c> 覆盖竖直方向。
///         若 <paramref name="knownOnConic" /> 在无穷远，且方向点与它成比例，线未定义，
///         返回 <see cref="GeometryError.UndefinedParameter" /> 而不是零向量。
///     </para>
/// </summary>
public readonly struct ConicParametrization<T>(BilinearForm3<T> conic, Primal3<T> knownOnConic)
    where T : IExactRing<T>
{
    public BilinearForm3<T> Conic => conic;
    public Primal3<T> Known => knownOnConic;

    /// <summary>参数 (u,v) 对应的曲线点；方向点为 (u,v,0)，覆盖竖直方向。</summary>
    public Result<Primal3<T>, GeometryError> At(T u, T v)
    {
        var direction = new Primal3<T>(u, v, T.Zero);
        return conic.OtherIntersection(knownOnConic, direction);
    }

    /// <summary>参数 t 对应的曲线点（等价于 At(1,t)）。</summary>
    public Result<Primal3<T>, GeometryError> At(T t) => At(T.One, t);
}
