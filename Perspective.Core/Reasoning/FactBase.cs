using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

// === 语义事实基类与具体事实类型（用 ID 绑定，天然支持 HashSet 去重） ===

public abstract record GeoFact;

public sealed record OnLineFact(string PointId, string LineId) : GeoFact;
public sealed record OnConicFact(string PointId, string ConicId) : GeoFact;
public sealed record TangentFact(string LineId, string ConicId, string TouchPointId) : GeoFact;
public sealed record PolarOfFact(string LineId, string PoleId, string ConicId) : GeoFact;
public sealed record CenterOfConicFact(string CenterId, string ConicId) : GeoFact;
public sealed record MidpointFact(string MidId, string AId, string BId) : GeoFact;
public sealed record HarmonicFact(string AId, string BId, string CId, string DId) : GeoFact;

/// <summary>
/// 三点共线（幂集事实）。三个 id **必须按序规范化**（见 <see cref="Create"/>），
/// 否则 (A,B,C) 与 (B,A,C) 会被当成两条事实。
/// </summary>
public sealed record CollinearFact(string P1Id, string P2Id, string P3Id) : GeoFact
{
    /// <summary>规范化构造：三个 id 排序后存放，保证排列去重。</summary>
    public static CollinearFact Create(string a, string b, string c)
    {
        var ids = new[] { a, b, c };
        Array.Sort(ids, StringComparer.Ordinal);
        return new CollinearFact(ids[0], ids[1], ids[2]);
    }
}

/// <summary>
/// 三线共点（幂集事实）。三个 id **必须按序规范化**（见 <see cref="Create"/>）。
/// </summary>
public sealed record ConcurrentFact(string L1Id, string L2Id, string L3Id) : GeoFact
{
    /// <summary>规范化构造：三个 id 排序后存放，保证排列去重。</summary>
    public static ConcurrentFact Create(string a, string b, string c)
    {
        var ids = new[] { a, b, c };
        Array.Sort(ids, StringComparer.Ordinal);
        return new ConcurrentFact(ids[0], ids[1], ids[2]);
    }
}

public sealed record ConjugateDirectionsFact(string Line1Id, string Line2Id, string ConicId) : GeoFact;
public sealed record ConstantSlopeProductFact(string Line1Id, string Line2Id, ExactScalar Value) : GeoFact;

/// <summary>
/// 几何场景知识库：存放几何实体、当前视角等级、已知事实及完整推演记录。
/// <para>
/// 事实的**唯一写入入口**是 <see cref="AddFact"/>：它同时维护按类型的只读视图与按 id 的索引，
/// 因此定理规则可以直接用声明式查询表达前提（"过 Q 的割线"、"在曲线上的点"），
/// 而不必每次对全表做 OfType/Where 扫描。事实库是只增不改的（append-only），
/// 所以增量维护天然与去重集合保持同步。
/// </para>
/// </summary>
public sealed class FactBase
{
    public TransformLevel CurrentLevel { get; set; } = TransformLevel.Isometry;

    public Dictionary<string, HPoint> Points { get; } = [];
    public Dictionary<string, HLine> Lines { get; } = [];
    public Dictionary<string, Conic> Conics { get; } = [];

    public HashSet<GeoFact> Facts { get; } = [];
    public List<(GeoFact Fact, string Trace)> DerivationLog { get; } = [];

    // --- 按类型的只读视图（由 AddFact 增量维护） ---
    private readonly List<OnLineFact> _onLines = [];
    private readonly List<OnConicFact> _onConics = [];
    private readonly List<TangentFact> _tangents = [];
    private readonly List<PolarOfFact> _polars = [];
    private readonly List<CenterOfConicFact> _centers = [];
    private readonly List<MidpointFact> _midpoints = [];
    private readonly List<HarmonicFact> _harmonics = [];
    private readonly List<CollinearFact> _collinears = [];
    private readonly List<ConcurrentFact> _concurrents = [];
    private readonly List<ConjugateDirectionsFact> _conjugateDirections = [];
    private readonly List<ConstantSlopeProductFact> _slopeProducts = [];

