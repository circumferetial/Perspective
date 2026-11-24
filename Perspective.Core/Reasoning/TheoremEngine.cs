using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Core.Reasoning;

/// <summary>
/// 定理规则的 static abstract 契约：零堆内存分配，编译期绑定变换等级。
/// <para>
/// <see cref="ConclusionTypes"/> 与 <see cref="PremiseTypes"/> 是**目标导向推演**的元数据：
/// 引擎从待证结论的事实类型出发，反向闭包出需要激活的规则子集（类型级反向链），
/// 避免"所有规则对所有事实"的广度爆炸。规则本身仍只暴露 <see cref="Apply"/>，
/// 因此不需要为反向链另写一份推导逻辑。
/// </para>
/// </summary>
public interface IFactRule<TSelf> where TSelf : IFactRule<TSelf>
{
    static abstract string Title { get; }
    static abstract TransformLevel RequiredLevel { get; }

    /// <summary>本规则可能产出的事实类型（用于反向链的"能证明什么"）。</summary>
    static abstract Type[] ConclusionTypes { get; }

    /// <summary>本规则依赖的事实类型（用于反向链的"还需要什么"）。</summary>
    static abstract Type[] PremiseTypes { get; }

    static abstract int Apply(FactBase facts);
}

/// <summary>
/// 目标导向推演的结果。
/// </summary>
public readonly record struct ProofAttempt(
    bool Proved,
    string? Trace,
    int RulesActivated,
    int FactsBefore,
    int FactsAfter);

/// <summary>
/// 前向推演引擎：既能"全量跑到不动点"（数据驱动 BFS），也能"从结论反推规则子集"
/// （类型级反向链，目标导向）。两者共用同一批规则与同一套严通道纪律。
/// </summary>
public sealed class TheoremEngine
{
    private readonly List<(string Title, TransformLevel Level, Func<FactBase, int> Run,
        Type[] Conclusions, Type[] Premises)> _rules = [];

    public TheoremEngine Register<TRule>() where TRule : IFactRule<TRule>
    {
        _rules.Add((TRule.Title, TRule.RequiredLevel, TRule.Apply,
            TRule.ConclusionTypes, TRule.PremiseTypes));
        return this;
    }

    /// <summary>已注册规则数。</summary>
    public int RuleCount => _rules.Count;

    /// <summary>
    /// 数据驱动推演：所有规则反复执行到不动点（广度搜索，可能产生无关事实）。
    /// </summary>
    public void RunToFixedPoint(FactBase facts, int maxPasses = 10)
    {
        for (int pass = 1; pass <= maxPasses; pass++)
        {
            int addedInPass = 0;
            foreach (var (title, reqLevel, run, _, _) in _rules)
            {
                // 前提守卫 1：当前视角必须满足该定理的埃尔朗根不变性级别！
                if (facts.CurrentLevel > reqLevel)
                    continue;

                addedInPass += run(facts);
            }

            if (addedInPass == 0) break; // 没有新结论产生，推理完成！
        }
    }

