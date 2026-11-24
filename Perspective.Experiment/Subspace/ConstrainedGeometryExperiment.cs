using System.Numerics;
using System.Text;

namespace Perspective.Experiment.Subspace;

// ============================================================================
// 第一层：精确多元多项式环 Z[x0, x1, ...]（零 double 近似、零除法）
// 定值只是“不含任何变量的 0 次多项式”的特例。
// ============================================================================

/// <summary>单项式：按 VarId 降序排列的稀疏 (VarId, Exp) 列表，构成严格的字典序全序。</summary>
public readonly struct Monomial : IEquatable<Monomial>, IComparable<Monomial>
{
    public static readonly Monomial One = new([]);
    public readonly (int VarId, int Exp)[] Terms;

    private Monomial((int VarId, int Exp)[] terms)
    {
        Terms = terms;
    }

    public bool IsConstant => Terms.Length == 0;
    public int HighestVar => Terms.Length == 0 ? -1 : Terms[0].VarId;

    public static Monomial Var(int varId, int exp = 1) =>
        exp == 0 ? One : new Monomial([(varId, exp)]);

    public int ExpOf(int varId)
    {
        foreach (var (v, e) in Terms)
        {
            if (v == varId) return e;
            if (v < varId) break;
        }
        return 0;
    }

    public Monomial Without(int varId)
    {
        if (ExpOf(varId) == 0) return this;
        var list = new List<(int, int)>(Terms.Length);
        foreach (var t in Terms)
            if (t.VarId != varId)
                list.Add(t);
        return new Monomial([.. list]);
    }

    public static Monomial operator *(Monomial a, Monomial b)
    {
        if (a.IsConstant) return b;
        if (b.IsConstant) return a;

        var res = new List<(int, int)>(a.Terms.Length + b.Terms.Length);
        int i = 0, j = 0;
        while (i < a.Terms.Length && j < b.Terms.Length)
        {
            if (a.Terms[i].VarId > b.Terms[j].VarId)
            {
                res.Add(a.Terms[i++]);
            }
            else if (a.Terms[i].VarId < b.Terms[j].VarId)
            {
                res.Add(b.Terms[j++]);
            }
            else
            {
                res.Add((a.Terms[i].VarId, a.Terms[i].Exp + b.Terms[j].Exp));
                i++;
                j++;
            }
        }
        while (i < a.Terms.Length) res.Add(a.Terms[i++]);
        while (j < b.Terms.Length) res.Add(b.Terms[j++]);
        return new Monomial([.. res]);
    }

    public int CompareTo(Monomial other)
    {
        var n = Math.Min(Terms.Length, other.Terms.Length);
        for (var i = 0; i < n; i++)
        {
            var vc = Terms[i].VarId.CompareTo(other.Terms[i].VarId);
            if (vc != 0) return vc;
            var ec = Terms[i].Exp.CompareTo(other.Terms[i].Exp);
            if (ec != 0) return ec;
        }
        return Terms.Length.CompareTo(other.Terms.Length);
    }

    public bool Equals(Monomial other)
    {
        if (Terms.Length != other.Terms.Length) return false;
        for (var i = 0; i < Terms.Length; i++)
            if (Terms[i] != other.Terms[i])
                return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is Monomial m && Equals(m);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        foreach (var t in Terms) hc.Add(t);
        return hc.ToHashCode();
    }

    public static bool operator ==(Monomial left, Monomial right) => left.Equals(right);

    public static bool operator !=(Monomial left, Monomial right) => !(left == right);
}

/// <summary>整系数稀疏多元多项式（按单项式字典序降序排列）。</summary>
public sealed class MPoly : IEquatable<MPoly>
{
    public static readonly MPoly Zero = new([]);
    public static readonly MPoly One = new([(Monomial.One, BigInteger.One)]);

    private readonly (Monomial M, BigInteger C)[] _terms;

    private MPoly((Monomial M, BigInteger C)[] terms)
    {
        _terms = terms;
    }

