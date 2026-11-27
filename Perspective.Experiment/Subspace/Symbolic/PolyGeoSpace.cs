// ============================================================================
// PolyGeoSpace：PolyExpr（系数为 ExactScalar 的稀疏多元多项式）
//               + UnifiedGeoSpace（线性缩维 + 吴特征列/伪除法证明）
//
// 这是"把几何骨架 T 换成多项式环"的那一半：因为骨架只依赖 IExactRing<T>，
// 同一份 Join/Meet/Det/Perpendicular 既能吃 ExactScalar，也能吃 PolyExpr。
// ============================================================================
using System.Numerics;
using System.Text;
using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

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

/// <summary>系数为 ExactScalar 的稀疏多元多项式：既能装 π、√3，也能装符号变量。</summary>
public sealed class PolyExpr : IExactRing<PolyExpr>, IIntegralDomain<PolyExpr>, IEquatable<PolyExpr>
{
    private readonly (Monomial M, ExactScalar C)[] _terms;

    private PolyExpr((Monomial M, ExactScalar C)[] terms)
    {
        _terms = terms;
    }

    /// <summary>数值探测（零除法）：给每个 VarId 一个 double 探针值。</summary>
    public double EvalApprox(Func<int, double> varVal)
    {
        double sum = 0;
        foreach (var (m, c) in _terms)
        {
            double term = c.Approx;
            foreach (var (v, e) in m.Terms)
                term *= Math.Pow(varVal(v), e);
            sum += term;
        }
        return sum;
    }

    public bool IsZero => _terms.Length == 0;
    public bool IsConstant => IsZero || _terms is [{ M.IsConstant: true }];
    public int HighestVar => IsZero ? -1 : _terms[0].M.HighestVar;
    public int TermCount => _terms.Length;
    internal IReadOnlyList<(Monomial M, ExactScalar C)> RawTerms => _terms;

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

    /// <summary>多项式零判定（结构 + 系数化简）。</summary>
    /// <remarks>
    ///     注意：只返回 StrictlyTrue / StrictlyFalse。系数含纯近似数时 <c>IsCoeffZero</c>
    ///     会按数值容差压成零，因此混合 <c>ExactScalar.FromApprox</c> 的链路里它可能把
    ///     “近似零”报成严格零；纯符号证明场景不受影响。
    /// </remarks>
    public static StrictResult VerifyZero(PolyExpr a)
    {
        var simplified = a.SimplifyCoeffs();
        if (simplified.IsZero) return StrictResult.StrictlyTrue;

        // 纯近似系数不能被当成“已证非零”，也不能被容差吞成零。
        if (simplified.RawTerms.Any(t => t.C.Exact is null))
            return StrictResult.NoSymbolicData;
        return StrictResult.StrictlyFalse;
    }

    static bool IExactRing<PolyExpr>.IsZeroFast(PolyExpr a) => a.IsZero;

    /// <summary>多项式环的单位只有非零常数；非常数多项式确定不是单位。</summary>
    static StrictVerdict IExactRing<PolyExpr>.IsUnit(PolyExpr a)
    {
        if (!a.TryAsConstant(out var c))
            return StrictVerdict.StrictlyFalse;

        return c.VerifyStrictZero() switch
        {
            StrictResult.StrictlyTrue => StrictVerdict.StrictlyFalse,
            StrictResult.StrictlyFalse => StrictVerdict.StrictlyTrue,
            StrictResult.NearZeroUnproven => StrictVerdict.Unknown,
            _ => StrictVerdict.NoSymbolicData
        };
    }

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