    /// <summary>
    /// **目标导向推演**：从待证结论的事实类型出发，反向闭包出可能产出它的规则集合，
    /// 只激活这些规则（及其前提类型的产出规则），推到不动点后检查结论是否命中。
    /// <para>
    /// 【为什么这样是对的】反向闭包只可能**多**激活规则（类型粒度比实例粒度粗），
    /// 不会漏掉能产出目标类型的规则；而每条规则内部仍做严通道验证，
    /// 所以结论命中即可信。它是完备性的保守近似，不是启发式剪枝。
    /// </para>
    /// </summary>
    public ProofAttempt Prove(FactBase facts, Type targetFactType, Func<FactBase, string?> traceOf, int maxPasses = 10)
    {
        int before = facts.Facts.Count;

        // 类型级反向闭包：目标类型 -> 产出它的规则 -> 其前提类型 -> ...
        var neededTypes = new HashSet<Type>();
        var selected = new List<int>();
        var selectedSet = new HashSet<int>();
        var queue = new Queue<Type>();
        queue.Enqueue(targetFactType);
        neededTypes.Add(targetFactType);

        while (queue.Count > 0)
        {
            var need = queue.Dequeue();
            for (int i = 0; i < _rules.Count; i++)
            {
                if (selectedSet.Contains(i)) continue;
                if (!_rules[i].Conclusions.Contains(need)) continue;

                selectedSet.Add(i);
                selected.Add(i);
                foreach (var prem in _rules[i].Premises)
                    if (neededTypes.Add(prem))
                        queue.Enqueue(prem);
            }
        }

        // 只跑被选中的规则
        for (int pass = 1; pass <= maxPasses; pass++)
        {
            int addedInPass = 0;
            foreach (int i in selected)
            {
                var (_, reqLevel, run, _, _) = _rules[i];
                if (facts.CurrentLevel > reqLevel) continue;
                addedInPass += run(facts);
            }
            if (addedInPass == 0) break;
        }

        var trace = traceOf(facts);
        return new ProofAttempt(trace is not null, trace, selected.Count, before, facts.Facts.Count);
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
    public static Type[] ConclusionTypes => [typeof(PolarOfFact)];
    public static Type[] PremiseTypes => [typeof(TangentFact), typeof(OnLineFact)];

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
                    if (facts.AddDerivedFact(
                            new PolarOfFact(chordId, qId, t1.ConicId),
                            RuleIds.ChordOfContactIsPolar,
                            t1, t2,
                            new OnLineFact(qId, t1.LineId),
                            new OnLineFact(qId, t2.LineId)))
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
    public static Type[] ConclusionTypes => [typeof(OnLineFact)];
    public static Type[] PremiseTypes => [typeof(PolarOfFact), typeof(OnLineFact)];

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
                if (ConicTools.IsProperPolarLine(conic, facts.Points[pFact.PoleId], facts.Lines[pFact.LineId])
                    != StrictResult.StrictlyTrue) continue;
                if (ConicTools.IsProperPolarLine(conic, facts.Points[qFact.PoleId], facts.Lines[qFact.LineId])
                    != StrictResult.StrictlyTrue) continue;

                // 严通道验真：L_Q 真的严格经过 P
                var lineQ = facts.Lines[qFact.LineId];
                var ptP = facts.Points[pFact.PoleId];
                if (lineQ.ContainsStrictly(ptP) != StrictResult.StrictlyTrue) continue;

                if (facts.AddDerivedFact(
                        new OnLineFact(pFact.PoleId, qFact.LineId),
                        RuleIds.PolePolarReciprocity,
                        pFact, qFact,
                        new OnLineFact(qFact.PoleId, pFact.LineId)))
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
    public static Type[] ConclusionTypes => [typeof(HarmonicFact)];
    public static Type[] PremiseTypes => [typeof(PolarOfFact), typeof(OnLineFact), typeof(OnConicFact)];

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

                        if (facts.AddDerivedFact(
                                new HarmonicFact(polar.PoleId, rId, mId, nId),
                                RuleIds.HarmonicPolarBundle,
                                polar,
                                new OnLineFact(polar.PoleId, secId),
                                new OnLineFact(rId, secId),
                                new OnLineFact(rId, polar.LineId),
                                new OnConicFact(mId, polar.ConicId),
                                new OnConicFact(nId, polar.ConicId)))
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
    public static TransformLevel RequiredLevel => TransformLevel.Affine;
    public static Type[] ConclusionTypes => [typeof(ConjugateDirectionsFact), typeof(ConstantSlopeProductFact)];
    public static Type[] PremiseTypes => [typeof(CenterOfConicFact), typeof(MidpointFact), typeof(OnConicFact)]; // 射影视角不保中点与斜率积！

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

                if (facts.AddDerivedFact(
                        new ConjugateDirectionsFact(lineOM, lineAB, cen.ConicId),
                        RuleIds.ConicMidpointSlope,
                        cen, mid,
                        new OnConicFact(mid.AId, cen.ConicId),
                        new OnConicFact(mid.BId, cen.ConicId)))
                    count++;

                // 特例：无交叉项且斜率有限时，额外给出斜率积定值 −M₀₀/M₁₁
                if (m.M01.VerifyStrictZero() != StrictResult.StrictlyTrue) continue;

                var k1 = facts.Lines[lineOM].TryGetSlope();
                var k2 = facts.Lines[lineAB].TryGetSlope();
                if (k1 is null || k2 is null) continue;

                var expected = (-m.M00 / m.M11).Simplified();
                if ((k1.Value * k2.Value - expected).VerifyStrictZero() != StrictResult.StrictlyTrue) continue;

                if (facts.AddDerivedFact(
                        new ConstantSlopeProductFact(lineOM, lineAB, expected),
                        RuleIds.ConicMidpointSlope,
                        cen, mid,
                        new OnConicFact(mid.AId, cen.ConicId),
                        new OnConicFact(mid.BId, cen.ConicId)))
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
    public static Type[] ConclusionTypes => [typeof(TangentFact)];
    public static Type[] PremiseTypes => [typeof(OnConicFact), typeof(PolarOfFact)];

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
            if (ConicTools.IsProperPolarLine(conic, pole, facts.Lines[polar.LineId]) != StrictResult.StrictlyTrue) continue;

            if (facts.AddDerivedFact(
                    new TangentFact(polar.LineId, polar.ConicId, polar.PoleId),
                    RuleIds.PolarFundamental,
                    polar,
                    new OnConicFact(polar.PoleId, polar.ConicId)))
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
    public static Type[] ConclusionTypes => [typeof(MidpointFact)];
    public static Type[] PremiseTypes => [typeof(ConjugateDirectionsFact), typeof(OnLineFact), typeof(OnConicFact)];

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

            if (facts.AddDerivedFact(
                    new MidpointFact(midId, onChord[0], onChord[1]),
                    RuleIds.ConjugateDiameterBisectsChord,
                    conj,
                    new OnLineFact(onChord[0], conj.Line2Id),
                    new OnLineFact(onChord[1], conj.Line2Id),
                    new OnConicFact(onChord[0], conj.ConicId),
                    new OnConicFact(onChord[1], conj.ConicId)))
                count++;
        }

        return count;
    }
}

