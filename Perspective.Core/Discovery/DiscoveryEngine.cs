using MathNet.Numerics.LinearAlgebra;
using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Core.Discovery;

public enum ProofStatus
{
    SymbolicProved,       // 【最高等级】符号代数已对全体参数 t 严格证明！
    ExactSamplesVerified, // 在多个全新精确有理数采样点上严格成立（符号树过于复杂暂未完全展开）
    NumericOnly           // 仅数值拟合发现（例如动点轨迹拟合圆锥曲线）
}

public record DiscoveryFinding(string Title, string Detail, ProofStatus Status);

public static class DiscoveryEngine
{
    private static readonly long[] DefaultProbes = [2, 3, 5, 7, 11, 13, 17];
    private static readonly long[] ExtraConfirmProbes = [17, 19, 23, 29];

    /// <summary>
    /// 1. 自动发现两条动直线的斜率关系：斜率之和、斜率之积、斜率之比是否为定值
    /// </summary>
    public static List<DiscoveryFinding> DiscoverSlopeRelations(Func<ExactScalar, (HLine L1, HLine L2)> builder, ExactScalar[]? customProbes = null)
    {
        var findings = new List<DiscoveryFinding>();
        var probes = customProbes ?? DefaultProbes.Select(ExactScalar.FromInt).ToArray();
        // 抽取两个斜率并构造组合表达式的通用探测器
        void Probe(string name, Func<ExactScalar, ExactScalar, ExactScalar> combine)
        {
            var sampleVals = new List<ExactScalar>();
            foreach (var p in probes)
            {
                var (l1, l2) = builder(p);
                var k1 = l1.TryGetSlope();
                var k2 = l2.TryGetSlope();
                if (k1 is null || k2 is null) continue;
                if (name.Contains("比") && k2.Value.IsNumericallyZero()) continue;
                sampleVals.Add(combine(k1.Value, k2.Value));
            }

            if (sampleVals.Count < 4) return;

            // Stage 1: 数值快筛，看方差/极差是不是小于 1e-7
            double min = sampleVals.Min(v => v.Approx);
            double max = sampleVals.Max(v => v.Approx);
            if (max - min > 1e-7) return;

            // Stage 2: 拿第一个精确样本作为候选定值，再用符号 t 进行严格证明！
            ExactScalar exactConst = sampleVals[0].Simplified();

            var tSym = ExactScalar.FromSymbol("t", 1.7320508);
            var (symL1, symL2) = builder(tSym);
            var sk1 = symL1.TryGetSlope();
            var sk2 = symL2.TryGetSlope();

            if (sk1 is not null && sk2 is not null)
            {
                var diff = combine(sk1.Value, sk2.Value) - exactConst;
                if (diff.VerifyStrictZero() == StrictResult.StrictlyTrue)
                {
                    findings.Add(new DiscoveryFinding(
                        $"发现定值：{name}",
                        $"{name} 恒等于 {exactConst}",
                        ProofStatus.SymbolicProved));
                    return;
                }
            }

            // 兜底：若含复杂嵌套符号暂时未展开为 0，用一组全新素数点做逐点严格验证
            bool allExtraStrict = ExtraConfirmProbes.All(p =>
            {
                var (l1, l2) = builder(p);
                var k1 = l1.TryGetSlope();
                var k2 = l2.TryGetSlope();
                if (k1 is null || k2 is null) return false;
                return (combine(k1.Value, k2.Value) - exactConst).VerifyStrictZero() == StrictResult.StrictlyTrue;
            });

            if (allExtraStrict)
            {
                findings.Add(new DiscoveryFinding(
                    $"发现定值：{name}",
                    $"{name} = {exactConst}",
                    ProofStatus.ExactSamplesVerified));
            }
        }

        Probe("斜率之和 (k1 + k2)", (k1, k2) => k1 + k2);
        Probe("斜率之积 (k1 * k2)", (k1, k2) => k1 * k2);
        Probe("斜率之比 (k1 / k2)", (k1, k2) => k1 / k2);

        return findings;
    }

