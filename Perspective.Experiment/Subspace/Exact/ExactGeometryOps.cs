// ============================================================================
// ExactGeometryOps：专属于 ExactScalar 的扩展
//
// 这里的每个操作都越出了"只用 +、-、* 与 VerifyZero"的纯射影骨架上界：
//   · 需要除法 /（交比、笛卡尔坐标、线性消元）
//   · 需要 Sqrt（直线 ∩ 二次曲线）
//   · 需要 Approx 挑数值稳定的基点（PickNonIncidentPoint / SpanLine 等）
// 因此不做泛型，一律写成 ExactScalar 专用的扩展方法。
//
// 退化是数据，不是异常：凡"点重合 / 在无穷远 / 挑不出基点 / 无实交点"这类
// 几何常态都返回 Result<T, GeometryError>；异常只留给真正的不变量破坏。
// ============================================================================
using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

public static class ExactGeometryOps
{
    private static readonly Primal3<ExactScalar>[] BasisPoints =
        [Primal3<ExactScalar>.Fixed(0, 0, 1), Primal3<ExactScalar>.Fixed(1, 0, 0), Primal3<ExactScalar>.Fixed(0, 1, 0)];

    private static readonly Dual3<ExactScalar>[] BasisLines =
        [Dual3<ExactScalar>.Fixed(0, 0, 1), Dual3<ExactScalar>.Fixed(1, 0, 0), Dual3<ExactScalar>.Fixed(0, 1, 0)];

    // --- 数值/退化判定（依赖 Approx） ---

    public static bool IsNumericallyZero(this Primal3<ExactScalar> p, double tol = 1e-9) =>
        p.X.IsNumericallyZero(tol) && p.Y.IsNumericallyZero(tol) && p.W.IsNumericallyZero(tol);

    public static bool IsNumericallyZero(this Dual3<ExactScalar> l, double tol = 1e-9) =>
        l.A.IsNumericallyZero(tol) && l.B.IsNumericallyZero(tol) && l.C.IsNumericallyZero(tol);

    public static bool IsZeroVector(this Primal3<ExactScalar> p)
    {
        var verdict = p.VerifyZero();
        return verdict == StrictResult.StrictlyTrue
               || verdict == StrictResult.NoSymbolicData && p.IsNumericallyZero();
    }

    public static bool IsZeroVector(this Dual3<ExactScalar> l)
    {
        var verdict = l.VerifyZero();
        return verdict == StrictResult.StrictlyTrue
               || verdict == StrictResult.NoSymbolicData && l.IsNumericallyZero();
    }

    public static bool AreDistinct(this Primal3<ExactScalar> a, Primal3<ExactScalar> b)
    {
        var same = SubspaceAlgebra.SameSubspace(a, b);
        if (!same.TryGet(out var verdict)) return false;
        if (verdict == StrictResult.StrictlyFalse) return true;
        if (verdict == StrictResult.StrictlyTrue) return false;
        return !SubspaceAlgebra.Join(a.Normalized(), b.Normalized()).IsNumericallyZero();
    }

    // --- 代表元规范化：避免齐次尺度引发的“灾难性消去” ---

    /// <summary>
    ///     把射影点规范化成“|Approx| 最大的分量 = 1”的代表元（精确相除，不丢精确基因）。
    ///     <para>
    ///         用途：<c>Det</c> / <c>Pair</c> 这类齐次判定与整体缩放成正比；坐标尺度很大时，
    ///         <c>Approx</c> 会发生灾难性消去，<c>VerifyStrictZero()</c> 的快判可能把恒为零的式子
    ///         误报成 StrictlyFalse。先规范化代表元再判定即可避免。
    ///     </para>
    /// </summary>
    public static Primal3<ExactScalar> Normalized(this Primal3<ExactScalar> p)
    {
        var ax = FiniteAbs(p.X);
        var ay = FiniteAbs(p.Y);
        var aw = FiniteAbs(p.W);

        var scale = ax >= ay && ax >= aw ? p.X : ay >= aw ? p.Y : p.W;
        if (!double.IsFinite(scale.Approx) || scale.IsZeroWithFallback()) return p;
        return new Primal3<ExactScalar>(p.X / scale, p.Y / scale, p.W / scale);
    }

    /// <summary>直线的代表元规范化（同 <see cref="Normalized(Primal3{ExactScalar})" />）。</summary>
    public static Dual3<ExactScalar> Normalized(this Dual3<ExactScalar> l)
    {
        var aa = FiniteAbs(l.A);
        var ab = FiniteAbs(l.B);
        var ac = FiniteAbs(l.C);

        var scale = aa >= ab && aa >= ac ? l.A : ab >= ac ? l.B : l.C;
        if (!double.IsFinite(scale.Approx) || scale.IsZeroWithFallback()) return l;
        return new Dual3<ExactScalar>(l.A / scale, l.B / scale, l.C / scale);
    }

    /// <summary>三点共线的严格判定（先规范化代表元，避免快判误报）。</summary>
    public static StrictResult AreCollinearExact(
        this Primal3<ExactScalar> a,
        Primal3<ExactScalar> b,
        Primal3<ExactScalar> c) =>
        SubspaceAlgebra.Det(a.Normalized(), b.Normalized(), c.Normalized()).VerifyStrictZero();

    // --- 交比 ---

    /// <summary>纯射影交比 (A, B; C, D)，要求四点共线且 A≠B、A≠D、B≠C。</summary>
    public static Result<ExactScalar, GeometryError> CrossRatio(
        this Primal3<ExactScalar> a,
        Primal3<ExactScalar> b,
        Primal3<ExactScalar> c,
        Primal3<ExactScalar> d)
    {
        if (a.IsZeroVector() || b.IsZeroVector() || c.IsZeroVector() || d.IsZeroVector())
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.ZeroVector);