/// <summary>
/// 命题 9：Pascal 定理（内接六边形三对边交点共线）。
/// 【前提】① 同一非退化圆锥曲线上有 6 个互异点 A₁…A₆（OnConic 事实，且逐点
///         ContainsStrictly 严通道回验）；② 六边形按 A₁…A₆ 的遍历顺序取对边：
///         (A₁A₂, A₄A₅)、(A₂A₃, A₅A₆)、(A₃A₄, A₆A₁)。
/// 【结论】三个对边交点 P、Q、R 共线（Pascal 线）→ CollinearFact(P, Q, R)。
/// 【推导】射影几何标准定理：内接于非退化二次曲线的六边形，其三组对边交点共线。
///         对自交六边形同样成立（射影意义下）。退化情形（某两对边平行 ⇒ 交点在
///         无穷远）依然成立，因为齐次坐标下"共线"包含无穷远点。
///         本规则对结论做严通道回验（PointTools.Collinear），不通过就不写库。
/// 【等级】Projective——Pascal 是射影不变量。
/// 【实现说明】六边形交点会作为辅助点登记进事实库（幂等 id），供 CollinearFact 引用；
///         每条曲线最多取 8 个已登记点做组合（C(8,6)=28），避免组合爆炸。
/// </summary>
public readonly struct PascalRule : IFactRule<PascalRule>
{
    public static string Title => "Pascal 内接六边形定理";
    public static TransformLevel RequiredLevel => TransformLevel.Projective;
    public static Type[] ConclusionTypes => [typeof(CollinearFact)];
    public static Type[] PremiseTypes => [typeof(OnConicFact)];

    /// <summary>单条曲线上参与组合的点数上限（控制组合数与符号验证成本）。</summary>
    private const int MaxPool = 6;

    public static int Apply(FactBase facts)
    {
        int count = 0;

        foreach (var conicId in facts.ConicIdsWithPoints)
        {
            if (!facts.Conics.TryGetValue(conicId, out var conic)) continue;

            // 非退化要求：退化曲线不适用 Pascal 的常规形式
            if (conic.Matrix.Determinant().VerifyStrictZero() == StrictResult.StrictlyTrue) continue;

            // 严通道回验：只保留真正落在曲线上的点
            var pool = new List<string>();
            foreach (var pid in facts.PointsOnConic(conicId))
            {
                if (pool.Count >= MaxPool) break;
                if (!facts.Points.TryGetValue(pid, out var pt)) continue;
                if (conic.ContainsStrictly(pt) == StrictResult.StrictlyTrue) pool.Add(pid);
            }
            if (facts.PointsOnConic(conicId).Count() > pool.Count)
                facts.Diagnostics.Add(
                    $"[Pascal] 曲线上候选点 {facts.PointsOnConic(conicId).Count()} 个，仅取前 {pool.Count} 个；可能漏解");
            if (pool.Count < 6) continue;

            for (int i = 0; i < pool.Count - 5; i++)
            for (int j = i + 1; j < pool.Count - 4; j++)
            for (int k = j + 1; k < pool.Count - 3; k++)
            for (int l = k + 1; l < pool.Count - 2; l++)
            for (int m = l + 1; m < pool.Count - 1; m++)
            for (int n = m + 1; n < pool.Count; n++)
            {
                string a1 = pool[i], a2 = pool[j], a3 = pool[k];
                string a4 = pool[l], a5 = pool[m], a6 = pool[n];

                // 三组对边交点：P = A₁A₂ ∩ A₄A₅，Q = A₂A₃ ∩ A₅A₆，R = A₃A₄ ∩ A₆A₁
                var p = HLine.Intersect(
                    HLine.FromPoints(facts.Points[a1], facts.Points[a2]),
                    HLine.FromPoints(facts.Points[a4], facts.Points[a5]));
                var q = HLine.Intersect(
                    HLine.FromPoints(facts.Points[a2], facts.Points[a3]),
                    HLine.FromPoints(facts.Points[a5], facts.Points[a6]));
                var r = HLine.Intersect(
                    HLine.FromPoints(facts.Points[a3], facts.Points[a4]),
                    HLine.FromPoints(facts.Points[a6], facts.Points[a1]));

                // 退化守卫：交点不能是零向量；三点不得有重合
                if (IsZeroVector(p) || IsZeroVector(q) || IsZeroVector(r)) continue;
                if (PointTools.Same(p, q) || PointTools.Same(q, r) || PointTools.Same(p, r)) continue;

                // 结论严通道回验：三点确实共线
                if (PointTools.Collinear(p, q, r) != StrictResult.StrictlyTrue) continue;

                // 交点作为辅助点登记（幂等 id），供 CollinearFact 引用
                string pId = EnsurePoint(facts, $"AUX:PASCAL[{a1}{a2}|{a4}{a5}]", p);
                string qId = EnsurePoint(facts, $"AUX:PASCAL[{a2}{a3}|{a5}{a6}]", q);
                string rId = EnsurePoint(facts, $"AUX:PASCAL[{a3}{a4}|{a6}{a1}]", r);

                if (facts.AddDerivedFact(
                        CollinearFact.Create(pId, qId, rId),
                        RuleIds.Pascal,
                        new OnConicFact(a1, conicId), new OnConicFact(a2, conicId),
                        new OnConicFact(a3, conicId), new OnConicFact(a4, conicId),
                        new OnConicFact(a5, conicId), new OnConicFact(a6, conicId)))
                    count++;
            }
        }

        return count;
    }

    private static bool IsZeroVector(HPoint p) =>
        p.X.IsNumericallyZero() && p.Y.IsNumericallyZero() && p.W.IsNumericallyZero();

    /// <summary>确保辅助点已登记（同 id 已存在则不覆盖）。</summary>
    private static string EnsurePoint(FactBase facts, string id, HPoint p)
    {
        // 规则生成的派生点：不参与组合型规则的点枚举
        if (!facts.Points.ContainsKey(id)) facts.AddPoint(id, p, structural: false);
        return id;
    }
}

