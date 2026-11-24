using System.Numerics;
using System.Text;
using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

// ============================================================================
// 一、唯一的一套泛型几何骨架（ExactScalar 与 PolyExpr 共用！）
// ============================================================================

public readonly record struct Primal3<T>(T X, T Y, T W) where T : IExactRing<T>
{
    public static Primal3<T> Fixed(long x, long y, long w = 1) =>
        new(T.FromLong(x), T.FromLong(y), T.FromLong(w));

    public static Primal3<T> operator +(Primal3<T> a, Primal3<T> b) => new(a.X + b.X, a.Y + b.Y, a.W + b.W);
    public static Primal3<T> operator -(Primal3<T> a, Primal3<T> b) => new(a.X - b.X, a.Y - b.Y, a.W - b.W);
    public static Primal3<T> operator -(Primal3<T> a) => new(-a.X, -a.Y, -a.W);
    public static Primal3<T> operator *(T k, Primal3<T> v) => new(k * v.X, k * v.Y, k * v.W);
    public static Primal3<T> operator *(Primal3<T> v, T k) => k * v;
}

public readonly record struct Dual3<T>(T A, T B, T C) where T : IExactRing<T>
{
    public static Dual3<T> Fixed(long a, long b, long c) =>
        new(T.FromLong(a), T.FromLong(b), T.FromLong(c));

    public static Dual3<T> operator +(Dual3<T> a, Dual3<T> b) => new(a.A + b.A, a.B + b.B, a.C + b.C);
    public static Dual3<T> operator -(Dual3<T> a, Dual3<T> b) => new(a.A - b.A, a.B - b.B, a.C - b.C);
    public static Dual3<T> operator -(Dual3<T> a) => new(-a.A, -a.B, -a.C);
    public static Dual3<T> operator *(T k, Dual3<T> v) => new(k * v.A, k * v.B, k * v.C);
    public static Dual3<T> operator *(Dual3<T> v, T k) => k * v;
}

public readonly record struct BilinearForm3<T>(
    T Q00,
    T Q01,
    T Q02,
    T Q11,
    T Q12,
    T Q22) where T : IExactRing<T>
{
    public Dual3<T> Polar(Primal3<T> p) => new(
        Q00 * p.X + Q01 * p.Y + Q02 * p.W,
        Q01 * p.X + Q11 * p.Y + Q12 * p.W,
        Q02 * p.X + Q12 * p.Y + Q22 * p.W);

    public T Eval(Primal3<T> p) => SubspaceAlgebra.Pair(Polar(p), p);

    public DualBilinearForm3<T> DualForm() => new(
        Q11 * Q22 - Q12 * Q12, Q02 * Q12 - Q01 * Q22, Q01 * Q12 - Q02 * Q11,
        Q00 * Q22 - Q02 * Q02, Q01 * Q02 - Q00 * Q12,
        Q00 * Q11 - Q01 * Q01);
}

public readonly record struct DualBilinearForm3<T>(
    T S00,
    T S01,
    T S02,
    T S11,
    T S12,
    T S22) where T : IExactRing<T>
{
    public Primal3<T> Pole(Dual3<T> l) => new(
        S00 * l.A + S01 * l.B + S02 * l.C,
        S01 * l.A + S11 * l.B + S12 * l.C,
        S02 * l.A + S12 * l.B + S22 * l.C);

    public T Eval(Dual3<T> l) => SubspaceAlgebra.Pair(l, Pole(l));
}

public static class SubspaceAlgebra
{
    public static T Pair<T>(Dual3<T> l, Primal3<T> p) where T : IExactRing<T> =>
        l.A * p.X + l.B * p.Y + l.C * p.W;

    public static Dual3<T> Join<T>(Primal3<T> p, Primal3<T> q) where T : IExactRing<T> => new(
        p.Y * q.W - p.W * q.Y,
        p.W * q.X - p.X * q.W,
        p.X * q.Y - p.Y * q.X);

