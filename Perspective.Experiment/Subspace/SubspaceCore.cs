using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

/// <summary>原空间 V 中的向量（射影点 = 一维子空间，不带方向）。</summary>
public readonly record struct Primal3(ExactScalar X, ExactScalar Y, ExactScalar W)
{
    public static Primal3 operator +(Primal3 a, Primal3 b) => new(a.X + b.X, a.Y + b.Y, a.W + b.W);
    public static Primal3 operator -(Primal3 a, Primal3 b) => new(a.X - b.X, a.Y - b.Y, a.W - b.W);
    public static Primal3 operator -(Primal3 a) => new(-a.X, -a.Y, -a.W);
    public static Primal3 operator *(ExactScalar k, Primal3 v) => new(k * v.X, k * v.Y, k * v.W);
    public static Primal3 operator *(Primal3 v, ExactScalar k) => k * v;

    public StrictResult VerifyZero() => SubspaceOps.AllZero(X, Y, W);

    public bool IsNumericallyZero(double tol = 1e-9) =>
        X.IsNumericallyZero(tol) && Y.IsNumericallyZero(tol) && W.IsNumericallyZero(tol);

    public override string ToString() => $"[{X}, {Y}, {W}]";
}

/// <summary>对偶空间 V* 中的余向量（射影直线 = ker(l) 二维子空间）。</summary>
public readonly record struct Dual3(ExactScalar A, ExactScalar B, ExactScalar C)
{
    public static Dual3 operator +(Dual3 a, Dual3 b) => new(a.A + b.A, a.B + b.B, a.C + b.C);
    public static Dual3 operator -(Dual3 a, Dual3 b) => new(a.A - b.A, a.B - b.B, a.C - b.C);
    public static Dual3 operator -(Dual3 a) => new(-a.A, -a.B, -a.C);
    public static Dual3 operator *(ExactScalar k, Dual3 v) => new(k * v.A, k * v.B, k * v.C);
    public static Dual3 operator *(Dual3 v, ExactScalar k) => k * v;

    public StrictResult VerifyZero() => SubspaceOps.AllZero(A, B, C);

    public bool IsNumericallyZero(double tol = 1e-9) =>
        A.IsNumericallyZero(tol) && B.IsNumericallyZero(tol) && C.IsNumericallyZero(tol);

    public override string ToString() => $"({A}·X + {B}·Y + {C}·W = 0)";
}

/// <summary>纯向量空间/外代数运算（零度量、零定向、公共选基底与调和判定集中在此）。</summary>
public static class SubspaceOps
{
    private static readonly Primal3[] BasisPoints = [new(0, 0, 1), new(1, 0, 0), new(0, 1, 0)];
    private static readonly Dual3[] BasisLines = [new(0, 0, 1), new(1, 0, 0), new(0, 1, 0)];

    public static ExactScalar Pair(Dual3 l, Primal3 p) =>
        l.A * p.X + l.B * p.Y + l.C * p.W;

    public static StrictResult IsIncident(Dual3 l, Primal3 p) =>
        Pair(l, p).VerifyStrictZero();

    /// <summary>两点张成直线（代数上与 Meet 同为三维叉积，但方向从 V×V 到 V*）。</summary>
    public static Dual3 Join(Primal3 p, Primal3 q)
    {
        var (a, b, c) = Cross(p.X, p.Y, p.W, q.X, q.Y, q.W);
        return new Dual3(a, b, c);
    }

    /// <summary>两线求交（代数上与 Join 同为三维叉积，方向从 V*×V* 到 V）。</summary>
    public static Primal3 Meet(Dual3 l1, Dual3 l2)
    {
        var (x, y, w) = Cross(l1.A, l1.B, l1.C, l2.A, l2.B, l2.C);
        return new Primal3(x, y, w);
    }

    public static ExactScalar Det(Primal3 a, Primal3 b, Primal3 c) => Pair(Join(b, c), a);
    public static ExactScalar Det(Dual3 l1, Dual3 l2, Dual3 l3) => Pair(l1, Meet(l2, l3));

    public static StrictResult AreCollinear(Primal3 a, Primal3 b, Primal3 c) => Det(a, b, c).VerifyStrictZero();
    public static StrictResult AreConcurrent(Dual3 l1, Dual3 l2, Dual3 l3) => Det(l1, l2, l3).VerifyStrictZero();

    public static bool IsZeroVector(Primal3 p) =>
        p.VerifyZero() == StrictResult.StrictlyTrue || p.X.Exact is null && p.IsNumericallyZero();

    public static bool IsZeroVector(Dual3 l) =>
        l.VerifyZero() == StrictResult.StrictlyTrue || l.A.Exact is null && l.IsNumericallyZero();

    public static StrictResult SameSubspace(Primal3 a, Primal3 b)
    {
        var w = Join(a, b);
        return AllZero(w.A, w.B, w.C);
    }

    public static StrictResult SameSubspace(Dual3 a, Dual3 b)
    {
        var m = Meet(a, b);
        return AllZero(m.X, m.Y, m.W);
    }