/// <summary>
/// 命题 10：完全四点形（4 点、6 边、3 对角点）的调和性。
/// 【前提】① 4 个已登记点 A、B、C、D 处于一般位置（任三点不共线，严通道回验）；
///         ② 三个对角点 P = AB∩CD、Q = AC∩BD、R = AD∩BC 均为非零向量且互不重合。
/// 【结论】每条边上：两个顶点、该边上的对角点、另一条对角线与该边的交点，构成调和点列。
///         例如边 AB：P = AB∩CD 是 AB 上的对角点，S = QR∩AB 是另一条对角线 QR 与 AB 的交点，
///         则 (A, B; P, S) = −1。六条边各得一条 HarmonicFact。
/// 【推导】完全四点形的经典调和性质（射影几何标准定理）。以 AB 边为例：
///         把 QR 与 AB 的交点记为 S，则交比 (A,B;P,S) 在射影变换下不变；
///         取标准四点形（如 A=(0,0),B=(1,0),C=(2,1),D=(0,2)）可直接算出
///         P=(4,0), S=(4/7,0)，(A,B;P,S) = −1，故恒等于 −1。
///         本规则对每条边都做 IsHarmonic 严通道回验，不通过不写库。
/// 【等级】Projective——调和比是射影不变量，本定理不依赖圆锥曲线。
/// 【成本】组合型规则 O(C(n,4))：设点数池与单次产出上限，避免场景里无关的点引发爆炸。
/// </summary>
public readonly struct CompleteQuadrangleHarmonicRule : IFactRule<CompleteQuadrangleHarmonicRule>
{
    public static string Title => "完全四点形的调和性";
    public static TransformLevel RequiredLevel => TransformLevel.Projective;
    public static Type[] ConclusionTypes => [typeof(HarmonicFact)];
    public static Type[] PremiseTypes => [];

    private const int MaxPool = 5;

    /// <summary>单次推演允许产出的上限（成本闸；非语义限制，见 docs/architecture.md）。</summary>
    private const int MaxFactsPerRun = 36;

    public static int Apply(FactBase facts)
    {
        int count = 0;

        // 候选池：只枚举**结构化点**（用户/题面声明的点），派生辅助点默认不参与；
        // 被截断/排除的数量会写入 Diagnostics，避免静默漏解。
        var allStructural = facts.StructuralPointIds.ToList();
        var pool = new List<string>();
        foreach (var pid in allStructural)
        {
            if (pool.Count >= MaxPool) break;
            pool.Add(pid);
        }
        if (allStructural.Count > pool.Count)
            facts.Diagnostics.Add(
                $"[完全四点形] 候选点 {allStructural.Count} 个，仅取前 {pool.Count} 个组合；" +
                "可能漏解（可用 PromoteToStructural 或提高 MaxPool）");
        if (pool.Count < 4) return 0;

        for (int i = 0; i < pool.Count - 3; i++)
        for (int j = i + 1; j < pool.Count - 2; j++)
        for (int k = j + 1; k < pool.Count - 1; k++)
        for (int l = k + 1; l < pool.Count; l++)
        {
            string aId = pool[i], bId = pool[j], cId = pool[k], dId = pool[l];
            var A = facts.Points[aId]; var B = facts.Points[bId];
            var C = facts.Points[cId]; var D = facts.Points[dId];

            // 廉价过滤：已知共线事实 / 数值行列式（只用于筛除明显退化的组合，结论仍由严通道决定）
            if (KnownCollinear(facts, aId, bId, cId) || KnownCollinear(facts, aId, bId, dId)
             || KnownCollinear(facts, aId, cId, dId) || KnownCollinear(facts, bId, cId, dId)) continue;
            if (CollinearApprox(A, B, C) || CollinearApprox(A, B, D)
             || CollinearApprox(A, C, D) || CollinearApprox(B, C, D)) continue;

            // 一般位置：任三点不共线（严通道复核）
            if (PointTools.Collinear(A, B, C) == StrictResult.StrictlyTrue) continue;
            if (PointTools.Collinear(A, B, D) == StrictResult.StrictlyTrue) continue;
            if (PointTools.Collinear(A, C, D) == StrictResult.StrictlyTrue) continue;
            if (PointTools.Collinear(B, C, D) == StrictResult.StrictlyTrue) continue;

            // 三个对角点
            var P = HLine.Intersect(HLine.FromPoints(A, B), HLine.FromPoints(C, D));
            var Q = HLine.Intersect(HLine.FromPoints(A, C), HLine.FromPoints(B, D));
            var R = HLine.Intersect(HLine.FromPoints(A, D), HLine.FromPoints(B, C));

            if (IsZeroVector(P) || IsZeroVector(Q) || IsZeroVector(R)) continue;
            if (PointTools.Same(P, Q) || PointTools.Same(Q, R) || PointTools.Same(P, R)) continue;

            string pId = EnsurePoint(facts, $"AUX:CQ[{aId}{bId}|{cId}{dId}]", P);
            string qId = EnsurePoint(facts, $"AUX:CQ[{aId}{cId}|{bId}{dId}]", Q);
            string rId = EnsurePoint(facts, $"AUX:CQ[{aId}{dId}|{bId}{cId}]", R);

            // 六条边：每条边上取"该边上的对角点" + "另一条对角线与该边的交点"
            var sides = new (string XId, string YId, HPoint X, HPoint Y, HPoint G, string GId, HLine OtherDiag, HLine Edge)[]
            {
                (aId, bId, A, B, P, pId, HLine.FromPoints(Q, R), HLine.FromPoints(A, B)),
                (cId, dId, C, D, P, pId, HLine.FromPoints(Q, R), HLine.FromPoints(C, D)),
                (aId, cId, A, C, Q, qId, HLine.FromPoints(P, R), HLine.FromPoints(A, C)),
                (bId, dId, B, D, Q, qId, HLine.FromPoints(P, R), HLine.FromPoints(B, D)),
                (aId, dId, A, D, R, rId, HLine.FromPoints(P, Q), HLine.FromPoints(A, D)),
                (bId, cId, B, C, R, rId, HLine.FromPoints(P, Q), HLine.FromPoints(B, C)),
            };

            foreach (var s in sides)
            {
                var S = HLine.Intersect(s.Edge, s.OtherDiag);
                if (IsZeroVector(S)) continue;
                if (PointTools.Same(S, s.X) || PointTools.Same(S, s.Y) || PointTools.Same(S, s.G)) continue;

                string sId = $"AUX:CQ[{aId}{bId}{cId}{dId}][{s.XId}{s.YId}]";

                // 该边已验证过：跳过昂贵的交比严证（后续 pass 不再重复付成本）
                if (facts.Facts.Contains(new HarmonicFact(s.XId, s.YId, s.GId, sId))) continue;

                // 数值预筛：交比明显不是 −1 的组合直接跳过（结论仍由严通道决定）
                if (!HarmonicApprox(s.X, s.Y, s.G, S)) continue;

                // 交比严证：坐标级判定，按实体 id 缓存（同一构形在后续 pass 中不再重复验）
                bool harmonic = facts.CachedCheck(
                    $"CQH|{s.XId}|{s.YId}|{s.GId}|{sId}",
                    () => GeometryAudit.IsHarmonic(s.X, s.Y, s.G, S) == StrictResult.StrictlyTrue);
                if (!harmonic) continue;

                EnsurePoint(facts, sId, S);
                if (facts.AddDerivedFact(
                        new HarmonicFact(s.XId, s.YId, s.GId, sId),
                        RuleIds.CompleteQuadrangleHarmonic))
                    count++;

                // 组合型规则的显式成本上限（非语义限制，见 docs/architecture.md）
                if (count >= MaxFactsPerRun)
                {
                    facts.Diagnostics.Add(
                        $"[完全四点形] 单次产出达到上限 {MaxFactsPerRun}，其余组合未检查；可能漏解");
                    return count;
                }
            }
        }

        return count;
    }

    private static bool IsZeroVector(HPoint p) =>
        p.X.IsNumericallyZero() && p.Y.IsNumericallyZero() && p.W.IsNumericallyZero();

    /// <summary>已知共线事实的 O(1) 查询（廉价过滤用）。</summary>
    private static bool KnownCollinear(FactBase facts, string a, string b, string c) =>
        facts.Facts.Contains(CollinearFact.Create(a, b, c));

    /// <summary>数值共线预筛：只判"明显共线"，用作廉价过滤，结论仍由严通道决定。</summary>
    private static bool CollinearApprox(HPoint p, HPoint q, HPoint r)
    {
        double det = p.X.Approx * (q.Y.Approx * r.W.Approx - q.W.Approx * r.Y.Approx)
                   - p.Y.Approx * (q.X.Approx * r.W.Approx - q.W.Approx * r.X.Approx)
                   + p.W.Approx * (q.X.Approx * r.Y.Approx - q.Y.Approx * r.X.Approx);
        double scale = System.Math.Max(1.0,
            System.Math.Max(System.Math.Abs(p.X.Approx) + System.Math.Abs(p.Y.Approx) + System.Math.Abs(p.W.Approx),
            System.Math.Max(System.Math.Abs(q.X.Approx) + System.Math.Abs(q.Y.Approx) + System.Math.Abs(q.W.Approx),
                            System.Math.Abs(r.X.Approx) + System.Math.Abs(r.Y.Approx) + System.Math.Abs(r.W.Approx))));
        return System.Math.Abs(det) <= 1e-9 * scale * scale * scale;
    }

    /// <summary>
    /// 数值交比预筛：沿 AB 参数化算交比 (A,B;C,D)，明显不等于 −1 时跳过该边。
    /// 只作过滤；真正的结论由 GeometryAudit.IsHarmonic 严通道给出。
    /// </summary>
    private static bool HarmonicApprox(HPoint a, HPoint b, HPoint c, HPoint d)
    {
        if (a.IsAtInfinity || b.IsAtInfinity || c.IsAtInfinity || d.IsAtInfinity) return true;

        var pa = a.ToCartesianApprox(); var pb = b.ToCartesianApprox();
        var pc = c.ToCartesianApprox(); var pd = d.ToCartesianApprox();
        if (pa is null || pb is null || pc is null || pd is null) return true;

        double ax = pa.Value.X, ay = pa.Value.Y;
        double bx = pb.Value.X, by = pb.Value.Y;
        double vx = bx - ax, vy = by - ay;
        double len2 = vx * vx + vy * vy;
        if (len2 < 1e-15) return true;

        double Param(double px, double py) => ((px - ax) * vx + (py - ay) * vy) / len2;
        double tc = Param(pc.Value.X, pc.Value.Y);
        double td = Param(pd.Value.X, pd.Value.Y);

        double denom = (tc - 1.0) * (td - 0.0);   // (tC − tB)(tD − tA)，tA=0, tB=1
        if (System.Math.Abs(denom) < 1e-12) return true;   // 退化交比交给严通道

        double cr = (tc - 0.0) * (td - 1.0) / denom;
        return System.Math.Abs(cr + 1.0) <= 1e-9;
    }

    private static string EnsurePoint(FactBase facts, string id, HPoint p)
    {
        // 规则生成的派生点：不参与组合型规则的点枚举
        if (!facts.Points.ContainsKey(id)) facts.AddPoint(id, p, structural: false);
        return id;
    }
}