    public IReadOnlyList<OnLineFact> OnLines => _onLines;
    public IReadOnlyList<OnConicFact> OnConics => _onConics;
    public IReadOnlyList<TangentFact> Tangents => _tangents;
    public IReadOnlyList<PolarOfFact> Polars => _polars;
    public IReadOnlyList<CenterOfConicFact> Centers => _centers;
    public IReadOnlyList<MidpointFact> Midpoints => _midpoints;
    public IReadOnlyList<HarmonicFact> Harmonics => _harmonics;
    public IReadOnlyList<CollinearFact> Collinears => _collinears;
    public IReadOnlyList<ConcurrentFact> Concurrents => _concurrents;
    public IReadOnlyList<ConjugateDirectionsFact> ConjugateDirections => _conjugateDirections;
    public IReadOnlyList<ConstantSlopeProductFact> SlopeProducts => _slopeProducts;

    // --- 按 id 的索引（同上，由 AddFact 增量维护） ---
    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>();

    private readonly Dictionary<string, HashSet<string>> _pointsOnLine = [];
    private readonly Dictionary<string, HashSet<string>> _linesThroughPoint = [];
    private readonly Dictionary<string, HashSet<string>> _conicsOfPoint = [];
    private readonly Dictionary<string, HashSet<string>> _pointsOnConic = [];
    private readonly Dictionary<string, List<PolarOfFact>> _polarsOfConic = [];

    // 幂集事实（三点共线 / 三线共点）无法按单个 id 索引，按"对"索引第三元。
    private readonly Dictionary<string, HashSet<string>> _thirdPointsByPair = [];
    private readonly Dictionary<string, HashSet<string>> _thirdLinesByPair = [];

    public void AddPoint(string id, HPoint p) => Points[id] = p;
    public void AddLine(string id, HLine l) => Lines[id] = l;
    public void AddConic(string id, Conic c) => Conics[id] = c;

    /// <summary>
    /// 用变换矩阵自动鉴定当前视角等级并写入。
    /// 分类结果直接驱动定理引擎的守卫：等级越高（变换越一般），能激活的定理越少。
    /// </summary>
    /// <exception cref="InvalidOperationException">矩阵退化（行列式为 0）时无法归类。</exception>
    public void SetLevelFromTransform(Matrix3x3 transform) =>
        CurrentLevel = PerspectiveTransform.Classify(transform);

    // ========================================================================
    // 声明式查询：让规则的前提读起来就像定理陈述
    // ========================================================================

    /// <summary>过某条直线的所有点 id（"L 上的点"）。</summary>
    public IReadOnlySet<string> PointsOn(string lineId) =>
        _pointsOnLine.TryGetValue(lineId, out var s) ? s : EmptySet;

    /// <summary>过某个点的所有直线 id（"过 Q 的直线"）。</summary>
    public IReadOnlySet<string> LinesThrough(string pointId) =>
        _linesThroughPoint.TryGetValue(pointId, out var s) ? s : EmptySet;

    /// <summary>位于某条圆锥曲线上的所有点 id。</summary>
    public IReadOnlySet<string> PointsOnConic(string conicId) =>
        _pointsOnConic.TryGetValue(conicId, out var s) ? s : EmptySet;

    /// <summary>某个点为极点的全部极线事实。</summary>
    public IReadOnlyList<PolarOfFact> PolarsOf(string conicId) =>
        _polarsOfConic.TryGetValue(conicId, out var l) ? l : [];

    /// <summary>点是否在直线上（O(1) 事实查询，不触碰坐标）。</summary>
    public bool HasOnLine(string pointId, string lineId) =>
        _linesThroughPoint.TryGetValue(pointId, out var s) && s.Contains(lineId);

    /// <summary>点是否在圆锥曲线上（O(1) 事实查询）。</summary>
    public bool HasOnConic(string pointId, string conicId) =>
        _conicsOfPoint.TryGetValue(pointId, out var s) && s.Contains(conicId);

    /// <summary>已知两点，查与它们共线的第三点（幂集事实按"对"索引）。</summary>
    public IReadOnlySet<string> ThirdPointsCollinearWith(string p1, string p2) =>
        _thirdPointsByPair.TryGetValue(PairKey(p1, p2), out var s) ? s : EmptySet;

    /// <summary>已知两线，查与它们共点的第三条线（幂集事实按"对"索引）。</summary>
    public IReadOnlySet<string> ThirdLinesConcurrentWith(string l1, string l2) =>
        _thirdLinesByPair.TryGetValue(PairKey(l1, l2), out var s) ? s : EmptySet;

    /// <summary>已有直线 id（供规则枚举"每条线上的点"）。</summary>
    public IEnumerable<string> LineIdsWithPoints => _pointsOnLine.Keys;

