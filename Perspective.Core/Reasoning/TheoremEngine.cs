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
        var tangents = facts.Tangents;

        for (int i = 0; i < tangents.Count; i++)
        {
            for (int j = i + 1; j < tangents.Count; j++)
            {
                var t1 = tangents[i];
                var t2 = tangents[j];
                if (t1.ConicId != t2.ConicId || t1.TouchPointId == t2.TouchPointId) continue;

                // 找两条切线的公共点 Q = (T1 上的点) ∩ (T2 上的点)，排除切点
                var qIds = facts.PointsOn(t1.LineId).Intersect(facts.PointsOn(t2.LineId))
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
/// 【前提】① 同一曲线的两条极线声明 L_P = C·P、L_Q = C·Q（ConicTools.IsPolarLine
///         严通道回验，不信任 PolarOfFact 标签）；② Q ∈ L_P（信任 OnLineFact）；
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

    public static int Apply(FactBase facts)
    {
        int count = 0;
        var polars = facts.Polars;

        foreach (var pFact in polars)
        {
            foreach (var qFact in polars)
            {
                if (pFact.ConicId != qFact.ConicId || pFact.PoleId == qFact.PoleId) continue;

                // 前提守卫按"代价递增"排序：先做 O(1) 的事实查询，再做昂贵的严通道验证。
                // 绝大多数配对根本不含前提②，提前把它们筛掉，避免为每个配对都构造成本高昂的
                // 极线表达式（实测 K=100 时该顺序差异约 1000 倍耗时 / 400 倍分配）。
                // 前提：Q 在 P 的极线 L_P 上
                if (!facts.HasOnLine(qFact.PoleId, pFact.LineId)) continue;

                // 前提严通道回验：确认两条线确实分别是两极点的极线（不信任 PolarOfFact 标签）。
                // 判据收敛在 ConicTools.IsPolarLine（与裁决器共用，避免两处实现漂移）。
                var conic = facts.Conics[pFact.ConicId];
                if (ConicTools.IsPolarLine(conic, facts.Points[pFact.PoleId], facts.Lines[pFact.LineId])
                    != StrictResult.StrictlyTrue) continue;
                if (ConicTools.IsPolarLine(conic, facts.Points[qFact.PoleId], facts.Lines[qFact.LineId])
                    != StrictResult.StrictlyTrue) continue;

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
        var polars = facts.Polars;

        foreach (var polar in polars)
        {
            // 找所有过极点 Q 的直线（排除极线自己）
            var secantIds = facts.LinesThrough(polar.PoleId).Where(lid => lid != polar.LineId).ToList();

            foreach (var secId in secantIds)
            {
                // 找交极线于 R
                var rIds = facts.PointsOn(secId).Intersect(facts.PointsOn(polar.LineId)).ToList();

                // 找割线与曲线的两个交点 M, N
                var mnIds = facts.PointsOn(secId).Intersect(facts.PointsOnConic(polar.ConicId)).ToList();

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
/// 【前提】① 有心非退化（δ = M₀₀M₁₁ − M₀₁² ≠ 0 且 Δ = det C ≠ 0；抛物线无有限中心、
///         退化曲线不适用，由 ConicTools.Classify 判定）；② M 严格为 AB 中点
///         （IsMidpoint 回验）；③ A、B 严格在曲线上（ContainsStrictly 回验）；
///         ④ O 为曲线中心（CenterOfConicFact）。
/// 【结论】**共轭方向**：d_ABᵀ · C₂ · (M − O) = 0，其中 C₂ 为二次部分 [[M₀₀,M₀₁],[M₀₁,M₁₁]]。
///         当曲线无交叉项（M₀₁ = 0）且斜率有限时，进一步导出特例
///         k_OM · k_AB = −M₀₀/M₁₁。
/// 【推导】把原点平移到中心 O：f(X) = XᵀC₂X + c′。弦上两点 M ± t·d 都在曲线上，
///         两式相减得 4t·dᵀC₂(M−O) = 0 ⇒ dᵀC₂(M−O) = 0——这是坐标无关的共轭条件
///         （双曲/椭圆/圆统一成立，与曲线是否轴对齐无关）。
///         轴对齐时取 d = (1, k_AB)、M−O ∝ (1, k_OM)，代入得
///         M₀₀ + M₁₁·k_AB·k_OM = 0，即 k_OM·k_AB = −M₀₀/M₁₁。
/// 【为什么不是直接旋转后套斜率公式】斜率是欧氏量，坐标旋转后 k 会变，
///         原坐标系里的 k_OM·k_AB 就不再是 −M₀₀/M₁₁；能保持形式的是共轭关系本身。
/// 【等级】Affine——中点与共轭方向在射影变换下不保持。
/// </summary>
public readonly struct ConicMidpointSlopeRule : IFactRule<ConicMidpointSlopeRule>
{
    public static string Title => "点差法中点斜率积定理";
    public static TransformLevel RequiredLevel => TransformLevel.Affine; // 射影视角不保中点与斜率积！

    public static int Apply(FactBase facts)
    {
        int count = 0;
        var centers = facts.Centers;
        var midpoints = facts.Midpoints;

        foreach (var cen in centers)
        {
            var conic = facts.Conics[cen.ConicId];

            // 前提：有心非退化曲线（椭圆/圆/双曲）。抛物线无有限中心，退化曲线不适用。
            var kind = ConicTools.Classify(conic).Kind;
            if (kind is not (ConicKind.Ellipse or ConicKind.Circle or ConicKind.Hyperbola)) continue;

            var center = facts.Points[cen.CenterId];
            var m = conic.Matrix;

            foreach (var mid in midpoints)
            {
                if (!facts.HasOnConic(mid.AId, cen.ConicId) ||
                    !facts.HasOnConic(mid.BId, cen.ConicId)) continue;

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

                // 弦方向（齐次无穷远点）：直线 A·x + B·y + C = 0 的方向为 (−B, A)
                var chordDir = new HPoint(-facts.Lines[lineAB].B, facts.Lines[lineAB].A, 0);

                // M − O 的齐次表示（两点均为有限点）
                var mx = ptM.X * center.W - center.X * ptM.W;
                var my = ptM.Y * center.W - center.Y * ptM.W;
                var mw = ptM.W * center.W;

                // 共轭判据 dᵀ·C·(M−O) = 0；d 的 W 分量为 0，故自动只命中二次部分
                var conj = chordDir.X * (m.M00 * mx + m.M01 * my + m.M02 * mw)
                         + chordDir.Y * (m.M10 * mx + m.M11 * my + m.M12 * mw)
                         + chordDir.W * (m.M20 * mx + m.M21 * my + m.M22 * mw);

                if (conj.VerifyStrictZero() != StrictResult.StrictlyTrue) continue;

                if (facts.AddFact(new ConjugateDirectionsFact(lineOM, lineAB, cen.ConicId),
                    $"[{Title}] {mid.MidId} 为弦 {mid.AId}{mid.BId} 中点 => 弦方向与 O{mid.MidId} 关于二次部分共轭"))
                    count++;

                // 特例：无交叉项且斜率有限时，额外给出斜率积定值 −M₀₀/M₁₁
                if (m.M01.VerifyStrictZero() != StrictResult.StrictlyTrue) continue;

                var k1 = facts.Lines[lineOM].TryGetSlope();
                var k2 = facts.Lines[lineAB].TryGetSlope();
                if (k1 is null || k2 is null) continue;

                var expected = (-m.M00 / m.M11).Simplified();
                if ((k1.Value * k2.Value - expected).VerifyStrictZero() != StrictResult.StrictlyTrue) continue;

                if (facts.AddFact(new ConstantSlopeProductFact(lineOM, lineAB, expected),
                    $"[{Title}] {mid.MidId} 为弦 {mid.AId}{mid.BId} 中点 => 斜率之积 k({lineOM}) * k({lineAB}) = {expected}"))
                    count++;
            }
        }
        return count;
    }
}

/// <summary>
/// 命题 5：极线基本定理——曲线上的点的极线就是该点处的切线。
/// 【前提】① P 在曲线 C 上（严格回验 ContainsStrictly）；② L 是 P 的极线
///         （ConicTools.IsPolarLine 严通道回验，不信任 PolarOfFact 标签）。
/// 【结论】TangentFact(L, C, P)：L 切 C 于 P。
/// 【推导】P 在曲线上 ⟹ PᵀCP = 0；极线 l = C·P 过 P（PᵀCP = 0 即 l·P = 0），且
///         与曲线只有 P 一个交点（代入 l 的判别式为 0），故 l 为切线。
///         本规则的作用是**让 TangentFact 第一次可以被自动生成**，闭合
///         OnConic → Polar → Tangent 循环，从而为切点弦极线定理提供前提。
/// 【等级】Projective。
/// </summary>
public readonly struct PolarFundamentalRule : IFactRule<PolarFundamentalRule>
{
    public static string Title => "极线基本定理";
    public static TransformLevel RequiredLevel => TransformLevel.Projective;

    public static int Apply(FactBase facts)
    {
        int count = 0;

        foreach (var polar in facts.Polars)
        {
            // 廉价前提先行：非曲线上的点，极线不一定是切线
            if (!facts.HasOnConic(polar.PoleId, polar.ConicId)) continue;

            var conic = facts.Conics[polar.ConicId];
            var pole = facts.Points[polar.PoleId];

            if (conic.ContainsStrictly(pole) != StrictResult.StrictlyTrue) continue;
            if (ConicTools.IsPolarLine(conic, pole, facts.Lines[polar.LineId]) != StrictResult.StrictlyTrue) continue;

            if (facts.AddFact(new TangentFact(polar.LineId, polar.ConicId, polar.PoleId),
                $"[{Title}] {polar.PoleId} 在曲线 {polar.ConicId} 上且 {polar.LineId} 是其极线 => {polar.LineId} 切曲线于 {polar.PoleId}"))
                count++;
        }

        return count;
    }
}

/// <summary>
/// 命题 6：共轭直径平分弦——点差法的逆命题。
/// 【前提】① 曲线有心非退化（Ellipse/Circle/Hyperbola）；② 已知共轭方向事实
///         ConjugateDirectionsFact(OM, AB, C)；③ 弦 AB 与曲线恰有 2 个已登记交点；
///         ④ 交点 M = OM ∩ AB 是已登记的有限点；⑤ 严通道回验 A、B 在曲线上且
///         M 严格是 AB 中点（防止事实库污染）。
/// 【结论】MidpointFact(M, A, B)。
/// 【推导】设中心 O，弦方向 d，弦上一点 M。若 dᵀC₂(M−O) = 0，则沿弦取 M ± t·d，
///         两点的二次型值相等（因为线性项 2t·dᵀC₂(M−O) = 0），故两点同时落在曲线上
///         ⟹ M 就是弦 AB 的中点。这正是点差法结论的逆命题。
/// 【等级】Affine。
/// </summary>
public readonly struct ConjugateDiameterBisectsChordRule : IFactRule<ConjugateDiameterBisectsChordRule>
{
    public static string Title => "共轭直径平分弦定理";
    public static TransformLevel RequiredLevel => TransformLevel.Affine;

    public static int Apply(FactBase facts)
    {
        int count = 0;

        foreach (var conj in facts.ConjugateDirections)
        {
            if (!facts.Conics.TryGetValue(conj.ConicId, out var conic)) continue;

            var kind = ConicTools.Classify(conic).Kind;
            if (kind is not (ConicKind.Ellipse or ConicKind.Circle or ConicKind.Hyperbola)) continue;

            // 弦上的曲线交点应恰为两个
            var onChord = facts.PointsOn(conj.Line2Id)
                .Where(pid => facts.HasOnConic(pid, conj.ConicId))
                .ToList();
            if (onChord.Count != 2) continue;

            // M = OM ∩ AB；必须是已登记的有限点
            var inter = HLine.Intersect(facts.Lines[conj.Line1Id], facts.Lines[conj.Line2Id]);
            if (inter.IsAtInfinity) continue;

            string? midId = null;
            foreach (var (pid, pt) in facts.Points)
                if (PointTools.SameStrictly(pt, inter) == StrictResult.StrictlyTrue)
                {
                    midId = pid;
                    break;
                }
            if (midId is null) continue;

            var ptM = facts.Points[midId];
            var ptA = facts.Points[onChord[0]];
            var ptB = facts.Points[onChord[1]];

            if (GeometryAudit.IsMidpoint(ptM, ptA, ptB) != StrictResult.StrictlyTrue) continue;
            if (conic.ContainsStrictly(ptA) != StrictResult.StrictlyTrue) continue;
            if (conic.ContainsStrictly(ptB) != StrictResult.StrictlyTrue) continue;

            if (facts.AddFact(new MidpointFact(midId, onChord[0], onChord[1]),
                $"[{Title}] 弦 {conj.Line2Id} 与直径 {conj.Line1Id} 共轭 => 交点 {midId} 是弦 {onChord[0]}{onChord[1]} 的中点"))
                count++;
        }

        return count;
    }
}