/// <summary>
/// 命题 11：Brianchon 定理（外切六边形三主对角线共点）——Pascal 的射影对偶。
/// 【前提】① 同一非退化圆锥曲线上有 6 条互异切线（TangentFact，且逐条用
///         ConicTools.IsTangent 严通道回验）；② 按遍历顺序记相邻切线交点为顶点
///         V₁ = T₁∩T₂, …, V₆ = T₆∩T₁；③ 三条主对角线 V₁V₄、V₂V₅、V₃V₆ 互异。
/// 【结论】三条主对角线共点 → ConcurrentFact(d₁, d₂, d₃)。
/// 【推导】Brianchon 是 Pascal 的对偶命题：把"点在曲线上"对偶为"线切于曲线"、
///         "共线"对偶为"共点"。对自交外切六边形同样成立；顶点在无穷远
///         （相邻切线平行）时结论依然成立（齐次意义下）。
///         本规则对结论做严通道回验（两条对角线求交后 SameStrictly），不通过不写库。
/// 【等级】Projective。
/// 【实现说明】主对角线作为辅助直线登记（AUX: 前缀，id 含 6 条切线，保证不撞 id）；
///         每条曲线最多取 8 条切线做组合。
/// </summary>
public readonly struct BrianchonRule : IFactRule<BrianchonRule>
{
    public static string Title => "Brianchon 外切六边形定理";
    public static TransformLevel RequiredLevel => TransformLevel.Projective;
    public static Type[] ConclusionTypes => [typeof(ConcurrentFact)];
    public static Type[] PremiseTypes => [typeof(TangentFact)];

    private const int MaxPool = 6;

    public static int Apply(FactBase facts)
    {
        int count = 0;
        if (facts.Tangents.Count < 6) return 0;

        // 按曲线分组
        foreach (var group in facts.Tangents.GroupBy(t => t.ConicId))
        {
            if (!facts.Conics.TryGetValue(group.Key, out var conic)) continue;
            if (conic.Matrix.Determinant().VerifyStrictZero() == StrictResult.StrictlyTrue) continue;

            // 严通道回验 + 线互异
            var lines = new List<string>();
            var lineFacts = new List<TangentFact>();
            var seen = new List<HLine>();
            foreach (var t in group)
            {
                if (lines.Count >= MaxPool) break;
                if (!facts.Lines.TryGetValue(t.LineId, out var line)) continue;
                if (ConicTools.IsTangent(conic, line) != StrictResult.StrictlyTrue) continue;
                if (seen.Any(prev => LineTools.Same(prev, line) == StrictResult.StrictlyTrue)) continue;
                seen.Add(line);
                lines.Add(t.LineId);
                lineFacts.Add(t);
            }
            if (lines.Count < 6) continue;

            for (int i = 0; i < lines.Count - 5; i++)
            for (int j = i + 1; j < lines.Count - 4; j++)
            for (int k = j + 1; k < lines.Count - 3; k++)
            for (int l = k + 1; l < lines.Count - 2; l++)
            for (int m = l + 1; m < lines.Count - 1; m++)
            for (int n = m + 1; n < lines.Count; n++)
            {
                var t = new[] { lines[i], lines[j], lines[k], lines[l], lines[m], lines[n] };
                var h = t.Select(id => facts.Lines[id]).ToArray();

                // 相邻切线交点：V₁ = T₁∩T₂, …, V₆ = T₆∩T₁
                var v = new HPoint[6];
                bool bad = false;
                for (int s = 0; s < 6; s++)
                {
                    v[s] = HLine.Intersect(h[s], h[(s + 1) % 6]);
                    if (IsZeroVector(v[s])) { bad = true; break; }
                }
                if (bad) continue;

                // 三条主对角线 V₁V₄、V₂V₅、V₃V₆
                var d1 = HLine.FromPoints(v[0], v[3]);
                var d2 = HLine.FromPoints(v[1], v[4]);
                var d3 = HLine.FromPoints(v[2], v[5]);
                if (IsZeroLine(d1) || IsZeroLine(d2) || IsZeroLine(d3)) continue;
                if (LineTools.Same(d1, d2) == StrictResult.StrictlyTrue
                 || LineTools.Same(d1, d3) == StrictResult.StrictlyTrue
                 || LineTools.Same(d2, d3) == StrictResult.StrictlyTrue) continue;

                // 共点严通道回验：d₁∩d₂ 与 d₁∩d₃ 是同一射影点
                var x = HLine.Intersect(d1, d2);
                var y = HLine.Intersect(d1, d3);
                if (IsZeroVector(x) || IsZeroVector(y)) continue;
                if (PointTools.SameStrictly(x, y) != StrictResult.StrictlyTrue) continue;

                string tag = string.Join("", t);
                string d1Id = EnsureLine(facts, $"AUX:BRI[{tag}][1-4]", d1);
                string d2Id = EnsureLine(facts, $"AUX:BRI[{tag}][2-5]", d2);
                string d3Id = EnsureLine(facts, $"AUX:BRI[{tag}][3-6]", d3);

                if (facts.AddDerivedFact(
                        ConcurrentFact.Create(d1Id, d2Id, d3Id),
                        RuleIds.Brianchon,
                        lineFacts[i], lineFacts[j], lineFacts[k],
                        lineFacts[l], lineFacts[m], lineFacts[n]))
                    count++;
            }
        }

        return count;
    }

    private static bool IsZeroVector(HPoint p) =>
        p.X.IsNumericallyZero() && p.Y.IsNumericallyZero() && p.W.IsNumericallyZero();

    private static bool IsZeroLine(HLine l) =>
        l.A.IsNumericallyZero() && l.B.IsNumericallyZero() && l.C.IsNumericallyZero();

    private static string EnsureLine(FactBase facts, string id, HLine line)
    {
        if (!facts.Lines.ContainsKey(id)) facts.AddLine(id, line);
        return id;
    }
}

