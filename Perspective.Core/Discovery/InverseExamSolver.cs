using AngouriMath;
using MathNet.Numerics.LinearAlgebra;
using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Core.Discovery;

public record CircleCenterResult(HPoint Center, ExactScalar RadiusSquared, ProofStatus Status);

/// <summary>
/// 定点 E 的求解结果。
/// <para>
/// <see cref="ExcludedParams"/> 是**显式的非退化条件（NDG）**：使斜率无定义、从而结论不成立的
/// 参数值集合（对照 I2GATP 的 ndg 字段）。<see cref="ExcludedParamsNote"/> 只是人类可读的
/// 渲染，不再承担语义；机器推理请用集合本身（可做并/交/差，见 <see cref="RealSets"/>）。
/// </para>
/// </summary>
public record FixedSlopePointResult(HPoint Point, ExactScalar ConstantValue, string? ExcludedParamsNote)
{
    /// <summary>非退化条件：被排除的参数值集合（空集表示无排除）。</summary>
    public Entity.Set ExcludedParams { get; init; } = Entity.Set.Empty;
}

public static class InverseExamSolver
{
    /// <summary>
    /// 【第 1 问能力】求动直线 l(k) 与圆锥曲线 C 有两个不同交点时，参数 k 的取值范围
    /// </summary>
    public static Entity.Set SolveSecantRange(
        Conic conic, Func<ExactScalar, HLine> lineBuilder, string paramName = "k")
    {
        var kSym = ExactScalar.FromSymbol(paramName, -1.0);
        var disc = conic.LineRelationDiscriminant(lineBuilder(kSym));
        if (disc.Exact is null)
            throw new InvalidOperationException(
                $"求解割线范围需要符号精确数据：当前 '{paramName}' 对应的线/曲线含纯近似输入，无法给出严格区间。");

        var discExpr = disc.Exact.Expand(4).Simplify(6);
        return MathS.LessThan(discExpr, 0).Solve(paramName);
    }

    /// <summary>
    /// 【第 2 问能力】给定动点 M(k)，自动探测隐圆并反推精确圆心 D，再用符号 k 严格证明 |DM| 为定值
    /// </summary>
    public static CircleCenterResult? FindFixedDistanceCenter(
        Func<ExactScalar, HPoint> pointBuilder, ExactScalar[] validProbes, string paramName = "k")
    {
        var samples = validProbes
            .Select(pointBuilder)
            .Where(pt => !pt.IsAtInfinity)
            .ToList();

        if (samples.Count < 6) return null;

        var mat = new double[samples.Count, 6];
        for (int i = 0; i < samples.Count; i++)
        {
            var (x, y) = samples[i].ToCartesianApprox()!.Value;
            mat[i, 0] = x * x; mat[i, 1] = x * y; mat[i, 2] = y * y;
            mat[i, 3] = x;     mat[i, 4] = y;     mat[i, 5] = 1.0;
        }
        var svd = Matrix<double>.Build.DenseOfArray(mat).Svd(true);
        if (svd.S.Min() / svd.S.Max() > 1e-7) return null;

        int minIdx = 0;
        for (int i = 1; i < svd.S.Count; i++)
            if (svd.S[i] < svd.S[minIdx]) minIdx = i;
        var v = svd.VT.Row(minIdx);
        v /= v.AbsoluteMaximum();

        if (System.Math.Abs(v[0]) < 1e-6 ||
            System.Math.Abs(v[0] - v[2]) > 1e-6 ||
            System.Math.Abs(v[1]) > 1e-6)
            return null;

        static HLine PerpBisector(HPoint p, HPoint q)
        {
            var x1 = p.X / p.W; var y1 = p.Y / p.W;
            var x2 = q.X / q.W; var y2 = q.Y / q.W;
            return new HLine(2 * (x2 - x1), 2 * (y2 - y1), x1 * x1 + y1 * y1 - (x2 * x2 + y2 * y2));
        }

        var dRaw = HLine.Intersect(PerpBisector(samples[0], samples[1]), PerpBisector(samples[1], samples[2]));
        var dx = (dRaw.X / dRaw.W).Simplified();
        var dy = (dRaw.Y / dRaw.W).Simplified();
        var centerD = HPoint.FromCartesian(dx, dy);

        var m0x = samples[0].X / samples[0].W;
        var m0y = samples[0].Y / samples[0].W;
        var r2 = ((m0x - dx) * (m0x - dx) + (m0y - dy) * (m0y - dy)).Simplified();

        // 无分母多项式严格证明：|DM|^2 - r^2 === 0
        var kSym = ExactScalar.FromSymbol(paramName, validProbes[0].Approx);
        var mSym = pointBuilder(kSym);
        var dxTerm = mSym.X - dx * mSym.W;
        var dyTerm = mSym.Y - dy * mSym.W;
        var diff = dxTerm * dxTerm + dyTerm * dyTerm - r2 * (mSym.W * mSym.W);

        var status = diff.VerifyStrictZero() == StrictResult.StrictlyTrue
            ? ProofStatus.SymbolicProved
            : ProofStatus.ExactSamplesVerified;

        return new CircleCenterResult(centerD, r2, status);
    }