    public bool IsZero => _terms.Length == 0;
    public bool IsConstant => IsZero || _terms is [{ M.IsConstant: true }];
    public int HighestVar => IsZero ? -1 : _terms[0].M.HighestVar;
    public int TermCount => _terms.Length;

    public bool Equals(MPoly? other)
    {
        if (other is null || _terms.Length != other._terms.Length) return false;
        for (var i = 0; i < _terms.Length; i++)
            if (_terms[i].M != other._terms[i].M || _terms[i].C != other._terms[i].C)
                return false;
        return true;
    }

    public static MPoly FromLong(long v) =>
        v == 0 ? Zero : new MPoly([(Monomial.One, new BigInteger(v))]);

    public static MPoly FromBigInt(BigInteger v) =>
        v.IsZero ? Zero : new MPoly([(Monomial.One, v)]);

    public static MPoly Var(int varId) => new([(Monomial.Var(varId), BigInteger.One)]);
    public static MPoly PowVar(int varId, int exp) => new([(Monomial.Var(varId, exp), BigInteger.One)]);

    public static implicit operator MPoly(long v) => FromLong(v);

    public bool TryAsConstant(out BigInteger value)
    {
        if (IsZero)
        {
            value = BigInteger.Zero;
            return true;
        }
        if (_terms is [{ M.IsConstant: true }])
        {
            value = _terms[0].C;
            return true;
        }
        value = BigInteger.Zero;
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

    /// <summary>按指定变量拆分成各次幂的系数多项式：coeff[k] 是 varId^k 的系数。</summary>
    public MPoly[] CoeffsIn(int varId)
    {
        var deg = DegreeIn(varId);
        var buckets = new List<(Monomial, BigInteger)>[deg + 1];
        for (var k = 0; k <= deg; k++) buckets[k] = [];

        foreach (var (m, c) in _terms)
        {
            var e = m.ExpOf(varId);
            buckets[e].Add((m.Without(varId), c));
        }

        var result = new MPoly[deg + 1];
        for (var k = 0; k <= deg; k++)
            result[k] = FromSortedTerms(buckets[k]);
        return result;
    }

    public MPoly LeadingCoeffIn(int varId)
    {
        var deg = DegreeIn(varId);
        var terms = new List<(Monomial, BigInteger)>();
        foreach (var (m, c) in _terms)
            if (m.ExpOf(varId) == deg)
                terms.Add((m.Without(varId), c));
        return FromSortedTerms(terms);
    }

    /// <summary>去掉系数的正最大公因数（保符号不变，防止大整数爆炸）。</summary>
    public MPoly PrimitivePart()
    {
        if (IsZero) return Zero;
        var gcd = BigInteger.Zero;
        foreach (var (_, c) in _terms)
        {
            gcd = BigInteger.GreatestCommonDivisor(gcd, BigInteger.Abs(c));
            if (gcd.IsOne) return this;
        }
        if (gcd <= BigInteger.One) return this;

        var scaled = new (Monomial, BigInteger)[_terms.Length];
        for (var i = 0; i < _terms.Length; i++)
            scaled[i] = (_terms[i].M, _terms[i].C / gcd);
        return new MPoly(scaled);
    }

    public static MPoly operator +(MPoly a, MPoly b)
    {
        if (a.IsZero) return b;
        if (b.IsZero) return a;

        var res = new List<(Monomial, BigInteger)>(a._terms.Length + b._terms.Length);
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
                if (!sum.IsZero) res.Add((a._terms[i].M, sum));
                i++;
                j++;
            }
        }
        while (i < a._terms.Length) res.Add(a._terms[i++]);
        while (j < b._terms.Length) res.Add(b._terms[j++]);
        return new MPoly([.. res]);
    }

    public static MPoly operator -(MPoly a)
    {
        if (a.IsZero) return Zero;
        var res = new (Monomial, BigInteger)[a._terms.Length];
        for (var i = 0; i < a._terms.Length; i++)
            res[i] = (a._terms[i].M, -a._terms[i].C);
        return new MPoly(res);
    }

    public static MPoly operator -(MPoly a, MPoly b) => a + -b;

    public static MPoly operator *(MPoly a, MPoly b)
    {
        if (a.IsZero || b.IsZero) return Zero;
        var dict = new Dictionary<Monomial, BigInteger>(a._terms.Length * b._terms.Length);
        foreach (var (ma, ca) in a._terms)
        foreach (var (mb, cb) in b._terms)
        {
            var m = ma * mb;
            var prod = ca * cb;
            dict[m] = dict.TryGetValue(m, out var prev) ? prev + prod : prod;
        }
        return FromDictionary(dict);
    }

    /// <summary>有理替换 varId ← num/den，并统一乘以 den^targetDeg 清除分母（零除法！）。</summary>
    public MPoly SubstituteAndScale(int varId, MPoly num, BigInteger den, int targetDeg)
    {
        if (IsZero) return Zero;
        var coeffs = CoeffsIn(varId);
        var d = coeffs.Length - 1;

        // 先构造 num^k 与 den^(targetDeg - k)
        var numPows = new MPoly[d + 1];
        numPows[0] = One;
        for (var k = 1; k <= d; k++) numPows[k] = numPows[k - 1] * num;

        var denPows = new MPoly[targetDeg + 1];
        denPows[0] = One;
        var denPoly = FromBigInt(den);
        for (var k = 1; k <= targetDeg; k++) denPows[k] = denPows[k - 1] * denPoly;

        var sum = Zero;
        for (var k = 0; k <= d; k++)
        {
            if (coeffs[k].IsZero) continue;
            sum += coeffs[k] * numPows[k] * denPows[targetDeg - k];
        }
        return sum;
    }

    /// <summary>多项式伪除法 Prem(f, g, varId)：满足 lc(g)^k · f ≡ rem (mod g) 且 deg_v(rem) &lt; deg_v(g)。</summary>
    public static MPoly PseudoRemainder(MPoly f, MPoly g, int varId, List<MPoly>? usedInitials = null)
    {
        var dg = g.DegreeIn(varId);
        if (dg <= 0) throw new ArgumentException("除式必须在指定变量上有正次数。");

        var lg = g.LeadingCoeffIn(varId);
        while (!f.IsZero)
        {
            var df = f.DegreeIn(varId);
            if (df < dg) break;

            var lf = f.LeadingCoeffIn(varId);
            f = (lg * f - lf * PowVar(varId, df - dg) * g).PrimitivePart();
            if (usedInitials is not null && !lg.IsConstant && !usedInitials.Contains(lg))
                usedInitials.Add(lg);
        }
        return f;
    }

    public string Format(Func<int, string> varName)
    {
        if (IsZero) return "0";
        var sb = new StringBuilder();
        for (var i = 0; i < _terms.Length; i++)
        {
            var (m, c) = _terms[i];
            var absC = BigInteger.Abs(c);
            if (i == 0)
            {
                if (c < 0) sb.Append('-');
            }
            else
            {
                sb.Append(c < 0 ? " - " : " + ");
            }

            if (m.IsConstant || absC != BigInteger.One)
                sb.Append(absC);

            if (!m.IsConstant)
            {
                if (absC != BigInteger.One) sb.Append('·');
                for (var k = 0; k < m.Terms.Length; k++)
                {
                    if (k > 0) sb.Append('·');
                    var (v, e) = m.Terms[k];
                    sb.Append(varName(v));
                    if (e > 1) sb.Append('^').Append(e);
                }
            }
        }
        return sb.ToString();
    }

    private static MPoly FromDictionary(Dictionary<Monomial, BigInteger> dict)
    {
        var list = new List<(Monomial M, BigInteger C)>(dict.Count);
        foreach (var (m, c) in dict)
            if (!c.IsZero)
                list.Add((m, c));
        return FromSortedTerms(list);
    }

    private static MPoly FromSortedTerms(List<(Monomial M, BigInteger C)> list)
    {
        list.RemoveAll(t => t.C.IsZero);
        list.Sort((a, b) => b.M.CompareTo(a.M));
        return list.Count == 0 ? Zero : new MPoly([.. list]);
    }

    // 加在 MPoly 类里：
    public BigInteger Content()
    {
        var g = BigInteger.Zero;
        foreach (var (_, c) in _terms)
        {
            g = BigInteger.GreatestCommonDivisor(g, BigInteger.Abs(c));
            if (g.IsOne) return BigInteger.One;
        }
        return g;
    }

    public MPoly DivideBy(BigInteger divisor)
    {
        if (IsZero || divisor.IsOne) return this;
        var scaled = new (Monomial, BigInteger)[_terms.Length];
        for (var i = 0; i < _terms.Length; i++)
            scaled[i] = (_terms[i].M, _terms[i].C / divisor);
        return new MPoly(scaled);
    }

    public override bool Equals(object? obj) => obj is MPoly p && Equals(p);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        foreach (var t in _terms) hc.Add(t);
        return hc.ToHashCode();
    }
}