/// <summary>
/// 命题 7：同一条直线上的三点共线。
/// 【前提】① 存在直线 L，使 P1、P2、P3 都登记为 L 上的点（事实查询）；
///         ② 严通道回验三点坐标确实共线（PointTools.Collinear），不信任标签。
/// 【结论】CollinearFact(P1, P2, P3)（三元组规范排序后入库）。
/// 【推导】由共线定义，三点同在一条直线上即为共线；这里额外做坐标级严通道回验，
///         防止事实库被污染时推出错误结论。
/// 【等级】Projective——共线是射影不变量。
/// </summary>
public readonly struct CollinearFromCommonLineRule : IFactRule<CollinearFromCommonLineRule>
{
    public static string Title => "同线三点共线";
    public static TransformLevel RequiredLevel => TransformLevel.Projective;
    public static Type[] ConclusionTypes => [typeof(CollinearFact)];
    public static Type[] PremiseTypes => [typeof(OnLineFact)];

    public static int Apply(FactBase facts)
    {
        int count = 0;

        foreach (var lineId in facts.LineIdsWithPoints)
        {
            var pts = facts.PointsOn(lineId).ToList();
            if (pts.Count < 3) continue;

            for (int i = 0; i < pts.Count; i++)
            for (int j = i + 1; j < pts.Count; j++)
            for (int k = j + 1; k < pts.Count; k++)
            {
                if (!facts.Points.TryGetValue(pts[i], out var a) ||
                    !facts.Points.TryGetValue(pts[j], out var b) ||
                    !facts.Points.TryGetValue(pts[k], out var c)) continue;

                // 三点坐标必须两两互异（id 不同但坐标重合时，"三点共线"是退化命题，不能入库）
                if (PointTools.SameStrictly(a, b) == StrictResult.StrictlyTrue ||
                    PointTools.SameStrictly(a, c) == StrictResult.StrictlyTrue ||
                    PointTools.SameStrictly(b, c) == StrictResult.StrictlyTrue) continue;

                // 严通道回验：坐标确实共线
                if (PointTools.Collinear(a, b, c) != StrictResult.StrictlyTrue) continue;

                if (facts.AddDerivedFact(
                        CollinearFact.Create(pts[i], pts[j], pts[k]),
                        RuleIds.CollinearFromCommonLine,
                        new OnLineFact(pts[i], lineId),
                        new OnLineFact(pts[j], lineId),
                        new OnLineFact(pts[k], lineId)))
                    count++;
            }
        }

        return count;
    }
}

/// <summary>
/// 命题 8：过同一个点的三条直线共点。
/// 【前提】① 存在点 P，使 L1、L2、L3 都登记为过 P 的直线（事实查询）；
///         ② 三条线两两不重合（LineTools.Same 严通道排除退化的"三线"）；
///         ③ 严通道回验三条线都严格经过 P（ContainsStrictly）。
/// 【结论】ConcurrentFact(L1, L2, L3)（三元组规范排序后入库）。
/// 【推导】三线共点的定义即三条互异直线交于同一点；平行线情形在射影视角下
///         交于无穷远点，本规则同样成立。
/// 【等级】Projective——共点是射影不变量。
/// </summary>
public readonly struct ConcurrentFromCommonPointRule : IFactRule<ConcurrentFromCommonPointRule>
{
    public static string Title => "同点三线共点";
    public static TransformLevel RequiredLevel => TransformLevel.Projective;
    public static Type[] ConclusionTypes => [typeof(ConcurrentFact)];
    public static Type[] PremiseTypes => [typeof(OnLineFact)];

    public static int Apply(FactBase facts)
    {
        int count = 0;

        foreach (var pointId in facts.PointIdsWithLines)
        {
            var lines = facts.LinesThrough(pointId).ToList();
            if (lines.Count < 3) continue;
            if (!facts.Points.TryGetValue(pointId, out var p)) continue;

            for (int i = 0; i < lines.Count; i++)
            for (int j = i + 1; j < lines.Count; j++)
            for (int k = j + 1; k < lines.Count; k++)
            {
                if (!facts.Lines.TryGetValue(lines[i], out var l1) ||
                    !facts.Lines.TryGetValue(lines[j], out var l2) ||
                    !facts.Lines.TryGetValue(lines[k], out var l3)) continue;

                // 三条线必须两两互异（重合线不能算作三线共点）
                if (LineTools.Same(l1, l2) == StrictResult.StrictlyTrue
                 || LineTools.Same(l1, l3) == StrictResult.StrictlyTrue
                 || LineTools.Same(l2, l3) == StrictResult.StrictlyTrue) continue;

                // 严通道回验：三条线都严格过 P
                if (l1.ContainsStrictly(p) != StrictResult.StrictlyTrue) continue;
                if (l2.ContainsStrictly(p) != StrictResult.StrictlyTrue) continue;
                if (l3.ContainsStrictly(p) != StrictResult.StrictlyTrue) continue;

                if (facts.AddDerivedFact(
                        ConcurrentFact.Create(lines[i], lines[j], lines[k]),
                        RuleIds.ConcurrentFromCommonPoint,
                        new OnLineFact(pointId, lines[i]),
                        new OnLineFact(pointId, lines[j]),
                        new OnLineFact(pointId, lines[k])))
                    count++;
            }
        }

        return count;
    }

}