    /// <summary>
    /// 【第 3 问能力】反求是否存在定点 E(u, v)，使得直线 AE, BE 的斜率和 k1 + k2 为定值
    /// </summary>
    public static List<FixedSlopePointResult> SolveFixedPointsForSlopeSum(
        Conic conic, Func<ExactScalar, HLine> lineBuilder, string paramName = "k")
    {
        const string uName = "u";
        const string vName = "v";

        var uSym = ExactScalar.FromSymbol(uName, 0);
        var vSym = ExactScalar.FromSymbol(vName, 0);
        var kSym = ExactScalar.FromSymbol(paramName, -1);

        var pair = conic.LinePairFrom(HPoint.FromCartesian(uSym, vSym), lineBuilder(kSym));
        var nExpr = (-2 * pair.Matrix.M01).Exact!.Expand(4).Simplify(6);
        var dExpr = pair.Matrix.M11.Exact!.Expand(4).Simplify(6);

        static (Entity C2, Entity C1, Entity C0) ExtractQuad(Entity expr, string kName)
        {
            var c0 = expr.Substitute(kName, 0).Expand(4).Simplify(6);
            var v1 = expr.Substitute(kName, 1);
            var vNeg1 = expr.Substitute(kName, -1);
            var c1 = ((v1 - vNeg1) / 2).Expand(4).Simplify(6);
            var c2 = ((v1 + vNeg1) / 2 - c0).Expand(4).Simplify(6);
            return (c2, c1, c0);
        }

        var (n2, n1, n0) = ExtractQuad(nExpr, paramName);
        var (d2, d1, d0) = ExtractQuad(dExpr, paramName);

        var eq1 = (n2 * d1 - n1 * d2).Expand(4).Simplify(6);
        var eq2 = (n1 * d0 - n0 * d1).Expand(4).Simplify(6);
        var eq3 = (n2 * d0 - n0 * d2).Expand(4).Simplify(6);

        // 用真正的多项式结式消去 v：Res_v(eq_i, eq_j) 是 u 的多项式，其根给出公共解的 u 坐标。
        // 注意：结式可能携带“两个多项式首项系数同时为 0”的增根，必须回代 eq1/eq2/eq3 与恒等式严格验证。
        // （此前用“假设 eq 对 v 线性 + 2x2 行列式”近似结式，只在特定曲线/直线下恰好成立，一般二次曲线下不健全。）
        // 某一组配对可能解出非有限集（例如方程在退化配置下恒为 0），此时回退尝试下一组，全部失败才返回空。
        var vVar = (Entity.Variable)vSym.Exact!;
        Entity.Set.FiniteSet? xRoots = null;
        foreach (var (p, q) in new[] { (eq1, eq2), (eq2, eq3), (eq1, eq3) })
        {
            var res = AngouriMath.MathS.Polynomials.Resultant(p, q, vVar);
            if (res is null || res.Equals(Entity.Number.Integer.Zero)) continue;

            if (res.SolveEquation(uName) is Entity.Set.FiniteSet fr && fr.Count > 0)
            {
                xRoots = fr;
                break;
            }
        }

        var results = new List<FixedSlopePointResult>();
        if (xRoots is null)
            return results;

        foreach (var rx in xRoots)
        {
            if (rx.EvalNumerical() is Entity.Number.Complex cx && !cx.ImaginaryPart.IsZero)
                continue;

            // 枚举 v 候选：依次用 eq1/eq2/eq3 在给定 u 处求解。
            // 某个方程在特定 u 处可能恒为 0（解集为全集而非有限集），此时换下一个方程。
            Entity.Set.FiniteSet? yRoots = null;
            foreach (var eq in new[] { eq1, eq2, eq3 })
            {
                var eqAtX = eq.Substitute(uName, rx).Expand(4).Simplify(6);
                if (eqAtX.SolveEquation(vVar) is Entity.Set.FiniteSet fr)
                {
                    yRoots = fr;
                    break;
                }
            }
            if (yRoots is null) continue;

            foreach (var ry in yRoots)
            {
                if (ry.EvalNumerical() is Entity.Number.Complex cy && !cy.ImaginaryPart.IsZero)
                    continue;

                var nCand = nExpr.Substitute(uName, rx).Substitute(vName, ry).Expand(4).Simplify(6);
                var dCand = dExpr.Substitute(uName, rx).Substitute(vName, ry).Expand(4).Simplify(6);
                if (dCand.Equals(Entity.Number.Integer.Zero)) continue;

                // 结式可能携带增根：先验证 eq2、eq3 也严格为 0
                var eq2Check = eq2.Substitute(uName, rx).Substitute(vName, ry).Expand(4).Simplify(6);
                var eq3Check = eq3.Substitute(uName, rx).Substitute(vName, ry).Expand(4).Simplify(6);
                if (!eq2Check.Equals(Entity.Number.Integer.Zero) || !eq3Check.Equals(Entity.Number.Integer.Zero))
                    continue;

                Entity? lambda = null;
                foreach (int probeK in new[] { -1, -3, -5, 1, 3 })
                {
                    var dProbe = dCand.Substitute(paramName, probeK).Simplify();
                    if (!dProbe.Equals(Entity.Number.Integer.Zero))
                    {
                        lambda = (nCand.Substitute(paramName, probeK) / dProbe).Simplify();
                        break;
                    }
                }
                if (lambda is null) continue;

                var identityCheck = (nCand - lambda * dCand).Expand(4).Simplify(6);
                if (!identityCheck.Equals(Entity.Number.Integer.Zero)) continue;

                // 排除口径（严谨）：dCand(k)=0 等价于点差二次方程的 y^2 系数为 0，
                // 即 AE 或 BE 中必有竖直方向，斜率（从而斜率和）在该 k 无定义。
                // 这与判别式无关，因此记录分母的全部实零点，不再按数值阈值过滤。
                // 结果以集合形式保存（NDG 字段），人类可读文本只是渲染。
                var excludedRoots = new List<Entity>();
                if (dCand.SolveEquation(paramName) is Entity.Set.FiniteSet dZeros)
                {
                    foreach (var z in dZeros)
                        if (z.EvalNumerical() is Entity.Number.Complex cz && cz.ImaginaryPart.IsZero)
                            excludedRoots.Add(z.Simplify());
                }

                var excludedSet = excludedRoots.Count == 0
                    ? Entity.Set.Empty
                    : MathS.Sets.Finite(excludedRoots.ToArray());

                var pt = new HPoint(
                    new ExactScalar((double)rx.EvalNumerical(), rx),
                    new ExactScalar((double)ry.EvalNumerical(), ry),
                    1);
                var val = new ExactScalar((double)lambda.EvalNumerical(), lambda);
                string? note = excludedRoots.Count > 0
                    ? $"非退化条件（NDG）：{paramName} ∉ {RealSets.Describe(excludedSet)}（这些值使斜率无定义）"
                    : null;

                results.Add(new FixedSlopePointResult(pt, val, note) { ExcludedParams = excludedSet });
            }
        }

        return results;
    }