// ============================================================================
// 第二层：统一的几何载体（定点与动点、定线与动线、定曲线与动曲线完全统一！）
// ============================================================================

public readonly record struct GeoPoint(MPoly X, MPoly Y, MPoly W)
{
    public bool IsFixed => X.IsConstant && Y.IsConstant && W.IsConstant;
    public static GeoPoint Fixed(long x, long y, long w = 1) => new(x, y, w);
    public string Format(GeoSpace space) => $"[{space.Format(X)}, {space.Format(Y)}, {space.Format(W)}]";
}

public readonly record struct GeoLine(MPoly A, MPoly B, MPoly C)
{
    public bool IsFixed => A.IsConstant && B.IsConstant && C.IsConstant;
    public static GeoLine Fixed(long a, long b, long c) => new(a, b, c);
    public string Format(GeoSpace space) => $"({space.Format(A)}·X + {space.Format(B)}·Y + {space.Format(C)}·W = 0)";
}

public readonly record struct GeoConic(
    MPoly Q00,
    MPoly Q01,
    MPoly Q02,
    MPoly Q11,
    MPoly Q12,
    MPoly Q22)
{
    public static GeoConic Fixed(long q00, long q01, long q02, long q11, long q12, long q22) =>
        new(q00, q01, q02, q11, q12, q22);

    public GeoLine Polar(GeoPoint p) => new(
        Q00 * p.X + Q01 * p.Y + Q02 * p.W,
        Q01 * p.X + Q11 * p.Y + Q12 * p.W,
        Q02 * p.X + Q12 * p.Y + Q22 * p.W);

    public MPoly Eval(GeoPoint p) => GeoOps.Pair(Polar(p), p);

    public GeoDualConic DualForm() => new(
        Q11 * Q22 - Q12 * Q12, Q02 * Q12 - Q01 * Q22, Q01 * Q12 - Q02 * Q11,
        Q00 * Q22 - Q02 * Q02, Q01 * Q02 - Q00 * Q12,
        Q00 * Q11 - Q01 * Q01);
}

