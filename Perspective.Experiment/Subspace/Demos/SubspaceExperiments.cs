using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using PointE = Perspective.Experiment.Subspace.Primal3<Perspective.Core.Math.ExactScalar>;
using LineE = Perspective.Experiment.Subspace.Dual3<Perspective.Core.Math.ExactScalar>;
using ConicE = Perspective.Experiment.Subspace.BilinearForm3<Perspective.Core.Math.ExactScalar>;
using LensE = Perspective.Experiment.Subspace.GeometryLens<Perspective.Core.Math.ExactScalar>;
using MapE = Perspective.Experiment.Subspace.LinearMap3<Perspective.Core.Math.ExactScalar>;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>收口后的全部子空间验证实验（零重复脚手架）。</summary>
public static class SubspaceExperiments
{
    public static void RunAll()
    {
        RunFoundation();
        RunUnifiedMidpoint();
        RunConicIntersection();
    }

    public static void RunFoundation()
    {
        Console.WriteLine("\n================ [A] 子空间地基实验 ================");
        var lens = LensE.EuclideanXoy;
        var t = ExactScalar.FromSymbol("t", 2.71828);
        var s = ExactScalar.FromSymbol("s", 1.73205);

        // 1. 无向性与平行交点
        Console.WriteLine(
            $"  [2,1,1] ≡ [-2,-1,-1]？{SubspaceAlgebra.SameSubspace(new PointE(2, 1, 1), new PointE(-2, -1, -1)).Value}");
        var inter = SubspaceAlgebra.Meet(new LineE(1, 1, -1), new LineE(1, 1, 2));
        Console.WriteLine($"  平行线交点 = {inter}，在 l∞ 上？{lens.Affine.IsAtInfinity(inter).Value}");

        // 2. Pappus 定理（双符号参数）
        var (a1, b1, c1) = (new PointE(0, 0, 1), new PointE(1, 0, 1), new PointE(t, 0, 1));
        var (a2, b2, c2) = (new PointE(0, 1, 1), new PointE(1, 2, 1), new PointE(s, s + 1, 1));
        var p = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(a1, b2), SubspaceAlgebra.Join(a2, b1));
        var q = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(a1, c2), SubspaceAlgebra.Join(a2, c1));
        var r = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(b1, c2), SubspaceAlgebra.Join(b2, c1));
        Console.WriteLine($"  Pappus 定理 (t, s)？{SubspaceAlgebra.AreCollinear(p, q, r)}");

        // 3. 透视变换与交比守恒
        var (q1, q2, q3, q4) = (new PointE(0, 0, 1), new PointE(1, 0, 1), new PointE(3, 0, 1), new PointE(7, 0, 1));
        var proj = new MapE(2, 1, 3, -1, 4, 2, ExactScalar.FromFormula("1/3"), ExactScalar.FromFormula("1/5"), 1);
        var crBefore = q1.CrossRatio(q2, q3, q4).Value;
        var crAfter = proj.Apply(q1).CrossRatio(proj.Apply(q2), proj.Apply(q3), proj.Apply(q4)).Value;
        Console.WriteLine(
            $"  透视变换 det={proj.Determinant().ToSimplifiedString()}，交比严格守恒？{ExactScalar.AreStrictlyEqual(crBefore, crAfter)}");

        // 4. 垂心定理（双符号参数）与精确距离²
        var (triA, triB, triC) = (new PointE(0, 0, 1), new PointE(1, 0, 1), new PointE(t, s, 1));
        var altA = lens.Metric.PerpendicularThrough(triA, SubspaceAlgebra.Join(triB, triC)).Value;
        var altB = lens.Metric.PerpendicularThrough(triB, SubspaceAlgebra.Join(triC, triA)).Value;
        var altC = lens.Metric.PerpendicularThrough(triC, SubspaceAlgebra.Join(triA, triB)).Value;
        Console.WriteLine($"  任意三角形三高线共点 (t, s)？{SubspaceAlgebra.AreConcurrent(altA, altB, altC).Value}");
        Console.WriteLine(
            $"  (3,0)-(0,4) 距离² = {lens.EuclideanDistanceSquared(new PointE(3, 0, 1), new PointE(0, 4, 1)).Value.ToSimplifiedString()}");
    }

    public static void RunUnifiedMidpoint()
    {
        Console.WriteLine("\n================ [B] 统一极点极线（取代点差法 & 共轭直径） ================");
        var lens = LensE.EuclideanXoy;
        var lInf = lens.InfinityLine;
        var u = ExactScalar.FromSymbol("u", 0.3);
        var v = ExactScalar.FromSymbol("v", 2.1);

        // 1. 剪切+平移后的斜置偏心椭圆
        static PointE OnUnitCircle(ExactScalar t)
        {
            return new PointE(1 - t * t, 2 * t, 1 + t * t);
        }

        var warp = new MapE(1, 1, 2, 0, 1, -1, 0, 0, 1);
        var ellipse = warp.Apply(new ConicE(1, 0, 0, 1, 0, -1));
        var (a, b) = (warp.Apply(OnUnitCircle(u)), warp.Apply(OnUnitCircle(v)));

        var center = ellipse.DualForm().Pole(lInf);
        var chord = SubspaceAlgebra.Join(a, b);
        var dir = SubspaceAlgebra.Meet(chord, lInf);
        var polarOfDir = ellipse.Polar(dir);
        var mFromPolar = SubspaceAlgebra.Meet(chord, polarOfDir);
        var mMid = lens.Affine.Midpoint(a, b).Value;
        var lineOM = SubspaceAlgebra.Join(center, mMid);

        Console.WriteLine($"  [斜椭圆] La Hire（共轭径过中心）？{SubspaceAlgebra.IsIncident(polarOfDir, center).Value}");
        Console.WriteLine($"  [斜椭圆] 调和 (D∞, M; A, B)=-1？{dir.IsHarmonic(mFromPolar, a, b)}");
        Console.WriteLine($"  [斜椭圆] 极线交点 ≡ 中点 ≡ OM=Polar？" +
                          $"{SubspaceAlgebra.SameSubspace(mFromPolar, mMid).Value} / {SubspaceAlgebra.SameSubspace(lineOM, polarOfDir).Value}");

        // 2. 抛物线 y² = 4xw
        var parabola = new ConicE(0, 0, -2, 1, 0, 0);
        var (pa, pb) = (new PointE(u * u, 2 * u, 1), new PointE(v * v, 2 * v, 1));
        var axisInf = parabola.DualForm().Pole(lInf);
        var pChord = SubspaceAlgebra.Join(pa, pb);
        var pDir = SubspaceAlgebra.Meet(pChord, lInf);
        var pPolar = parabola.Polar(pDir);
        var pMid = lens.Affine.Midpoint(pa, pb).Value;

        Console.WriteLine($"  [抛物线] 极线自动过对称轴无穷远点 {axisInf}？{SubspaceAlgebra.IsIncident(pPolar, axisInf).Value}");
        Console.WriteLine($"  [抛物线] 极线交点 ≡ 仿射中点？{SubspaceAlgebra.SameSubspace(SubspaceAlgebra.Meet(pChord, pPolar), pMid).Value}");
    }

    public static void RunConicIntersection()
    {
        Console.WriteLine("\n================ [C] 直线 ∩ 二次曲线 & 动弦过定点 ================");
        var lens = LensE.EuclideanXoy;
        var ellipse = new ConicE(1, 0, 0, 4, 0, -4); // x² + 4y² - 4w² = 0

        var r1 = ConicIntersection.Intersect(new LineE(1, 2, -2), ellipse);
        Console.WriteLine($"  椭圆∩斜线：{r1.Kind}，P1={lens.Format(r1.P1!.Value)}，P2={lens.Format(r1.P2!.Value)}");

        var rTan = ConicIntersection.Intersect(new LineE(1, 0, -2), ellipse);
        Console.WriteLine(
            $"  椭圆∩切线：{rTan.Kind}，Δ={rTan.Discriminant.ToSimplifiedString()}，切点={lens.Format(rTan.P1!.Value)}");

        var rMiss = ConicIntersection.Intersect(new LineE(1, 0, -5), ellipse);
        Console.WriteLine($"  椭圆∩外离线：{rMiss.Kind}，Δ={rMiss.Discriminant.ToSimplifiedString()}");

        var parabola = new ConicE(0, 0, -2, 1, 0, 0);
        var rPar = ConicIntersection.Intersect(new LineE(0, 1, -2), parabola);
        Console.WriteLine($"  抛物线∩平行轴线：{rPar.Kind}，P1={lens.Format(rPar.P1!.Value)}，P2={lens.Format(rPar.P2!.Value)}");

        // 零根号弦中点
        var t = ExactScalar.FromSymbol("t");
        var movingLine = new LineE(t, -1, 1);
        var mid = lens.ChordMidpoint(movingLine, ellipse).Value;
        var dir = SubspaceAlgebra.Meet(movingLine, lens.InfinityLine);
        Console.WriteLine($"  符号弦零根号中点在直线与极线上？" +
                          $"{SubspaceAlgebra.IsIncident(movingLine, mid)} / {SubspaceAlgebra.IsIncident(ellipse.Polar(dir), mid)}");

        // Frégier 定点
        var vertexP = new PointE(2, 0, 1);

        LineE MovingChordAB(ExactScalar param)
        {
            var linePA = new LineE(1, param, -2);
            var linePB = lens.Metric.PerpendicularThrough(vertexP, linePA).Value;
            var ptA = ConicIntersection.OtherIntersection(linePA, vertexP, ellipse).Value;
            var ptB = ConicIntersection.OtherIntersection(linePB, vertexP, ellipse).Value;
            return SubspaceAlgebra.Join(ptA, ptB);
        }

        var fixedPt = SubspaceAlgebra.Meet(MovingChordAB(1), MovingChordAB(2));
        Console.WriteLine(
            $"  盲猜定点 = {lens.Format(fixedPt)}，符号动弦恒过？{SubspaceAlgebra.IsIncident(MovingChordAB(t), fixedPt)}");
    }
}
