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

/// <summary>两直线平行（不重合）。仿射不变量。</summary>
public sealed record ParallelFact(string Line1Id, string Line2Id) : GeoFact
{
    /// <summary>规范化为排序二元组（排列无关去重）。</summary>
    public static ParallelFact Create(string l1, string l2) =>
        string.CompareOrdinal(l1, l2) <= 0 ? new(l1, l2) : new(l2, l1);
}

/// <summary>两直线垂直。相似不变量。</summary>
public sealed record PerpendicularFact(string Line1Id, string Line2Id) : GeoFact
{
    /// <summary>规范化为排序二元组（排列无关去重）。</summary>
    public static PerpendicularFact Create(string l1, string l2) =>
        string.CompareOrdinal(l1, l2) <= 0 ? new(l1, l2) : new(l2, l1);
}

/// <summary>
/// 面积比（仿射不变量）：S(A1A2A3)/S(B1B2B3) = Value（三角形顶点顺序无关，内部排序去重）。
/// 约定用**无向面积**（正值），验证用 area²·r² 的消根号形式。
/// </summary>
public sealed record AreaRatioFact(
    string A1, string A2, string A3,
    string B1, string B2, string B3,
    ExactScalar Value) : GeoFact
{
    public static AreaRatioFact Create(
        string a1, string a2, string a3, string b1, string b2, string b3, ExactScalar value)
    {
        var (x, y, z) = Sort3(a1, a2, a3);
        var (u, v, w) = Sort3(b1, b2, b3);
        return new(x, y, z, u, v, w, value.Simplified());
    }

    private static (string, string, string) Sort3(string a, string b, string c)
    {
        var ids = new[] { a, b, c };
        Array.Sort(ids, string.CompareOrdinal);
        return (ids[0], ids[1], ids[2]);
    }
}

