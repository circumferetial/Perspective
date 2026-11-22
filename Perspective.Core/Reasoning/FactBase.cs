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
public sealed record ConstantSlopeProductFact(string Line1Id, string Line2Id, ExactScalar Value) : GeoFact;

/// <summary>
/// 几何场景知识库：存放几何实体、当前视角等级、已知事实及完整推演记录
/// </summary>
public sealed class FactBase
{
    public TransformLevel CurrentLevel { get; set; } = TransformLevel.Isometry;

    public Dictionary<string, HPoint> Points { get; } = [];
    public Dictionary<string, HLine> Lines { get; } = [];
    public Dictionary<string, Conic> Conics { get; } = [];

    public HashSet<GeoFact> Facts { get; } = [];
    public List<(GeoFact Fact, string Trace)> DerivationLog { get; } = [];

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

    /// <summary>注册一条新事实（如果已存在则忽略；返回 true 表示推出了新知识！）</summary>
    public bool AddFact(GeoFact fact, string trace)
    {
        if (!Facts.Add(fact)) return false;
        DerivationLog.Add((fact, trace));
        return true;
    }

    /// <summary>查找经过两点的既有直线；若场景里还没画，就自动辅助连一条并记录事实</summary>
    public string GetOrCreateLine(string p1Id, string p2Id)
    {
        var existing = Facts.OfType<OnLineFact>()
            .Where(f => f.PointId == p1Id)
            .Select(f => f.LineId)
            .FirstOrDefault(lid => Facts.Contains(new OnLineFact(p2Id, lid)));

        if (existing is not null) return existing;

        string newId = $"L_{p1Id}{p2Id}";
        Lines[newId] = HLine.FromPoints(Points[p1Id], Points[p2Id]);
        AddFact(new OnLineFact(p1Id, newId), $"[辅助连线] 过 {p1Id}, {p2Id} 作直线");
        AddFact(new OnLineFact(p2Id, newId), $"[辅助连线] 过 {p1Id}, {p2Id} 作直线");
        return newId;
    }
}