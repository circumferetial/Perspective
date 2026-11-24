using Perspective.Core.Math;

namespace Perspective.Experiment.Subspace;

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
        var lens = GeometryLens.EuclideanXoy;
        var t = ExactScalar.FromSymbol("t", 2.71828);
        var s = ExactScalar.FromSymbol("s", 1.73205);

        // 1. 无向性与平行交点
        Console.WriteLine(
            $"  [2,1,1] ≡ [-2,-1,-1]？{SubspaceOps.SameSubspace(new Primal3(2, 1, 1), new Primal3(-2, -1, -1))}");
        var inter = SubspaceOps.Meet(new Dual3(1, 1, -1), new Dual3(1, 1, 2));
        Console.WriteLine($"  平行线交点 = {inter}，在 l∞ 上？{lens.IsAtInfinity(inter)}");

        // 2. Pappus 定理（双符号参数）
        var (a1, b1, c1) = (new Primal3(0, 0, 1), new Primal3(1, 0, 1), new Primal3(t, 0, 1));
        var (a2, b2, c2) = (new Primal3(0, 1, 1), new Primal3(1, 2, 1), new Primal3(s, s + 1, 1));
        var p = SubspaceOps.Meet(SubspaceOps.Join(a1, b2), SubspaceOps.Join(a2, b1));
        var q = SubspaceOps.Meet(SubspaceOps.Join(a1, c2), SubspaceOps.Join(a2, c1));
        var r = SubspaceOps.Meet(SubspaceOps.Join(b1, c2), SubspaceOps.Join(b2, c1));
        Console.WriteLine($"  Pappus 定理 (t, s)？{SubspaceOps.AreCollinear(p, q, r)}");

        // 3. 透视变换与交比守恒
        var (q1, q2, q3, q4) = (new Primal3(0, 0, 1), new Primal3(1, 0, 1), new Primal3(3, 0, 1), new Primal3(7, 0, 1));
        var proj = new LinearMap3(2, 1, 3, -1, 4, 2, ExactScalar.FromFormula("1/3"), ExactScalar.FromFormula("1/5"), 1);
        var crBefore = SubspaceOps.CrossRatio(q1, q2, q3, q4);
        var crAfter = SubspaceOps.CrossRatio(proj.Apply(q1), proj.Apply(q2), proj.Apply(q3), proj.Apply(q4));
        Console.WriteLine(
            $"  透视变换 det={proj.Determinant().ToSimplifiedString()}，交比严格守恒？{ExactScalar.AreStrictlyEqual(crBefore, crAfter)}");

        // 4. 垂心定理（双符号参数）与精确距离²
        var (triA, triB, triC) = (new Primal3(0, 0, 1), new Primal3(1, 0, 1), new Primal3(t, s, 1));
        var altA = lens.PerpendicularThrough(triA, SubspaceOps.Join(triB, triC));
        var altB = lens.PerpendicularThrough(triB, SubspaceOps.Join(triC, triA));
        var altC = lens.PerpendicularThrough(triC, SubspaceOps.Join(triA, triB));
        Console.WriteLine($"  任意三角形三高线共点 (t, s)？{SubspaceOps.AreConcurrent(altA, altB, altC)}");
        Console.WriteLine(
            $"  (3,0)-(0,4) 距离² = {lens.EuclideanDistanceSquared(new Primal3(3, 0, 1), new Primal3(0, 4, 1)).ToSimplifiedString()}");
    }

    public static void RunUnifiedMidpoint()
    {
        Console.WriteLine("\n================ [B] 统一极点极线（取代点差法 & 共轭直径） ================");
        var lens = GeometryLens.EuclideanXoy;
        var lInf = lens.InfinityLine;
        var u = ExactScalar.FromSymbol("u", 0.3);
        var v = ExactScalar.FromSymbol("v", 2.1);

        // 1. 剪切+平移后的斜置偏心椭圆
        static Primal3 OnUnitCircle(ExactScalar t)
        {
            return new Primal3(1 - t * t, 2 * t, 1 + t * t);
        }

        var warp = new LinearMap3(1, 1, 2, 0, 1, -1, 0, 0, 1);
        var ellipse = warp.Apply(new BilinearForm3(1, 0, 0, 1, 0, -1));
        var (a, b) = (warp.Apply(OnUnitCircle(u)), warp.Apply(OnUnitCircle(v)));

        var center = ellipse.DualForm().Pole(lInf);
        var chord = SubspaceOps.Join(a, b);
        var dir = SubspaceOps.Meet(chord, lInf);
        var polarOfDir = ellipse.Polar(dir);
        var mFromPolar = SubspaceOps.Meet(chord, polarOfDir);
        var mMid = lens.Midpoint(a, b);
        var lineOM = SubspaceOps.Join(center, mMid);

        Console.WriteLine($"  [斜椭圆] La Hire（共轭径过中心）？{SubspaceOps.IsIncident(polarOfDir, center)}");
        Console.WriteLine($"  [斜椭圆] 调和 (D∞, M; A, B)=-1？{SubspaceOps.IsHarmonic(dir, mFromPolar, a, b)}");
        Console.WriteLine($"  [斜椭圆] 极线交点 ≡ 中点 ≡ OM=Polar？" +
                          $"{SubspaceOps.SameSubspace(mFromPolar, mMid)} / {SubspaceOps.SameSubspace(lineOM, polarOfDir)}");

        // 2. 抛物线 y² = 4xw
        var parabola = new BilinearForm3(0, 0, -2, 1, 0, 0);
        var (pa, pb) = (new Primal3(u * u, 2 * u, 1), new Primal3(v * v, 2 * v, 1));
        var axisInf = parabola.DualForm().Pole(lInf);
        var pChord = SubspaceOps.Join(pa, pb);
        var pDir = SubspaceOps.Meet(pChord, lInf);
        var pPolar = parabola.Polar(pDir);
        var pMid = lens.Midpoint(pa, pb);

        Console.WriteLine($"  [抛物线] 极线自动过对称轴无穷远点 {axisInf}？{SubspaceOps.IsIncident(pPolar, axisInf)}");
        Console.WriteLine($"  [抛物线] 极线交点 ≡ 仿射中点？{SubspaceOps.SameSubspace(SubspaceOps.Meet(pChord, pPolar), pMid)}");
    }

    public static void RunConicIntersection()
    {
        Console.WriteLine("\n================ [C] 直线 ∩ 二次曲线 & 动弦过定点 ================");
        var lens = GeometryLens.EuclideanXoy;
        var ellipse = new BilinearForm3(1, 0, 0, 4, 0, -4);// x² + 4y² - 4w² = 0

        var r1 = ConicIntersection.Intersect(new Dual3(1, 2, -2), ellipse);
        Console.WriteLine($"  椭圆∩斜线：{r1.Kind}，P1={lens.Format(r1.P1!.Value)}，P2={lens.Format(r1.P2!.Value)}");

        var rTan = ConicIntersection.Intersect(new Dual3(1, 0, -2), ellipse);
        Console.WriteLine(
            $"  椭圆∩切线：{rTan.Kind}，Δ={rTan.Discriminant.ToSimplifiedString()}，切点={lens.Format(rTan.P1!.Value)}");

        var rMiss = ConicIntersection.Intersect(new Dual3(1, 0, -5), ellipse);
        Console.WriteLine($"  椭圆∩外离线：{rMiss.Kind}，Δ={rMiss.Discriminant.ToSimplifiedString()}");

        var parabola = new BilinearForm3(0, 0, -2, 1, 0, 0);
        var rPar = ConicIntersection.Intersect(new Dual3(0, 1, -2), parabola);
        Console.WriteLine($"  抛物线∩平行轴线：{rPar.Kind}，P1={lens.Format(rPar.P1!.Value)}，P2={lens.Format(rPar.P2!.Value)}");

        // 零根号弦中点
        var t = ExactScalar.FromSymbol("t");
        var movingLine = new Dual3(t, -1, 1);
        var mid = lens.ChordMidpoint(movingLine, ellipse);
        var dir = SubspaceOps.Meet(movingLine, lens.InfinityLine);
        Console.WriteLine($"  符号弦零根号中点在直线与极线上？" +
                          $"{SubspaceOps.IsIncident(movingLine, mid)} / {SubspaceOps.IsIncident(ellipse.Polar(dir), mid)}");

        // Frégier 定点
        var vertexP = new Primal3(2, 0, 1);

        Dual3 MovingChordAB(ExactScalar param)
        {
            var linePA = new Dual3(1, param, -2);
            var linePB = lens.PerpendicularThrough(vertexP, linePA);
            var ptA = ConicIntersection.OtherIntersection(linePA, vertexP, ellipse);
            var ptB = ConicIntersection.OtherIntersection(linePB, vertexP, ellipse);
            return SubspaceOps.Join(ptA, ptB);
        }

        var fixedPt = SubspaceOps.Meet(MovingChordAB(1), MovingChordAB(2));
        Console.WriteLine(
            $"  盲猜定点 = {lens.Format(fixedPt)}，符号动弦恒过？{SubspaceOps.IsIncident(MovingChordAB(t), fixedPt)}");
    }
}