    public static Primal3<T> Meet<T>(Dual3<T> l1, Dual3<T> l2) where T : IExactRing<T> => new(
        l1.B * l2.C - l1.C * l2.B,
        l1.C * l2.A - l1.A * l2.C,
        l1.A * l2.B - l1.B * l2.A);

    public static T Det<T>(Primal3<T> a, Primal3<T> b, Primal3<T> c) where T : IExactRing<T> =>
        Pair(Join(b, c), a);

    public static T Det<T>(Dual3<T> l1, Dual3<T> l2, Dual3<T> l3) where T : IExactRing<T> =>
        Pair(l1, Meet(l2, l3));
}

public readonly record struct GeometryLens<T>(Dual3<T> InfinityLine, DualBilinearForm3<T> OmegaStar)
    where T : IExactRing<T>
{
    public static GeometryLens<T> EuclideanXoy { get; } = new(
        new Dual3<T>(T.Zero, T.Zero, T.One),
        new DualBilinearForm3<T>(T.One, T.Zero, T.Zero, T.One, T.Zero, T.Zero));

    public Primal3<T> Midpoint(Primal3<T> a, Primal3<T> b)
    {
        var wa = SubspaceAlgebra.Pair(InfinityLine, a);
        var wb = SubspaceAlgebra.Pair(InfinityLine, b);
        return wb * a + wa * b;
    }

    public T PerpendicularCond(Dual3<T> l1, Dual3<T> l2) =>
        SubspaceAlgebra.Pair(l1, OmegaStar.Pole(l2));

    public Dual3<T> PerpendicularThrough(Primal3<T> p, Dual3<T> l) =>
        SubspaceAlgebra.Join(p, OmegaStar.Pole(l));
}

// ============================================================================
// 二、重接法核心：系数为 ExactScalar（支持 pi, e, sqrt）的多元多项式 PolyExpr
// ============================================================================

public sealed class PolyExpr : IExactRing<PolyExpr>, IEquatable<PolyExpr>
{
    private readonly (Monomial M, ExactScalar C)[] _terms;

    private PolyExpr((Monomial M, ExactScalar C)[] terms)
    {
        _terms = terms;
    }

    public bool IsZero => _terms.Length == 0;
    public bool IsConstant => IsZero || _terms is [{ M.IsConstant: true }];
    public int HighestVar => IsZero ? -1 : _terms[0].M.HighestVar;
    public int TermCount => _terms.Length;

    public bool Equals(PolyExpr? other)
    {
        if (other is null || _terms.Length != other._terms.Length) return false;
        for (var i = 0; i < _terms.Length; i++)
        {
            if (!_terms[i].M.Equals(other._terms[i].M)) return false;
            if (ExactScalar.AreStrictlyEqual(_terms[i].C, other._terms[i].C) != StrictResult.StrictlyTrue)
                return false;
        }
        return true;
    }

    public static PolyExpr Zero { get; } = new([]);
    public static PolyExpr One { get; } = new([(Monomial.One, ExactScalar.One)]);

    public static PolyExpr FromLong(long v) =>
        v == 0 ? Zero : new PolyExpr([(Monomial.One, ExactScalar.FromInt(v))]);

    public static PolyExpr operator +(PolyExpr a, PolyExpr b)
    {
        if (a.IsZero) return b;
        if (b.IsZero) return a;

        var res = new List<(Monomial, ExactScalar)>(a._terms.Length + b._terms.Length);
        int i = 0, j = 0;
        while (i < a._terms.Length && j < b._terms.Length)
        {
            var cmp = a._terms[i].M.CompareTo(b._terms[j].M);
            if (cmp > 0)
            {
                res.Add(a._terms[i++]);
            }
            else if (cmp < 0)
            {
                res.Add(b._terms[j++]);
            }
            else
            {
                var sum = a._terms[i].C + b._terms[j].C;
                if (!IsCoeffZero(sum)) res.Add((a._terms[i].M, sum));
                i++;
                j++;
            }
        }
        while (i < a._terms.Length) res.Add(a._terms[i++]);
        while (j < b._terms.Length) res.Add(b._terms[j++]);
        return new PolyExpr([.. res]);
    }

