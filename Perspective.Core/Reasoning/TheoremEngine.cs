using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

/// <summary>
/// 定理规则的 static abstract 契约：零堆内存分配，编译期绑定变换等级
/// </summary>
public interface IFactRule<TSelf> where TSelf : IFactRule<TSelf>
{
    static abstract string Title { get; }
    static abstract TransformLevel RequiredLevel { get; }
    static abstract int Apply(FactBase facts);
}

/// <summary>
/// 前向推演引擎：不断用已有事实匹配定理前提，推出新事实，直到推不动为止（不动点）
/// </summary>
public sealed class TheoremEngine
{
    private readonly List<(string Title, TransformLevel Level, Func<FactBase, int> Run)> _rules = [];

    public TheoremEngine Register<TRule>() where TRule : IFactRule<TRule>
    {
        _rules.Add((TRule.Title, TRule.RequiredLevel, TRule.Apply));
        return this;
    }

    public void RunToFixedPoint(FactBase facts, int maxPasses = 10)
    {
        for (int pass = 1; pass <= maxPasses; pass++)
        {
            int addedInPass = 0;
            foreach (var (title, reqLevel, run) in _rules)
            {
                // 前提守卫 1：当前视角必须满足该定理的埃尔朗根不变性级别！
                if (facts.CurrentLevel > reqLevel)
                    continue;

                addedInPass += run(facts);
            }

            if (addedInPass == 0) break; // 没有新结论产生，推理完成！
        }
    }
}

// ============================================================================
// 首批内置命题库（每条规则独立封装：查前提 -> 严通道验真 -> 写入新结论）
// ============================================================================

/// <summary>
/// 命题 1：从外部点 Q 引两条切线 QA, QB，则切点弦 AB 即为 Q 关于曲线的极线
/// 【前提】① l₁, l₂ 严格切同一非退化曲线（CheckLineRelation 回验）；② 切点 A、B 严格在
///         曲线上（ContainsStrictly 回验）；③ Q 为 l₁∩l₂ 且异于 A、B——两条互异切线
///         的公共点必在曲线外，故无需单独验证。
/// 【结论】切点弦 AB = Q 的极线 C·Q。
/// 【推导】A 在曲线上 ⟹ A 的极线即过 A 的切线 l₁；Q ∈ l₁。由配极双线性（La Hire），
///         Q ∈ A 的极线 ⟺ A ∈ Q 的极线；同理 B ∈ Q 的极线。两点定线，故 Q 的极线 = AB。
/// 【等级】Projective——配极关系是射影不变量。
/// </summary>
public readonly struct ChordOfContactIsPolarRule : IFactRule<ChordOfContactIsPolarRule>
{
    public static string Title => "切点弦极线定理";
    public static TransformLevel RequiredLevel => TransformLevel.Projective;

    public static int Apply(FactBase facts)
    {
        int count = 0;
        var tangents = facts.Facts.OfType<TangentFact>().ToList();
        var onLines = facts.Facts.OfType<OnLineFact>().ToList();

        for (int i = 0; i < tangents.Count; i++)
        {
            for (int j = i + 1; j < tangents.Count; j++)
            {
                var t1 = tangents[i];
                var t2 = tangents[j];
                if (t1.ConicId != t2.ConicId || t1.TouchPointId == t2.TouchPointId) continue;

                // 找两条切线的公共点 Q
                var qIds = onLines.Where(f => f.LineId == t1.LineId).Select(f => f.PointId)
                    .Intersect(onLines.Where(f => f.LineId == t2.LineId).Select(f => f.PointId))
                    .Where(pid => pid != t1.TouchPointId && pid != t2.TouchPointId)
                    .ToList();

                foreach (var qId in qIds)
                {
                    var conic = facts.Conics[t1.ConicId];
                    var ptQ = facts.Points[qId];
                    var ptA = facts.Points[t1.TouchPointId];
                    var ptB = facts.Points[t2.TouchPointId];

                    // 前提严通道验真：两条线真的严格相切，且 Q 在两条切线上
                    if (conic.CheckLineRelation(facts.Lines[t1.LineId]).Relation != LineConicRelation.Tangent) continue;
                    if (conic.CheckLineRelation(facts.Lines[t2.LineId]).Relation != LineConicRelation.Tangent) continue;

                    // 前提严通道验真：切点 A、B 必须严格落在圆锥曲线上。
                    // 仅凭“极线过 A、B”不能推出“切点弦 = 极线”，A、B 在曲线上是必要前提。
                    if (conic.ContainsStrictly(ptA) != StrictResult.StrictlyTrue) continue;
                    if (conic.ContainsStrictly(ptB) != StrictResult.StrictlyTrue) continue;

                    var polarQ = conic.PolarLine(ptQ);
                    if (polarQ.ContainsStrictly(ptA) != StrictResult.StrictlyTrue ||
                        polarQ.ContainsStrictly(ptB) != StrictResult.StrictlyTrue) continue;

                    string chordId = facts.GetOrCreateLine(t1.TouchPointId, t2.TouchPointId);
                    if (facts.AddFact(new PolarOfFact(chordId, qId, t1.ConicId),
                        $"[{Title}] 因 {t1.LineId}, {t2.LineId} 相切且交于 {qId} => 切点弦 {chordId} 即为 {qId} 的极线"))
                        count++;
                }
            }
        }
        return count;
    }
}

