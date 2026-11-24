using System.Numerics;
using System.Text;
using Perspective.Core.Math;

namespace Perspective.Constraint;

public sealed class PolyExpr : IExactArithmetic<PolyExpr>, IEquatable<PolyExpr>
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

    public static PolyExpr FromInt(long v) =>
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

    public static StrictResult DecideZero(PolyExpr a) =>
        a.SimplifyCoeffs().IsZero ? StrictResult.StrictlyTrue : StrictResult.StrictlyFalse;

    public static PolyExpr FromConst(ExactScalar c) =>
        IsCoeffZero(c) ? Zero : new PolyExpr([(Monomial.One, c)]);

    public static PolyExpr Var(int varId) => new([(Monomial.Var(varId), ExactScalar.One)]);
    public static PolyExpr PowVar(int varId, int exp) => new([(Monomial.Var(varId, exp), ExactScalar.One)]);

    public static implicit operator PolyExpr(long v) => FromInt(v);
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