public readonly record struct GeoDualConic(
    MPoly S00,
    MPoly S01,
    MPoly S02,
    MPoly S11,
    MPoly S12,
    MPoly S22)
{
    public GeoPoint Pole(GeoLine l) => new(
        S00 * l.A + S01 * l.B + S02 * l.C,
        S01 * l.A + S11 * l.B + S12 * l.C,
        S02 * l.A + S12 * l.B + S22 * l.C);

    public MPoly Eval(GeoLine l) => GeoOps.Pair(l, Pole(l));
}

/// <summary>统一的几何运算：完全不区分“定点”还是“动点”，一套代码全通吃。</summary>
public static class GeoOps
{
    public static readonly GeoLine InfinityLine = GeoLine.Fixed(0, 0, 1);
    public static readonly GeoDualConic EuclideanOmega = new(1, 0, 0, 1, 0, 0);

    public static MPoly Pair(GeoLine l, GeoPoint p) => l.A * p.X + l.B * p.Y + l.C * p.W;

    public static GeoLine Join(GeoPoint p, GeoPoint q) => new(
        p.Y * q.W - p.W * q.Y,
        p.W * q.X - p.X * q.W,
        p.X * q.Y - p.Y * q.X);

    public static GeoPoint Meet(GeoLine l1, GeoLine l2) => new(
        l1.B * l2.C - l1.C * l2.B,
        l1.C * l2.A - l1.A * l2.C,
        l1.A * l2.B - l1.B * l2.A);