/// <summary>
/// 命题 12：平行传递（L₁∥L₂ 且 L₂∥L₃ ⇒ L₁∥L₃，三线互异）。
/// 【前提】两条 ParallelFact；严通道回验三线互不重合且确实平行（不信标签）。
/// 【结论】ParallelFact(L₁, L₃)。
/// 【等级】Affine——平行是仿射不变量。
/// </summary>
public readonly struct ParallelTransitivityRule : IFactRule<ParallelTransitivityRule>
{
    public static string Title => "平行传递";
    public static TransformLevel RequiredLevel => TransformLevel.Affine;
    public static Type[] ConclusionTypes => [typeof(ParallelFact)];
    public static Type[] PremiseTypes => [typeof(ParallelFact)];

    public static int Apply(FactBase facts)
    {
        int count = 0;
        foreach (var first in facts.Parallels.ToList())
        {
            foreach (var thirdId in facts.LinesParallelTo(first.Line2Id).ToList())
            {
                if (thirdId == first.Line1Id) continue;
                if (!facts.Lines.TryGetValue(first.Line1Id, out var a) ||
                    !facts.Lines.TryGetValue(thirdId, out var b)) continue;

                if (LineTools.Same(a, b) == StrictResult.StrictlyTrue) continue;
                if (GeometryAudit.AreParallel(a, b) != StrictResult.StrictlyTrue) continue;

                var second = ParallelFact.Create(first.Line2Id, thirdId);
                if (facts.AddDerivedFact(
                        ParallelFact.Create(first.Line1Id, thirdId),
                        RuleIds.ParallelTransitivity,
                        first, second))
                    count++;
            }
        }
        return count;
    }
}

/// <summary>
/// 命题 13：垂直与平行相容（L₁⊥L₂ 且 L₃∥L₂ ⇒ L₁⊥L₃；对称地 L₃∥L₁ ⇒ L₃⊥L₂）。
/// 【前提】PerpendicularFact + ParallelFact；严通道回验两线互异且确实垂直。
/// 【结论】PerpendicularFact。
/// 【等级】Similarity——垂直不是仿射不变量。
/// </summary>
public readonly struct PerpendicularParallelRule : IFactRule<PerpendicularParallelRule>
{
    public static string Title => "垂直与平行相容";
    public static TransformLevel RequiredLevel => TransformLevel.Similarity;
    public static Type[] ConclusionTypes => [typeof(PerpendicularFact)];
    public static Type[] PremiseTypes => [typeof(PerpendicularFact), typeof(ParallelFact)];

    public static int Apply(FactBase facts)
    {
        int count = 0;
        foreach (var perpendicular in facts.Perpendiculars.ToList())
        {
            count += Derive(facts, perpendicular, perpendicular.Line1Id, perpendicular.Line2Id);
            count += Derive(facts, perpendicular, perpendicular.Line2Id, perpendicular.Line1Id);
        }
        return count;
    }

    private static int Derive(
        FactBase facts, PerpendicularFact perpendicular, string keptLineId, string otherLineId)
    {
        int count = 0;
        foreach (var parallelTo in facts.LinesParallelTo(otherLineId).ToList())
        {
            if (parallelTo == keptLineId) continue;
            if (!facts.Lines.TryGetValue(keptLineId, out var kept) ||
                !facts.Lines.TryGetValue(parallelTo, out var moved)) continue;

            if (LineTools.Same(kept, moved) == StrictResult.StrictlyTrue) continue;
            if (GeometryAudit.ArePerpendicular(kept, moved) != StrictResult.StrictlyTrue) continue;

            var parallelFact = ParallelFact.Create(otherLineId, parallelTo);
            if (facts.AddDerivedFact(
                    PerpendicularFact.Create(keptLineId, parallelTo),
                    RuleIds.PerpendicularParallel,
                    perpendicular, parallelFact))
                count++;
        }
        return count;
    }
}

/// <summary>
/// 命题 14：平行线的对应角相等（L₁∥L₂ ⇒ θ(L₁, M) ≡ θ(L₂, M) mod π）。
/// 【前提】ParallelFact；严通道回验两线确实平行，M 与两线互异。
/// 【等级】Similarity——有向角是相似不变量。
/// </summary>
public readonly struct AngleFromParallelRule : IFactRule<AngleFromParallelRule>
{
    public static string Title => "平行线的对应角相等";
    public static TransformLevel RequiredLevel => TransformLevel.Similarity;
    public static Type[] ConclusionTypes => [typeof(AngleEqualFact)];
    public static Type[] PremiseTypes => [typeof(ParallelFact)];

    public static int Apply(FactBase facts)
    {
        int count = 0;
        foreach (var parallel in facts.Parallels.ToList())
        {
            if (!facts.Lines.TryGetValue(parallel.Line1Id, out var first) ||
                !facts.Lines.TryGetValue(parallel.Line2Id, out var second)) continue;

            foreach (var thirdId in facts.Lines.Keys.ToList())
            {
                if (thirdId == parallel.Line1Id || thirdId == parallel.Line2Id) continue;
                if (!facts.Lines.TryGetValue(thirdId, out var third)) continue;

                var candidate = AngleEqualFact.Create(parallel.Line1Id, thirdId, parallel.Line2Id, thirdId);
                if (candidate.A1 == candidate.B1 && candidate.A2 == candidate.B2) continue;
                if (GeometryAudit.AreAngleEqual(first, third, second, third, candidate.Sign) != StrictResult.StrictlyTrue)
                    continue;

                if (facts.AddDerivedFact(candidate, RuleIds.AngleFromParallel, parallel)) count++;
            }
        }
        return count;
    }
}