        // 注意：这里绝不能对 lg 做 SimplifyCoeffs！
        // 它会把系数多项式的整体整数内容约掉（如 4 → 1），而 g 本身没被同倍约分，
        // 于是 lg*f - lf*g 抵消不掉首项、次数永不下降（旧版就是在这一步死循环的）。
        var lg = g.LeadingCoeffIn(varId);
        while (!f.IsZero)
        {
            var df = f.DegreeIn(varId);
            if (df < dg) break;

            var lf = f.LeadingCoeffIn(varId);
            // 内层不做全项 SimplifyCoeffs，消除高次项已由 ExactScalar 快排+严判完成
            f = lg * f - lf * PowVar(varId, df - dg) * g;

            if (f.DegreeIn(varId) >= df)
                throw new InvalidOperationException(
                    $"伪除法未能消去首项（var={varId}, deg={df}）：系数环未做规范化时不会死循环，直接报错。" +
                    $"请检查 lg/lf 是否被意外约分或 ExactScalar 是否判定失败。");
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

    internal static bool IsCoeffZero(ExactScalar c) =>
        c.VerifyStrictZero() == StrictResult.StrictlyTrue;

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

public enum UnifiedProofStatus
{
    Proved,
    ProvedUnderConditions,
    NotZero,
    Inconsistent
}

public readonly record struct UnifiedProofOutcome(
    UnifiedProofStatus Status,
    PolyExpr Remainder,
    IReadOnlyList<PolyExpr> CharacteristicSet,
    IReadOnlyList<PolyExpr> NdgConditions);

/// <summary>统一约束空间：线性约束自动缩维 + 非线性约束走吴方法（Ritt-Wu）特征列。</summary>
public sealed class UnifiedGeoSpace
{
    private readonly List<PolyExpr> _constraints = [];
    private readonly Dictionary<int, (PolyExpr Num, ExactScalar Den)> _linearSubs = [];
    private readonly List<string> _varNames = [];
    private readonly HashSet<PolyExpr> _nonzeroWitnesses = [];

    public int VariableCount => _varNames.Count;

    public string Format(PolyExpr p) => p.Format(id => id < _varNames.Count ? _varNames[id] : $"v{id}");
    public string Format(Primal3<PolyExpr> p) => $"[{Format(p.X)}, {Format(p.Y)}, {Format(p.W)}]";

    public (bool Consistent, IReadOnlyList<PolyExpr> Chain) GetCharacteristicSet()
    {
        var active = _constraints.Select(c => ApplySubs(c).SimplifyCoeffs()).Where(c => !c.IsZero).ToList();
        foreach (var c in active)
            if (c.IsConstant) return (false, [c]);
        if (active.Count == 0) return (true, []);
        var (ok, chain) = BuildCharacteristicSet(active);
        return (ok, chain);
    }

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

    /// <summary>
    ///     添加 g ≠ 0 约束（Rabinowitsch 技巧）：引入辅助变量 z，令 z·g − 1 = 0。
    ///     g = 0 时无解，从而在特征列里自动杀掉退化分支；g ≠ 0 时 z = 1/g 存在。
    /// </summary>
    public void RequireNonZero(PolyExpr expr)
    {
        var reduced = ApplySubs(expr).SimplifyCoeffs();
        if (reduced.IsZero)
        {
            // 直接要求 1 = 0，让 ProveZero/GetCharacteristicSet 报 Inconsistent。
            _constraints.Add(PolyExpr.One);
            return;
        }
        if (reduced.IsConstant) return;

        var witness = NewSymbol($"z{VariableCount}");
        _nonzeroWitnesses.Add(witness);
        RequireZero(witness * reduced - PolyExpr.One);
    }

    public void RequireIncident(Dual3<PolyExpr> l, Primal3<PolyExpr> p) => RequireZero(SubspaceAlgebra.Pair(l, p));
    public void RequireOnConic(Primal3<PolyExpr> p, BilinearForm3<PolyExpr> q) => RequireZero(q.Eval(p));

    /// <summary>添加“两线垂直”约束；零向量直线会抛 ArgumentException（DSL 内部应保证非零）。</summary>
    public void RequirePerpendicular(Dual3<PolyExpr> l1, Dual3<PolyExpr> l2)
    {
        var condition = GeometryLens<PolyExpr>.EuclideanXoy.Metric.PerpendicularCond(l1, l2);
        if (!condition.TryGet(out var value))
            throw new ArgumentException($"RequirePerpendicular: {condition.Error}", nameof(l1));
        RequireZero(value);
    }

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

        // RequireNonZero 的见证变量 z 满足 z·g−1=0，故 z≠0 已由约束保证。
        ndg.RemoveAll(p => _nonzeroWitnesses.Contains(p));

        if (!rem.IsZero)
            return new UnifiedProofOutcome(UnifiedProofStatus.NotZero, rem, chain, ndg);

        // 伪除法只证明了“在非退化初式非零时”成立；ndg 非空不能当无条件 Proved。
        return ndg.Count == 0
            ? new UnifiedProofOutcome(UnifiedProofStatus.Proved, rem, chain, ndg)
            : new UnifiedProofOutcome(UnifiedProofStatus.ProvedUnderConditions, rem, chain, ndg);
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
                // 线性消元必须保持"变量拓扑序"：被解出的变量不能出现在自己的解里。
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

/// <summary>一元多项式（系数升序，ExactScalar 系数），供有理函数值域分析使用。</summary>
internal static class UniPoly
{
    public static ExactScalar[] Trim(ExactScalar[] a)
    {
        if (a.Length == 0) return [ExactScalar.Zero];
        var n = a.Length - 1;
        while (n > 0 && a[n].IsZeroWithFallback()) n--;
        return a[..(n + 1)];
    }

    public static bool IsZero(ExactScalar[] a) => a.Length == 1 && a[0].IsZeroWithFallback();

    public static ExactScalar[] FromPolyExpr(PolyExpr p, int varId)
    {
        // 直接按单项式分组，避开 FromSortedTerms 的逐个 IsCoeffZero（大系数上很贵）。
        var map = new Dictionary<int, ExactScalar>();
        foreach (var (m, c) in p.RawTerms)
        {
            var rest = m.Without(varId);
            if (!rest.IsConstant)
                throw new NotSupportedException("RationalRange v1 只支持一元多项式。");

            var exp = m.ExpOf(varId);
            map[exp] = map.TryGetValue(exp, out var previous) ? previous + c : c;
        }

        if (map.Count == 0) return [ExactScalar.Zero];

        var degree = 0;
        foreach (var exp in map.Keys)
            if (exp > degree) degree = exp;

        var a = new ExactScalar[degree + 1];
        Array.Fill(a, ExactScalar.Zero);
        foreach (var (exp, coefficient) in map)
            a[exp] = coefficient;
        return Trim(a);
    }

    public static ExactScalar Evaluate(ExactScalar[] a, ExactScalar x)
    {
        var sum = ExactScalar.Zero;
        for (var i = a.Length - 1; i >= 0; i--) sum = sum * x + a[i];
        return sum;
    }

    public static ExactScalar[] Derivative(ExactScalar[] a)
    {
        if (a.Length <= 1) return [ExactScalar.Zero];
        var d = new ExactScalar[a.Length - 1];
        for (var i = 1; i < a.Length; i++) d[i - 1] = a[i] * ExactScalar.FromInt(i);
        return Trim(d);
    }

    public static ExactScalar[] Add(ExactScalar[] a, ExactScalar[] b)
    {
        var len = Math.Max(a.Length, b.Length);
        var r = new ExactScalar[len];
        for (var i = 0; i < len; i++)
            r[i] = (i < a.Length ? a[i] : ExactScalar.Zero) + (i < b.Length ? b[i] : ExactScalar.Zero);
        return Trim(r);
    }

    public static ExactScalar[] Sub(ExactScalar[] a, ExactScalar[] b)
    {
        var len = Math.Max(a.Length, b.Length);
        var r = new ExactScalar[len];
        for (var i = 0; i < len; i++)
            r[i] = (i < a.Length ? a[i] : ExactScalar.Zero) - (i < b.Length ? b[i] : ExactScalar.Zero);
        return Trim(r);
    }

    public static ExactScalar[] Mul(ExactScalar[] a, ExactScalar[] b)
    {
        var r = new ExactScalar[a.Length + b.Length - 1];
        Array.Fill(r, ExactScalar.Zero);
        for (var i = 0; i < a.Length; i++)
        for (var j = 0; j < b.Length; j++)
            r[i + j] = r[i + j] + a[i] * b[j];
        return Trim(r);
    }

    public static ExactScalar[] Scale(ExactScalar[] a, ExactScalar k)
    {
        var r = new ExactScalar[a.Length];
        for (var i = 0; i < a.Length; i++) r[i] = a[i] * k;
        return Trim(r);
    }

    /// <summary>多项式带余除法（要求除式首项非零）。</summary>
    public static ExactScalar[] DivRem(ExactScalar[] a, ExactScalar[] b, out ExactScalar[] rem)
    {
        a = Trim(a);
        b = Trim(b);
        if (IsZero(b)) throw new DivideByZeroException("多项式除零。");
        if (a.Length < b.Length)
        {
            rem = a;
            return [ExactScalar.Zero];
        }

        var r = (ExactScalar[])a.Clone();
        var q = new ExactScalar[a.Length - b.Length + 1];
        Array.Fill(q, ExactScalar.Zero);
        var invLead = ExactScalar.One / b[^1];
        for (var k = a.Length - b.Length; k >= 0; k--)
        {
            var lead = r[k + b.Length - 1];
            if (lead.IsZeroWithFallback()) continue;
            var factor = lead * invLead;
            q[k] = factor;
            for (var j = 0; j < b.Length; j++)
                r[k + j] = r[k + j] - factor * b[j];
        }
        rem = Trim(b.Length == 1 ? [] : r[..(b.Length - 1)]);
        return Trim(q);
    }

    public static ExactScalar[] DivideExact(ExactScalar[] a, ExactScalar[] b)
    {
        var q = DivRem(a, b, out var rem);
        if (!IsZero(rem)) throw new InvalidOperationException("多项式不能整除。");
        return q;
    }

    public static ExactScalar[] Gcd(ExactScalar[] a, ExactScalar[] b)
    {
        a = Trim(a);
        b = Trim(b);
        while (!IsZero(b))
        {
            DivRem(a, b, out var r);
            a = b;
            b = Trim(r);
        }
        if (IsZero(a)) return [ExactScalar.One];
        return Scale(a, ExactScalar.One / a[^1]);
    }

    /// <summary>偶多项式 a(u) = b(u²)，返回 b。</summary>
    public static ExactScalar[] ToEvenVariable(ExactScalar[] a)
    {
        var b = new ExactScalar[(a.Length + 1) / 2];
        for (var i = 0; i < a.Length; i += 2) b[i / 2] = a[i];
        return Trim(b);
    }
}

public readonly record struct RangeBound(ExactScalar Value, bool Closed, string Source);

public sealed record RangeResult(
    RangeBound Lower,
    RangeBound Upper,
    IReadOnlyList<RangeBound> Candidates,
    int ReducedDegreeN,
    int ReducedDegreeD)
{
    public string Format() =>
        $"{(Lower.Closed ? "[" : "(")}{Lower.Value.ToSimplifiedString()}, " +
        $"{Upper.Value.ToSimplifiedString()}{(Upper.Closed ? "]" : ")")}";
}

/// <summary>
///     单变量有理函数的值域（v1：偶函数、n/d 约分后次数 ≤ 2/2、D 在 s&gt;0 上无零点）。
///     做法：约分 → 代 s = u² → 求导找临界点 → 连同 s→0、s→∞ 的极限一起取 min/max；
///     临界点可取则闭，只在极限/排除点出现则开。
/// </summary>
public readonly record struct BoundCertificate(bool Valid, ExactScalar Bound, bool IsLower, string Detail);

public sealed record RangeCertificate(RangeResult Range, BoundCertificate Lower, BoundCertificate Upper);

public static class RationalRange
{
    public static RangeResult OfPositive(PolyExpr numerator, PolyExpr denominator, int varId, params ExactScalar[] excluded)
    {
        var (ns, ds) = PrepareEven(numerator, denominator, varId);
        return RangeFromPrepared(ns, ds, excluded);
    }

    private static RangeResult RangeFromPrepared(ExactScalar[] ns, ExactScalar[] ds, ExactScalar[] excluded)
    {
        var degN = ns.Length - 1;
        var degD = ds.Length - 1;

        var deriv = UniPoly.Sub(
            UniPoly.Mul(UniPoly.Derivative(ns), ds),
            UniPoly.Mul(ns, UniPoly.Derivative(ds)));

        if (UniPoly.IsZero(deriv))
        {
            var v = Eval(ns, ds, ExactScalar.One);
            return new RangeResult(
                new RangeBound(v, true, "常数"),
                new RangeBound(v, true, "常数"),
                [],
                degN, degD);
        }

        var candidates = new List<RangeBound>();
        foreach (var root in RealRoots(deriv))
        {
            if (root.Approx <= 1e-12) continue;
            var value = Eval(ns, ds, root);
            var isExcluded = excluded.Any(e => Math.Abs(e.Approx - root.Approx) < 1e-9);
            candidates.Add(new RangeBound(value, !isExcluded,
                isExcluded ? $"s≈{root.Approx:G6}（排除点）" : $"s≈{root.Approx:G6}（临界点）"));
        }

        candidates.Add(new RangeBound(ns[0] / ds[0], false, "s→0+"));
        if (degN < degD) candidates.Add(new RangeBound(ExactScalar.Zero, false, "s→∞"));
        else if (degN == degD) candidates.Add(new RangeBound(ns[^1] / ds[^1], false, "s→∞"));
        else throw new NotSupportedException("f 在 s→∞ 发散，v1 不支持。");

        foreach (var e in excluded)
            if (e.Approx > 1e-12)
                candidates.Add(new RangeBound(Eval(ns, ds, e), false, $"s≈{e.Approx:G6}（排除点）"));

        var lower = candidates.MinBy(c => c.Value.Approx)!;
        var upper = candidates.MaxBy(c => c.Value.Approx)!;
        var lowerClosed = candidates.Any(c => c.Closed && Math.Abs(c.Value.Approx - lower.Value.Approx) < 1e-9);
        var upperClosed = candidates.Any(c => c.Closed && Math.Abs(c.Value.Approx - upper.Value.Approx) < 1e-9);

        return new RangeResult(
            new RangeBound(lower.Value, lowerClosed, lower.Source),
            new RangeBound(upper.Value, upperClosed, upper.Source),
            candidates,
            degN, degD);
    }

    public static RangeCertificate Certify(PolyExpr numerator, PolyExpr denominator, int varId, params ExactScalar[] excluded)
    {
        var (ns, ds) = PrepareEven(numerator, denominator, varId);
        var range = RangeFromPrepared(ns, ds, excluded);

        var lowerPoly = UniPoly.Sub(ns, UniPoly.Scale(ds, range.Lower.Value));
        var upperPoly = UniPoly.Sub(UniPoly.Scale(ds, range.Upper.Value), ns);

        var lower = CheckNonnegative(lowerPoly);
        var upper = CheckNonnegative(upperPoly);
        return new RangeCertificate(
            range,
            new BoundCertificate(lower.Valid, range.Lower.Value, true, lower.Detail),
            new BoundCertificate(upper.Valid, range.Upper.Value, false, upper.Detail));
    }

    private static bool IsEvenFast(ExactScalar[] a)
    {
        for (var i = 1; i < a.Length; i += 2)
            if (!ExactScalar.IsZeroFast(a[i]) && Math.Abs(a[i].Approx) > 1e-12)
                return false;
        return true;
    }

    private static (ExactScalar[] N, ExactScalar[] D) PrepareEven(
        PolyExpr numerator, PolyExpr denominator, int varId)
    {
        var n = UniPoly.FromPolyExpr(numerator, varId);
        var d = UniPoly.FromPolyExpr(denominator, varId);
        var g = UniPoly.Gcd(n, d);
        if (g.Length > 1)
        {
            n = UniPoly.DivideExact(n, g);
            d = UniPoly.DivideExact(d, g);
        }

        // 约分后只按“数值零/结构零”判奇偶，避免在大系数上逐个 VerifyStrictZero。
        if (!IsEvenFast(n) || !IsEvenFast(d))
            throw new NotSupportedException("RationalRange v1 只支持偶函数（关于 u 的偶多项式）。");

        var ns = UniPoly.ToEvenVariable(n);
        var ds = UniPoly.ToEvenVariable(d);
        if (ds[0].Approx < 0)
        {
            ns = UniPoly.Scale(ns, -ExactScalar.One);
            ds = UniPoly.Scale(ds, -ExactScalar.One);
        }
        if (ds[0].Approx <= 0 || ExactScalar.IsZeroFast(ds[0]))
            throw new NotSupportedException(
                $"要求 D(s) 在 s>0 上为正。deg n={n.Length - 1}, deg d={d.Length - 1}, " +
                $"ds=[{string.Join(", ", ds.Select(c => c.Approx.ToString("G6")))}]");

        // 所有系数同为正 ⇒ s>0 上恒正，不需要开方求根（P2 走这条快路）。
        if (!ds.All(c => c.Approx > 0))
        {
            foreach (var root in RealRoots(ds))
                if (root.Approx > 1e-12)
                    throw new NotSupportedException("D(s) 在 s>0 上有零点（极点），v1 不支持。");
        }

        return (ns, ds);
    }

    /// <summary>对 P(s) ≥ 0（s&gt;0）做符号证书（v1 覆盖常数/一次/二次）。</summary>
    private static (bool Valid, string Detail) CheckNonnegative(ExactScalar[] p)
    {
        p = UniPoly.Trim(p);
        if (UniPoly.IsZero(p)) return (true, "P ≡ 0");

        var deg = p.Length - 1;
        if (deg == 0) return (p[0].Approx >= 0, $"常数 {p[0].Approx:G6}");
        if (deg == 1)
        {
            var a = p[1];
            var b = p[0];
            var valid = a.Approx > 0 && b.Approx >= -1e-9;
            return (valid, $"一次式 a={a.Approx:G6}, b={b.Approx:G6}");
        }

        if (deg == 2)
        {
            var a = p[2];
            var b = p[1];
            var c = p[0];

            // 可能是未化简的零或数值 0；先降为一次式，绝不除以零。
            if (a.Approx == 0 || !double.IsFinite(a.Approx))
                return CheckNonnegative(p[..2]);
            if (a.Approx < 0) return (false, "二次项系数为负");

            var fourAC = ExactScalar.FromInt(4) * a * c;
            var disc = b * b - fourAC;
            var scale = Math.Max(1.0, Math.Max(Math.Abs(b.Approx * b.Approx), Math.Abs(fourAC.Approx)));

            // 整数系数的判别式用 BigInteger 精确判零；大系数/非整数时退回相对判零，
            // 避免 AngouriMath 在巨表达式上做昂贵化简。
            if (TryIntegerDiscriminant(p, out var exactDisc))
            {
                if (exactDisc.IsZero)
                    return (true, "二次式首项>0，判别式精确为 0（整数算术）");
                if (exactDisc.Sign < 0)
                    return (true, $"二次式首项>0，判别式 {exactDisc} < 0");
            }

            if (disc.Approx < 0)
                return (true, $"二次式首项>0，判别式 {disc.Approx:G6} < 0");
            if (Math.Abs(disc.Approx) <= 1e-9 * scale)
                return (true, $"二次式首项>0，判别式≈0（相对 {scale:G6}）");

            var sqrt = ExactScalar.Sqrt(disc);
            var twoA = ExactScalar.FromInt(2) * a;
            var r1 = (-b - sqrt) / twoA;
            var r2 = (-b + sqrt) / twoA;
            if (r1.Approx > r2.Approx) (r1, r2) = (r2, r1);

            var valid = r2.Approx <= 1e-12;
            return (valid, $"二次式根 [{r1.Approx:G6}, {r2.Approx:G6}]，需全 ≤ 0");
        }

        return (false, $"次数 {deg} > 2，v1 证书未覆盖");
    }

    private static bool TryIntegerDiscriminant(ExactScalar[] p, out BigInteger discriminant)
    {
        discriminant = BigInteger.Zero;
        if (p.Length != 3) return false;
        if (!TryInteger(p[2], out var a) || !TryInteger(p[1], out var b) || !TryInteger(p[0], out var c))
            return false;

        discriminant = b * b - 4 * a * c;
        return true;
    }

    private static bool TryInteger(ExactScalar value, out BigInteger integer)
    {
        integer = BigInteger.Zero;
        if (value.Exact is not null && BigInteger.TryParse(value.Exact.ToString(), out integer))
            return true;

        // 系数可能只是未化简的整数表达式；先化简再试一次。
        var simplified = value.Simplified();
        return simplified.Exact is not null
               && BigInteger.TryParse(simplified.Exact.ToString(), out integer);
    }

    private static ExactScalar Eval(ExactScalar[] n, ExactScalar[] d, ExactScalar s) =>
        UniPoly.Evaluate(n, s) / UniPoly.Evaluate(d, s);

    private static List<ExactScalar> RealRoots(ExactScalar[] p)
    {
        p = UniPoly.Trim(p);
        var deg = p.Length - 1;
        if (deg <= 0) return [];
        if (deg == 1) return [-p[0] / p[1]];
        if (deg == 2)
        {
            var disc = p[1] * p[1] - ExactScalar.FromInt(4) * p[2] * p[0];
            if (disc.Approx < 0 && !disc.IsNumericallyZero(1e-9)) return [];
            if (disc.IsNumericallyZero(1e-9)) return [-p[1] / (ExactScalar.FromInt(2) * p[2])];

            var sqrt = ExactScalar.Sqrt(disc);
            var twoA = ExactScalar.FromInt(2) * p[2];
            return [(-p[1] + sqrt) / twoA, (-p[1] - sqrt) / twoA];
        }
        throw new NotSupportedException($"RationalRange v1 只支持次数 ≤ 2 的导数/分母，当前 {deg}。");
    }
}

/// <summary>Sturm 序列与实根隔离（v2 的基础设施，数值隔离 + 精确符号）。</summary>
internal static class SturmTools
{
    public static ExactScalar[] SquareFree(ExactScalar[] p)
    {
        var dp = UniPoly.Derivative(p);
        if (UniPoly.IsZero(dp)) return p;
        var g = UniPoly.Gcd(p, dp);
        return g.Length > 1 ? UniPoly.DivideExact(p, g) : p;
    }

    public static List<ExactScalar[]> Sequence(ExactScalar[] p)
    {
        p = SquareFree(p);
        var seq = new List<ExactScalar[]> { p };
        var dp = UniPoly.Derivative(p);
        if (UniPoly.IsZero(dp)) return seq;
        seq.Add(dp);
        while (true)
        {
            UniPoly.DivRem(seq[^2], seq[^1], out var rem);
            if (UniPoly.IsZero(rem)) break;
            seq.Add(UniPoly.Scale(rem, -ExactScalar.One));
        }
        return seq;
    }

    public static int Variations(IReadOnlyList<ExactScalar[]> seq, ExactScalar x)
    {
        var signs = new List<int>();
        foreach (var p in seq)
        {
            var v = UniPoly.Evaluate(p, x);
            if (v.IsZeroWithFallback()) continue;
            signs.Add(v.Approx > 0 ? 1 : -1);
        }
        var changes = 0;
        for (var i = 1; i < signs.Count; i++)
            if (signs[i] != signs[i - 1]) changes++;
        return changes;
    }

    public static int Count(IReadOnlyList<ExactScalar[]> seq, ExactScalar a, ExactScalar b) =>
        Variations(seq, a) - Variations(seq, b);

    public static List<(ExactScalar Lo, ExactScalar Hi)> Isolate(
        ExactScalar[] p, ExactScalar a, ExactScalar b, double tol = 1e-7)
    {
        var seq = Sequence(p);
        var result = new List<(ExactScalar, ExactScalar)>();
        var tiny = ExactScalar.FromApprox(1e-9);
        var pa = UniPoly.Evaluate(p, a);
        var pb = UniPoly.Evaluate(p, b);
        var lo = pa.IsZeroWithFallback() ? a + tiny : a;
        var hi = pb.IsZeroWithFallback() ? b - tiny : b;

        void Rec(ExactScalar l, ExactScalar h, int depth)
        {
            var count = Count(seq, l, h);
            if (count <= 0) return;
            if (count == 1 && (h - l).Approx < tol) { result.Add((l, h)); return; }
            if (depth > 80) { result.Add((l, h)); return; }
            var mid = (l + h) / ExactScalar.FromInt(2);
            Rec(l, mid, depth + 1);
            Rec(mid, h, depth + 1);
        }

        Rec(lo, hi, 0);
        return result;
    }
}

public readonly record struct RangeInterval(double Lo, double Hi, bool LoClosed, bool HiClosed);

public sealed record ApproxRangeSet(IReadOnlyList<RangeInterval> Intervals)
{
    public string Format()
    {
        static string F(double x) => double.IsNegativeInfinity(x) ? "-∞"
            : double.IsPositiveInfinity(x) ? "+∞"
            : x.ToString("G8");

        return Intervals.Count == 0
            ? "∅"
            : string.Join(" ∪ ", Intervals.Select(i =>
                $"{(i.LoClosed ? "[" : "(")}{F(i.Lo)}, {F(i.Hi)}{(i.HiClosed ? "]" : ")")}"));
    }
}

/// <summary>
///     v2：任意次有理函数在 R 上的值域（数值隔离；支持极点与多区间）。
///     每个极点把 R 切开，逐段求临界值 + 两端极限，再合并区间。
/// </summary>
/// <summary>
///     区间上的多项式值域（v1）：端点 + 导数临界点；不处理有理函数极点。
///     只用于“参数范围/最值”型题目的最小层。
/// </summary>
public static class IntervalRange
{
    public static RangeResult OfPolynomial(
        PolyExpr polynomial,
        int varId,
        ExactScalar left,
        bool leftClosed,
        ExactScalar right,
        bool rightClosed,
        params ExactScalar[] excluded)
    {
        if (left.Approx > right.Approx)
            throw new ArgumentException("区间左端点大于右端点。", nameof(left));

        var coeffs = UniPoly.FromPolyExpr(polynomial, varId);
        var candidates = new List<RangeBound>
        {
            new(UniPoly.Evaluate(coeffs, left), leftClosed && !IsExcluded(left, excluded), "左端点"),
            new(UniPoly.Evaluate(coeffs, right), rightClosed && !IsExcluded(right, excluded), "右端点")
        };

        foreach (var root in RootsUpToQuadratic(UniPoly.Derivative(coeffs)))
            if (root.Approx > left.Approx && root.Approx < right.Approx)
                candidates.Add(new RangeBound(
                    UniPoly.Evaluate(coeffs, root),
                    !IsExcluded(root, excluded),
                    $"临界点 s≈{root.Approx:G6}"));

        var lower = candidates.MinBy(c => c.Value.Approx)!;
        var upper = candidates.MaxBy(c => c.Value.Approx)!;
        var lowerClosed = candidates.Any(c => c.Closed && Near(c.Value, lower.Value));
        var upperClosed = candidates.Any(c => c.Closed && Near(c.Value, upper.Value));

        return new RangeResult(
            new RangeBound(lower.Value, lowerClosed, lower.Source),
            new RangeBound(upper.Value, upperClosed, upper.Source),
            candidates,
            coeffs.Length - 1,
            0);
    }

    private static List<ExactScalar> RootsUpToQuadratic(ExactScalar[] p)
    {
        p = UniPoly.Trim(p);
        var deg = p.Length - 1;
        if (deg <= 0) return [];
        if (deg == 1) return [-p[0] / p[1]];
        if (deg > 2) throw new NotSupportedException($"IntervalRange v1 只支持二次导数，当前 {deg}。");

        var disc = p[1] * p[1] - ExactScalar.FromInt(4) * p[2] * p[0];
        if (disc.Approx < 0 && !disc.IsNumericallyZero(1e-9)) return [];
        if (disc.IsNumericallyZero(1e-9)) return [-p[1] / (ExactScalar.FromInt(2) * p[2])];

        var sqrt = ExactScalar.Sqrt(disc);
        var twoA = ExactScalar.FromInt(2) * p[2];
        return [(-p[1] + sqrt) / twoA, (-p[1] - sqrt) / twoA];
    }

    private static bool IsExcluded(ExactScalar value, ExactScalar[] excluded) =>
        excluded.Any(e => Near(e, value));

    private static bool Near(ExactScalar a, ExactScalar b) =>
        Math.Abs(a.Approx - b.Approx) < 1e-9;
}

public static class RationalRangeApprox
{
    public static ApproxRangeSet OfReal(PolyExpr numerator, PolyExpr denominator, int varId)
    {
        var n = UniPoly.FromPolyExpr(numerator, varId);
        var d = UniPoly.FromPolyExpr(denominator, varId);
        var g = UniPoly.Gcd(n, d);
        if (g.Length > 1)
        {
            n = UniPoly.DivideExact(n, g);
            d = UniPoly.DivideExact(d, g);
        }

        var critical = UniPoly.Sub(
            UniPoly.Mul(UniPoly.Derivative(n), d),
            UniPoly.Mul(n, UniPoly.Derivative(d)));

        if (UniPoly.IsZero(critical))        {
            var c = EvalDouble(n, 1) / EvalDouble(d, 1);
            return new ApproxRangeSet([new RangeInterval(c, c, true, true)]);
        }

        var bound = Math.Max(CauchyBound(d).Approx, CauchyBound(critical).Approx) + 1;
        var boundCeiling = Math.Ceiling(bound);
        if (!double.IsFinite(boundCeiling) || boundCeiling > long.MaxValue)
            throw new NotSupportedException("有理函数值域：Cauchy 界溢出 long。");

        // 数值求根前先去掉重根，保证 Aberth-Ehrlich 在重根处仍是二次收敛；
        // Sturm 计数本来就统计互异根，两边口径一致。
        var dSquareFree = SturmTools.SquareFree(d);
        var criticalSquareFree = SturmTools.SquareFree(critical);

        var poles = RealRootsNumeric(dSquareFree)
            .Where(p => p > -bound && p < bound)
            .OrderBy(p => p)
            .ToList();
        poles = Dedupe(poles);

        var criticalPoints = Dedupe(RealRootsNumeric(criticalSquareFree));

        // Sturm 计数做一致性校验：数值根个数必须与 Sturm 变号数一致
        var boundExact = ExactScalar.FromInt((long)boundCeiling);
        var expectedPoles = SturmTools.Count(SturmTools.Sequence(dSquareFree), -boundExact, boundExact);
        if (expectedPoles != poles.Count)
            throw new InvalidOperationException($"Sturm 极点计数 {expectedPoles} 与数值根 {poles.Count} 不一致。");
        var expectedCritical = SturmTools.Count(SturmTools.Sequence(criticalSquareFree), -boundExact, boundExact);
        if (expectedCritical != criticalPoints.Count)
            throw new InvalidOperationException($"Sturm 临界点计数 {expectedCritical} 与数值根 {criticalPoints.Count} 不一致。");

        var breaks = new List<double> { double.NegativeInfinity };
        breaks.AddRange(poles);
        breaks.Add(double.PositiveInfinity);

        var pieces = new List<RangeInterval>();
        for (var i = 0; i < breaks.Count - 1; i++)
        {
            var left = breaks[i];
            var right = breaks[i + 1];
            var sample = SamplePoint(left, right);
            var dSample = EvalDouble(d, sample);
            if (Math.Abs(dSample) < 1e-9) continue;

            var loLimit = double.IsNegativeInfinity(left)
                ? InfinityLimit(n, d, positive: false)
                : PoleLimit(n, d, left, dSample);
            var hiLimit = double.IsPositiveInfinity(right)
                ? InfinityLimit(n, d, positive: true)
                : PoleLimit(n, d, right, dSample);

            var values = new List<(double V, bool Closed)>();
            AddLimit(values, loLimit);
            AddLimit(values, hiLimit);
            foreach (var c in criticalPoints)
            {
                if (c <= left || c >= right) continue;
                var dc = EvalDouble(d, c);
                if (Math.Abs(dc) < 1e-12) continue;
                values.Add((EvalDouble(n, c) / dc, true));
            }

            if (values.Count == 0) continue;
            var lo = values.MinBy(v => v.V)!;
            var hi = values.MaxBy(v => v.V)!;
            var loClosed = values.Any(v => v.Closed && Math.Abs(v.V - lo.V) < 1e-9);
            var hiClosed = values.Any(v => v.Closed && Math.Abs(v.V - hi.V) < 1e-9);
            pieces.Add(new RangeInterval(lo.V, hi.V, loClosed, hiClosed));
        }

        return new ApproxRangeSet(Merge(pieces));
    }

    private static void AddLimit(List<(double V, bool Closed)> values, double limit)
    {
        if (!double.IsNaN(limit)) values.Add((limit, false));
    }

    private static double SamplePoint(double left, double right)
    {
        if (double.IsNegativeInfinity(left)) return right - 1;
        if (double.IsPositiveInfinity(right)) return left + 1;
        return (left + right) / 2;
    }

    private static double EvalDouble(ExactScalar[] p, double x) =>
        UniPoly.Evaluate(p, ExactScalar.FromApprox(x)).Approx;

    private static double PoleLimit(ExactScalar[] n, ExactScalar[] d, double pole, double dSample)
    {
        var nAtPole = EvalDouble(n, pole);
        if (Math.Abs(nAtPole) < 1e-12) return double.NaN;
        var sign = Math.Sign(nAtPole) * Math.Sign(dSample);
        return sign > 0 ? double.PositiveInfinity : double.NegativeInfinity;
    }

    private static double InfinityLimit(ExactScalar[] n, ExactScalar[] d, bool positive)
    {
        var degN = n.Length - 1;
        var degD = d.Length - 1;
        if (degN < degD) return 0;
        var ratio = n[^1].Approx / d[^1].Approx;
        if (degN == degD) return ratio;
        var sign = ratio > 0 ? 1 : -1;
        if (!positive && (degN - degD) % 2 == 1) sign = -sign;
        return sign > 0 ? double.PositiveInfinity : double.NegativeInfinity;
    }

    private static List<RangeInterval> Merge(List<RangeInterval> input)
    {
        var result = new List<RangeInterval>();
        foreach (var iv in input.OrderBy(i => i.Lo).ThenByDescending(i => i.LoClosed))
        {
            if (result.Count == 0)
            {
                result.Add(iv);
                continue;
            }

            var last = result[^1];

            // 只有真正相交，或恰好共享一个“闭”端点时才合并；
            // (0,1) ∪ (1,2) 的 1 不在值域里，不能吞掉。
            var overlaps = iv.Lo < last.Hi
                           || iv.Lo == last.Hi && (last.HiClosed || iv.LoClosed);
            if (!overlaps)
            {
                result.Add(iv);
                continue;
            }

            var hi = iv.Hi > last.Hi ? iv.Hi : last.Hi;
            var hiClosed = iv.Hi > last.Hi
                ? iv.HiClosed
                : iv.Hi < last.Hi
                    ? last.HiClosed
                    : iv.HiClosed || last.HiClosed;
            var loClosed = last.LoClosed || iv.Lo == last.Lo && iv.LoClosed;
            result[^1] = last with { LoClosed = loClosed, Hi = hi, HiClosed = hiClosed };
        }
        return result;
    }

    private static List<double> RealRootsNumeric(ExactScalar[] p)
    {
        p = UniPoly.Trim(p);
        var deg = p.Length - 1;
        if (deg <= 0) return [];
        if (deg == 1) return [-p[0].Approx / p[1].Approx];

        var coeff = new double[deg + 1];
        for (var i = 0; i <= deg; i++) coeff[i] = p[i].Approx;
        var lead = coeff[deg];
        for (var i = 0; i <= deg; i++) coeff[i] /= lead;

        var radius = 1.0;
        for (var i = 0; i < deg; i++) radius = Math.Max(radius, Math.Abs(coeff[i]) + 1);

        var roots = new Complex[deg];
        for (var i = 0; i < deg; i++)
            roots[i] = Complex.FromPolarCoordinates(radius, 2 * Math.PI * i / deg + 0.4);

        for (var iter = 0; iter < 500; iter++)
        {
            var maxDelta = 0.0;
            for (var i = 0; i < deg; i++)
            {
                var num = EvalComplex(coeff, deg, roots[i]);
                var den = Complex.One;
                for (var j = 0; j < deg; j++)
                    if (j != i) den *= roots[i] - roots[j];
                if (den == Complex.Zero) continue;
                var delta = num / den;
                roots[i] -= delta;
                maxDelta = Math.Max(maxDelta, delta.Magnitude);
            }
            if (maxDelta < 1e-14) break;
        }

        var real = new List<double>();
        foreach (var z in roots)
            if (Math.Abs(z.Imaginary) < 1e-6 * Math.Max(1, Math.Abs(z.Real)))
                real.Add(z.Real);
        real.Sort();
        return real;
    }

    private static List<double> Dedupe(List<double> xs)
    {
        var result = new List<double>();
        foreach (var x in xs.OrderBy(v => v))
            if (result.Count == 0 || Math.Abs(x - result[^1]) > 1e-6)
                result.Add(x);
        return result;
    }

    private static Complex EvalComplex(double[] coeff, int deg, Complex x)
    {
        var sum = Complex.Zero;
        for (var i = deg; i >= 0; i--) sum = sum * x + coeff[i];
        return sum;
    }

    private static ExactScalar CauchyBound(ExactScalar[] d)
    {
        var lead = d[^1].Approx;
        if (!double.IsFinite(lead) || lead == 0)
            throw new NotSupportedException("Cauchy 界：首项系数非有限或为零。");

        var max = 0.0;
        for (var i = 0; i < d.Length - 1; i++)
        {
            var ratio = d[i].Approx / lead;
            if (!double.IsFinite(ratio))
                throw new NotSupportedException("Cauchy 界：系数比值非有限。");
            max = Math.Max(max, Math.Abs(ratio));
        }

        var ceiling = Math.Ceiling(max + 1);
        if (!double.IsFinite(ceiling) || ceiling > long.MaxValue)
            throw new NotSupportedException("Cauchy 界溢出 long。");
        return ExactScalar.FromInt((long)ceiling);
    }
}