    public static PolyExpr operator -(PolyExpr a)
    {
        if (a.IsZero) return Zero;
        var res = new (Monomial, ExactScalar)[a._terms.Length];
        for (var i = 0; i < a._terms.Length; i++)
            res[i] = (a._terms[i].M, -a._terms[i].C);
        return new PolyExpr(res);
    }

    public static PolyExpr operator -(PolyExpr a, PolyExpr b) => a + -b;

    public static PolyExpr operator *(PolyExpr a, PolyExpr b)
    {
        if (a.IsZero || b.IsZero) return Zero;
        var dict = new Dictionary<Monomial, ExactScalar>(a._terms.Length * b._terms.Length);
        foreach (var (ma, ca) in a._terms)
        foreach (var (mb, cb) in b._terms)
        {
            var m = ma * mb;
            var prod = ca * cb;
            dict[m] = dict.TryGetValue(m, out var prev) ? prev + prod : prod;
        }
        return FromDictionary(dict);
    }

    public static StrictResult VerifyZero(PolyExpr a) =>
        a.SimplifyCoeffs().IsZero ? StrictResult.StrictlyTrue : StrictResult.StrictlyFalse;

    public static PolyExpr FromConst(ExactScalar c) =>
        IsCoeffZero(c) ? Zero : new PolyExpr([(Monomial.One, c)]);

    public static PolyExpr Var(int varId) => new([(Monomial.Var(varId), ExactScalar.One)]);
    public static PolyExpr PowVar(int varId, int exp) => new([(Monomial.Var(varId, exp), ExactScalar.One)]);

    public static implicit operator PolyExpr(long v) => FromLong(v);
    public static implicit operator PolyExpr(ExactScalar c) => FromConst(c);

    public bool TryAsConstant(out ExactScalar value)
    {
        if (IsZero)
        {
            value = ExactScalar.Zero;
            return true;
        }
        if (_terms is [{ M.IsConstant: true }])
        {
            value = _terms[0].C;
            return true;
        }
        value = ExactScalar.Zero;
        return false;
    }

    public int DegreeIn(int varId)
    {
        var max = 0;
        foreach (var (m, _) in _terms)
        {
            var e = m.ExpOf(varId);
            if (e > max) max = e;
        }
        return max;
    }

    public PolyExpr[] CoeffsIn(int varId)
    {
        var deg = DegreeIn(varId);
        var buckets = new List<(Monomial, ExactScalar)>[deg + 1];
        for (var k = 0; k <= deg; k++) buckets[k] = [];

        foreach (var (m, c) in _terms)
            buckets[m.ExpOf(varId)].Add((m.Without(varId), c));

        var result = new PolyExpr[deg + 1];
        for (var k = 0; k <= deg; k++)
            result[k] = FromSortedTerms(buckets[k]);
        return result;
    }

    public PolyExpr LeadingCoeffIn(int varId)
    {
        var deg = DegreeIn(varId);
        var terms = new List<(Monomial, ExactScalar)>();
        foreach (var (m, c) in _terms)
            if (m.ExpOf(varId) == deg)
                terms.Add((m.Without(varId), c));
        return FromSortedTerms(terms);
    }

