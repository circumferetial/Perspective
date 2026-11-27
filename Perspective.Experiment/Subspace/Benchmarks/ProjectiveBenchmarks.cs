using System.Diagnostics;
using Perspective.Core.Math;
using Perspective.Experiment.Subspace;

namespace Perspective.Experiment.Subspace.Benchmarks;

/// <summary>
///     射影降维基准：极点极线 + 交比调和。
///     取抛物线 y²=xz 上的具体有理点，验证 (U,V;X,Y)=−1。
/// </summary>
public static class ProjectiveBenchmarks
{
    public static IReadOnlyList<BenchmarkResult> RunAll() =>
    [
        HarmonicCrossRatio(),
        DegeneratePascal(),
        DegenerateBrianchon(),
        SymbolicHarmonicCrossRatio(),
        SymbolicDegeneratePascal(),
        SymbolicDegenerateBrianchon(),
        FrameInvariantHarmonicCrossRatio(),
        FrameInvariantDegeneratePascal(),
        FrameInvariantDegenerateBrianchon()
    ];

    public static BenchmarkResult HarmonicCrossRatio()
    {
        var sw = Stopwatch.StartNew();

        // y²=xz 参数化 P(t)=(t²,t,1)。
        var u = Primal3<ExactScalar>.Fixed(0, 0);
        var v = Primal3<ExactScalar>.Fixed(1, 1);
        var a = Primal3<ExactScalar>.Fixed(4, 2);
        var b = new Primal3<ExactScalar>(
            ExactScalar.FromInt(4) / ExactScalar.FromInt(9),
            ExactScalar.FromInt(2) / ExactScalar.FromInt(3),
            ExactScalar.One);
        var p = Primal3<ExactScalar>.Fixed(9, 3);

        // 验证 A,B,U,V 共 conic y²=xz。
        bool onConic = OnParabola(u) && OnParabola(v) && OnParabola(a) && OnParabola(b) && OnParabola(p);
        if (!onConic)
            return Fail("PR-3", "取点不在 y²=xz 上");

        var uv = SubspaceAlgebra.Join(u, v);
        var x = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(p, a), uv);
        var y = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(p, b), uv);

        var cross = u.CrossRatio(v, x, y);
        if (!cross.TryGet(out var cr))
        {
            sw.Stop();
            return new BenchmarkResult("PR-3", "(U,V;X,Y)=−1", "交比/极点", false, cross.Error.ToString(), sw.ElapsedMilliseconds);
        }

        var ok = (cr + ExactScalar.One).VerifyStrictZero() == StrictResult.StrictlyTrue;
        sw.Stop();
        var detail = ok
            ? "抛物线 y²=xz 上 (U,V;X,Y)=−1，极点极线调和"
            : $"交比={cr.ToSimplifiedString()}，不为 −1";

        Console.WriteLine($"[PR-3] {(ok ? "PASS" : "FAIL")}  {sw.ElapsedMilliseconds,4}ms  {detail}");
        return new BenchmarkResult("PR-3", "(U,V;X,Y)=−1", "交比/极点", ok, detail, sw.ElapsedMilliseconds);
    }

    public static BenchmarkResult DegeneratePascal()
    {
        var sw = Stopwatch.StartNew();

        // Γ: y²=xz；参数化 P(t)=(t²,t,1)。
        var conic = new BilinearForm3<ExactScalar>(
            ExactScalar.Zero,
            ExactScalar.Zero,
            -ExactScalar.FromInt(1) / ExactScalar.FromInt(2),
            ExactScalar.One,
            ExactScalar.Zero,
            ExactScalar.Zero);

        var a = Primal3<ExactScalar>.Fixed(0, 0);
        var b = Primal3<ExactScalar>.Fixed(4, 2);
        var m = Primal3<ExactScalar>.Fixed(9, 3);
        var n = new Primal3<ExactScalar>(
            ExactScalar.FromInt(9) / ExactScalar.FromInt(4),
            ExactScalar.FromInt(3) / ExactScalar.FromInt(2),
            ExactScalar.One);

        var pa = conic.Polar(a);
        var pb = conic.Polar(b);
        var x1 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(a, m), pb);
        var y1 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(b, m), pa);
        var x2 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(a, n), pb);
        var y2 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(b, n), pa);

        var ab = SubspaceAlgebra.Join(a, b);
        var l1 = SubspaceAlgebra.Join(x1, y1);
        var l2 = SubspaceAlgebra.Join(x2, y2);
        var concurrent = SubspaceAlgebra.AreConcurrent(l1, l2, ab);

        bool ok = concurrent.TryGet(out var verdict)
                  && verdict == StrictResult.StrictlyTrue;
        sw.Stop();
        var detail = ok
            ? "退化 Pascal：X1Y1、X2Y2、AB 三线共点"
            : $"共点判定失败：{(concurrent.IsFailure ? concurrent.Error.ToString() : verdict.ToString())}";

        Console.WriteLine($"[PR-2] {(ok ? "PASS" : "FAIL")}  {sw.ElapsedMilliseconds,4}ms  {detail}");
        return new BenchmarkResult("PR-2", "X1Y1、X2Y2、AB 共点", "退化 Pascal", ok, detail, sw.ElapsedMilliseconds);
    }

    public static BenchmarkResult DegenerateBrianchon()
    {
        var sw = Stopwatch.StartNew();

        // Γ: y²=xz。取 E=P(0),F=P(1),G=P(2),H=P(4)，四条切线围成外切四边形。
        var conic = new BilinearForm3<ExactScalar>(
            ExactScalar.Zero,
            ExactScalar.Zero,
            -ExactScalar.FromInt(1) / ExactScalar.FromInt(2),
            ExactScalar.One,
            ExactScalar.Zero,
            ExactScalar.Zero);

        var e = Primal3<ExactScalar>.Fixed(0, 0);
        var f = Primal3<ExactScalar>.Fixed(1, 1);
        var g = Primal3<ExactScalar>.Fixed(4, 2);
        var h = Primal3<ExactScalar>.Fixed(16, 4);

        var re = conic.Polar(e);
        var rf = conic.Polar(f);
        var rg = conic.Polar(g);
        var rh = conic.Polar(h);

        var r = SubspaceAlgebra.Meet(re, rg); // AB∩CD
        var s = SubspaceAlgebra.Meet(rh, rf); // AD∩BC
        var m = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(e, f), SubspaceAlgebra.Join(g, h));
        var n = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(f, g), SubspaceAlgebra.Join(h, e));

        var rsm = SubspaceAlgebra.AreCollinear(r, s, m);
        var rsn = SubspaceAlgebra.AreCollinear(r, s, n);

        bool ok = rsm.TryGet(out var v1) && v1 == StrictResult.StrictlyTrue
                  && rsn.TryGet(out var v2) && v2 == StrictResult.StrictlyTrue;
        sw.Stop();
        var detail = ok
            ? "退化 Brianchon：R,S,M,N 四点共线"
            : "共线判定失败";

        Console.WriteLine($"[PR-1] {(ok ? "PASS" : "FAIL")}  {sw.ElapsedMilliseconds,4}ms  {detail}");
        return new BenchmarkResult("PR-1", "R,S,M,N 共线", "退化 Brianchon", ok, detail, sw.ElapsedMilliseconds);
    }

    public static BenchmarkResult SymbolicHarmonicCrossRatio()
    {
        var sw = Stopwatch.StartNew();

        // U=P(0),V=P(1)，条件 t_U∩t_V∈AB 化为 2ab=a+b，取 b=a/(2a−1)。
        // B 用齐次缩放 (a², a(2a−1), (2a−1)²)，避免除法。
        var a = PolyExpr.Var(0);
        var p = PolyExpr.Var(1);
        var one = PolyExpr.One;
        var zero = PolyExpr.Zero;

        var u = new Primal3<PolyExpr>(zero, zero, one);
        var v = new Primal3<PolyExpr>(one, one, one);
        var aa = new Primal3<PolyExpr>(a * a, a, one);
        var den = 2 * a - one;
        var bb = new Primal3<PolyExpr>(a * a, a * den, den * den);
        var point = new Primal3<PolyExpr>(p * p, p, one);

        var uv = SubspaceAlgebra.Join(u, v);
        var x = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(point, aa), uv);
        var y = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(point, bb), uv);
        var o = new Primal3<PolyExpr>(one, zero, zero);

        var target = SubspaceAlgebra.Det(o, u, x) * SubspaceAlgebra.Det(o, v, y)
                     + SubspaceAlgebra.Det(o, u, y) * SubspaceAlgebra.Det(o, v, x);

        var outcome = new UnifiedGeoSpace().ProveZero(target);
        var ok = outcome.Status == UnifiedProofStatus.Proved;
        sw.Stop();
        var detail = ok
            ? "参数 a,p：U=P(0),V=P(1)，b=a/(2a−1) 下 (U,V;X,Y)=−1"
            : $"status={outcome.Status}";

        Console.WriteLine($"[PR-3S] {(ok ? "PASS" : "FAIL")}  {sw.ElapsedMilliseconds,4}ms  {detail}");
        return new BenchmarkResult("PR-3S", "(U,V;X,Y)=−1", "符号交比", ok, detail, sw.ElapsedMilliseconds);
    }

    public static BenchmarkResult SymbolicDegeneratePascal()
    {
        var sw = Stopwatch.StartNew();

        // A=P(0), B=P(1)；P 为两条切线的交点。割线过 P ⇔ 2mn=m+n，
        // 取 n=m/(2m−1)，N 用齐次缩放 (m², m(2m−1), (2m−1)²)。
        var m = PolyExpr.Var(0);
        var one = PolyExpr.One;
        var zero = PolyExpr.Zero;
        var two = PolyExpr.FromLong(2);

        var a = new Primal3<PolyExpr>(zero, zero, one);
        var b = new Primal3<PolyExpr>(one, one, one);
        var mm = new Primal3<PolyExpr>(m * m, m, one);
        var den = two * m - one;
        var nn = new Primal3<PolyExpr>(m * m, m * den, den * den);

        var ta = new Dual3<PolyExpr>(one, zero, zero);
        var tb = new Dual3<PolyExpr>(one, -two, one);

        var x1 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(a, mm), tb);
        var y1 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(b, mm), ta);
        var x2 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(a, nn), tb);
        var y2 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(b, nn), ta);

        var l1 = SubspaceAlgebra.Join(x1, y1);
        var l2 = SubspaceAlgebra.Join(x2, y2);
        var ab = SubspaceAlgebra.Join(a, b);
        var target = SubspaceAlgebra.Det(l1, l2, ab);

        var outcome = new UnifiedGeoSpace().ProveZero(target);
        var ok = outcome.Status == UnifiedProofStatus.Proved;
        sw.Stop();
        var detail = ok
            ? "参数 m：A=P(0),B=P(1)，2mn=m+n，X1Y1、X2Y2、AB 恒共点"
            : $"status={outcome.Status}";

        Console.WriteLine($"[PR-2S] {(ok ? "PASS" : "FAIL")}  {sw.ElapsedMilliseconds,4}ms  {detail}");
        return new BenchmarkResult("PR-2S", "X1Y1、X2Y2、AB 共点", "符号退化 Pascal", ok, detail, sw.ElapsedMilliseconds);
    }

    public static BenchmarkResult SymbolicDegenerateBrianchon()
    {
        var sw = Stopwatch.StartNew();

        // 归一化 E=P(0), F=P(1), G=P(2)，H=P(h)；验证 R,S,M,N 对 h 恒共线。
        var h = PolyExpr.Var(0);
        var one = PolyExpr.One;
        var zero = PolyExpr.Zero;
        var two = PolyExpr.FromLong(2);
        var four = PolyExpr.FromLong(4);

        var e = new Primal3<PolyExpr>(zero, zero, one);
        var f = new Primal3<PolyExpr>(one, one, one);
        var g = new Primal3<PolyExpr>(four, two, one);
        var hh = new Primal3<PolyExpr>(h * h, h, one);

        var te = new Dual3<PolyExpr>(one, zero, zero);
        var tf = new Dual3<PolyExpr>(one, -two, one);
        var tg = new Dual3<PolyExpr>(one, -four, four);
        var th = new Dual3<PolyExpr>(one, -two * h, h * h);

        var r = SubspaceAlgebra.Meet(te, tg);
        var s = SubspaceAlgebra.Meet(th, tf);
        var m = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(e, f), SubspaceAlgebra.Join(g, hh));
        var n = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(f, g), SubspaceAlgebra.Join(hh, e));

        var space = new UnifiedGeoSpace();
        var o1 = space.ProveZero(SubspaceAlgebra.Det(r, s, m));
        var o2 = space.ProveZero(SubspaceAlgebra.Det(r, s, n));
        var ok = o1.Status == UnifiedProofStatus.Proved && o2.Status == UnifiedProofStatus.Proved;
        sw.Stop();
        var detail = ok
            ? "参数 h：E=P(0),F=P(1),G=P(2),H=P(h)，R,S,M,N 恒共线"
            : $"status={o1.Status}/{o2.Status}";

        Console.WriteLine($"[PR-1S] {(ok ? "PASS" : "FAIL")}  {sw.ElapsedMilliseconds,4}ms  {detail}");
        return new BenchmarkResult("PR-1S", "R,S,M,N 共线", "符号退化 Brianchon", ok, detail, sw.ElapsedMilliseconds);
    }

    public static BenchmarkResult FrameInvariantHarmonicCrossRatio()
    {
        var sw = Stopwatch.StartNew();

        // 任取 U=P(2),V=P(5),A=P(3),B=P(-1)：满足 t_U∩t_V∈AB；P=P(7)。
        var u = Primal3<ExactScalar>.Fixed(4, 2);
        var v = Primal3<ExactScalar>.Fixed(25, 5);
        var a = Primal3<ExactScalar>.Fixed(9, 3);
        var b = Primal3<ExactScalar>.Fixed(1, -1);
        var p = Primal3<ExactScalar>.Fixed(49, 7);

        var original = CrossRatioOnUv(u, v, a, b, p);
        if (!original.TryGet(out var crOriginal)
            || (crOriginal + ExactScalar.One).VerifyStrictZero() != StrictResult.StrictlyTrue)
            return Fail("PR-3F", "原配置交比不为 −1");

        // 用 ConicFrame 把 U,V 送到 0,1；第三个参数取 4，避开 A/B/P。
        var frame = ConicFrame.FromThreeParameters(
            ExactScalar.FromInt(2),
            ExactScalar.FromInt(5),
            ExactScalar.FromInt(4)).Value;

        bool frameOk = FrameMapsTo(frame, u, Primal3<ExactScalar>.Fixed(0, 0))
                       && FrameMapsTo(frame, v, Primal3<ExactScalar>.Fixed(1, 1))
                       && FrameSendsToInfinity(frame, ExactScalar.FromInt(4));

        var u2 = frame.ApplyPoint(u).Value;
        var v2 = frame.ApplyPoint(v).Value;
        var a2 = frame.ApplyPoint(a).Value;
        var b2 = frame.ApplyPoint(b).Value;
        var p2 = frame.ApplyPoint(p).Value;

        var transformed = CrossRatioOnUv(u2, v2, a2, b2, p2);
        bool ok = frameOk
                  && transformed.TryGet(out var cr2)
                  && (cr2 + ExactScalar.One).VerifyStrictZero() == StrictResult.StrictlyTrue;

        sw.Stop();
        var detail = ok
            ? "ConicFrame 把 U,V 归一到 0,1 后，(U,V;X,Y) 仍为 −1"
            : frameOk ? "归一化后交比不为 −1" : "ConicFrame 未把 U,V 送到 0,1";

        Console.WriteLine($"[PR-3F] {(ok ? "PASS" : "FAIL")}  {sw.ElapsedMilliseconds,4}ms  {detail}");
        return new BenchmarkResult("PR-3F", "(U,V;X,Y)=−1", "ConicFrame 归一化", ok, detail, sw.ElapsedMilliseconds);
    }

    public static BenchmarkResult FrameInvariantDegeneratePascal()
    {
        var sw = Stopwatch.StartNew();

        // 任意 A=P(2),B=P(5)，割线 M=P(3),N=P(−1) 过两条切线的交点。
        var a = Primal3<ExactScalar>.Fixed(4, 2);
        var b = Primal3<ExactScalar>.Fixed(25, 5);
        var m = Primal3<ExactScalar>.Fixed(9, 3);
        var n = Primal3<ExactScalar>.Fixed(1, -1);

        var frame = ConicFrame.FromThreeParameters(
            ExactScalar.FromInt(2),
            ExactScalar.FromInt(5),
            ExactScalar.FromInt(4)).Value;

        bool frameOk = FrameMapsTo(frame, a, Primal3<ExactScalar>.Fixed(0, 0))
                       && FrameMapsTo(frame, b, Primal3<ExactScalar>.Fixed(1, 1));

        var a2 = frame.ApplyPoint(a).Value;
        var b2 = frame.ApplyPoint(b).Value;
        var m2 = frame.ApplyPoint(m).Value;
        var n2 = frame.ApplyPoint(n).Value;

        var conic = new BilinearForm3<ExactScalar>(
            ExactScalar.Zero,
            ExactScalar.Zero,
            -ExactScalar.FromInt(1) / ExactScalar.FromInt(2),
            ExactScalar.One,
            ExactScalar.Zero,
            ExactScalar.Zero);

        var ta = conic.Polar(a2);
        var tb = conic.Polar(b2);
        var x1 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(a2, m2), tb);
        var y1 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(b2, m2), ta);
        var x2 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(a2, n2), tb);
        var y2 = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(b2, n2), ta);

        var concurrent = SubspaceAlgebra.AreConcurrent(
            SubspaceAlgebra.Join(x1, y1),
            SubspaceAlgebra.Join(x2, y2),
            SubspaceAlgebra.Join(a2, b2));

        var ok = frameOk
                  && concurrent.TryGet(out var verdict)
                  && verdict == StrictResult.StrictlyTrue;
        sw.Stop();
        var detail = ok
            ? "ConicFrame 把 A,B 归一到 0,1 后，退化 Pascal 三线仍共点"
            : frameOk ? "归一化后三线不共点" : "ConicFrame 未把 A,B 送到 0,1";

        Console.WriteLine($"[PR-2F] {(ok ? "PASS" : "FAIL")}  {sw.ElapsedMilliseconds,4}ms  {detail}");
        return new BenchmarkResult("PR-2F", "X1Y1、X2Y2、AB 共点", "ConicFrame + Pascal", ok, detail, sw.ElapsedMilliseconds);
    }

    public static BenchmarkResult FrameInvariantDegenerateBrianchon()
    {
        var sw = Stopwatch.StartNew();

        // 任意 E=P(−1),F=P(2),G=P(5),H=P(7)。
        var e = Primal3<ExactScalar>.Fixed(1, -1);
        var f = Primal3<ExactScalar>.Fixed(4, 2);
        var g = Primal3<ExactScalar>.Fixed(25, 5);
        var h = Primal3<ExactScalar>.Fixed(49, 7);

        // 先把 E,F,G 送到 0,1,∞；再用 2t/(t+1) 把 ∞ 送到 2。
        var first = ConicFrame.FromThreeParameters(
            ExactScalar.FromInt(-1),
            ExactScalar.FromInt(2),
            ExactScalar.FromInt(5)).Value;
        var second = new ConicFrame(
            ExactScalar.FromInt(2), ExactScalar.Zero,
            ExactScalar.One, ExactScalar.One);
        var frame = second.Compose(first).Value;

        bool frameOk = FrameMapsTo(frame, e, Primal3<ExactScalar>.Fixed(0, 0))
                       && FrameMapsTo(frame, f, Primal3<ExactScalar>.Fixed(1, 1))
                       && FrameMapsTo(frame, g, Primal3<ExactScalar>.Fixed(4, 2));

        var e2 = frame.ApplyPoint(e).Value;
        var f2 = frame.ApplyPoint(f).Value;
        var g2 = frame.ApplyPoint(g).Value;
        var h2 = frame.ApplyPoint(h).Value;

        var conic = new BilinearForm3<ExactScalar>(
            ExactScalar.Zero,
            ExactScalar.Zero,
            -ExactScalar.FromInt(1) / ExactScalar.FromInt(2),
            ExactScalar.One,
            ExactScalar.Zero,
            ExactScalar.Zero);

        var te = conic.Polar(e2);
        var tf = conic.Polar(f2);
        var tg = conic.Polar(g2);
        var th = conic.Polar(h2);

        var r = SubspaceAlgebra.Meet(te, tg);
        var s = SubspaceAlgebra.Meet(th, tf);
        var m = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(e2, f2), SubspaceAlgebra.Join(g2, h2));
        var n = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(f2, g2), SubspaceAlgebra.Join(h2, e2));

        var rsm = SubspaceAlgebra.AreCollinear(r, s, m);
        var rsn = SubspaceAlgebra.AreCollinear(r, s, n);
        var ok = frameOk
                  && rsm.TryGet(out var v1) && v1 == StrictResult.StrictlyTrue
                  && rsn.TryGet(out var v2) && v2 == StrictResult.StrictlyTrue;
        sw.Stop();
        var detail = ok
            ? "ConicFrame 复合把 E,F,G 归一到 0,1,2 后，R,S,M,N 仍共线"
            : frameOk ? "归一化后四点不共线" : "ConicFrame 未把 E,F,G 送到 0,1,2";

        Console.WriteLine($"[PR-1F] {(ok ? "PASS" : "FAIL")}  {sw.ElapsedMilliseconds,4}ms  {detail}");
        return new BenchmarkResult("PR-1F", "R,S,M,N 共线", "ConicFrame + Brianchon", ok, detail, sw.ElapsedMilliseconds);
    }

    private static Result<ExactScalar, GeometryError> CrossRatioOnUv(
        Primal3<ExactScalar> u,
        Primal3<ExactScalar> v,
        Primal3<ExactScalar> a,
        Primal3<ExactScalar> b,
        Primal3<ExactScalar> p)
    {
        var uv = SubspaceAlgebra.Join(u, v);
        var x = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(p, a), uv);
        var y = SubspaceAlgebra.Meet(SubspaceAlgebra.Join(p, b), uv);
        return u.CrossRatio(v, x, y);
    }

    private static bool OnParabola(Primal3<ExactScalar> p) =>
        (p.Y * p.Y - p.X * p.W).VerifyStrictZero() == StrictResult.StrictlyTrue;

    private static bool FrameMapsTo(ConicFrame frame, Primal3<ExactScalar> source, Primal3<ExactScalar> target)
    {
        if (!frame.ApplyPoint(source).TryGet(out var image))
            return false;
        return SubspaceAlgebra.SameSubspace(image, target).TryGet(out var verdict)
               && verdict == StrictResult.StrictlyTrue;
    }

    private static bool FrameSendsToInfinity(ConicFrame frame, ExactScalar parameter)
    {
        var applied = frame.ApplyParameter(parameter);
        return applied.IsFailure && applied.Error == GeometryError.AtInfinity;
    }

    private static BenchmarkResult Fail(string id, string detail) =>
        new(id, "(U,V;X,Y)=−1", "交比/极点", false, detail, 0);
}