    /// <summary>
    /// 【弦族斜率条件】已知点 A 在圆锥曲线 C 上，求弦 l（不经过 A）满足
    /// "AP、AQ 的斜率之和为零" 时 l 的斜率。
    /// <para>
    /// 【推导】A 在曲线上时双线组矩阵每项都含因子 s = l(A)（退化分支：l 过 A），
    /// 故斜率和条件等价于 M01/s = 0。若 M01/s 与截距 c 无关，则斜率被唯一确定。
    /// </para>
    /// </summary>
    /// <returns>满足条件的斜率集合（空集表示条件不能唯一确定斜率，或输入退化）。</returns>
    public static Entity.Set SolveChordSlopeForZeroSlopeSum(
        Conic conic, HPoint pointOnConic, string slopeName = "m", string interceptName = "c")
    {
        if (conic.ContainsStrictly(pointOnConic) != StrictResult.StrictlyTrue)
            return Entity.Set.Empty;

        var m = ExactScalar.FromSymbol(slopeName);
        var c = ExactScalar.FromSymbol(interceptName);
        var line = new HLine(m, -1, c);
        var pair = conic.LinePairFrom(pointOnConic, line);

        // A 在曲线上 => M01 = s · g，其中 s = l(A)（l 过 A 的退化分支）
        var s = line.A * pointOnConic.X + line.B * pointOnConic.Y + line.C * pointOnConic.W;
        var g = (pair.Matrix.M01 / s).Simplified();
        if (g.Exact is null) return Entity.Set.Empty;

        // 验证 g 与截距 c 无关（分别取 c=0,1 后应严格相等）
        var g0 = g.Exact.Substitute(interceptName, 0).Simplify();
        var g1 = g.Exact.Substitute(interceptName, 1).Simplify();
        var diffExact = (g0 - g1).Simplify();
        // 注意：Providedf（"…provided …"）不能直接 EvalNumerical，用 Approx=0 走符号判定即可
        var diffScalar = new ExactScalar(0, diffExact);
        if (diffScalar.VerifyStrictZero() != StrictResult.StrictlyTrue)
        {
            // 与 c 有关：单条"斜率和为零"不能确定斜率（存在参数族）
            return Entity.Set.Empty;
        }

        return MathS.Equality(g0, 0).Solve(slopeName);
    }