    /// <summary>
    ///     折叠常数系数并尽量去掉纯整数 GCD（保持 pi / sqrt 等超越/根式常数不被人为引进除法）。
    /// </summary>
    public PolyExpr SimplifyCoeffs()
    {
        if (IsZero) return Zero;
        var list = new List<(Monomial M, ExactScalar C)>(_terms.Length);
        var intGcd = BigInteger.Zero;
        var allInt = true;

        foreach (var (m, c) in _terms)
        {
            var sc = c.Simplified();
            if (IsCoeffZero(sc)) continue;
            list.Add((m, sc));

            if (allInt && sc.Exact is not null && BigInteger.TryParse(sc.Exact.ToString(), out var bi))
                intGcd = BigInteger.GreatestCommonDivisor(intGcd, BigInteger.Abs(bi));
            else
                allInt = false;
        }

        if (list.Count == 0) return Zero;
        if (allInt && intGcd > BigInteger.One)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var bi = BigInteger.Parse(list[i].C.Exact!.ToString()) / intGcd;
                var scaled = bi <= long.MaxValue && bi >= long.MinValue
                    ? ExactScalar.FromInt((long)bi)
                    : ExactScalar.FromFormula(bi.ToString());
                list[i] = (list[i].M, scaled);
            }
        }
        return new PolyExpr([.. list]);
    }

    public PolyExpr SubstituteAndScale(int varId, PolyExpr num, ExactScalar den, int targetDeg)
    {
        if (IsZero) return Zero;
        var coeffs = CoeffsIn(varId);
        var d = coeffs.Length - 1;

        var numPows = new PolyExpr[d + 1];
        numPows[0] = One;
        for (var k = 1; k <= d; k++) numPows[k] = numPows[k - 1] * num;

        var denPows = new PolyExpr[targetDeg + 1];
        denPows[0] = One;
        var denPoly = FromConst(den);
        for (var k = 1; k <= targetDeg; k++) denPows[k] = denPows[k - 1] * denPoly;

        var sum = Zero;
        for (var k = 0; k <= d; k++)
        {
            if (coeffs[k].IsZero) continue;
            sum += coeffs[k] * numPows[k] * denPows[targetDeg - k];
        }
        return sum.SimplifyCoeffs();
    }

    public static PolyExpr PseudoRemainder(PolyExpr f, PolyExpr g, int varId, List<PolyExpr>? usedInitials = null)
    {
        var dg = g.DegreeIn(varId);
        if (dg <= 0) throw new ArgumentException("除式必须在指定变量上有正次数。");

        var lg = g.LeadingCoeffIn(varId).SimplifyCoeffs();
        while (!f.IsZero)
        {
            var df = f.DegreeIn(varId);
            if (df < dg) break;

            var lf = f.LeadingCoeffIn(varId);
            // 内层不做全项 SimplifyCoeffs，消除高次项已由 ExactScalar 快排+严判完成
            f = lg * f - lf * PowVar(varId, df - dg) * g;
            if (usedInitials is not null && !lg.IsConstant && !usedInitials.Contains(lg))
                usedInitials.Add(lg);
        }
        return f.SimplifyCoeffs();
    }

    public string Format(Func<int, string> varName)
    {
        if (IsZero) return "0";
        var sb = new StringBuilder();
        for (var i = 0; i < _terms.Length; i++)
        {
            var (m, c) = _terms[i];
            var isNeg = double.IsFinite(c.Approx) && c.Approx < 0;
            var absC = isNeg ? -c : c;

            if (i == 0)
            {
                if (isNeg) sb.Append('-');
            }
            else
            {
                sb.Append(isNeg ? " - " : " + ");
            }

            var cStr = absC.Exact is not null ? absC.Exact.Simplify().ToString() : $"{absC.Approx:G6}";
            if (m.IsConstant)
            {
                sb.Append(cStr.Contains('+') || cStr.Contains('-') ? $"({cStr})" : cStr);
                continue;
            }

            if (cStr != "1")
                sb.Append(cStr.Contains('+') || cStr.Contains('-') || cStr.Contains(' ') ? $"({cStr})·" : $"{cStr}·");

            for (var k = 0; k < m.Terms.Length; k++)
            {
                if (k > 0) sb.Append('·');
                var (v, e) = m.Terms[k];
                sb.Append(varName(v));
                if (e > 1) sb.Append('^').Append(e);
            }
        }
        return sb.ToString();
    }

    // 只哈希结构规范化的单项式，保证与数学相等性 Equals (AreStrictlyEqual) 100% 一致
    public override int GetHashCode()
    {
        unchecked
        {
            var h = _terms.Length;
            foreach (var (m, _) in _terms)
                h = h * 31 + m.GetHashCode();
            return h;
        }
    }

    internal static bool IsCoeffZero(ExactScalar c)
    {
        if (c.Exact is null) return c.IsNumericallyZero();
        return c.VerifyStrictZero() == StrictResult.StrictlyTrue;
    }


    private static PolyExpr FromDictionary(Dictionary<Monomial, ExactScalar> dict)
    {
        var list = new List<(Monomial M, ExactScalar C)>(dict.Count);
        foreach (var (m, c) in dict)
            if (!IsCoeffZero(c))
                list.Add((m, c));
        return FromSortedTerms(list);
    }

    private static PolyExpr FromSortedTerms(List<(Monomial M, ExactScalar C)> list)
    {
        list.RemoveAll(t => IsCoeffZero(t.C));
        list.Sort((a, b) => b.M.CompareTo(a.M));
        return list.Count == 0 ? Zero : new PolyExpr([.. list]);
    }

    public override bool Equals(object? obj) => obj is PolyExpr p && Equals(p);
}