/// <summary>
/// 命题 2：La Hire 极点极线互反定理（Q 在 P 的极线上 => P 必在 Q 的极线上）
/// 【前提】① 同一曲线的两条极线声明 L_P = C·P、L_Q = C·Q（SameLineStrictly 严通道
///         回验，不信任 PolarOfFact 标签）；② Q ∈ L_P（信任 OnLineFact）；
///         ③ P ∈ L_Q（严通道回验）。
/// 【结论】OnLineFact(P, L_Q)。
/// 【推导】C 对称 ⟹ Qᵀ(CP) = (QᵀCP)ᵀ = Pᵀ(CQ)，即配极关系对称：Q 在 P 的极线上
///         ⟺ P 在 Q 的极线上。
/// 【注】前提①③与结论均经严通道回验；本规则的作用是把"Q 在 P 的极线上"这一
///       配极对称性以 OnLineFact 形式传播进事实库，供后续规则（如调和点列）使用。
/// 【等级】Projective。
/// </summary>
public readonly struct PolePolarReciprocityRule : IFactRule<PolePolarReciprocityRule>
{
    public static string Title => "La Hire 极点极线互反定理";
    public static TransformLevel RequiredLevel => TransformLevel.Projective;

    /// <summary>严通道判断两条齐次直线是否重合：叉积分量全部严格为零（齐次向量成比例）。</summary>
    private static bool SameLineStrictly(HLine a, HLine b)
    {
        var cx = a.B * b.C - a.C * b.B;
        var cy = a.C * b.A - a.A * b.C;
        var cz = a.A * b.B - a.B * b.A;
        return cx.VerifyStrictZero() == StrictResult.StrictlyTrue
            && cy.VerifyStrictZero() == StrictResult.StrictlyTrue
            && cz.VerifyStrictZero() == StrictResult.StrictlyTrue;
    }

    public static int Apply(FactBase facts)
    {
        int count = 0;
        var polars = facts.Facts.OfType<PolarOfFact>().ToList();

        foreach (var pFact in polars)
        {
            foreach (var qFact in polars)
            {
                if (pFact.ConicId != qFact.ConicId || pFact.PoleId == qFact.PoleId) continue;

                // 前提严通道回验：确认两条线确实分别是两极点的极线（不信任 PolarOfFact 标签）。
                // 线与线重合的判据：叉积分量全为 0（齐次向量成比例）。
                // 与其他规则统一纪律：结论写库前，几何前提必须自己过一遍严通道。
                var conic = facts.Conics[pFact.ConicId];
                if (!SameLineStrictly(conic.PolarLine(facts.Points[pFact.PoleId]), facts.Lines[pFact.LineId])) continue;
                if (!SameLineStrictly(conic.PolarLine(facts.Points[qFact.PoleId]), facts.Lines[qFact.LineId])) continue;

                // 前提：Q 在 P 的极线 L_P 上
                if (!facts.Facts.Contains(new OnLineFact(qFact.PoleId, pFact.LineId))) continue;

                // 严通道验真：L_Q 真的严格经过 P
                var lineQ = facts.Lines[qFact.LineId];
                var ptP = facts.Points[pFact.PoleId];
                if (lineQ.ContainsStrictly(ptP) != StrictResult.StrictlyTrue) continue;

                if (facts.AddFact(new OnLineFact(pFact.PoleId, qFact.LineId),
                    $"[{Title}] 因 {qFact.PoleId} 在 {pFact.PoleId} 的极线 {pFact.LineId} 上 => {qFact.PoleId} 的极线 {qFact.LineId} 恒过定点 {pFact.PoleId}！"))
                    count++;
            }
        }
        return count;
    }
}