    public static bool AreDistinct(Primal3 a, Primal3 b)
    {
        var same = SameSubspace(a, b);
        if (same == StrictResult.StrictlyFalse) return true;
        if (same == StrictResult.StrictlyTrue) return false;
        return !Join(a, b).IsNumericallyZero();
    }

    /// <summary>纯射影交比 (A, B; C, D)，要求四点共线且 A≠B、A≠D、B≠C。</summary>
    public static ExactScalar CrossRatio(Primal3 a, Primal3 b, Primal3 c, Primal3 d)
    {
        if (IsZeroVector(a) || IsZeroVector(b) || IsZeroVector(c) || IsZeroVector(d))
            throw new ArgumentException("交比计算不支持零向量。");
        if (SameSubspace(a, b) == StrictResult.StrictlyTrue)
            throw new ArgumentException("退化：前两点 A、B 重合，无法确定公共直线。");
        if (AreCollinear(a, b, c) == StrictResult.StrictlyFalse ||
            AreCollinear(a, b, d) == StrictResult.StrictlyFalse)
            throw new ArgumentException("前置条件不满足：CrossRatio 要求四点共线。");

        var o = PickNonIncidentPoint(Join(a, b));
        var num = Det(o, a, c) * Det(o, b, d);
        var den = Det(o, a, d) * Det(o, b, c);

        if (den.IsZeroWithFallback(treatUnprovenAsZero: true))
            throw new DivideByZeroException("交比分母为零（可能由于 A 与 D 重合，或 B 与 C 重合）。");

        return num / den;
    }

    /// <summary>纯多项式零除法判定四点调和 (A, B; C, D) = -1。</summary>
    public static StrictResult IsHarmonic(Primal3 a, Primal3 b, Primal3 c, Primal3 d)
    {
        if (IsZeroVector(a) || IsZeroVector(b) || IsZeroVector(c) || IsZeroVector(d))
            return StrictResult.StrictlyFalse;
        if (!AreDistinct(a, b))
            return StrictResult.StrictlyFalse;
        if (AreCollinear(a, b, c) == StrictResult.StrictlyFalse ||
            AreCollinear(a, b, d) == StrictResult.StrictlyFalse)
            return StrictResult.StrictlyFalse;

        var o = PickNonIncidentPoint(Join(a, b));
        var num = Det(o, a, c) * Det(o, b, d);
        var den = Det(o, a, d) * Det(o, b, c);

        var denZero = den.VerifyStrictZero();
        if (denZero == StrictResult.StrictlyTrue) return StrictResult.StrictlyFalse;
        if (denZero == StrictResult.NoSymbolicData)
            return den.IsNumericallyZero() ? StrictResult.StrictlyFalse : StrictResult.NoSymbolicData;
        if (denZero == StrictResult.NearZeroUnproven)
            return StrictResult.NearZeroUnproven;

        return (num + den).VerifyStrictZero();
    }

    /// <summary>为直线挑选一个肯定不在其上的标准基点。</summary>
    public static Primal3 PickNonIncidentPoint(Dual3 line)
    {
        Primal3? best = null;
        double bestAbs = -1;

        foreach (var e in BasisPoints)
        {
            var pair = Pair(line, e);
            if (pair.VerifyStrictZero() == StrictResult.StrictlyFalse)
            {
                var abs = Math.Abs(pair.Approx);
                if (abs > bestAbs)
                {
                    bestAbs = abs;
                    best = e;
                }
            }
        }

        if (best is not null) return best.Value;

        foreach (var e in BasisPoints)
        {
            var pair = Pair(line, e);
            if (!pair.IsNumericallyZero())
            {
                var abs = Math.Abs(pair.Approx);
                if (abs > bestAbs)
                {
                    bestAbs = abs;
                    best = e;
                }
            }
        }

        return best ?? throw new InvalidOperationException("退化：直线为零向量或无法确定非共线参考点。");
    }

    /// <summary>为直线挑选两个线性无关的支撑点 (U, V)。</summary>
    public static (Primal3 U, Primal3 V) SpanLine(Dual3 line)
    {
        if (IsZeroVector(line))
            throw new ArgumentException("直线不能为零向量。", nameof(line));

        Primal3? first = null;
        foreach (var axis in BasisLines)
        {
            var pt = Meet(line, axis);
            if (IsZeroVector(pt)) continue;
            if (first is null)
            {
                first = pt;
                continue;
            }
            if (AreDistinct(first.Value, pt))
                return (first.Value, pt);
        }

        throw new InvalidOperationException("无法为给定直线构造两个独立支撑点。");
    }

    /// <summary>在直线上找一个与 anchor 不同的点；若传入 preferFiniteAgainst 则优先挑远离它的点。</summary>
    public static Primal3 PickPointOnLineDistinctFrom(
        Dual3 line, Primal3 anchor, Dual3? preferFiniteAgainst = null)
    {
        Primal3? best = null;
        double bestScore = -1;

        foreach (var axis in BasisLines)
        {
            var cand = Meet(line, axis);
            if (IsZeroVector(cand)) continue;
            if (SameSubspace(anchor, cand) == StrictResult.StrictlyFalse)
            {
                var score = ScoreCandidate(cand, preferFiniteAgainst);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = cand;
                }
            }
        }