/// <summary>
/// 线段比（相似不变量）：|A1A2|/|B1B2| = Value（端点顺序无关）。
/// 验证用平方形式 |A1A2|² = r²·|B1B2|²（无根号），要求分母线段非退化。
/// </summary>
public sealed record SegmentRatioFact(
    string A1, string A2, string B1, string B2, ExactScalar Value) : GeoFact
{
    public static SegmentRatioFact Create(
        string a1, string a2, string b1, string b2, ExactScalar value)
    {
        var (x, y) = Sort2(a1, a2);
        var (u, v) = Sort2(b1, b2);
        return new(x, y, u, v, value.Simplified());
    }

    private static (string, string) Sort2(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
}

/// <summary>
/// 交比（射影不变量）：(A,B;C,D) = Value。
/// 规范化利用 4 元子群：对内换序取倒数，交换两对不变；对间按字典序排列。
/// </summary>
public sealed record CrossRatioFact(
    string A, string B, string C, string D, ExactScalar Value) : GeoFact
{
    public static CrossRatioFact Create(string a, string b, string c, string d, ExactScalar value)
    {
        bool swapA = string.CompareOrdinal(a, b) > 0;
        bool swapC = string.CompareOrdinal(c, d) > 0;

        var (x, y) = swapA ? (b, a) : (a, b);
        var (u, v) = swapC ? (d, c) : (c, d);

        var normalized = value;
        if (swapA ^ swapC)
            normalized = (ExactScalar.FromInt(1) / value).Simplified();  // 交换一对取倒数

        // 两对按字典序排列（交换两对不改变交比）
        if (string.CompareOrdinal(x, u) > 0 || (x == u && string.CompareOrdinal(y, v) > 0))
            (x, y, u, v) = (u, v, x, y);

        return new(x, y, u, v, normalized);
    }
}

/// <summary>
/// 有向角相等（mod π）：θ(A1,A2) ≡ Sign·θ(B1,B2) (mod π)。
/// <para>
/// 【规范化】每对线内部按 id 排序（换序使角取负 → 记入 Sign），
/// 两对整体再按 lexicographic 排序（交换两对不改变 Sign）。
/// 因此"∠(L1,L2)=∠(L3,L4)"与"∠(L4,L3)=∠(L2,L1)"等全部归一到同一事实。
/// </para>
/// <para>
/// 【视角】有向角是相似不变量（Similarity 层）。
/// </para>
/// </summary>
public sealed record AngleEqualFact(string A1, string A2, string B1, string B2, int Sign) : GeoFact
{
    /// <summary>从"已知 θ(l1,l2) = sign·θ(l3,l4)"构造规范事实（再乘以两对内排序引入的符号）。</summary>
    public static AngleEqualFact CreateExplicit(string l1, string l2, string l3, string l4, int sign)
    {
        var ((a1, a2), sa) = CanonPair(l1, l2);
        var ((b1, b2), sb) = CanonPair(l3, l4);
        int totalSign = sign * sa * sb;

        if (ComparePair((a1, a2), (b1, b2)) > 0)
            ((a1, a2), (b1, b2)) = ((b1, b2), (a1, a2));

        return new(a1, a2, b1, b2, totalSign);
    }

    public static AngleEqualFact Create(string l1, string l2, string l3, string l4)
    {
        var ((a1, a2), sa) = CanonPair(l1, l2);
        var ((b1, b2), sb) = CanonPair(l3, l4);

        // 两对按 lexicographic 排序；交换两对不改变 Sign
        if (ComparePair((a1, a2), (b1, b2)) > 0)
            ((a1, a2), (b1, b2)) = ((b1, b2), (a1, a2));

        return new(a1, a2, b1, b2, sa * sb);
    }

    private static ((string, string) Pair, int Sign) CanonPair(string x, string y) =>
        string.CompareOrdinal(x, y) <= 0 ? ((x, y), 1) : ((y, x), -1);

    private static int ComparePair((string, string) a, (string, string) b)
    {
        int first = string.CompareOrdinal(a.Item1, b.Item1);
        return first != 0 ? first : string.CompareOrdinal(a.Item2, b.Item2);
    }
}

/// <summary>非退化假设：<see cref="Expression"/> 不为零（不参与推导，只限定结论适用域）。</summary>
public sealed record NonDegenerateAssumption(string Label, ExactScalar Expression);

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
    /// <summary>结构化证明记录（事实 ← 规则 ← 前提）。解释文本由 ProofNarrator 渲染。</summary>
    public List<Derivation> DerivationLog { get; } = [];

    /// <summary>
    /// 非退化假设（NDG）：几何证明里"任取的一般点不在某线上"这类前提无法证明，
    /// 只能作为假设登记。它们不参与推导，只影响结论的适用条件。
    /// </summary>
    public List<NonDegenerateAssumption> Assumptions { get; } = [];

    /// <summary>
    /// 坐标级验证缓存（一次运行内有效）。
    /// <para>
    /// 【能缓存什么】只依赖坐标的判定：IsHarmonic、ContainsStrictly、Collinear、IsTangent…
    /// 这类判定与事实库后来新增了什么无关，重复调用结果不变。
    /// </para>
    /// <para>
    /// 【绝不能缓存什么】依赖"事实是否存在"的判定（如"找不到某条极线"）——
    /// 事实库是 append-only，后续规则可能补上该事实，缓存失败会导致漏推。
    /// </para>
    /// </summary>
    private readonly Dictionary<string, bool> _checkCache = [];

    /// <summary>带缓存的布尔判定：同一 key 只计算一次（仅用于坐标级判定，见类注释）。</summary>
    public bool CachedCheck(string key, Func<bool> compute)
    {
        if (_checkCache.TryGetValue(key, out bool cached)) return cached;
        bool result = compute();
        _checkCache[key] = result;
        return result;
    }

    /// <summary>缓存条目数（诊断/基准用）。</summary>
    public int CachedCheckCount => _checkCache.Count;

    /// <summary>
    /// 结构化点（用户/题面声明的构造点）与派生点（规则生成的辅助点）的区分。
    /// 组合型规则只枚举结构化点；派生点需要参与时显式提升（见 <see cref="PromoteToStructural"/>）。
    /// </summary>
    private readonly HashSet<string> _structuralPoints = [];

    /// <summary>黑箱代数步骤记录（外部判定器的结论；不参与符号推导，仅供解释层引用）。</summary>
    public List<OracleStep> OracleLog { get; } = [];

    /// <summary>推演诊断：截断、跳过的候选等"可能漏解"的提示（不静默）。</summary>
    public List<string> Diagnostics { get; } = [];

    /// <summary>某点是否属于结构化点（用户声明）而非规则派生的辅助点。</summary>
    public bool IsStructural(string pointId) => _structuralPoints.Contains(pointId);

    /// <summary>把派生点提升为结构化点（例如用户希望它也参与组合枚举）。</summary>
    public void PromoteToStructural(string pointId) => _structuralPoints.Add(pointId);

    /// <summary>结构化点 id 列表（组合型规则的候选池）。</summary>
    public IEnumerable<string> StructuralPointIds => Points.Keys.Where(IsStructural);

    /// <summary>
    /// 登记一条"某表达式不为零"的非退化假设。
    /// 【一致性检查】若该表达式**恒为零**，说明在这些对象下假设必然失败（构形退化），
    /// 返回 false 并不登记。
    /// </summary>
    public bool AssumeNonZero(ExactScalar expression, string label)
    {
        if (expression.VerifyStrictZero() == StrictResult.StrictlyTrue) return false;
        Assumptions.Add(new NonDegenerateAssumption(label, expression));
        return true;
    }

    /// <summary>登记"点不在直线上"（代入直线方程 ≠ 0）的非退化假设。</summary>
    public bool AssumePointNotOnLine(HPoint point, HLine line, string label) =>
        AssumeNonZero(line.A * point.X + line.B * point.Y + line.C * point.W, label);

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
    private readonly List<ParallelFact> _parallels = [];
    private readonly List<PerpendicularFact> _perpendiculars = [];
    private readonly List<AngleEqualFact> _angleEquals = [];
    private readonly List<AreaRatioFact> _areaRatios = [];
    private readonly List<SegmentRatioFact> _segmentRatios = [];
    private readonly List<CrossRatioFact> _crossRatios = [];

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
    public IReadOnlyList<ParallelFact> Parallels => _parallels;
    public IReadOnlyList<PerpendicularFact> Perpendiculars => _perpendiculars;
    public IReadOnlyList<AngleEqualFact> AngleEquals => _angleEquals;
    public IReadOnlyList<AreaRatioFact> AreaRatios => _areaRatios;
    public IReadOnlyList<SegmentRatioFact> SegmentRatios => _segmentRatios;
    public IReadOnlyList<CrossRatioFact> CrossRatios => _crossRatios;

    // --- 按 id 的索引（同上，由 AddFact 增量维护） ---
    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>();

    private readonly Dictionary<string, HashSet<string>> _pointsOnLine = [];
    private readonly Dictionary<string, HashSet<string>> _linesThroughPoint = [];
    private readonly Dictionary<string, HashSet<string>> _conicsOfPoint = [];
    private readonly Dictionary<string, HashSet<string>> _pointsOnConic = [];
    private readonly Dictionary<string, List<PolarOfFact>> _polarsOfConic = [];
    private readonly Dictionary<string, HashSet<string>> _linesParallelTo = [];
    private readonly Dictionary<string, HashSet<string>> _linesPerpendicularTo = [];
    private readonly Dictionary<string, List<AngleEqualFact>> _anglesByPair = [];
    private readonly Dictionary<string, List<AreaRatioFact>> _areaRatiosByTriangle = [];
    private readonly Dictionary<string, List<SegmentRatioFact>> _segmentRatiosBySegment = [];
    private readonly Dictionary<string, List<CrossRatioFact>> _crossRatiosByPair = [];

    // 幂集事实（三点共线 / 三线共点）无法按单个 id 索引，按"对"索引第三元。
    private readonly Dictionary<string, HashSet<string>> _thirdPointsByPair = [];
    private readonly Dictionary<string, HashSet<string>> _thirdLinesByPair = [];

    /// <summary>
    /// 登记点。<paramref name="structural"/> 为 false 表示这是规则生成的派生点
    /// （不参与组合型规则的点枚举）；用户/题面声明的点默认是结构化点。
    /// </summary>
    public void AddPoint(string id, HPoint p, bool structural = true)
    {
        Points[id] = p;
        if (structural) _structuralPoints.Add(id);
    }

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

    /// <summary>已有曲线 id（供规则枚举"每条曲线上的点"）。</summary>
    public IEnumerable<string> ConicIdsWithPoints => _pointsOnConic.Keys;

    /// <summary>与给定直线平行的直线（无序索引）。</summary>
    public IReadOnlySet<string> LinesParallelTo(string lineId) =>
        _linesParallelTo.TryGetValue(lineId, out var set) ? set : EmptySet;

    /// <summary>与给定直线垂直的直线（无序索引）。</summary>
    public IReadOnlySet<string> LinesPerpendicularTo(string lineId) =>
        _linesPerpendicularTo.TryGetValue(lineId, out var set) ? set : EmptySet;

    /// <summary>包含指定线对（任一侧）的所有"角相等"事实。</summary>
    public IReadOnlyList<AngleEqualFact> AnglesInvolving(string line1, string line2) =>
        _anglesByPair.TryGetValue(PairKey(line1, line2), out var list) ? list : [];

    /// <summary>包含指定三角形的所有面积比事实（任一侧）。</summary>
    public IReadOnlyList<AreaRatioFact> AreaRatiosWith(string a, string b, string c) =>
        _areaRatiosByTriangle.TryGetValue(TripleKey(a, b, c), out var list) ? list : [];

    /// <summary>包含指定线段的所有线段比事实（任一侧）。</summary>
    public IReadOnlyList<SegmentRatioFact> SegmentRatiosWith(string a, string b) =>
        _segmentRatiosBySegment.TryGetValue(PairKey(a, b), out var list) ? list : [];

    /// <summary>包含指定点对的所有交比事实（任一侧）。</summary>
    public IReadOnlyList<CrossRatioFact> CrossRatiosWith(string a, string b) =>
        _crossRatiosByPair.TryGetValue(PairKey(a, b), out var list) ? list : [];

    /// <summary>注册一条新事实（无规则出处：初始条件 / 辅助连线；解释由 Note 承担）。</summary>
    public bool AddFact(GeoFact fact, string note) =>
        AddFactCore(fact, new Derivation(RuleIds.AdHoc, fact, [], note));

    /// <summary>
    /// 注册一条由规则推出的事实：**只记录结构化前提**（规则 id + 前提事实），不携带自然语言。
    /// 解释文本由 <see cref="ProofNarrator"/> 统一渲染。
    /// 【命名说明】刻意与 <see cref="AddFact"/> 分开：零前提时 params 重载会与"note 重载"
    /// 产生重载歧义，导致规则记录被误当成初始条件。
    /// </summary>
    public bool AddDerivedFact(GeoFact fact, string ruleId, params GeoFact[] premises) =>
        AddFactCore(fact, new Derivation(ruleId, fact, premises));

    /// <summary>登记"外部构造"产生的事实：来源写入 Derivation.Note（可追溯，不伪造推导）。</summary>
    public bool AddConstructedFact(GeoFact fact, string provenance) =>
        AddFactCore(fact, new Derivation(RuleIds.ExternalConstruction, fact, [], provenance));

    private bool AddFactCore(GeoFact fact, Derivation derivation)
    {
        fact = Canonicalize(fact);
        if (!Facts.Add(fact)) return false;
        DerivationLog.Add(derivation with { Fact = fact });
        Index(fact);
        return true;
    }

    /// <summary>幂集事实入库前规范化（三元组排序），保证排列去重。</summary>
    private static GeoFact Canonicalize(GeoFact fact) => fact switch
    {
        CollinearFact f => CollinearFact.Create(f.P1Id, f.P2Id, f.P3Id),
        ConcurrentFact f => ConcurrentFact.Create(f.L1Id, f.L2Id, f.L3Id),
        ParallelFact f => ParallelFact.Create(f.Line1Id, f.Line2Id),
        PerpendicularFact f => PerpendicularFact.Create(f.Line1Id, f.Line2Id),
        // 注意：Create 会按 id 顺序重推符号，这里必须保留原有 Sign
        AngleEqualFact f => AngleEqualFact.CreateExplicit(f.A1, f.A2, f.B1, f.B2, f.Sign),
        AreaRatioFact f => AreaRatioFact.Create(f.A1, f.A2, f.A3, f.B1, f.B2, f.B3, f.Value),
        SegmentRatioFact f => SegmentRatioFact.Create(f.A1, f.A2, f.B1, f.B2, f.Value),
        CrossRatioFact f => CrossRatioFact.Create(f.A, f.B, f.C, f.D, f.Value),
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

            case ParallelFact f:
                _parallels.Add(f);
                Bucket(_linesParallelTo, f.Line1Id).Add(f.Line2Id);
                Bucket(_linesParallelTo, f.Line2Id).Add(f.Line1Id);
                break;

            case PerpendicularFact f:
                _perpendiculars.Add(f);
                Bucket(_linesPerpendicularTo, f.Line1Id).Add(f.Line2Id);
                Bucket(_linesPerpendicularTo, f.Line2Id).Add(f.Line1Id);
                break;

            case AngleEqualFact f:
                _angleEquals.Add(f);
                BucketList(_anglesByPair, PairKey(f.A1, f.A2)).Add(f);
                BucketList(_anglesByPair, PairKey(f.B1, f.B2)).Add(f);
                break;

            case AreaRatioFact f:
                _areaRatios.Add(f);
                BucketList(_areaRatiosByTriangle, TripleKey(f.A1, f.A2, f.A3)).Add(f);
                BucketList(_areaRatiosByTriangle, TripleKey(f.B1, f.B2, f.B3)).Add(f);
                break;

            case SegmentRatioFact f:
                _segmentRatios.Add(f);
                BucketList(_segmentRatiosBySegment, PairKey(f.A1, f.A2)).Add(f);
                BucketList(_segmentRatiosBySegment, PairKey(f.B1, f.B2)).Add(f);
                break;

            case CrossRatioFact f:
                _crossRatios.Add(f);
                BucketList(_crossRatiosByPair, PairKey(f.A, f.B)).Add(f);
                BucketList(_crossRatiosByPair, PairKey(f.C, f.D)).Add(f);
                break;
        }
    }

    private static List<T> BucketList<T>(Dictionary<string, List<T>> map, string key)
    {
        if (!map.TryGetValue(key, out var list))
            map[key] = list = [];
        return list;
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

    /// <summary>无序三元组的规范键（面积比的三角形索引）。</summary>
    private static string TripleKey(string a, string b, string c)
    {
        var ids = new[] { a, b, c };
        Array.Sort(ids, string.CompareOrdinal);
        return string.Join("\u0001", ids);
    }
}
