using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

/// <summary>外挂结构层：往纯射影骨架上叠放无穷远线 l∞ 与绝对二次型 Ω*。</summary>
public readonly record struct GeometryLens(Dual3 InfinityLine, DualBilinearForm3 OmegaStar)
{
    public static GeometryLens EuclideanXoy { get; } = new(
        new Dual3(0, 0, 1),
        new DualBilinearForm3(1, 0, 0, 1, 0, 0));

    /// <summary>双曲几何视角（Klein 模型，Minkowski 符号差 (+, +, -) 约定）。</summary>
    public static GeometryLens Hyperbolic { get; } = new(
        new Dual3(0, 0, 1),
        new DualBilinearForm3(1, 0, 0, 1, 0, -1));

    // --- 仿射结构 ---

    public StrictResult IsAtInfinity(Primal3 p) =>
        SubspaceOps.IsIncident(InfinityLine, p);

    public StrictResult AreParallel(Dual3 l1, Dual3 l2) =>
        IsAtInfinity(SubspaceOps.Meet(l1, l2));

    public Primal3 Midpoint(Primal3 a, Primal3 b)
    {
        EnsureFinitePoint(a, nameof(a));
        EnsureFinitePoint(b, nameof(b));

        var wa = SubspaceOps.Pair(InfinityLine, a);
        var wb = SubspaceOps.Pair(InfinityLine, b);
        return wb * a + wa * b;
    }

    /// <summary>【零根号弦中点】只用直线、二次曲线和本镜头的 l∞ 算出弦中点。</summary>
    public Primal3 ChordMidpoint(Dual3 line, BilinearForm3 conic)
    {
        if (SubspaceOps.SameSubspace(line, InfinityLine) == StrictResult.StrictlyTrue)
            throw new InvalidOperationException("无穷远线本身不存在仿射弦中点。");

        var uInf = SubspaceOps.Meet(line, InfinityLine);
        if (SubspaceOps.IsZeroVector(uInf))
            throw new InvalidOperationException("无法确定直线与无穷远线的交点。");

        var v = SubspaceOps.PickPointOnLineDistinctFrom(line, uInf, InfinityLine);
        var a = conic.Eval(uInf);
        if (a.IsZeroWithFallback())
            throw new InvalidOperationException("直线交曲线于无穷远点，不存在有限弦中点。");

        var b = SubspaceOps.Pair(conic.Polar(uInf), v);
        return a * v - b * uInf;
    }

    // --- 有序仿射结构 ---

    public ExactScalar OrientSigned(Primal3 a, Primal3 b, Primal3 p)
    {
        var wa = SubspaceOps.Pair(InfinityLine, a);
        var wb = SubspaceOps.Pair(InfinityLine, b);
        var wp = SubspaceOps.Pair(InfinityLine, p);
        return SubspaceOps.Det(a, b, p) * wa * wb * wp;
    }

    // --- 度量结构 ---

    public StrictResult ArePerpendicular(Dual3 l1, Dual3 l2) =>
        SubspaceOps.Pair(l1, OmegaStar.Pole(l2)).VerifyStrictZero();

    public Dual3 PerpendicularThrough(Primal3 p, Dual3 l) =>
        SubspaceOps.Join(p, OmegaStar.Pole(l));

    public ExactScalar EuclideanDistanceSquared(Primal3 p1, Primal3 p2)
    {
        EnsureFinitePoint(p1, nameof(p1));
        EnsureFinitePoint(p2, nameof(p2));

        var line = SubspaceOps.Join(p1, p2);
        var w1 = SubspaceOps.Pair(InfinityLine, p1);
        var w2 = SubspaceOps.Pair(InfinityLine, p2);
        return OmegaStar.Eval(line) / (w1 * w1 * w2 * w2);
    }

    public bool TryToCartesian(Primal3 p, out ExactScalar x, out ExactScalar y)
    {
        x = y = ExactScalar.FromInt(0);
        if (p.VerifyZero() == StrictResult.StrictlyTrue)
            return false;

        var w = SubspaceOps.Pair(InfinityLine, p);
        var wZero = w.VerifyStrictZero();
        var canDivide = wZero == StrictResult.StrictlyFalse ||
                        wZero == StrictResult.NoSymbolicData && !w.IsNumericallyZero();
        if (!canDivide) return false;

        x = p.X / w;
        y = p.Y / w;
        return true;
    }

    public string Format(Primal3 p) =>
        TryToCartesian(p, out var x, out var y)
            ? $"({x.ToSimplifiedString()}, {y.ToSimplifiedString()})"
            : $"无穷远方向 {p}";

    private void EnsureFinitePoint(Primal3 p, string paramName)
    {
        if (SubspaceOps.IsZeroVector(p))
            throw new ArgumentException("射影点不能为零向量 [0, 0, 0]。", paramName);

        var w = SubspaceOps.Pair(InfinityLine, p);
        if (w.IsZeroWithFallback())
            throw new InvalidOperationException($"点 {paramName} 位于无穷远线上（w = 0），不支持有限仿射/欧氏计算。");
    }
}