/// <summary>
/// 命题 15：角相等传递（θ(A)≡s₁θ(B) 且 θ(B)≡s₂θ(C) ⇒ θ(A)≡s₁s₂θ(C)）。
/// 【前提】两条 AngleEqualFact（共享同一线对）；严通道回验结论。
/// 【等级】Similarity。
/// </summary>
public readonly struct AngleTransitivityRule : IFactRule<AngleTransitivityRule>
{
    public static string Title => "角相等传递";
    public static TransformLevel RequiredLevel => TransformLevel.Similarity;
    public static Type[] ConclusionTypes => [typeof(AngleEqualFact)];
    public static Type[] PremiseTypes => [typeof(AngleEqualFact)];

    public static int Apply(FactBase facts)
    {
        int count = 0;
        foreach (var first in facts.AngleEquals.ToList())
        {
            foreach (var second in facts.AnglesInvolving(first.B1, first.B2).ToList())
            {
                string otherA1, otherA2;
                if (second.A1 == first.B1 && second.A2 == first.B2)
                {
                    otherA1 = second.B1; otherA2 = second.B2;
                }
                else if (second.B1 == first.B1 && second.B2 == first.B2)
                {
                    otherA1 = second.A1; otherA2 = second.A2;
                }
                else continue;

                var candidate = AngleEqualFact.CreateExplicit(
                    first.A1, first.A2, otherA1, otherA2, first.Sign * second.Sign);
                if (candidate.A1 == candidate.B1 && candidate.A2 == candidate.B2) continue;

                if (!facts.Lines.TryGetValue(candidate.A1, out var a1) ||
                    !facts.Lines.TryGetValue(candidate.A2, out var a2) ||
                    !facts.Lines.TryGetValue(candidate.B1, out var b1) ||
                    !facts.Lines.TryGetValue(candidate.B2, out var b2)) continue;

                if (GeometryAudit.AreAngleEqual(a1, a2, b1, b2, candidate.Sign) != StrictResult.StrictlyTrue)
                    continue;

                if (facts.AddDerivedFact(candidate, RuleIds.AngleTransitivity, first, second)) count++;
            }
        }
        return count;
    }
}
/// <summary>
/// 命题 16：面积比传递（S₁/S₂=r₁ 且 S₂/S₃=r₂ ⇒ S₁/S₃=r₁·r₂）。
/// 【前提】两条 AreaRatioFact（共享三角形）；严通道回验结论。
/// 【等级】Affine——面积比是仿射不变量。
/// </summary>
public readonly struct AreaRatioTransitivityRule : IFactRule<AreaRatioTransitivityRule>
{
    public static string Title => "面积比传递";
    public static TransformLevel RequiredLevel => TransformLevel.Affine;
    public static Type[] ConclusionTypes => [typeof(AreaRatioFact)];
    public static Type[] PremiseTypes => [typeof(AreaRatioFact)];

    public static int Apply(FactBase facts)
    {
        int count = 0;
        foreach (var first in facts.AreaRatios.ToList())
        {
            foreach (var second in facts.AreaRatiosWith(first.B1, first.B2, first.B3).ToList())
            {
                if (ReferenceEquals(second, first)) continue;

                if (second.A1 == first.B1 && second.A2 == first.B2 && second.A3 == first.B3)
                {
                    // S₁/S₂=r₁，S₂/S₃=r₂ ⇒ S₁/S₃ = r₁·r₂
                    count += TryAdd(facts, first, second, first.Value * second.Value,
                        first.A1, first.A2, first.A3, second.B1, second.B2, second.B3);
                }
                else if (second.B1 == first.B1 && second.B2 == first.B2 && second.B3 == first.B3)
                {
                    // S₃/S₂=r₂ ⇒ S₂/S₃ = 1/r₂
                    if (second.Value.VerifyStrictZero() == StrictResult.StrictlyTrue) continue;
                    count += TryAdd(facts, first, second, first.Value / second.Value,
                        first.A1, first.A2, first.A3, second.A1, second.A2, second.A3);
                }
            }
        }
        return count;
    }

    private static int TryAdd(
        FactBase facts, AreaRatioFact first, AreaRatioFact second, ExactScalar value,
        string a1, string a2, string a3, string b1, string b2, string b3)
    {
        if (!facts.Points.TryGetValue(a1, out var pa1) ||
            !facts.Points.TryGetValue(a2, out var pa2) ||
            !facts.Points.TryGetValue(a3, out var pa3) ||
            !facts.Points.TryGetValue(b1, out var pb1) ||
            !facts.Points.TryGetValue(b2, out var pb2) ||
            !facts.Points.TryGetValue(b3, out var pb3)) return 0;

        if (GeometryAudit.IsAreaRatio(pa1, pa2, pa3, pb1, pb2, pb3, value) != StrictResult.StrictlyTrue)
            return 0;

        var candidate = AreaRatioFact.Create(a1, a2, a3, b1, b2, b3, value);
        return facts.AddDerivedFact(candidate, RuleIds.AreaRatioTransitivity, first, second) ? 1 : 0;
    }
}

/// <summary>
/// 命题 17：线段比传递（|AB|/|CD|=r₁ 且 |CD|/|EF|=r₂ ⇒ |AB|/|EF|=r₁·r₂）。
/// 【前提】两条 SegmentRatioFact（共享线段）；严通道回验（平方形式，无根号）。
/// 【等级】Similarity——长度比是相似不变量。
/// </summary>
public readonly struct SegmentRatioTransitivityRule : IFactRule<SegmentRatioTransitivityRule>
{
    public static string Title => "线段比传递";
    public static TransformLevel RequiredLevel => TransformLevel.Similarity;
    public static Type[] ConclusionTypes => [typeof(SegmentRatioFact)];
    public static Type[] PremiseTypes => [typeof(SegmentRatioFact)];

    public static int Apply(FactBase facts)
    {
        int count = 0;
        foreach (var first in facts.SegmentRatios.ToList())
        {
            foreach (var second in facts.SegmentRatiosWith(first.B1, first.B2).ToList())
            {
                if (ReferenceEquals(second, first)) continue;

                if (second.A1 == first.B1 && second.A2 == first.B2)
                {
                    count += TryAdd(facts, first, second, first.Value * second.Value,
                        first.A1, first.A2, second.B1, second.B2);
                }
                else if (second.B1 == first.B1 && second.B2 == first.B2)
                {
                    if (second.Value.VerifyStrictZero() == StrictResult.StrictlyTrue) continue;
                    count += TryAdd(facts, first, second, first.Value / second.Value,
                        first.A1, first.A2, second.A1, second.A2);
                }
            }
        }
        return count;
    }

    private static int TryAdd(
        FactBase facts, SegmentRatioFact first, SegmentRatioFact second, ExactScalar value,
        string a1, string a2, string b1, string b2)
    {
        if (!facts.Points.TryGetValue(a1, out var pa1) ||
            !facts.Points.TryGetValue(a2, out var pa2) ||
            !facts.Points.TryGetValue(b1, out var pb1) ||
            !facts.Points.TryGetValue(b2, out var pb2)) return 0;

        if (GeometryAudit.IsSegmentRatio(pa1, pa2, pb1, pb2, value) != StrictResult.StrictlyTrue)
            return 0;

        var candidate = SegmentRatioFact.Create(a1, a2, b1, b2, value);
        return facts.AddDerivedFact(candidate, RuleIds.SegmentRatioTransitivity, first, second) ? 1 : 0;
    }
}