// ============================================================================
// 三、统一约束空间（同时支持 ExactScalar 常量 + 吴特征列消元）
// ============================================================================

public enum UnifiedProofStatus
{
    Proved,
    NotZero,
    Inconsistent
}

public readonly record struct UnifiedProofOutcome(
    UnifiedProofStatus Status,
    PolyExpr Remainder,
    IReadOnlyList<PolyExpr> CharacteristicSet,
    IReadOnlyList<PolyExpr> NdgConditions);

public sealed class UnifiedGeoSpace
{
    private readonly List<PolyExpr> _constraints = [];
    private readonly Dictionary<int, (PolyExpr Num, ExactScalar Den)> _linearSubs = [];
    private readonly List<string> _varNames = [];

    public string Format(PolyExpr p) => p.Format(id => _varNames[id]);
    public string Format(Primal3<PolyExpr> p) => $"[{Format(p.X)}, {Format(p.Y)}, {Format(p.W)}]";

    public PolyExpr NewSymbol(string name)
    {
        var id = _varNames.Count;
        _varNames.Add(name);
        return PolyExpr.Var(id);
    }

    public Primal3<PolyExpr> NewFreePoint(string name) =>
        new(NewSymbol($"{name}x"), NewSymbol($"{name}y"), PolyExpr.One);

    public void RequireZero(PolyExpr expr)
    {
        var reduced = ApplySubs(expr).SimplifyCoeffs();
        if (reduced.IsZero) return;
        _constraints.Add(reduced);
        PropagateLinearEliminations();
    }

    public void RequireIncident(Dual3<PolyExpr> l, Primal3<PolyExpr> p) => RequireZero(SubspaceAlgebra.Pair(l, p));
    public void RequireOnConic(Primal3<PolyExpr> p, BilinearForm3<PolyExpr> q) => RequireZero(q.Eval(p));

    public void RequirePerpendicular(Dual3<PolyExpr> l1, Dual3<PolyExpr> l2) =>
        RequireZero(GeometryLens<PolyExpr>.EuclideanXoy.PerpendicularCond(l1, l2));

    public Primal3<PolyExpr> Resolve(Primal3<PolyExpr> p)
    {
        var (x, y, w) = (p.X, p.Y, p.W);
        var changed = true;
        while (changed && _linearSubs.Count > 0)
        {
            changed = false;
            foreach (var (v, (num, den)) in _linearSubs)
            {
                var d = Math.Max(x.DegreeIn(v), Math.Max(y.DegreeIn(v), w.DegreeIn(v)));
                if (d == 0) continue;
                x = x.SubstituteAndScale(v, num, den, d);
                y = y.SubstituteAndScale(v, num, den, d);
                w = w.SubstituteAndScale(v, num, den, d);
                changed = true;
            }
        }
        return new Primal3<PolyExpr>(x, y, w);
    }