        if (best is not null) return best.Value;

        foreach (var axis in BasisLines)
        {
            var cand = Meet(line, axis);
            if (IsZeroVector(cand)) continue;
            if (!Join(anchor, cand).IsNumericallyZero())
            {
                var score = ScoreCandidate(cand, preferFiniteAgainst);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = cand;
                }
            }
        }

        return best ?? throw new InvalidOperationException("无法在直线上找到与给定点不重合的第二点。");
    }

    private static double ScoreCandidate(Primal3 cand, Dual3? preferFiniteAgainst) =>
        preferFiniteAgainst is not null
            ? Math.Abs(Pair(preferFiniteAgainst.Value, cand).Approx)
            : Math.Max(Math.Abs(cand.X.Approx),
                Math.Max(Math.Abs(cand.Y.Approx), Math.Abs(cand.W.Approx)));

    private static (ExactScalar X, ExactScalar Y, ExactScalar Z) Cross(
        ExactScalar x1, ExactScalar y1, ExactScalar z1,
        ExactScalar x2, ExactScalar y2, ExactScalar z2) =>
        (y1 * z2 - z1 * y2, z1 * x2 - x1 * z2, x1 * y2 - y1 * x2);

    internal static StrictResult AllZero(ExactScalar x, ExactScalar y, ExactScalar z)
    {
        var rx = x.VerifyStrictZero();
        var ry = y.VerifyStrictZero();
        var rz = z.VerifyStrictZero();

        if (rx == StrictResult.StrictlyFalse || ry == StrictResult.StrictlyFalse || rz == StrictResult.StrictlyFalse)
            return StrictResult.StrictlyFalse;
        if (rx == StrictResult.StrictlyTrue && ry == StrictResult.StrictlyTrue && rz == StrictResult.StrictlyTrue)
            return StrictResult.StrictlyTrue;
        if (rx == StrictResult.NoSymbolicData || ry == StrictResult.NoSymbolicData || rz == StrictResult.NoSymbolicData)
            return StrictResult.NoSymbolicData;
        return StrictResult.NearZeroUnproven;
    }
}

/// <summary>射影线性映射 V → V（严格区分原始矩阵乘 Multiply 与几何作用 Apply）。</summary>
public readonly record struct LinearMap3(
    ExactScalar M00,
    ExactScalar M01,
    ExactScalar M02,
    ExactScalar M10,
    ExactScalar M11,
    ExactScalar M12,
    ExactScalar M20,
    ExactScalar M21,
    ExactScalar M22)
{
    public ExactScalar Determinant() =>
        M00 * (M11 * M22 - M12 * M21)
        - M01 * (M10 * M22 - M12 * M20)
        + M02 * (M10 * M21 - M11 * M20);

    public StrictResult IsInvertible() =>
        Determinant().VerifyStrictZero() switch
        {
            StrictResult.StrictlyFalse => StrictResult.StrictlyTrue,
            StrictResult.StrictlyTrue => StrictResult.StrictlyFalse,
            var other => other
        };

    public Primal3 Multiply(Primal3 p) => new(
        M00 * p.X + M01 * p.Y + M02 * p.W,
        M10 * p.X + M11 * p.Y + M12 * p.W,
        M20 * p.X + M21 * p.Y + M22 * p.W);

    public Dual3 Multiply(Dual3 l) => new(
        M00 * l.A + M01 * l.B + M02 * l.C,
        M10 * l.A + M11 * l.B + M12 * l.C,
        M20 * l.A + M21 * l.B + M22 * l.C);

    public LinearMap3 Multiply(LinearMap3 r) => new(
        M00 * r.M00 + M01 * r.M10 + M02 * r.M20, M00 * r.M01 + M01 * r.M11 + M02 * r.M21,
        M00 * r.M02 + M01 * r.M12 + M02 * r.M22,
        M10 * r.M00 + M11 * r.M10 + M12 * r.M20, M10 * r.M01 + M11 * r.M11 + M12 * r.M21,
        M10 * r.M02 + M11 * r.M12 + M12 * r.M22,
        M20 * r.M00 + M21 * r.M10 + M22 * r.M20, M20 * r.M01 + M21 * r.M11 + M22 * r.M21,
        M20 * r.M02 + M21 * r.M12 + M22 * r.M22);

    public LinearMap3 Transpose() => new(
        M00, M10, M20,
        M01, M11, M21,
        M02, M12, M22);

    /// <summary>余子式矩阵 C = (adj M)^T。</summary>
    public LinearMap3 CofactorMatrix() => new(
        M11 * M22 - M12 * M21, M12 * M20 - M10 * M22, M10 * M21 - M11 * M20,
        M02 * M21 - M01 * M22, M00 * M22 - M02 * M20, M01 * M20 - M00 * M21,
        M01 * M12 - M02 * M11, M02 * M10 - M00 * M12, M00 * M11 - M01 * M10);

    public Primal3 Apply(Primal3 p) => Multiply(p);
    public Dual3 Apply(Dual3 l) => CofactorMatrix().Multiply(l);
}