        var an = NormalizedForPredicate(a);
        var bn = NormalizedForPredicate(b);
        var cn = NormalizedForPredicate(c);
        var dn = NormalizedForPredicate(d);

        if (!SubspaceAlgebra.SameSubspace(an, bn).TryGet(out var sameVerdict)
            || sameVerdict == StrictResult.StrictlyTrue)
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.CoincidentPoints);
        if (SubspaceAlgebra.Det(an, bn, cn).VerifyStrictZero()
                == StrictResult.StrictlyFalse
            || SubspaceAlgebra.Det(an, bn, dn).VerifyStrictZero()
                == StrictResult.StrictlyFalse)
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.NotCollinear);

        var oResult = SubspaceAlgebra.Join(an, bn).PickNonIncidentPoint();
        if (!oResult.TryGet(out var o))
            return Result<ExactScalar, GeometryError>.Fail(oResult.Error);

        var num = SubspaceAlgebra.Det(o, an, cn) * SubspaceAlgebra.Det(o, bn, dn);
        var den = SubspaceAlgebra.Det(o, an, dn) * SubspaceAlgebra.Det(o, bn, cn);

        if (den.IsZeroWithFallback(treatUnprovenAsZero: true))
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.CoincidentPoints);

        return Result<ExactScalar, GeometryError>.Ok(num / den);
    }

    /// <summary>纯多项式零除法判定四点调和 (A, B; C, D) = -1。</summary>
    public static StrictResult IsHarmonic(
        this Primal3<ExactScalar> a,
        Primal3<ExactScalar> b,
        Primal3<ExactScalar> c,
        Primal3<ExactScalar> d)
    {
        if (a.IsZeroVector() || b.IsZeroVector() || c.IsZeroVector() || d.IsZeroVector())
            return StrictResult.StrictlyFalse;

        // 入口归一化一次，后续全部复用：双线性 Det 对整体尺度敏感，但归一化是幂等的。
        var an = NormalizedForPredicate(a);
        var bn = NormalizedForPredicate(b);
        var cn = NormalizedForPredicate(c);
        var dn = NormalizedForPredicate(d);

        if (!an.AreDistinct(bn))
            return StrictResult.StrictlyFalse;
        if (SubspaceAlgebra.Det(an, bn, cn).VerifyStrictZero()
                == StrictResult.StrictlyFalse
            || SubspaceAlgebra.Det(an, bn, dn).VerifyStrictZero()
                == StrictResult.StrictlyFalse)
            return StrictResult.StrictlyFalse;

        var oResult = SubspaceAlgebra.Join(an, bn).PickNonIncidentPoint();
        // 连参考基点都挑不出来属于病态输入；没有合适的 StrictResult 取值，按“无法判定”处理。
        if (!oResult.TryGet(out var o))
            return StrictResult.NearZeroUnproven;

        var num = SubspaceAlgebra.Det(o, an, cn) * SubspaceAlgebra.Det(o, bn, dn);
        var den = SubspaceAlgebra.Det(o, an, dn) * SubspaceAlgebra.Det(o, bn, cn);

        var denZero = den.VerifyStrictZero();
        if (denZero == StrictResult.StrictlyTrue) return StrictResult.StrictlyFalse;
        if (denZero == StrictResult.NoSymbolicData)
            return den.IsNumericallyZero() ? StrictResult.StrictlyFalse : StrictResult.NoSymbolicData;
        if (denZero == StrictResult.NearZeroUnproven)
            return StrictResult.NearZeroUnproven;

        return (num + den).VerifyStrictZero();
    }

    // --- 靠 Approx 挑稳健基点/支撑点 ---

    /// <summary>为直线挑选一个肯定不在其上的标准基点。</summary>
    public static Result<Primal3<ExactScalar>, GeometryError> PickNonIncidentPoint(
        this Dual3<ExactScalar> line)
    {
        if (line.IsZeroVector())
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.ZeroVector);

        Primal3<ExactScalar>? best = null;
        double bestAbs = -1;

        foreach (var e in BasisPoints)
        {
            var pair = SubspaceAlgebra.Pair(line, e);
            if (pair.VerifyStrictZero() == StrictResult.StrictlyFalse)
            {
                var abs = SafeAbs(pair);
                if (abs > bestAbs)
                {
                    bestAbs = abs;
                    best = e;
                }
            }
        }

        if (best is not null) return Result<Primal3<ExactScalar>, GeometryError>.Ok(best.Value);

        foreach (var e in BasisPoints)
        {
            var pair = SubspaceAlgebra.Pair(line, e);
            if (!pair.IsNumericallyZero())
            {
                var abs = SafeAbs(pair);
                if (abs > bestAbs)
                {
                    bestAbs = abs;
                    best = e;
                }
            }
        }

        return best is not null
            ? Result<Primal3<ExactScalar>, GeometryError>.Ok(best.Value)
            : Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.UndeterminedBasis);
    }

    /// <summary>为直线挑选两个线性无关的支撑点 (U, V)。</summary>
    public static Result<(Primal3<ExactScalar> U, Primal3<ExactScalar> V), GeometryError> SpanLine(
        this Dual3<ExactScalar> line)
    {
        if (line.IsZeroVector())
            return Result<(Primal3<ExactScalar>, Primal3<ExactScalar>), GeometryError>.Fail(GeometryError.ZeroVector);

        Primal3<ExactScalar>? first = null;
        foreach (var axis in BasisLines)
        {
            var pt = SubspaceAlgebra.Meet(line, axis);
            if (pt.IsZeroVector()) continue;
            if (first is null)
            {
                first = pt;
                continue;
            }
            if (first.Value.AreDistinct(pt))
                return Result<(Primal3<ExactScalar>, Primal3<ExactScalar>), GeometryError>.Ok((first.Value, pt));
        }

        return Result<(Primal3<ExactScalar>, Primal3<ExactScalar>), GeometryError>.Fail(GeometryError.UndeterminedBasis);
    }

    /// <summary>在直线上找一个与 anchor 不同的点；若传入 preferFiniteAgainst 则优先挑远离它的点。</summary>
    public static Result<Primal3<ExactScalar>, GeometryError> PickPointOnLineDistinctFrom(
        this Dual3<ExactScalar> line,
        Primal3<ExactScalar> anchor,
        Dual3<ExactScalar>? preferFiniteAgainst = null)
    {
        if (line.IsZeroVector() || anchor.IsZeroVector())
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.ZeroVector);

        Primal3<ExactScalar>? best = null;
        double bestScore = -1;

        foreach (var axis in BasisLines)
        {
            var cand = SubspaceAlgebra.Meet(line, axis);
            if (cand.IsZeroVector()) continue;
            if (SubspaceAlgebra.SameSubspace(anchor, cand).TryGet(out var sameVerdict)
                && sameVerdict == StrictResult.StrictlyFalse)
            {
                var score = ScoreCandidate(cand, preferFiniteAgainst);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = cand;
                }
            }
        }

        if (best is not null) return Result<Primal3<ExactScalar>, GeometryError>.Ok(best.Value);

        foreach (var axis in BasisLines)
        {
            var cand = SubspaceAlgebra.Meet(line, axis);
            if (cand.IsZeroVector()) continue;
            if (!SubspaceAlgebra.Join(anchor, cand).IsNumericallyZero())
            {
                var score = ScoreCandidate(cand, preferFiniteAgainst);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = cand;
                }
            }
        }

        return best is not null
            ? Result<Primal3<ExactScalar>, GeometryError>.Ok(best.Value)
            : Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.UndeterminedBasis);
    }

    private static double ScoreCandidate(Primal3<ExactScalar> cand, Dual3<ExactScalar>? preferFiniteAgainst) =>
        preferFiniteAgainst is not null
            ? SafeAbs(SubspaceAlgebra.Pair(preferFiniteAgainst.Value, cand))
            : Math.Max(SafeAbs(cand.X),
                Math.Max(SafeAbs(cand.Y), SafeAbs(cand.W)));

    // --- 度量 / 仿射坐标（需要除法） ---

    /// <summary>标准 XOY 图卡（l∞=(0,0,1)，欧氏 Ω*=diag(1,1,0)）下的欧氏距离平方。</summary>
    /// <remarks>非标准 l∞ 或非欧度量（例如 Hyperbolic）返回 UndefinedParameter，不会静默算错。</remarks>
    public static Result<ExactScalar, GeometryError> EuclideanDistanceSquared(
        this GeometryLens<ExactScalar> lens,
        Primal3<ExactScalar> p1,
        Primal3<ExactScalar> p2)
    {
        if (!IsStandardInfinityLine(lens.Affine.InfinityLine) || !IsEuclideanMetric(lens.OmegaStar))
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.UndefinedParameter);

        if (p1.IsZeroVector() || p2.IsZeroVector())
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.ZeroVector);

        var w1 = SubspaceAlgebra.Pair(lens.InfinityLine, p1);
        var w2 = SubspaceAlgebra.Pair(lens.InfinityLine, p2);
        if (w1.IsZeroWithFallback() || w2.IsZeroWithFallback())
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.AtInfinity);

        var line = SubspaceAlgebra.Join(p1, p2);
        return Result<ExactScalar, GeometryError>.Ok(lens.OmegaStar.Eval(line) / (w1 * w1 * w2 * w2));
    }

    /// <summary>标准 XOY 图卡（l∞=(0,0,1)）下的笛卡尔坐标。</summary>
    /// <remarks>非标准 l∞ 返回 false；不会把其它仿射图卡的坐标当成 (x,y)。</remarks>
    public static bool TryToCartesian(
        this GeometryLens<ExactScalar> lens,
        Primal3<ExactScalar> p,
        out ExactScalar x,
        out ExactScalar y)
    {
        x = y = ExactScalar.FromInt(0);
        if (!IsStandardInfinityLine(lens.Affine.InfinityLine))
            return false;
        if (p.VerifyZero() == StrictResult.StrictlyTrue)
            return false;

        var w = SubspaceAlgebra.Pair(lens.InfinityLine, p);
        var wZero = w.VerifyStrictZero();
        var canDivide = wZero == StrictResult.StrictlyFalse ||
                        wZero == StrictResult.NoSymbolicData && !w.IsNumericallyZero();
        if (!canDivide) return false;

        x = p.X / w;
        y = p.Y / w;
        return true;
    }

    public static string Format(this GeometryLens<ExactScalar> lens, Primal3<ExactScalar> p) =>
        lens.TryToCartesian(p, out var x, out var y)
            ? $"({x.ToSimplifiedString()}, {y.ToSimplifiedString()})"
            : $"无穷远方向 {p}";

    /// <summary>【零根号弦中点】只用直线、二次曲线和镜头的 l∞ 算出弦中点。</summary>
    public static Result<Primal3<ExactScalar>, GeometryError> ChordMidpoint(
        this GeometryLens<ExactScalar> lens,
        Dual3<ExactScalar> line,
        BilinearForm3<ExactScalar> conic)
    {
        if (line.IsZeroVector())
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.ZeroVector);

        var same = SubspaceAlgebra.SameSubspace(line, lens.InfinityLine);
        if (!same.TryGet(out var sameVerdict))
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(same.Error);
        if (sameVerdict == StrictResult.StrictlyTrue)
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.AtInfinity);

        var uInf = SubspaceAlgebra.Meet(line, lens.InfinityLine);
        if (uInf.IsZeroVector())
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.UndeterminedBasis);

        var vResult = line.PickPointOnLineDistinctFrom(uInf, lens.InfinityLine);
        if (!vResult.TryGet(out var v))
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(vResult.Error);

        var a = conic.Eval(uInf);
        if (a.IsZeroWithFallback())
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.AtInfinity);

        var b = SubspaceAlgebra.Pair(conic.Polar(uInf), v);
        return Result<Primal3<ExactScalar>, GeometryError>.Ok(a * v - b * uInf);
    }

    // --- 精确零空间（需要除法） ---

    /// <summary>求 ExactScalar 矩阵的 1 维零空间（自由列取 1，其余由回代除法给出）。</summary>
    public static ExactScalar[]? FindExactNullVector1D(ExactScalar[,] m)
    {
        int rows = m.GetLength(0), cols = m.GetLength(1);
        var a = (ExactScalar[,])m.Clone();
        var pivotCol = new int[rows];
        Array.Fill(pivotCol, -1);
        int r = 0;

        for (int col = 0; col < cols && r < rows; col++)
        {
            int piv = -1;
            double bestAbs = -1;
            for (int i = r; i < rows; i++)
            {
                if (!a[i, col].IsZeroWithFallback(treatUnprovenAsZero: true))
                {
                    double abs = SafeAbs(a[i, col]);
                    if (abs > bestAbs) { bestAbs = abs; piv = i; }
                }
            }
            if (piv < 0) continue;

            if (piv != r)
                for (int j = col; j < cols; j++)
                    (a[r, j], a[piv, j]) = (a[piv, j], a[r, j]);

            var pv = a[r, col];
            for (int j = col; j < cols; j++)
                a[r, j] = (a[r, j] / pv).Simplified();

            for (int i = 0; i < rows; i++)
            {
                if (i == r || a[i, col].IsZeroWithFallback(treatUnprovenAsZero: true)) continue;
                var factor = a[i, col];
                for (int j = col; j < cols; j++)
                    a[i, j] = (a[i, j] - factor * a[r, j]).Simplified();
            }
            pivotCol[r] = col;
            r++;
        }

        if (cols - r != 1) return null;

        var used = new bool[cols];
        for (int i = 0; i < r; i++) used[pivotCol[i]] = true;
        int freeCol = Array.IndexOf(used, false);

        var vec = new ExactScalar[cols];
        for (int i = 0; i < cols; i++) vec[i] = ExactScalar.Zero;
        vec[freeCol] = ExactScalar.One;
        for (int i = 0; i < r; i++)
            vec[pivotCol[i]] = -a[i, freeCol];
        return vec;
    }

    // ===== 解析几何计算器：从 5 题基准抽出的可复用形状 =====

    /// <summary>垂直斜率：m ↦ −1/m（要求 m≠0；水平/竖直由调用方另处理）。</summary>
    public static ExactScalar PerpendicularSlope(ExactScalar slope) => -ExactScalar.One / slope;

    /// <summary>
    ///     中心二次曲线（Q02=Q12=0）沿过原点直线 y=mx 的半径平方。
    ///     slope=null 表示竖直线 x=0。对椭圆 x²/a²+y²/b²=1：
    ///     r²(m) = a²b²(1+m²)/(b²+a²m²)。
    /// </summary>
    /// <remarks>5 题基准脚本的便捷方法；非中心曲线会抛 InvalidOperationException，通用路径请用 Result API。</remarks>
    public static ExactScalar CentralRadiusSquared(
        this BilinearForm3<ExactScalar> conic, ExactScalar? slope)
    {
        if (!conic.Q02.IsZeroWithFallback() || !conic.Q12.IsZeroWithFallback())
            throw new InvalidOperationException("CentralRadiusSquared 只适用于中心二次曲线（Q02=Q12=0）。");

        if (slope is null) return -conic.Q22 / conic.Q11;

        var m = slope.Value;
        var denominator = conic.Q00 + conic.Q01 * m + conic.Q11 * m * m;
        return -conic.Q22 * (ExactScalar.One + m * m) / denominator;
    }

    /// <summary>一对互相垂直的中心半径的平方：(r(m)², r(−1/m)²)。</summary>
    public static (ExactScalar A, ExactScalar B) PerpendicularCentralRadii(
        this BilinearForm3<ExactScalar> conic, ExactScalar slope) =>
        (conic.CentralRadiusSquared(slope),
         conic.CentralRadiusSquared(PerpendicularSlope(slope)));

    /// <summary>二次函数 f(y)=a y²+b y+c 在闭区间 [lo,hi] 上的最大值与取点。</summary>
    public static (ExactScalar Value, ExactScalar At) QuadraticMaxOnInterval(
        ExactScalar a, ExactScalar b, ExactScalar c, ExactScalar lo, ExactScalar hi)
    {
        if (hi.Approx < lo.Approx) (lo, hi) = (hi, lo);

        var candidates = new List<(ExactScalar Value, ExactScalar At)>
        {
            (a * lo * lo + b * lo + c, lo),
            (a * hi * hi + b * hi + c, hi),
        };

        if (!a.IsZeroWithFallback(treatUnprovenAsZero: true))
        {
            var vertex = -b / (ExactScalar.FromInt(2) * a);
            if (vertex.Approx >= lo.Approx - 1e-12 && vertex.Approx <= hi.Approx + 1e-12)
                candidates.Add((a * vertex * vertex + b * vertex + c, vertex));
        }

        var best = candidates[0];
        foreach (var candidate in candidates)
            if (candidate.Value.Approx > best.Value.Approx)
                best = candidate;
        return best;
    }

    /// <summary>
    ///     由“到焦点距离 / 到准线距离 = e”平方后得到的二次曲线：
    ///     |PF|² − e²·dist(P,l)² = 0。
    /// </summary>
    /// <remarks>5 题基准脚本的便捷方法；焦点在无穷远/准线为 l∞ 时抛 InvalidOperationException。</remarks>
    public static BilinearForm3<ExactScalar> ConicFromFocusDirectrix(
        Primal3<ExactScalar> focus, Dual3<ExactScalar> directrix, ExactScalar eccentricity)
    {
        if (!TryAffine(focus, out var fx, out var fy))
            throw new InvalidOperationException("焦点不能在无穷远。");
        if (directrix.A.IsZeroWithFallback() && directrix.B.IsZeroWithFallback())
            throw new InvalidOperationException("准线不能是无穷远线。");

        var e2 = eccentricity * eccentricity;
        var norm2 = directrix.A * directrix.A + directrix.B * directrix.B;

        var focusForm = new BilinearForm3<ExactScalar>(
            ExactScalar.One, ExactScalar.Zero, -fx,
            ExactScalar.One, -fy, fx * fx + fy * fy);

        var lineForm = new BilinearForm3<ExactScalar>(
            directrix.A * directrix.A,
            directrix.A * directrix.B,
            directrix.A * directrix.C,
            directrix.B * directrix.B,
            directrix.B * directrix.C,
            directrix.C * directrix.C);

        return new BilinearForm3<ExactScalar>(
            norm2 * focusForm.Q00 - e2 * lineForm.Q00,
            norm2 * focusForm.Q01 - e2 * lineForm.Q01,
            norm2 * focusForm.Q02 - e2 * lineForm.Q02,
            norm2 * focusForm.Q11 - e2 * lineForm.Q11,
            norm2 * focusForm.Q12 - e2 * lineForm.Q12,
            norm2 * focusForm.Q22 - e2 * lineForm.Q22);
    }

    /// <summary>三点有向面积的两倍；会先除掉三个 w，避免齐次尺度乘进面积。</summary>
    public static Result<ExactScalar, GeometryError> TwiceSignedArea(
        Primal3<ExactScalar> a, Primal3<ExactScalar> b, Primal3<ExactScalar> c)
    {
        if (a.W.IsZeroWithFallback(treatUnprovenAsZero: true)
            || b.W.IsZeroWithFallback(treatUnprovenAsZero: true)
            || c.W.IsZeroWithFallback(treatUnprovenAsZero: true))
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.AtInfinity);

        var det = SubspaceAlgebra.Det(a, b, c);
        return Result<ExactScalar, GeometryError>.Ok(det / (a.W * b.W * c.W));
    }

    /// <summary>顶点处的余弦：cos∠AVB（仿射点，必要时先除以 w）。</summary>
    public static Result<ExactScalar, GeometryError> CosAngleAt(
        Primal3<ExactScalar> vertex, Primal3<ExactScalar> a, Primal3<ExactScalar> b)
    {
        if (!TryAffine(vertex, out var vx, out var vy)
            || !TryAffine(a, out var ax, out var ay)
            || !TryAffine(b, out var bx, out var by))
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.AtInfinity);

        var ux = ax - vx;
        var uy = ay - vy;
        var wx = bx - vx;
        var wy = by - vy;

        var dot = ux * wx + uy * wy;
        var u2 = ux * ux + uy * uy;
        var w2 = wx * wx + wy * wy;
        if (u2.IsZeroWithFallback() || w2.IsZeroWithFallback())
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.CoincidentPoints);

        var denominator = ExactScalar.Sqrt(u2) * ExactScalar.Sqrt(w2);
        return Result<ExactScalar, GeometryError>.Ok(dot / denominator);
    }

    /// <summary>有限射影点 → 仿射向量 (X/W, Y/W)。</summary>
    public static Result<Vec2<ExactScalar>, GeometryError> ToVector(this Primal3<ExactScalar> p)
    {
        var affine = ToAffinePoint(p);
        if (!affine.TryGet(out var a))
            return Result<Vec2<ExactScalar>, GeometryError>.Fail(affine.Error);
        return Result<Vec2<ExactScalar>, GeometryError>.Ok(new Vec2<ExactScalar>(a.X, a.Y));
    }

    /// <summary>仿射向量 → 有限射影点 [X:Y:1]。</summary>
    public static Primal3<ExactScalar> ToPrimal3(this Vec2<ExactScalar> v) =>
        new(v.X, v.Y, ExactScalar.One);

    private static Result<Primal3<ExactScalar>, GeometryError> ToAffinePoint(Primal3<ExactScalar> p)
    {
        if (p.IsZeroVector())
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.ZeroVector);
        if (p.W.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.AtInfinity);

        return Result<Primal3<ExactScalar>, GeometryError>.Ok(new Primal3<ExactScalar>(
            p.X / p.W, p.Y / p.W, ExactScalar.One));
    }

    private static bool TryAffine(Primal3<ExactScalar> p, out ExactScalar x, out ExactScalar y)
    {
        x = ExactScalar.Zero;
        y = ExactScalar.Zero;
        if (p.W.IsZeroWithFallback()) return false;
        x = p.X / p.W;
        y = p.Y / p.W;
        return true;
    }

    private static double FiniteAbs(ExactScalar x) =>
        double.IsFinite(x.Approx) ? Math.Abs(x.Approx) : -1.0;

    /// <summary>只在尺度极端时归一化，避免对普通符号点做昂贵的符号除法。</summary>
    private static Primal3<ExactScalar> NormalizedForPredicate(Primal3<ExactScalar> p)
    {
        var max = Math.Max(FiniteAbs(p.X), Math.Max(FiniteAbs(p.Y), FiniteAbs(p.W)));
        if (max < 0 || max > 1e3 || max < 1e-3) return p.Normalized();
        return p;
    }

    private static double SafeAbs(ExactScalar x) =>
        double.IsFinite(x.Approx) ? Math.Abs(x.Approx) : 0.0;

    private static bool IsStandardInfinityLine(Dual3<ExactScalar> l) =>
        l.A.IsZeroWithFallback(treatUnprovenAsZero: true)
        && l.B.IsZeroWithFallback(treatUnprovenAsZero: true)
        && !l.C.IsZeroWithFallback(treatUnprovenAsZero: true);

    private static bool IsEuclideanMetric(DualBilinearForm3<ExactScalar> s)
    {
        var zero = ExactScalar.Zero;
        return ExactScalar.AreStrictlyEqual(s.S00, s.S11) == StrictResult.StrictlyTrue
               && ExactScalar.AreStrictlyEqual(s.S00, zero) == StrictResult.StrictlyFalse
               && ExactScalar.AreStrictlyEqual(s.S01, zero) == StrictResult.StrictlyTrue
               && ExactScalar.AreStrictlyEqual(s.S02, zero) == StrictResult.StrictlyTrue
               && ExactScalar.AreStrictlyEqual(s.S12, zero) == StrictResult.StrictlyTrue
               && ExactScalar.AreStrictlyEqual(s.S22, zero) == StrictResult.StrictlyTrue;
    }

    /// <summary>五点定圆锥曲线：Veronese 矩阵 + 一维零空间，纯加减乘。</summary>
    public static Result<BilinearForm3<ExactScalar>, GeometryError> ConicFromFivePoints(
        IReadOnlyList<Primal3<ExactScalar>> points)
    {
        if (points.Count != 5)
            return Result<BilinearForm3<ExactScalar>, GeometryError>.Fail(GeometryError.UndefinedParameter);
        foreach (var p in points)
            if (p.IsZeroVector())
                return Result<BilinearForm3<ExactScalar>, GeometryError>.Fail(GeometryError.ZeroVector);

        var nullVector = FindExactNullVector1D(Veronese.Matrix(points));
        if (nullVector is null)
            return Result<BilinearForm3<ExactScalar>, GeometryError>.Fail(GeometryError.UndeterminedBasis);

        return Result<BilinearForm3<ExactScalar>, GeometryError>.Ok(Veronese.ToConic(nullVector));
    }

    /// <summary>过 p 的垂足：l ∩ (过 p 且垂直于 l 的直线)。</summary>
    public static Result<Primal3<ExactScalar>, GeometryError> PerpendicularFoot(
        this GeometryLens<ExactScalar> lens,
        Primal3<ExactScalar> p,
        Dual3<ExactScalar> l)
    {
        var perpendicular = lens.Metric.PerpendicularThrough(p, l);
        if (!perpendicular.TryGet(out var through))
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(perpendicular.Error);

        var foot = SubspaceAlgebra.Meet(l, through);
        if (foot.IsZeroVector())
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.UndefinedParameter);
        return Result<Primal3<ExactScalar>, GeometryError>.Ok(foot);
    }

    /// <summary>
    ///     关于直线 l 的 Cayley–Klein 反射：中心 p = Ω*(l)、轴 l 的调和同调
    ///     R(X) = X − 2·⟨l,X⟩/⟨l,p⟩·p。
    ///     欧氏反射是 p 在无穷远时的特例；双曲反射是 p 为有限极点时的情形。
    /// </summary>
    public static Result<Primal3<ExactScalar>, GeometryError> ReflectPointAcrossLine(
        this GeometryLens<ExactScalar> lens,
        Primal3<ExactScalar> p,
        Dual3<ExactScalar> l)
    {
        if (p.IsZeroVector() || l.IsZeroVector())
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.ZeroVector);

        var pole = lens.Metric.OmegaStar.Pole(l);
        if (pole.IsZeroVector())
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var denominator = SubspaceAlgebra.Pair(l, pole);
        if (denominator.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var numerator = SubspaceAlgebra.Pair(l, p);
        var factor = 2 * numerator / denominator;
        var reflected = p - factor * pole;

        if (reflected.IsZeroVector())
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(GeometryError.UndefinedParameter);
        return Result<Primal3<ExactScalar>, GeometryError>.Ok(reflected);
    }

    /// <summary>三点外接圆：解 3×4 零空间，返回圆（Q00=Q11）。</summary>
    public static Result<BilinearForm3<ExactScalar>, GeometryError> Circumcircle(
        Primal3<ExactScalar> a,
        Primal3<ExactScalar> b,
        Primal3<ExactScalar> c)
    {
        var pa = ToAffinePoint(a);
        var pb = ToAffinePoint(b);
        var pc = ToAffinePoint(c);
        if (!pa.TryGet(out var p) || !pb.TryGet(out var q) || !pc.TryGet(out var r))
            return Result<BilinearForm3<ExactScalar>, GeometryError>.Fail(GeometryError.AtInfinity);

        var matrix = new ExactScalar[3, 4];
        var points = new[] { p, q, r };
        for (var i = 0; i < 3; i++)
        {
            matrix[i, 0] = points[i].X;
            matrix[i, 1] = points[i].Y;
            matrix[i, 2] = ExactScalar.One;
            matrix[i, 3] = points[i].X * points[i].X + points[i].Y * points[i].Y;
        }

        var nullVector = FindExactNullVector1D(matrix);
        if (nullVector is null)
            return Result<BilinearForm3<ExactScalar>, GeometryError>.Fail(GeometryError.UndeterminedBasis);

        var g = nullVector[3];
        if (g.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<BilinearForm3<ExactScalar>, GeometryError>.Fail(GeometryError.CollinearPoints);

        return Result<BilinearForm3<ExactScalar>, GeometryError>.Ok(new BilinearForm3<ExactScalar>(
            g, ExactScalar.Zero, nullVector[0] / 2,
            g, nullVector[1] / 2, nullVector[2]));
    }

    /// <summary>
    ///     Klein 模型的双曲 cosh²(d)：由直线 pq 与绝对圆 x²+y²−w²=0 的交点做交比，
    ///     再用 cosh²d = (cr+1)²/(4cr)。点必须在绝对圆内（有实交点）。
    /// </summary>
    public static Result<ExactScalar, GeometryError> HyperbolicCoshSquared(
        Primal3<ExactScalar> p,
        Primal3<ExactScalar> q)
    {
        if (p.IsZeroVector() || q.IsZeroVector())
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.ZeroVector);
        if (SubspaceAlgebra.SameSubspace(p, q).TryGet(out var same)
            && same == StrictResult.StrictlyTrue)
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.CoincidentPoints);

        var absolute = new BilinearForm3<ExactScalar>(
            ExactScalar.One, ExactScalar.Zero, ExactScalar.Zero,
            ExactScalar.One, ExactScalar.Zero, -ExactScalar.One);
        var line = SubspaceAlgebra.Join(p, q);
        var hit = ConicIntersection.Intersect(line, absolute);

        if (hit.Kind != LineConicKind.TwoPoints || hit.P1 is null || hit.P2 is null)
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var cross = hit.P1.Value.CrossRatio(hit.P2.Value, p, q);
        if (!cross.TryGet(out var cr))
            return Result<ExactScalar, GeometryError>.Fail(cross.Error);
        if (cr.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return Result<ExactScalar, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var one = ExactScalar.One;
        return Result<ExactScalar, GeometryError>.Ok((cr + one) * (cr + one) / (4 * cr));
    }

    /// <summary>过点 p 的切线切点：p 的极线与二次曲线的交点（0/1/2 个）。</summary>
    public static Result<IReadOnlyList<Primal3<ExactScalar>>, GeometryError> TangentsFromPoint(
        Primal3<ExactScalar> p,
        BilinearForm3<ExactScalar> conic)
    {
        if (p.IsZeroVector())
            return Result<IReadOnlyList<Primal3<ExactScalar>>, GeometryError>.Fail(GeometryError.ZeroVector);

        var polar = conic.Polar(p);
        if (polar.IsZeroVector())
            return Result<IReadOnlyList<Primal3<ExactScalar>>, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var hit = ConicIntersection.Intersect(polar, conic);
        return hit.Kind switch
        {
            LineConicKind.TwoPoints => Result<IReadOnlyList<Primal3<ExactScalar>>, GeometryError>.Ok(
                [hit.P1!.Value, hit.P2!.Value]),
            LineConicKind.Tangent => Result<IReadOnlyList<Primal3<ExactScalar>>, GeometryError>.Ok(
                [hit.P1!.Value]),
            LineConicKind.NoRealIntersection => Result<IReadOnlyList<Primal3<ExactScalar>>, GeometryError>.Ok([]),
            LineConicKind.WholeLineOnConic => Result<IReadOnlyList<Primal3<ExactScalar>>, GeometryError>.Fail(
                GeometryError.LineContainedInConic),
            _ => Result<IReadOnlyList<Primal3<ExactScalar>>, GeometryError>.Fail(GeometryError.DegenerateLine)
        };
    }

    /// <summary>两圆求交：先算根轴，再复用“直线 ∩ 圆”。</summary>
    public static Result<LineConicResult, GeometryError> CircleCircleIntersection(
        BilinearForm3<ExactScalar> c1,
        BilinearForm3<ExactScalar> c2)
    {
        if (!IsCircle(c1) || !IsCircle(c2))
            return Result<LineConicResult, GeometryError>.Fail(GeometryError.UndefinedParameter);

        if (c1.Q00.IsZeroWithFallback() || c2.Q00.IsZeroWithFallback())
            return Result<LineConicResult, GeometryError>.Fail(GeometryError.UndefinedParameter);

        // 各自按 Q00 归一化，抵消“同一圆不同整体尺度”的差异。
        var s1 = ExactScalar.One / c1.Q00;
        var s2 = ExactScalar.One / c2.Q00;
        var radical = new Dual3<ExactScalar>(
            ExactScalar.FromInt(2) * (c1.Q02 * s1 - c2.Q02 * s2),
            ExactScalar.FromInt(2) * (c1.Q12 * s1 - c2.Q12 * s2),
            c1.Q22 * s1 - c2.Q22 * s2);

        if (radical.IsZeroVector())
            return Result<LineConicResult, GeometryError>.Fail(GeometryError.UndefinedParameter);

        return Result<LineConicResult, GeometryError>.Ok(ConicIntersection.Intersect(radical, c1));
    }

    /// <summary>圆：Q01=0 且 Q00=Q11（整体尺度任意）。</summary>
    public static bool IsCircle(this BilinearForm3<ExactScalar> c)
    {
        var zero = ExactScalar.Zero;
        return ExactScalar.AreStrictlyEqual(c.Q01, zero) == StrictResult.StrictlyTrue
               && ExactScalar.AreStrictlyEqual(c.Q00, c.Q11) == StrictResult.StrictlyTrue
               && ExactScalar.AreStrictlyEqual(c.Q00, zero) == StrictResult.StrictlyFalse;
    }

    /// <summary>实仿射二次曲线分类（圆单独列出；符号用 Approx，零点用严格判定）。</summary>
    public static ConicKind Classify(this BilinearForm3<ExactScalar> q)
    {
        if (SubspaceAlgebra.AllZero(q.Q00, q.Q01, q.Q02) == StrictResult.StrictlyTrue
            && SubspaceAlgebra.AllZero(q.Q11, q.Q12, q.Q22) == StrictResult.StrictlyTrue)
            return ConicKind.Degenerate;

        var det = q.Q00 * (q.Q11 * q.Q22 - q.Q12 * q.Q12)
                  - q.Q01 * (q.Q01 * q.Q22 - q.Q12 * q.Q02)
                  + q.Q02 * (q.Q01 * q.Q12 - q.Q11 * q.Q02);
        var delta = q.Q00 * q.Q11 - q.Q01 * q.Q01;

        if (ExactScalar.TrySign(delta, out var ds) != StrictVerdict.StrictlyTrue
            || ExactScalar.TrySign(det, out var dt) != StrictVerdict.StrictlyTrue
            || ExactScalar.TrySign(q.Q00 + q.Q11, out var traceSign) != StrictVerdict.StrictlyTrue)
            return ConicKind.Unknown;

        // det(Q) 是三次型：Q ↦ −Q 会翻符号。实/虚的符号不变量要再乘 Sign(Q00+Q11)。
        var realSign = traceSign * dt;

        if (q.IsCircle())
            return realSign < 0 ? ConicKind.Circle
                 : realSign == 0 ? ConicKind.Point
                 : ConicKind.ImaginaryEllipse;

        if (ds > 0)
            return realSign < 0 ? ConicKind.Ellipse
                 : realSign == 0 ? ConicKind.Point
                 : ConicKind.ImaginaryEllipse;

        if (ds == 0)
        {
            if (dt != 0) return ConicKind.Parabola;
            return IsRankOne(q) ? ConicKind.CoincidentLines : ConicKind.ParallelLines;
        }

        return dt == 0 ? ConicKind.IntersectingLines : ConicKind.Hyperbola;
    }

    private static bool IsRankOne(BilinearForm3<ExactScalar> q)
    {
        var minors = new[]
        {
            q.Q00 * q.Q11 - q.Q01 * q.Q01,
            q.Q00 * q.Q22 - q.Q02 * q.Q02,
            q.Q11 * q.Q22 - q.Q12 * q.Q12,
            q.Q00 * q.Q12 - q.Q01 * q.Q02,
            q.Q01 * q.Q22 - q.Q02 * q.Q12,
            q.Q01 * q.Q12 - q.Q02 * q.Q11
        };

        foreach (var minor in minors)
        {
            if (ExactScalar.TrySign(minor, out var sign) != StrictVerdict.StrictlyTrue)
                return false;
            if (sign != 0) return false;
        }
        return true;
    }
}