    /// <summary>已有交点 id（供规则枚举"过每个点的线"）。</summary>
    public IEnumerable<string> PointIdsWithLines => _linesThroughPoint.Keys;

    /// <summary>注册一条新事实（若已存在则忽略；返回 true 表示推出了新知识）。</summary>
    public bool AddFact(GeoFact fact, string trace)
    {
        fact = Canonicalize(fact);
        if (!Facts.Add(fact)) return false;
        DerivationLog.Add((fact, trace));
        Index(fact);
        return true;
    }

    /// <summary>幂集事实入库前规范化（三元组排序），保证排列去重。</summary>
    private static GeoFact Canonicalize(GeoFact fact) => fact switch
    {
        CollinearFact f => CollinearFact.Create(f.P1Id, f.P2Id, f.P3Id),
        ConcurrentFact f => ConcurrentFact.Create(f.L1Id, f.L2Id, f.L3Id),
        _ => fact
    };

    /// <summary>查找经过两点的既有直线；若场景里还没画，就自动辅助连一条并记录事实。</summary>
    public string GetOrCreateLine(string p1Id, string p2Id)
    {
        var existing = LinesThrough(p1Id).FirstOrDefault(lid => HasOnLine(p2Id, lid));
        if (existing is not null) return existing;

        string newId = $"L_{p1Id}{p2Id}";
        Lines[newId] = HLine.FromPoints(Points[p1Id], Points[p2Id]);
        AddFact(new OnLineFact(p1Id, newId), $"[辅助连线] 过 {p1Id}, {p2Id} 作直线");
        AddFact(new OnLineFact(p2Id, newId), $"[辅助连线] 过 {p1Id}, {p2Id} 作直线");
        return newId;
    }

    /// <summary>把一条事实同时登记进类型视图与 id 索引（唯一维护点）。</summary>
    private void Index(GeoFact fact)
    {
        switch (fact)
        {
            case OnLineFact f:
                _onLines.Add(f);
                Bucket(_pointsOnLine, f.LineId).Add(f.PointId);
                Bucket(_linesThroughPoint, f.PointId).Add(f.LineId);
                break;

            case OnConicFact f:
                _onConics.Add(f);
                Bucket(_pointsOnConic, f.ConicId).Add(f.PointId);
                Bucket(_conicsOfPoint, f.PointId).Add(f.ConicId);
                break;

            case TangentFact f:
                _tangents.Add(f);
                break;

            case PolarOfFact f:
                _polars.Add(f);
                if (!_polarsOfConic.TryGetValue(f.ConicId, out var polars))
                    _polarsOfConic[f.ConicId] = polars = [];
                polars.Add(f);
                break;

            case CenterOfConicFact f:
                _centers.Add(f);
                break;

            case MidpointFact f:
                _midpoints.Add(f);
                break;

            case HarmonicFact f:
                _harmonics.Add(f);
                break;

            case CollinearFact f:
                _collinears.Add(f);
                Bucket(_thirdPointsByPair, PairKey(f.P1Id, f.P2Id)).Add(f.P3Id);
                Bucket(_thirdPointsByPair, PairKey(f.P1Id, f.P3Id)).Add(f.P2Id);
                Bucket(_thirdPointsByPair, PairKey(f.P2Id, f.P3Id)).Add(f.P1Id);
                break;

            case ConcurrentFact f:
                _concurrents.Add(f);
                Bucket(_thirdLinesByPair, PairKey(f.L1Id, f.L2Id)).Add(f.L3Id);
                Bucket(_thirdLinesByPair, PairKey(f.L1Id, f.L3Id)).Add(f.L2Id);
                Bucket(_thirdLinesByPair, PairKey(f.L2Id, f.L3Id)).Add(f.L1Id);
                break;

            case ConjugateDirectionsFact f:
                _conjugateDirections.Add(f);
                break;

            case ConstantSlopeProductFact f:
                _slopeProducts.Add(f);
                break;
        }
    }

    private static HashSet<string> Bucket(Dictionary<string, HashSet<string>> map, string key)
    {
        if (!map.TryGetValue(key, out var set))
            map[key] = set = [];
        return set;
    }

    /// <summary>无序二元组的规范键（用于"按对索引第三元"）。</summary>
    private static string PairKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? a + "\u0001" + b : b + "\u0001" + a;
}