    public static MPoly Det(GeoPoint a, GeoPoint b, GeoPoint c) => Pair(Join(b, c), a);
    public static MPoly Det(GeoLine l1, GeoLine l2, GeoLine l3) => Pair(l1, Meet(l2, l3));

    public static GeoLine PerpendicularThrough(GeoPoint p, GeoLine l) =>
        Join(p, EuclideanOmega.Pole(l));

    public static MPoly PerpendicularCond(GeoLine l1, GeoLine l2) =>
        Pair(l1, EuclideanOmega.Pole(l2));

    public static GeoPoint Midpoint(GeoPoint a, GeoPoint b)
    {
        var wa = Pair(InfinityLine, a);
        var wb = Pair(InfinityLine, b);
        return new GeoPoint(wb * a.X + wa * b.X, wb * a.Y + wa * b.Y, wb * a.W + wa * b.W);
    }
}

// ============================================================================
// 第三层：共享约束空间（线性自动缩维 + 吴方法特征列非线性证明）
// ============================================================================

public enum ProofStatus
{
    Proved,// 在当前约束空间（及列出的 NDG 条件）下严格恒成立
    NotZero,// 未化为 0（不成立，或当前特征列分解尚需分支）
    Inconsistent// 前提约束本身互相矛盾
}

public readonly record struct ProofOutcome(
    ProofStatus Status,
    MPoly Remainder,
    IReadOnlyList<MPoly> CharacteristicSet,
    IReadOnlyList<MPoly> NdgConditions);

public sealed class GeoSpace
{
    private readonly List<MPoly> _constraints = [];
    private readonly Dictionary<int, (MPoly Num, BigInteger Den)> _linearSubs = [];
    private readonly List<string> _varNames = [];

    public int VariableCount => _varNames.Count;
    public int RemainingLinearVariables => _varNames.Count - _linearSubs.Count;

    public string Format(MPoly p) => p.Format(id => _varNames[id]);

    public MPoly NewSymbol(string name)
    {
        var id = _varNames.Count;
        _varNames.Add(name);
        return MPoly.Var(id);
    }

    /// <summary>新建一个自由动点 (x, y, 1)（2 维自由度，尚未施加任何约束）。</summary>
    public GeoPoint NewFreePoint(string name) =>
        new(NewSymbol($"{name}x"), NewSymbol($"{name}y"), MPoly.One);

    // --- 施加约束：同时覆盖线性和非线性 ---

    public void RequireZero(MPoly expr)
    {
        var reduced = ApplySubs(expr).PrimitivePart();
        if (reduced.IsZero) return;

        _constraints.Add(reduced);
        PropagateLinearEliminations();
    }

    public void RequireIncident(GeoLine l, GeoPoint p) => RequireZero(GeoOps.Pair(l, p));
    public void RequireOnConic(GeoPoint p, GeoConic q) => RequireZero(q.Eval(p));
    public void RequirePerpendicular(GeoLine l1, GeoLine l2) => RequireZero(GeoOps.PerpendicularCond(l1, l2));

    /// <summary>把已经消掉的线性变量代回任意点/线，若缩到 0 维，自动变成纯常量定点！</summary>
    public GeoPoint Resolve(GeoPoint p)
    {
        var (x, y, w) = ApplySubsHomogeneous(p.X, p.Y, p.W);
        return new GeoPoint(x, y, w);
    }