public enum LineConicKind
{
    TwoPoints,
    Tangent,
    NoRealIntersection,
    WholeLineOnConic,
    DegenerateLine
}

public readonly record struct LineConicResult(
    LineConicKind Kind,
    Primal3<ExactScalar>? P1,
    Primal3<ExactScalar>? P2,
    ExactScalar Discriminant);

/// <summary>纯射影"直线 ∩ 二次曲线"统计算法（完全不依赖 l∞，但需要开方）。</summary>
public static class ConicIntersection
{
    public static LineConicResult Intersect(Dual3<ExactScalar> line, BilinearForm3<ExactScalar> conic)
    {
        var span = line.SpanLine();
        if (!span.TryGet(out var uv))
            return new LineConicResult(LineConicKind.DegenerateLine, null, null, ExactScalar.Zero);

        var (u, v) = uv;
        var a = conic.Eval(u);
        var b = SubspaceAlgebra.Pair(conic.Polar(u), v);
        var c = conic.Eval(v);

        if (a.IsZeroWithFallback())
        {
            if (b.IsZeroWithFallback())
            {
                return c.IsZeroWithFallback()
                    ? new LineConicResult(LineConicKind.WholeLineOnConic, null, null, ExactScalar.FromInt(0))
                    : new LineConicResult(LineConicKind.Tangent, u, u, ExactScalar.FromInt(0));
            }

            var pOther = c * u - 2 * b * v;
            return new LineConicResult(LineConicKind.TwoPoints, u, pOther, b * b);
        }

        var disc = b * b - a * c;
        if (disc.IsZeroWithFallback())
        {
            var touch = a * v - b * u;
            return new LineConicResult(LineConicKind.Tangent, touch, touch, disc);
        }

        try
        {
            var sqrtDisc = ExactScalar.Sqrt(disc);
            var p1 = (-b + sqrtDisc) * u + a * v;
            var p2 = (-b - sqrtDisc) * u + a * v;
            return new LineConicResult(LineConicKind.TwoPoints, p1, p2, disc);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new LineConicResult(LineConicKind.NoRealIntersection, null, null, disc);
        }
    }

    /// <summary>已知线上一点 knownOnConic 在曲线上时，零根号直接求出另一交点。</summary>
    public static Result<Primal3<ExactScalar>, GeometryError> OtherIntersection(
        Dual3<ExactScalar> lineThroughP,
        Primal3<ExactScalar> knownOnConic,
        BilinearForm3<ExactScalar> conic)
    {
        var uResult = lineThroughP.PickPointOnLineDistinctFrom(knownOnConic);
        if (!uResult.TryGet(out var u))
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(uResult.Error);

        var other = conic.OtherIntersection(knownOnConic, u);
        if (!other.TryGet(out var otherPoint))
            return Result<Primal3<ExactScalar>, GeometryError>.Fail(other.Error);
        return Result<Primal3<ExactScalar>, GeometryError>.Ok(otherPoint);
    }
}
