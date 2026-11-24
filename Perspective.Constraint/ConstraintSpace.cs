using Perspective.Core.Math;

namespace Perspective.Constraint;

public enum ConstraintProofStatus
{
    Proved,
    NotZero,
    Inconsistent
}

public readonly record struct ConstraintProofOutcome(
    ConstraintProofStatus Status,
    PolyExpr Remainder,
    IReadOnlyList<PolyExpr> CharacteristicSet,
    IReadOnlyList<PolyExpr> NdgConditions);

public sealed class ConstraintSpace
{
    private readonly List<PolyExpr> _constraints = [];
    private readonly Dictionary<int, (PolyExpr Num, ExactScalar Den)> _linearSubs = [];
    private readonly List<string> _varNames = [];

    public string Format(PolyExpr p) => p.Format(id => _varNames[id]);

    public PolyExpr NewSymbol(string name)
    {
        var id = _varNames.Count;
        _varNames.Add(name);
        return PolyExpr.Var(id);
    }


    public void RequireZero(PolyExpr expr)
    {
        var reduced = ApplySubs(expr).SimplifyCoeffs();
        if (reduced.IsZero) return;
        _constraints.Add(reduced);
        PropagateLinearEliminations();
    }




    public ConstraintProofOutcome ProveZero(PolyExpr conclusion)
    {
        var target = ApplySubs(conclusion).SimplifyCoeffs();
        var active = _constraints.Select(c => ApplySubs(c).SimplifyCoeffs()).Where(c => !c.IsZero).ToList();

        foreach (var c in active)
            if (c.IsConstant)
                return new ConstraintProofOutcome(ConstraintProofStatus.Inconsistent, target, active, []);

        if (target.IsZero)
            return new ConstraintProofOutcome(ConstraintProofStatus.Proved, PolyExpr.Zero, active, []);

        if (active.Count == 0)
            return new ConstraintProofOutcome(ConstraintProofStatus.NotZero, target, [], []);

        var (consistent, chain) = BuildCharacteristicSet(active);
        if (!consistent)
            return new ConstraintProofOutcome(ConstraintProofStatus.Inconsistent, target, chain, []);

        var ndg = new List<PolyExpr>();
        var rem = ReduceByChain(target, chain, ndg);
        return rem.IsZero
            ? new ConstraintProofOutcome(ConstraintProofStatus.Proved, rem, chain, ndg)
            : new ConstraintProofOutcome(ConstraintProofStatus.NotZero, rem, chain, ndg);
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