    public GeoLine Resolve(GeoLine l)
    {
        var (a, b, c) = ApplySubsHomogeneous(l.A, l.B, l.C);
        return new GeoLine(a, b, c);
    }


    /// <summary>
    ///     在当前约束空间（线性消元 + 非线性吴特征列）下，严格判定结论 expr 是否恒等于 0。
    /// </summary>
    public ProofOutcome ProveZero(MPoly conclusion)
    {
        var target = ApplySubs(conclusion).PrimitivePart();
        var active = _constraints
            .Select(c => ApplySubs(c).PrimitivePart())
            .Where(c => !c.IsZero)
            .ToList();

        foreach (var c in active)
            if (c.IsConstant)
                return new ProofOutcome(ProofStatus.Inconsistent, target, active, []);

        if (target.IsZero)
            return new ProofOutcome(ProofStatus.Proved, MPoly.Zero, active, []);

        if (active.Count == 0)
            return new ProofOutcome(ProofStatus.NotZero, target, [], []);

        // 构造吴方法三角化特征列
        var (consistent, chain) = BuildCharacteristicSet(active);
        if (!consistent)
            return new ProofOutcome(ProofStatus.Inconsistent, target, chain, []);

        var ndg = new List<MPoly>();
        var rem = ReduceByChain(target, chain, ndg);

        return rem.IsZero
            ? new ProofOutcome(ProofStatus.Proved, rem, chain, ndg)
            : new ProofOutcome(ProofStatus.NotZero, rem, chain, ndg);
    }

    // --- 内部线性消元（把一次且首项为常数的约束立即代换消元） ---

