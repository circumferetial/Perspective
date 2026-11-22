using AngouriMath;
using MathNet.Numerics.LinearAlgebra;
using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Core.Discovery;

public record CircleCenterResult(HPoint Center, ExactScalar RadiusSquared, ProofStatus Status);
public record FixedSlopePointResult(HPoint Point, ExactScalar ConstantValue, string? ExcludedParamsNote);

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
                var excluded = new List<string>();
                if (dCand.SolveEquation(paramName) is Entity.Set.FiniteSet dZeros)
                {
                    foreach (var z in dZeros)
                        if (z.EvalNumerical() is Entity.Number.Complex cz && cz.ImaginaryPart.IsZero)
                            excluded.Add($"{paramName} = {z.Simplify()}");
                }

                var pt = new HPoint(
                    new ExactScalar((double)rx.EvalNumerical(), rx),
                    new ExactScalar((double)ry.EvalNumerical(), ry),
                    1);
                var val = new ExactScalar((double)lambda.EvalNumerical(), lambda);
                string? note = excluded.Count > 0
                    ? $"注意：当 {string.Join(", ", excluded)} 时直线过点 E，斜率无意义"
                    : null;

                results.Add(new FixedSlopePointResult(pt, val, note));
            }
        }

        return results;
    }
}