    public UnifiedProofOutcome ProveZero(PolyExpr conclusion)
    {
        var target = ApplySubs(conclusion).SimplifyCoeffs();
        var active = _constraints.Select(c => ApplySubs(c).SimplifyCoeffs()).Where(c => !c.IsZero).ToList();

        foreach (var c in active)
            if (c.IsConstant)
                return new UnifiedProofOutcome(UnifiedProofStatus.Inconsistent, target, active, []);

        if (target.IsZero)
            return new UnifiedProofOutcome(UnifiedProofStatus.Proved, PolyExpr.Zero, active, []);

        if (active.Count == 0)
            return new UnifiedProofOutcome(UnifiedProofStatus.NotZero, target, [], []);

        var (consistent, chain) = BuildCharacteristicSet(active);
        if (!consistent)
            return new UnifiedProofOutcome(UnifiedProofStatus.Inconsistent, target, chain, []);

        var ndg = new List<PolyExpr>();
        var rem = ReduceByChain(target, chain, ndg);
        return rem.IsZero
            ? new UnifiedProofOutcome(UnifiedProofStatus.Proved, rem, chain, ndg)
            : new UnifiedProofOutcome(UnifiedProofStatus.NotZero, rem, chain, ndg);
    }

    private void PropagateLinearEliminations()
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var i = 0; i < _constraints.Count; i++)
            {
                var c = ApplySubs(_constraints[i]).SimplifyCoeffs();
                _constraints[i] = c;
                if (c.IsZero || c.IsConstant) continue;

                var v = c.HighestVar;
                if (c.DegreeIn(v) != 1) continue;

                var lc = c.LeadingCoeffIn(v);
                if (!lc.TryAsConstant(out var den) || PolyExpr.IsCoeffZero(den)) continue;

                var rem = c - lc * PolyExpr.Var(v);
                var num = -rem;
                // 在 PropagateLinearEliminations 里 _linearSubs[v] = (num, den); 前加一行防呆：
                if (num.HighestVar >= v)
                    throw new InvalidOperationException("内部错误：线性消元破坏了变量拓扑序。");
                _linearSubs[v] = (num, den);
                _constraints[i] = PolyExpr.Zero;
                CascadeSubstitution(v, num, den);
                changed = true;
            }
            _constraints.RemoveAll(c => c.IsZero);
        }
    }

    private void CascadeSubstitution(int v, PolyExpr num, ExactScalar den)
    {
        foreach (var k in _linearSubs.Keys.ToList())
        {
            if (k == v) continue;
            var (oldNum, oldDen) = _linearSubs[k];
            var deg = oldNum.DegreeIn(v);
            if (deg == 0) continue;

            var newNum = oldNum.SubstituteAndScale(v, num, den, deg);
            var newDen = oldDen;
            for (var i = 0; i < deg; i++) newDen *= den;
            _linearSubs[k] = (newNum, newDen.Simplified());
        }
    }

    private PolyExpr ApplySubs(PolyExpr p)
    {
        if (p.IsZero || p.IsConstant || _linearSubs.Count == 0) return p;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (v, (num, den)) in _linearSubs)
            {
                var deg = p.DegreeIn(v);
                if (deg == 0) continue;
                p = p.SubstituteAndScale(v, num, den, deg);
                changed = true;
            }
        }
        return p;
    }

    /// <summary>
    ///     真正的 Ritt-Wu 特征列（自约简升列）：
    ///     1) 保证链中任意 g_j 对所有先前的 g_i (i &lt; j) 都在主变量 c_i 上满足 deg_{c_i}(g_j) &lt; deg_{c_i}(g_i)；
    ///     2) 保证每轮特征链在 Ritt 良序下严格下降（以此替代硬编码迭代次数判断）。
    /// </summary>
    private static (bool Consistent, List<PolyExpr> Chain) BuildCharacteristicSet(List<PolyExpr> pool)
    {
        var working = pool.Distinct().ToList();
        List<PolyExpr>? prevChain = null;

        while (true)
        {
            foreach (var p in working)
                if (p is { IsZero: false, IsConstant: true })
                    return (false, [p]);

            var chain = ExtractAscendingBasicSet(working);
            if (prevChain is not null && !IsChainStrictlyLower(chain, prevChain))
                throw new InvalidOperationException("特征列秩未严格下降（可能出现未化简的系数膨胀）。");
            prevChain = chain;

            var newRemainders = new List<PolyExpr>();
            foreach (var p in working)
            {
                if (chain.Contains(p)) continue;
                var rem = ReduceByChain(p, chain, null);
                if (!rem.IsZero && !working.Contains(rem) && !newRemainders.Contains(rem))
                    newRemainders.Add(rem);
            }

            if (newRemainders.Count == 0)
                return (true, chain);

            working.AddRange(newRemainders);
        }
    }

    private static List<PolyExpr> ExtractAscendingBasicSet(List<PolyExpr> pool)
    {
        var sorted = pool.Where(p => !p.IsZero).ToList();
        sorted.Sort((a, b) =>
        {
            var cCmp = a.HighestVar.CompareTo(b.HighestVar);
            if (cCmp != 0) return cCmp;
            var dCmp = a.DegreeIn(a.HighestVar).CompareTo(b.DegreeIn(b.HighestVar));
            return dCmp != 0 ? dCmp : a.TermCount.CompareTo(b.TermCount);
        });

        var basic = new List<PolyExpr>();
        foreach (var p in sorted)
        {
            if (basic.Count == 0)
            {
                basic.Add(p);
                continue;
            }

            if (p.HighestVar <= basic[^1].HighestVar) continue;

            var reduced = true;
            foreach (var b in basic)
            {
                if (p.DegreeIn(b.HighestVar) >= b.DegreeIn(b.HighestVar))
                {
                    reduced = false;
                    break;
                }
            }
            if (reduced) basic.Add(p);
        }
        return basic;
    }

    private static bool IsChainStrictlyLower(IReadOnlyList<PolyExpr> next, IReadOnlyList<PolyExpr> prev)
    {
        var n = Math.Min(next.Count, prev.Count);
        for (var i = 0; i < n; i++)
        {
            int cNext = next[i].HighestVar, cPrev = prev[i].HighestVar;
            if (cNext != cPrev) return cNext < cPrev;

            int dNext = next[i].DegreeIn(cNext), dPrev = prev[i].DegreeIn(cPrev);
            if (dNext != dPrev) return dNext < dPrev;
        }
        // 前缀秩全等时，更长的升列在 Ritt 良序中严格更小
        return next.Count > prev.Count;
    }

    private static PolyExpr ReduceByChain(PolyExpr f, IReadOnlyList<PolyExpr> chain, List<PolyExpr>? ndgOut)
    {
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var g = chain[i];
            var c = g.HighestVar;
            if (f.DegreeIn(c) >= g.DegreeIn(c))
                f = PolyExpr.PseudoRemainder(f, g, c, ndgOut);
            if (f.IsZero) return PolyExpr.Zero;
        }
        return f;
    }
}