    private void PropagateLinearEliminations()
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var i = 0; i < _constraints.Count; i++)
            {
                var c = ApplySubs(_constraints[i]).PrimitivePart();
                _constraints[i] = c;
                if (c.IsZero || c.IsConstant) continue;

                var v = c.HighestVar;
                if (c.DegreeIn(v) != 1) continue;

                var lc = c.LeadingCoeffIn(v);
                if (!lc.TryAsConstant(out var den) || den.IsZero) continue;

                // 满足 den · v + rem = 0 ⇒ v = (-rem) / den
                var rem = c - lc * MPoly.Var(v);
                var num = -rem;
                if (den < 0)
                {
                    den = -den;
                    num = -num;
                }

                // 化简公因子
                var g = BigInteger.GreatestCommonDivisor(den, ContentGcd(num));
                if (g > BigInteger.One)
                {
                    den /= g;
                    num = DivideConstant(num, g);
                }

                _linearSubs[v] = (num, den);
                _constraints[i] = MPoly.Zero;
                CascadeSubstitutionIntoExisting(v, num, den);
                changed = true;
            }
            _constraints.RemoveAll(c => c.IsZero);
        }
    }

    private void CascadeSubstitutionIntoExisting(int v, MPoly num, BigInteger den)
    {
        var keys = _linearSubs.Keys.ToList();
        foreach (var k in keys)
        {
            if (k == v) continue;
            var (oldNum, oldDen) = _linearSubs[k];
            var deg = oldNum.DegreeIn(v);
            if (deg == 0) continue;

            var newNum = oldNum.SubstituteAndScale(v, num, den, deg);
            var newDen = oldDen * BigInteger.Pow(den, deg);
            var g = BigInteger.GreatestCommonDivisor(newDen, ContentGcd(newNum));
            if (g > BigInteger.One)
            {
                newDen /= g;
                newNum = DivideConstant(newNum, g);
            }
            _linearSubs[k] = (newNum, newDen);
        }
    }

    private MPoly ApplySubs(MPoly p)
    {
        if (p.IsZero || p.IsConstant || _linearSubs.Count == 0) return p;

        // 按“剩余变量先代入”的顺序反复替换，直到不再包含已被消元的变量
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (v, (num, den)) in _linearSubs)
            {
                var deg = p.DegreeIn(v);
                if (deg == 0) continue;
                p = p.SubstituteAndScale(v, num, den, deg).PrimitivePart();
                changed = true;
            }
        }
        return p;
    }


    // 注：在 Resolve(GeoPoint) 里必须让 X, Y, W 乘以同一个因子 den^D，所以重写一个齐次三元组缩放版本：
    private (MPoly A, MPoly B, MPoly C) ApplySubsHomogeneous(MPoly a, MPoly b, MPoly c)
    {
        if (_linearSubs.Count == 0) return (a, b, c);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (v, (num, den)) in _linearSubs)
            {
                var d = Math.Max(a.DegreeIn(v), Math.Max(b.DegreeIn(v), c.DegreeIn(v)));
                if (d == 0) continue;
                a = a.SubstituteAndScale(v, num, den, d);
                b = b.SubstituteAndScale(v, num, den, d);
                c = c.SubstituteAndScale(v, num, den, d);
                var g = ContentGcd(a, b, c);
                if (g > BigInteger.One)
                {
                    a = DivideConstant(a, g);
                    b = DivideConstant(b, g);
                    c = DivideConstant(c, g);
                }
                changed = true;
            }
        }
        return (a, b, c);
    }


    // --- 吴方法三角化（Ritt-Wu 特征列） ---

    private static (bool Consistent, List<MPoly> Chain) BuildCharacteristicSet(List<MPoly> pool)
    {
        var working = pool.Distinct().ToList();

        for (var step = 0; step < 200; step++)
        {
            foreach (var p in working)
                if (!p.IsZero && p.IsConstant)
                    return (false, [p]);

            // 1) 为每个出现的 HighestVar 挑选次数最低的多项式组成基本集 (Basic Set)
            var byClass = new SortedDictionary<int, MPoly>();
            foreach (var p in working)
            {
                if (p.IsZero) continue;
                var c = p.HighestVar;
                if (!byClass.TryGetValue(c, out var cur) || IsLowerRank(p, cur, c))
                    byClass[c] = p;
            }

            var chain = byClass.Values.ToList();
            var newRemainders = new List<MPoly>();

            // 2) 用当前特征链约简池中其余多项式
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

        throw new InvalidOperationException("特征列构造超过迭代上限。");
    }

    private static bool IsLowerRank(MPoly a, MPoly b, int cls)
    {
        int da = a.DegreeIn(cls), db = b.DegreeIn(cls);
        if (da != db) return da < db;
        return a.TermCount < b.TermCount;
    }

    private static MPoly ReduceByChain(MPoly f, IReadOnlyList<MPoly> chain, List<MPoly>? ndgOut)
    {
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var g = chain[i];
            var c = g.HighestVar;
            if (f.DegreeIn(c) >= g.DegreeIn(c))
                f = MPoly.PseudoRemainder(f, g, c, ndgOut);
            if (f.IsZero) return MPoly.Zero;
        }
        return f;
    }


    private static BigInteger ContentGcd(params MPoly[] polys)
    {
        var g = BigInteger.Zero;
        foreach (var p in polys)
            g = BigInteger.GreatestCommonDivisor(g, p.Content());
        return g.IsZero ? BigInteger.One : g;
    }

    private static MPoly DivideConstant(MPoly p, BigInteger k) => p.DivideBy(k);
}

// ============================================================================
// 实验运行入口
// ============================================================================

