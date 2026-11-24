using System;

namespace Perspective.Constraint;

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