    /// <summary>【度量反解】已知弦斜率与 ∠PAQ 的 tan，求弦截距与三角形面积。</summary>
    /// <param name="conic">圆锥曲线</param>
    /// <param name="vertex">三角形顶点 A（不要求在曲线上）</param>
    /// <param name="chordSlope">已知弦斜率</param>
    /// <param name="tanTarget">目标 tan∠PAQ（正数）</param>
    public static IReadOnlyList<TriangleAreaResult> SolveAreaFromTangent(
        Conic conic, HPoint vertex, ExactScalar chordSlope, ExactScalar tanTarget,
        string interceptName = "c")
    {
        var results = new List<TriangleAreaResult>();
        var c = ExactScalar.FromSymbol(interceptName);

        // 弦：y = chordSlope · x + c  =>  chordSlope·x − y + c = 0
        var symbolicLine = new HLine(chordSlope, -1, c);
        var pts = conic.Intersect(symbolicLine);
        if (pts.Length < 2) return results;

        var (px, py) = Metric.Vector(vertex, pts[0]);
        var (qx, qy) = Metric.Vector(vertex, pts[1]);
        var cross = px * qy - py * qx;
        var dot = px * qx + py * qy;

        // 平方去根号：tan² = cross²/dot²  =>  cross² − tan²·dot² = 0
        var equation = (cross * cross - tanTarget * tanTarget * dot * dot).Simplified();
        if (equation.Exact is null) return results;

        var statement = MathS.Equality(equation.Exact.Expand().Simplify(), 0);
        if (statement.Solve(interceptName) is not Entity.Set.FiniteSet roots) return results;

        foreach (var root in roots)
        {
            if (root.EvalNumerical() is Entity.Number.Complex complex && !complex.ImaginaryPart.IsZero)
                continue;

            var cVal = new ExactScalar((double)root.EvalNumerical(), root.Simplify());
            var concrete = conic.Intersect(new HLine(chordSlope, -1, cVal));
            if (concrete.Length < 2) continue;

            var tanActual = Metric.TanAngleUnsigned(
                Metric.Vector(vertex, concrete[0]),
                Metric.Vector(vertex, concrete[1]));

            // 回验 tan（平方会引入符号歧义与退化增根，必须逐步验真）
            if (tanActual is null) continue;
            if ((tanActual.Value - tanTarget).VerifyStrictZero() != StrictResult.StrictlyTrue) continue;

            results.Add(new TriangleAreaResult(
                Intercept: cVal,
                Area: Metric.TriangleArea(vertex, concrete[0], concrete[1]),
                Tangent: tanActual.Value));
        }

        return results;
    }
}

/// <summary>【度量反解】弦截距与三角形面积结果。</summary>
public sealed record TriangleAreaResult(ExactScalar Intercept, ExactScalar Area, ExactScalar Tangent);