// ============================================================================
// 四、合流验证实验：一套几何函数同时驱动 ExactScalar 与 PolyExpr（含 π 和 √3）
// ============================================================================

public static class UnifiedAlgebraExperiment
{
    /// <summary>完全同一份泛型几何代码：既能吃 ExactScalar，也能吃 PolyExpr！</summary>
    private static T OrthocenterDet<T>(Primal3<T> a, Primal3<T> b, Primal3<T> c) where T : IExactRing<T>
    {
        var lens = GeometryLens<T>.EuclideanXoy;
        var altA = lens.PerpendicularThrough(a, SubspaceAlgebra.Join(b, c));
        var altB = lens.PerpendicularThrough(b, SubspaceAlgebra.Join(c, a));
        var altC = lens.PerpendicularThrough(c, SubspaceAlgebra.Join(a, b));
        return SubspaceAlgebra.Det(altA, altB, altC);
    }

    public static void Run()
    {
        Console.WriteLine("\n================ 重接法：ExactScalar + PolyExpr 彻底合流 ================");

        // [1] 同一份 OrthocenterDet<T> 跑在 ExactScalar（含 π 与 √3）上
        var pi = ExactScalar.FromFormula("pi");
        var sqrt3 = ExactScalar.FromFormula("sqrt(3)");
        var detExact = OrthocenterDet(
            Primal3<ExactScalar>.Fixed(0, 0),
            new Primal3<ExactScalar>(pi, 0, 1),
            new Primal3<ExactScalar>(sqrt3, pi + 1, 1));
        Console.WriteLine($"  [1a] 同一套泛型垂心函数跑 ExactScalar(π, √3)：{detExact.VerifyStrictZero()}");

        // [1b] 同一份 OrthocenterDet<T> 跑在 PolyExpr（任意符号自由点 C）上
        var s1 = new UnifiedGeoSpace();
        var resPoly = s1.ProveZero(OrthocenterDet(
            Primal3<PolyExpr>.Fixed(0, 0),
            Primal3<PolyExpr>.Fixed(1, 0),
            s1.NewFreePoint("C")));
        Console.WriteLine($"  [1b] 同一套泛型垂心函数跑 PolyExpr(自由点 C)：{resPoly.Status}");

        // [2] 含超越数 π 系数的线性约束自动缩维成精确齐次定点
        Console.WriteLine("\n  [2] 含 π 的线性约束自动缩维：");
        var s2 = new UnifiedGeoSpace();
        var p = s2.NewFreePoint("P");
        s2.RequireIncident(new Dual3<PolyExpr>(pi, -1, -1), p);// π·x - y - 1 = 0
        s2.RequireIncident(Dual3<PolyExpr>.Fixed(1, 1, -3), p);// x + y - 3 = 0
        Console.WriteLine($"    动点 P 自动坍缩为精确定点 = {s2.Format(s2.Resolve(p))}");

        // [3] 半轴为超越数 π 和 √3 的椭圆：x²/π² + y²/3 = 1（即 3x² + π²y² - 3π²w² = 0）
        //     两个耦合动点 OA ⊥ OB，证明弦 AB 恒切于定圆 (3+π²)x² + (3+π²)y² - 3π²w² = 0！
        Console.WriteLine("\n  [3] 超越数 π + √3 椭圆上的耦合动弦 OA⊥OB 证明：");
        var s3 = new UnifiedGeoSpace();
        var pi2 = pi * pi;
        var origin = Primal3<PolyExpr>.Fixed(0, 0);
        var ellipse = new BilinearForm3<PolyExpr>(3, 0, 0, pi2, 0, -3 * pi2);
        var targetCircle = new BilinearForm3<PolyExpr>(3 + pi2, 0, 0, 3 + pi2, 0, -3 * pi2);

        var ptA = s3.NewFreePoint("A");
        var ptB = s3.NewFreePoint("B");
        s3.RequireOnConic(ptA, ellipse);
        s3.RequireOnConic(ptB, ellipse);
        s3.RequirePerpendicular(SubspaceAlgebra.Join(origin, ptA), SubspaceAlgebra.Join(origin, ptB));

        var chordAB = SubspaceAlgebra.Join(ptA, ptB);
        var proof = s3.ProveZero(targetCircle.DualForm().Eval(chordAB));

        Console.WriteLine($"    证明状态：{proof.Status}（余式：{s3.Format(proof.Remainder)}）");
        foreach (var eq in proof.CharacteristicSet)
            Console.WriteLine($"    特征列：{s3.Format(eq)} = 0");
        foreach (var ndg in proof.NdgConditions)
            Console.WriteLine($"    NDG：  {s3.Format(ndg)} ≠ 0");
    }
}