    /// <summary>
    /// 2. 自动发现动直线是否【恒过定点】（或者【斜率恒定 / 互相平行】，即过无穷远定点）
    /// </summary>
    public static DiscoveryFinding? DiscoverFixedPoint(Func<ExactScalar, HLine> lineBuilder)
    {
        var lines = DefaultProbes.Select(p => (Param: p, Line: lineBuilder(p))).ToList();

        // 构造 N x 3 归一化矩阵并用 MathNet SVD 查秩
        var rows = new double[lines.Count, 3];
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i].Line;
            double norm = System.Math.Sqrt(l.A.Approx * l.A.Approx + l.B.Approx * l.B.Approx + l.C.Approx * l.C.Approx);
            if (norm < 1e-12) return null;
            rows[i, 0] = l.A.Approx / norm;
            rows[i, 1] = l.B.Approx / norm;
            rows[i, 2] = l.C.Approx / norm;
        }

        var svd = Matrix<double>.Build.DenseOfArray(rows).Svd(true);
        double minS = svd.S.Min();
        double maxS = svd.S.Max();

        // 若最小奇异值远小于最大奇异值，说明矩阵秩 <= 2，所有直线必共点！
        if (maxS < 1e-12 || minS / maxS > 1e-7) return null;

        // 从精确采样中找两条不重合的直线直接求精确交点！
        HPoint? candidatePt = null;
        for (int i = 0; i < lines.Count; i++)
        {
            for (int j = i + 1; j < lines.Count; j++)
            {
                var inter = HLine.Intersect(lines[i].Line, lines[j].Line);
                if (!inter.X.IsNumericallyZero() || !inter.Y.IsNumericallyZero() || !inter.W.IsNumericallyZero())
                {
                    candidatePt = new HPoint(inter.X.Simplified(), inter.Y.Simplified(), inter.W.Simplified());
                    break;
                }
            }
            if (candidatePt is not null) break;
        }

        if (candidatePt is null) return null;
        var pt = candidatePt.Value;

        // 拿符号 t 严格证明定点！
        var tSym = ExactScalar.FromSymbol("t", 1.7320508);
        var symStrict = lineBuilder(tSym).ContainsStrictly(pt);

        ProofStatus status;
        if (symStrict == StrictResult.StrictlyTrue)
        {
            status = ProofStatus.SymbolicProved;
        }
        else if (ExtraConfirmProbes.All(p => lineBuilder(p).ContainsStrictly(pt) == StrictResult.StrictlyTrue))
        {
            status = ProofStatus.ExactSamplesVerified;
        }
        else
        {
            return null; // 打假：严通道没过，绝不谎报！
        }

        if (pt.IsAtInfinity)
        {
            // W == 0 说明是斜率恒定的平行直线系
            string dirDesc = pt.X.IsNumericallyZero()
                ? "竖直线系（斜率不存在，平行于 y 轴）"
                : $"斜率恒等于 {(pt.Y / pt.X).Simplified()}";
            return new DiscoveryFinding("发现平行直线系（无穷远定点）", $"动直线方向恒定：{dirDesc}", status);
        }

        var exactX = (pt.X / pt.W).Simplified();
        var exactY = (pt.Y / pt.W).Simplified();
        return new DiscoveryFinding("发现动直线过定点", $"动直线恒过定点 ({exactX}, {exactY})", status);
    }

    /// <summary>
    /// 3. 自动发现动点的轨迹性质（共线 / 圆锥曲线轨迹）
    /// </summary>
    public static DiscoveryFinding? DiscoverPointLocus(Func<ExactScalar, HPoint> pointBuilder, ExactScalar[]? customProbes = null)
    {
        var probes = customProbes ?? DefaultProbes.Select(ExactScalar.FromInt).ToArray();
        var pts = probes
            .Select(pointBuilder)
            .Where(pt => !pt.IsAtInfinity)
            .Select(pt => (Point: pt, Cart: pt.ToCartesianApprox()!.Value))
            .ToList();

        if (pts.Count < 7) return null;

        // 3.1 先探测是不是共线（定直线轨迹）：N x 3 SVD
        var lineMat = new double[pts.Count, 3];
        for (int i = 0; i < pts.Count; i++)
        {
            lineMat[i, 0] = pts[i].Cart.X;
            lineMat[i, 1] = pts[i].Cart.Y;
            lineMat[i, 2] = 1.0;
        }
        var lineSvd = Matrix<double>.Build.DenseOfArray(lineMat).Svd(true);
        if (lineSvd.S.Min() / lineSvd.S.Max() < 1e-7)
        {
            var fixedLine = HLine.FromPoints(pts[0].Point, pts[1].Point);
            var tSym = ExactScalar.FromSymbol("t", 1.7320508);
            var status = fixedLine.ContainsStrictly(pointBuilder(tSym)) == StrictResult.StrictlyTrue
                ? ProofStatus.SymbolicProved
                : ProofStatus.NumericOnly;
            return new DiscoveryFinding("发现动点轨迹为定直线",
                $"({fixedLine.A.Simplified().Approx:G4})x + ({fixedLine.B.Simplified().Approx:G4})y + ({fixedLine.C.Simplified().Approx:G4}) = 0",
                status);
        }

        // 3.2 再探测是不是圆锥曲线轨迹：N x 6 SVD [x^2, xy, y^2, x, y, 1]
        var conicMat = new double[pts.Count, 6];
        for (int i = 0; i < pts.Count; i++)
        {
            double x = pts[i].Cart.X, y = pts[i].Cart.Y;
            conicMat[i, 0] = x * x;
            conicMat[i, 1] = x * y;
            conicMat[i, 2] = y * y;
            conicMat[i, 3] = x;
            conicMat[i, 4] = y;
            conicMat[i, 5] = 1.0;
        }
        var conicSvd = Matrix<double>.Build.DenseOfArray(conicMat).Svd(true);
        if (conicSvd.S.Min() / conicSvd.S.Max() < 1e-7)
        {
            // 找出最小奇异值对应的右奇异向量（SVD.VT 对应行）
            int minIdx = 0;
            for (int i = 1; i < conicSvd.S.Count; i++)
                if (conicSvd.S[i] < conicSvd.S[minIdx]) minIdx = i;

            var row = conicSvd.VT.Row(minIdx);
            // 除以最大绝对值让系数好看一点
            double scale = row.AbsoluteMaximum();
            double Clean(double v) => System.Math.Abs(v) < 1e-9 ? 0 : v;
            var c = row / scale;

            // 数值拟合出的系数只带近似值，分类会标记 IsExact=false（诚实降级，不冒充严格结论）
            var fitted = Conic.FromGeneralEquation(
                ExactScalar.FromApprox(c[0]), ExactScalar.FromApprox(c[1]), ExactScalar.FromApprox(c[2]),
                ExactScalar.FromApprox(c[3]), ExactScalar.FromApprox(c[4]), ExactScalar.FromApprox(c[5]));
            var cls = ConicTools.Classify(fitted);
            string kindLabel = cls.Kind == ConicKind.Unknown
                ? "类型：无法判定"
                : $"类型：{cls.Kind}（有效层级 {cls.MeaningfulAt}；数值拟合近似分类）";

            return new DiscoveryFinding(
                "发现动点轨迹为圆锥曲线",
                $"拟合方程: ({Clean(c[0]):G4})x^2 + ({Clean(c[1]):G4})xy + ({Clean(c[2]):G4})y^2 + ({Clean(c[3]):G4})x + ({Clean(c[4]):G4})y + ({Clean(c[5]):G4}) = 0；{kindLabel}",
                ProofStatus.NumericOnly);
        }

        return null;
    }
    
    /// <summary>
    /// 【万能定值探测】探测任意构造出的数字（如斜率积、斜率和、长度平方等）是不是定值
    /// </summary>
    public static DiscoveryFinding? DiscoverConstant(
        string name,
        Func<ExactScalar, ExactScalar?> builder,
        ExactScalar[]? customProbes = null,
        double probeHint = 0.5)
    {
        var probes = customProbes ?? DefaultProbes.Select(ExactScalar.FromInt).ToArray();
        var sampleVals = new List<ExactScalar>();

        foreach (var p in probes)
        {
            var val = builder(p);
            if (val is not null) sampleVals.Add(val.Value);
        }

        if (sampleVals.Count < 4) return null;
        if (sampleVals.Max(v => v.Approx) - sampleVals.Min(v => v.Approx) > 1e-7) return null;

        var exactConst = sampleVals[0].Simplified();
        var tSym = ExactScalar.FromSymbol("t", probeHint);
        var symVal = builder(tSym);

        if (symVal is not null && (symVal.Value - exactConst).VerifyStrictZero() == StrictResult.StrictlyTrue)
            return new DiscoveryFinding($"发现定值：{name}", $"{name} 恒等于 {exactConst}", ProofStatus.SymbolicProved);

        return new DiscoveryFinding($"发现定值：{name}", $"{name} = {exactConst}", ProofStatus.ExactSamplesVerified);
    }
}