public static class ConstrainedGeometryExperiment
{
    public static void Run()
    {
        Console.WriteLine("\n================ 升级地基：约束空间与定/动统一 ================");

        // --------------------------------------------------------------------
        // [1] 定点只是“动点约束缩到 0 维”的特例（自动线性缩维，零 Approx）
        // --------------------------------------------------------------------
        Console.WriteLine("\n[1] 自由动点 P 逐步加线性约束 → 自动坍缩成纯整数定点");
        var s1 = new GeoSpace();
        var p = s1.NewFreePoint("P");
        Console.WriteLine($"  初始自由动点 P = {s1.Resolve(p).Format(s1)}（剩余自由变量：{s1.RemainingLinearVariables}）");

        s1.RequireIncident(GeoLine.Fixed(2, -1, -4), p);// 2x - y - 4 = 0
        Console.WriteLine(
            $"  加约束 P ∈ (2x-y-4=0) → P = {s1.Resolve(p).Format(s1)}（剩余自由变量：{s1.RemainingLinearVariables}）");

        s1.RequireIncident(GeoLine.Fixed(1, 1, -4), p);// x + y - 4 = 0 ⇒ (8/3, 4/3)
        var fixedP = s1.Resolve(p);
        Console.WriteLine($"  加约束 P ∈ (x+y-4=0)  → P = {fixedP.Format(s1)}，IsFixed={fixedP.IsFixed}（自动坍缩成定点！）");

        // --------------------------------------------------------------------
        // [2] 完全无探针证明任意三角形垂心定理
        // --------------------------------------------------------------------
        Console.WriteLine("\n[2] 无 double 探针：自由点 C(Cx, Cy) 构成的任意三角形垂心共点");
        var s2 = new GeoSpace();
        var triA = GeoPoint.Fixed(0, 0);
        var triB = GeoPoint.Fixed(1, 0);
        var triC = s2.NewFreePoint("C");

        var altA = GeoOps.PerpendicularThrough(triA, GeoOps.Join(triB, triC));
        var altB = GeoOps.PerpendicularThrough(triB, GeoOps.Join(triC, triA));
        var altC = GeoOps.PerpendicularThrough(triC, GeoOps.Join(triA, triB));

        var orthRes = s2.ProveZero(GeoOps.Det(altA, altB, altC));
        Console.WriteLine($"  任意三角形三高线共点？{orthRes.Status}（完全不用 1.73205 探针，直接多项式恒零）");

        // --------------------------------------------------------------------
        // [3] 杀手级验证：耦合动点（不能用单参数化偷懒！）
        //     椭圆 x² + 4y² = 4 上两个动点 A, B 满足 OA ⊥ OB，
        //     证明弦 AB 恒切于定圆 5x² + 5y² = 4（对偶形式 4A² + 4B² - 5C² = 0）
        // --------------------------------------------------------------------
        Console.WriteLine("\n[3] 耦合动弦（吴特征列 + 伪除法）：椭圆上 OA⊥OB 的动弦恒切定圆");
        var s3 = new GeoSpace();
        var origin = GeoPoint.Fixed(0, 0);
        var ellipse = GeoConic.Fixed(1, 0, 0, 4, 0, -4);// x² + 4y² - 4w² = 0
        var targetCircle = GeoConic.Fixed(5, 0, 0, 5, 0, -4);// 5x² + 5y² - 4w² = 0

        var ptA = s3.NewFreePoint("A");
        var ptB = s3.NewFreePoint("B");

        s3.RequireOnConic(ptA, ellipse);
        s3.RequireOnConic(ptB, ellipse);
        s3.RequirePerpendicular(GeoOps.Join(origin, ptA), GeoOps.Join(origin, ptB));

        // 动点照样用完全一样的 GeoOps.Join 连成动弦 AB
        var chordAB = GeoOps.Join(ptA, ptB);
        var tangencyExpr = targetCircle.DualForm().Eval(chordAB);

        var proof = s3.ProveZero(tangencyExpr);
        Console.WriteLine($"  证明状态：{proof.Status}");
        Console.WriteLine("  自动构造的三角化特征列：");
        foreach (var eq in proof.CharacteristicSet)
            Console.WriteLine($"    {s3.Format(eq)} = 0");
        if (proof.NdgConditions.Count > 0)
        {
            Console.WriteLine("  伴随的显式非退化条件 (NDG ≠ 0)：");
            foreach (var ndg in proof.NdgConditions)
                Console.WriteLine($"    {s3.Format(ndg)} ≠ 0");
        }
        Console.WriteLine($"  证明状态：{proof.Status}（余式：{s3.Format(proof.Remainder)}）");
    }
}