/// <summary>
/// 命题 3：极点极线调和点列定理（过 Q 的割线交曲线于 M, N 并交极线于 R => (Q, R; M, N) = -1）
/// 【前提】① Q 的极线 p（信任 PolarOfFact）；② 割线 s 过 Q 且 s ≠ p（事实过滤）；
///         ③ R = s ∩ p（取同时在两条线上的事实点）；④ M, N ∈ s ∩ C（事实点 ≥ 2，
///         切线情形自然被排除）；⑤ 四点互异且交比分母非零（IsHarmonic 内部守卫 +
///         严通道回验）。
/// 【结论】调和点列 (Q, R; M, N) = −1。
/// 【推导】射影几何标准定理（极点极线的调和性质）。证明骨架：沿割线取参数 t，
///         C ∩ s 的两根为 t_M, t_N；极线条件 Qᵀ(C X(t)) = 0 是 t 的线性方程，
///         其根 t_R = (t_M + t_N)/2 的射影版本，代入交比定义
///         (Q,R;M,N) = (t_M−t_Q)(t_N−t_R) / [(t_M−t_R)(t_N−t_Q)] 化简得 −1。
///         交比是射影不变量，故对任意非退化二次曲线成立。
/// 【等级】Projective。
/// </summary>
public readonly struct HarmonicPolarBundleRule : IFactRule<HarmonicPolarBundleRule>
{
    public static string Title => "极点极线调和点列定理";
    public static TransformLevel RequiredLevel => TransformLevel.Projective;

    public static int Apply(FactBase facts)
    {
        int count = 0;
        var polars = facts.Facts.OfType<PolarOfFact>().ToList();
        var onLines = facts.Facts.OfType<OnLineFact>().ToList();
        var onConics = facts.Facts.OfType<OnConicFact>().ToList();

        foreach (var polar in polars)
        {
            // 找所有过极点 Q 的直线（排除极线自己）
            var secantIds = onLines
                .Where(f => f.PointId == polar.PoleId && f.LineId != polar.LineId)
                .Select(f => f.LineId)
                .ToList();

            foreach (var secId in secantIds)
            {
                // 找交极线于 R
                var rIds = onLines.Where(f => f.LineId == secId).Select(f => f.PointId)
                    .Intersect(onLines.Where(f => f.LineId == polar.LineId).Select(f => f.PointId))
                    .ToList();

                // 找割线与曲线的两个交点 M, N
                var mnIds = onLines.Where(f => f.LineId == secId).Select(f => f.PointId)
                    .Intersect(onConics.Where(f => f.ConicId == polar.ConicId).Select(f => f.PointId))
                    .Distinct()
                    .ToList();

                if (mnIds.Count < 2) continue;

                foreach (var rId in rIds)
                {
                    for (int i = 0; i < mnIds.Count; i++)
                    for (int j = i + 1; j < mnIds.Count; j++)
                    {
                        string mId = mnIds[i], nId = mnIds[j];
                        var res = GeometryAudit.IsHarmonic(
                            facts.Points[polar.PoleId], facts.Points[rId],
                            facts.Points[mId], facts.Points[nId]);

                        if (res != StrictResult.StrictlyTrue) continue;

                        if (facts.AddFact(new HarmonicFact(polar.PoleId, rId, mId, nId),
                            $"[{Title}] 割线 {secId} 交极线 {polar.LineId} 于 {rId}、交曲线于 {mId},{nId} => 严格构成调和点列 ({polar.PoleId}, {rId}; {mId}, {nId}) = -1"))
                            count++;
                    }
                }
            }
        }
        return count;
    }
}

