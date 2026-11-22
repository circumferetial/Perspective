using Perspective.Core.Math;
using Perspective.Core.Reasoning;

namespace Perspective.Core.Geometry;

/// <summary>
/// 分段曲线的"归属条件"：在点坐标代入后满足 relation（表达式 relation 0）。
/// 表达式使用固定符号名 <see cref="PiecewiseConic.XSymbol"/> / <see cref="PiecewiseConic.YSymbol"/>。
/// </summary>
public sealed record PieceCondition(ExactScalar Expression, RelationKind Kind)
{
    /// <summary>构造常用条件：x &gt; 常数、y ≥ 常数等（以 x,y 符号表示）。</summary>
    public static PieceCondition Compare(RelationKind kind, ExactScalar left, ExactScalar right) =>
        new(left - right, kind);
}

/// <summary>分段曲线中的一段：某条圆锥曲线 + 该段生效的坐标条件。</summary>
public sealed record ConicPiece(Conic Conic, PieceCondition Condition);

/// <summary>
/// 【分段曲线】若干 (圆锥曲线, 条件) 分段的并集（如 2020 上海 20 的 Γ：
/// Γ₁ 的 x&gt;|x_A| 部分 与 Γ₂ 的 x&gt;|x_A| 部分）。
/// <para>
/// 【能力】成员判定、与直线求交（按段过滤 + 去重）、对交点集做进一步筛选。
/// 【注意】条件里的不等式用数值判定（与严通道的三态不同），仅用于"该点属于哪一段"的筛选；
/// 最终结论仍须走严通道/神谕。
/// </para>
/// </summary>
public sealed class PiecewiseConic
{
    public const string XSymbol = "x";
    public const string YSymbol = "y";

    private readonly List<ConicPiece> _pieces = [];

    public PiecewiseConic Add(Conic conic, PieceCondition condition)
    {
        _pieces.Add(new ConicPiece(conic, condition));
        return this;
    }

    public IReadOnlyList<ConicPiece> Pieces => _pieces;

    /// <summary>点是否属于该分段曲线：某一段上"在曲线上"且"满足该段条件"。</summary>
    public bool Contains(HPoint point)
    {
        if (point.IsAtInfinity) return false;

        foreach (var piece in _pieces)
        {
            if (piece.Conic.ContainsStrictly(point) != StrictResult.StrictlyTrue) continue;
            if (Satisfies(piece.Condition, point)) return true;
        }
        return false;
    }

    /// <summary>直线与分段曲线的交点（按段求交、按条件过滤、严格去重）。</summary>
    public IReadOnlyList<HPoint> IntersectLine(HLine line)
    {
        var result = new List<HPoint>();

        foreach (var piece in _pieces)
        {
            foreach (var point in piece.Conic.Intersect(line))
            {
                if (point.IsAtInfinity) continue;
                if (!Satisfies(piece.Condition, point)) continue;
                if (result.Any(existing => PointTools.SameStrictly(existing, point) == StrictResult.StrictlyTrue))
                    continue;
                result.Add(point);
            }
        }

        return result;
    }

    /// <summary>按点坐标求条件表达式的值（x,y 用齐次坐标归一化后的仿射值）。</summary>
    private static bool Satisfies(PieceCondition condition, HPoint point)
    {
        if (condition.Expression.Exact is null) return false;

        var x = point.X / point.W;
        var y = point.Y / point.W;
        if (x.Exact is null || y.Exact is null) return false;

        var value = condition.Expression.Exact
            .Substitute(XSymbol, x.Exact)
            .Substitute(YSymbol, y.Exact)
            .Simplify();

        if (!value.EvaluableNumerical) return false;
        double numeric;
        try { numeric = (double)value.EvalNumerical(); }
        catch (Exception) { return false; }

        return condition.Kind switch
        {
            RelationKind.LessThan => numeric < -1e-9,
            RelationKind.GreaterThan => numeric > 1e-9,
            RelationKind.LessOrEqual => numeric <= 1e-9,
            RelationKind.GreaterOrEqual => numeric >= -1e-9,
            RelationKind.Equal => System.Math.Abs(numeric) <= 1e-9,
            RelationKind.NotEqual => System.Math.Abs(numeric) > 1e-9,
            _ => false
        };
    }
}