/// <summary>
/// 命题 4：有心圆锥曲线点差法（仅 Affine 及以上视角有效！切到 Projective 自动屏蔽）
/// 【前提】① 交叉项 M₀₁ 严格为 0（VerifyStrictZero 回验）——符号验证确认含 xy 项时
///         公式失效，此为必要前提；② a = M₀₀ ≠ 0 且 c = M₁₁ ≠ 0（有心非退化；
///         抛物线无有限中心，不会注册 CenterOfConicFact，自然排除）；③ M 严格为
///         AB 中点（IsMidpoint 回验）；④ A、B 严格在曲线上（ContainsStrictly 回验）；
///         ⑤ O 为曲线中心（CenterOfConicFact）。注意：**不要求中心在原点**——
///         符号验证确认公式对平移后的曲线（d,e 任意）依然恒等。
/// 【结论】k_OM · k_AB = −M₀₀/M₁₁。
/// 【推导】设 A = (u+t, v+s)、B = (u−t, v−s)、M = (u,v)。f(A) = f(B) = 0 相减：
///         4a·u·t + 4c·v·s + 2d·t + 2e·s = 0 ⟹ k_AB = s/t = −(2a·u+d)/(2c·v+e)。
///         中心 O = (−d/2a, −e/2c) ⟹ k_OM = (v+e/2c)/(u+d/2a)。
///         两式相乘，(2a·u+d) 与 (2c·v+e) 恰好约去，得 −a/c = −M₀₀/M₁₁。
///         含 xy 项时相减多出 4b(x₀s + y₀t) 项，约简不再发生，公式失效。
/// 【等级】Affine——中点与斜率积在射影变换下不保持。
/// </summary>
public readonly struct ConicMidpointSlopeRule : IFactRule<ConicMidpointSlopeRule>
{
    public static string Title => "点差法中点斜率积定理";
    public static TransformLevel RequiredLevel => TransformLevel.Affine; // 射影视角不保中点与斜率积！

    public static int Apply(FactBase facts)
    {
        int count = 0;
        var centers = facts.Facts.OfType<CenterOfConicFact>().ToList();
        var midpoints = facts.Facts.OfType<MidpointFact>().ToList();
        var onConics = facts.Facts.OfType<OnConicFact>().ToList();

        foreach (var cen in centers)
        {
            var conic = facts.Conics[cen.ConicId];
            // 交叉项必须严格为 0：点差法的 -M00/M11 公式仅对无 xy 项的有心二次曲线成立。
            // 用严通道而非数值容差，避免“交叉项很小但不为 0”的曲线被误判为可用。
            if (conic.Matrix.M01.VerifyStrictZero() != StrictResult.StrictlyTrue) continue;

            foreach (var mid in midpoints)
            {
                if (!onConics.Contains(new OnConicFact(mid.AId, cen.ConicId)) ||
                    !onConics.Contains(new OnConicFact(mid.BId, cen.ConicId))) continue;

                // 前提严通道验真：M 确实严格是 AB 的中点，且 A、B 严格在曲线上。
                // 不能只信任 MidpointFact / OnConicFact 标签，必须回验坐标。
                var ptA = facts.Points[mid.AId];
                var ptB = facts.Points[mid.BId];
                var ptM = facts.Points[mid.MidId];
                if (GeometryAudit.IsMidpoint(ptM, ptA, ptB) != StrictResult.StrictlyTrue) continue;
                if (conic.ContainsStrictly(ptA) != StrictResult.StrictlyTrue) continue;
                if (conic.ContainsStrictly(ptB) != StrictResult.StrictlyTrue) continue;

                string lineOM = facts.GetOrCreateLine(cen.CenterId, mid.MidId);
                string lineAB = facts.GetOrCreateLine(mid.AId, mid.BId);

                var k1 = facts.Lines[lineOM].TryGetSlope();
                var k2 = facts.Lines[lineAB].TryGetSlope();
                if (k1 is null || k2 is null) continue;

                var expected = (-conic.Matrix.M00 / conic.Matrix.M11).Simplified();
                if ((k1.Value * k2.Value - expected).VerifyStrictZero() != StrictResult.StrictlyTrue) continue;

                if (facts.AddFact(new ConstantSlopeProductFact(lineOM, lineAB, expected),
                    $"[{Title}] {mid.MidId} 为弦 {mid.AId}{mid.BId} 中点 => 斜率之积 k({lineOM}) * k({lineAB}) = {expected}"))
                    count++;
            }
        }
        return count;
    }
}