using Perspective.Core.Math;

namespace Perspective.Core.Geometry;

public enum LineConicRelation
{
    Tangent,         // 相切
    Secant,          // 相交（2个实交点）
    Disjoint,        // 相离（无实交点）
    DegenerateConic  // 圆锥曲线已退化（例如退化成两条直线）
}

public readonly record struct LineConicCheck(LineConicRelation Relation, StrictResult Certainty);

/// <summary>
/// 圆锥曲线：用 3x3 对称矩阵 C 表示方程 p^T * C * p = 0
/// </summary>
public readonly record struct Conic(Matrix3x3 Matrix)
{
    /// <summary>
    /// 从一般二次方程 Ax^2 + Bxy + Cy^2 + Dx + Ey + F = 0 构造
    /// </summary>
    public static Conic FromGeneralEquation(
        ExactScalar a, ExactScalar b, ExactScalar c,
        ExactScalar d, ExactScalar e, ExactScalar f)
    {
        var half = ExactScalar.FromFormula("1/2");
        return new Conic(new Matrix3x3(
            a,        b * half, d * half,
            b * half, c,        e * half,
            d * half, e * half, f));
    }

    /// <summary>标准椭圆：x^2/a^2 + y^2/b^2 = 1（化为 b^2*x^2 + a^2*y^2 - a^2*b^2 = 0，避免多余分母）</summary>
    public static Conic Ellipse(ExactScalar a, ExactScalar b)
    {
        var a2 = a * a;
        var b2 = b * b;
        return new Conic(new Matrix3x3(
            b2, 0,  0,
            0,  a2, 0,
            0,  0,  -(a2 * b2)));
    }

    /// <summary>标准双曲线：x^2/a^2 - y^2/b^2 = 1</summary>
    public static Conic Hyperbola(ExactScalar a, ExactScalar b)
    {
        var a2 = a * a;
        var b2 = b * b;
        return new Conic(new Matrix3x3(
            b2, 0,   0,
            0,  -a2, 0,
            0,  0,   -(a2 * b2)));
    }

    /// <summary>标准抛物线：y^2 = 2px</summary>
    public static Conic Parabola(ExactScalar p) => new(new Matrix3x3(
        0,  0, -p,
        0,  1,  0,
        -p, 0,  0));

    /// <summary>圆锥曲线系：lambda * C1 + mu * C2（过 C1 与 C2 所有公共交点的曲线系）</summary>
    public static Conic Pencil(Conic c1, Conic c2, ExactScalar lambda, ExactScalar mu)
    {
        var m1 = c1.Matrix;
        var m2 = c2.Matrix;
        return new Conic(new Matrix3x3(
            lambda * m1.M00 + mu * m2.M00, lambda * m1.M01 + mu * m2.M01, lambda * m1.M02 + mu * m2.M02,
            lambda * m1.M10 + mu * m2.M10, lambda * m1.M11 + mu * m2.M11, lambda * m1.M12 + mu * m2.M12,
            lambda * m1.M20 + mu * m2.M20, lambda * m1.M21 + mu * m2.M21, lambda * m1.M22 + mu * m2.M22));
    }


    
    /// <summary>
    /// 极线 / 切线 / 切点弦：l = C * p
    /// 【前提】C 非退化；p 为任意射影点。
    /// 【结论】l = C·p 是 p 关于 C 的极线。
    /// 【推导】定义即极线的双线性配极 ⟨X, p⟩ := Xᵀ(Cp)；p 在曲线上时 pᵀCp=0，
    ///         极线过 p 且与曲线在 p 处相切（切线方程）；p 在曲线外时，
    ///         由 La Hire 定理，从 p 所引两条切线的切点 A、B 满足 Aᵀ(Cp)=Bᵀ(Cp)=0，
    ///         故极线 = 切点弦 AB。
    /// </summary>
    public HLine PolarLine(HPoint p) => new(
        A: Matrix.M00 * p.X + Matrix.M01 * p.Y + Matrix.M02 * p.W,
        B: Matrix.M10 * p.X + Matrix.M11 * p.Y + Matrix.M12 * p.W,
        C: Matrix.M20 * p.X + Matrix.M21 * p.Y + Matrix.M22 * p.W
    );

    /// <summary>严格检验：点 p 是否在圆锥曲线上？（几何等价于：点 p 在自己的极线上）</summary>
    public StrictResult ContainsStrictly(HPoint p)
    {
        var polar = PolarLine(p);
        ExactScalar residual = p.X * polar.A + p.Y * polar.B + p.W * polar.C;
        return residual.VerifyStrictZero();
    }

    /// <summary>
    /// 【严通道】严格判断直线与圆锥曲线的位置关系（相切 / 相交 / 相离）
    /// 【前提】C 非退化（本方法先检验 det(C)=0，退化时返回 DegenerateConic）。
    /// 【结论】disc = lᵀ·adj(C)·l 的符号决定位置：disc &lt; 0 ⇔ 相交（2 实交点），
    ///         disc = 0 ⇔ 相切，disc &gt; 0 ⇔ 相离。该符号约定对圆 / 椭圆 / 双曲线 / 抛物线一致。
    /// 【推导】沿直线参数化 X = X₀ + t·v，代入 XᵀCX 得关于 t 的二次方程，
    ///         其判别式 Δ ∝ lᵀ·adj(C)·l（比例因子为正的 |v|² 项）；
    ///         Δ 的符号即二次方程实根个数的判据。符号方向已对三类曲线符号验证。
    /// </summary>
    public LineConicCheck CheckLineRelation(HLine line)
    {
        // 先检查曲线本身是不是已经退化了（行列式为 0）
        var detCheck = Matrix.Determinant().VerifyStrictZero();
        if (detCheck == StrictResult.StrictlyTrue)
            return new LineConicCheck(LineConicRelation.DegenerateConic, StrictResult.StrictlyTrue);

        var adj = Matrix.Adjugate();
        var adjL = adj * line;
        ExactScalar disc = line.A * adjL.A + line.B * adjL.B + line.C * adjL.C;

        var zeroCheck = disc.VerifyStrictZero();

        if (zeroCheck == StrictResult.StrictlyTrue)
            return new LineConicCheck(LineConicRelation.Tangent, StrictResult.StrictlyTrue);

        if (zeroCheck == StrictResult.StrictlyFalse)
        {
            // 若极小且有符号基因，先让符号化简消除前面大数抵消的误差，再取精确符号方向
            double signVal = (disc.Exact is not null && System.Math.Abs(disc.Approx) <= 1e-4)
                ? (double)disc.Exact.Simplify().EvalNumerical()
                : disc.Approx;

            var rel = signVal < 0 ? LineConicRelation.Secant : LineConicRelation.Disjoint;
            return new LineConicCheck(rel, StrictResult.StrictlyTrue);
        }

        // 兜底：无符号数据（如鼠标自由点）或极端复杂公式未能化简
        var approxRel = disc.IsNumericallyZero()
            ? LineConicRelation.Tangent
            : (disc.Approx < 0 ? LineConicRelation.Secant : LineConicRelation.Disjoint);

        return new LineConicCheck(approxRel, zeroCheck);
    }

    /// <summary>把化简后的曲线方程友好地打印出来</summary>
    public string ToEquationString()
    {
        string F(ExactScalar s) => s.Exact is not null ? s.Exact.Simplify().ToString() : $"{s.Approx:G4}";
        var two = ExactScalar.FromInt(2);
        return $"({F(Matrix.M00)})x^2 + ({F(two * Matrix.M01)})xy + ({F(Matrix.M11)})y^2 + ({F(two * Matrix.M02)})x + ({F(two * Matrix.M12)})y + ({F(Matrix.M22)}) = 0";
    }
    
    /// <summary>
    /// 已知圆锥曲线上一点 knownPoint，以及过该点的直线 line，
    /// 直接算出【另一个交点】（纯多项式运算，免韦达定理，完美保留符号基因！）
    /// 【前提】P = knownPoint 在曲线上（PᵀCP = 0）；R 为 line 上一点且 RᵀCR ≠ 0
    ///         （本方法自动选与 P 最不重合的直线上点，R 恰在曲线上时直接返回 R）。
    /// 【结论】Q = (RᵀCR)·P − 2·(PᵀCR)·R（齐次向量组合，比例因子任意）。
    /// 【推导】直线上的点可写为 X = P + s·R。代入：
    ///         XᵀCX = PᵀCP + 2s·PᵀCR + s²·RᵀCR = 2s·mCr + s²·rCr（用 PᵀCP = 0）
    ///         = s·(2·mCr + s·rCr)。两根为 s = 0（即 P）与 s = −2·mCr/rCr。
    ///         故另一交点 ∝ P − (2mCr/rCr)·R，齐次地即 rCr·P − 2mCr·R。
    ///         已符号验证：Q 满足 QᵀCQ = 0 且与 P、R 共线（一般斜率符号 t）。
    /// </summary>
    public HPoint OtherIntersection(HPoint knownPoint, HLine line)
    {
        HPoint[] candidates =
        [
            new(0, line.C, -line.B),
            new(-line.C, 0, line.A),
            new(line.B, -line.A, 0)
        ];

        // 挑一个与 knownPoint 绝对不重合的直线上点 R
        HPoint r = candidates.MaxBy(pt =>
        {
            double cx = knownPoint.Y.Approx * pt.W.Approx - knownPoint.W.Approx * pt.Y.Approx;
            double cy = knownPoint.W.Approx * pt.X.Approx - knownPoint.X.Approx * pt.W.Approx;
            double cw = knownPoint.X.Approx * pt.Y.Approx - knownPoint.Y.Approx * pt.X.Approx;
            return cx * cx + cy * cy + cw * cw;
        });

        var polarR = PolarLine(r);
        ExactScalar rCr = r.X * polarR.A + r.Y * polarR.B + r.W * polarR.C;

        // 若选中的 R 恰在曲线上（rCr 严格为 0），公式分母为零：
        // 此时直线与曲线的两个交点正是 knownPoint 与 R，直接返回 R 作为“另一个交点”。
        if (rCr.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return r;

        ExactScalar mCr = knownPoint.X * polarR.A + knownPoint.Y * polarR.B + knownPoint.W * polarR.C;

        ExactScalar two = 2;
        return new HPoint(
            rCr * knownPoint.X - two * mCr * r.X,
            rCr * knownPoint.Y - two * mCr * r.Y,
            rCr * knownPoint.W - two * mCr * r.W);
    }
    
    /// <summary>构造圆：(x - cx)^2 + (y - cy)^2 = r^2</summary>
    public static Conic Circle(ExactScalar cx, ExactScalar cy, ExactScalar r) =>
        FromGeneralEquation(1, 0, 1, -2 * cx, -2 * cy, cx * cx + cy * cy - r * r);

    /// <summary>
    /// 【免开方大杀器 1：齐次化双线组】
    /// 当直线 l 交曲线 C 于未知两点 A, B 时，直接算出两直线 PA, PB 构成的二次型矩阵（零根号！）
    /// 【前提】p 为任意射影点（可在曲线上，此时 c₀ = 0，公式仍成立）；l 与 C 交于两点（实或复）。
    /// 【结论】D = s²·C − s·(l qᵀ + q lᵀ) + c₀·(l lᵀ)，其中 s = lᵀp，q = Cp（极线），c₀ = pᵀCp；
    ///         D 与线对 (pA ∪ pB) 的对称化矩阵成比例。
    /// 【推导】X 在 pA 或 pB 上 ⇔ 直线 pX 与 C 的某个交点落在 l 上。设该交点为
    ///         λp + μX，代入得二次方程 λ²c₀ + 2λμ(qᵀX) + μ²(XᵀCX) = 0，且要求
    ///         lᵀ(λp + μX) = λs + μ(lᵀX) = 0。消去 (λ:μ)：
    ///         c₀(lᵀX)² − 2s(lᵀX)(qᵀX) + s²(XᵀCX) = 0，
    ///         展开即上面的矩阵 D（每项对称化）。p 为奇点（梯度为零），
    ///         故方向二次型与 p 的位置无关，斜率只读 M00/M01/M11。
    ///         已符号验证：D ∝ (pA ∪ pB) 的对称化线对矩阵，含 p 在曲线上的退化情形。
    /// </summary>
    public LinePairInfo LinePairFrom(HPoint p, HLine l)
    {
        var s = l.A * p.X + l.B * p.Y + l.C * p.W;
        var q = PolarLine(p);
        var c0 = p.X * q.A + p.Y * q.B + p.W * q.C;
        var s2 = s * s;

        var d = new Matrix3x3(
            s2 * Matrix.M00 - s * (l.A * q.A + q.A * l.A) + c0 * l.A * l.A,
            s2 * Matrix.M01 - s * (l.A * q.B + q.A * l.B) + c0 * l.A * l.B,
            s2 * Matrix.M02 - s * (l.A * q.C + q.A * l.C) + c0 * l.A * l.C,

            s2 * Matrix.M10 - s * (l.B * q.A + q.B * l.A) + c0 * l.B * l.A,
            s2 * Matrix.M11 - s * (l.B * q.B + q.B * l.B) + c0 * l.B * l.B,
            s2 * Matrix.M12 - s * (l.B * q.C + q.B * l.C) + c0 * l.B * l.C,

            s2 * Matrix.M20 - s * (l.C * q.A + q.C * l.A) + c0 * l.C * l.A,
            s2 * Matrix.M21 - s * (l.C * q.B + q.C * l.B) + c0 * l.C * l.B,
            s2 * Matrix.M22 - s * (l.C * q.C + q.C * l.C) + c0 * l.C * l.C
        );
        return new LinePairInfo(d);
    }

    /// <summary>
    /// 【免开方大杀器 2：弦中点】
    /// 直线 line 与曲线交于 A, B 两点，直接求线段 AB 的中点（零根号，全二次曲线通用！）
    /// 【前提】C 非退化（抛物线亦可：此时"中心"为无穷远点，直径平行于轴）。
    /// 【结论】M = 直径(center, pole) ∩ line，其中 center = adj(C)·(0,0,1)（无穷远直线的极点，
    ///         即仿射中心），pole = adj(C)·line（弦线的极点，两切线交点）。
    /// 【推导】Newton 直径定理：一组平行弦的中点轨迹是一条直径，且该直径过中心与
    ///         弦方向的极点。等价判据：M 为弦中点 ⇔ M ∈ line 且方向共轭 vᵀ(CM) = 0
    ///         （v = (−B, A) 为弦方向）。中心与极点连线恰为该共轭直径，
    ///         故其与弦的交点即中点。
    ///         已符号验证（符号 k）：M ∈ line 与 vᵀ(CM) = 0 同时恒成立。
    /// </summary>
    public HPoint ChordMidpoint(HLine line)
    {
        var adj = Matrix.Adjugate();
        // 曲线中心（无穷远直线的极点）：adj * (0,0,1)^T
        var center = new HPoint(adj.M02, adj.M12, adj.M22);
        // 弦所在直线的极点（两条切线的交点）：adj * line
        var pole = adj * line;
        var polePt = new HPoint(pole.A, pole.B, pole.C);
        // 连接“中心”与“极点”的直径，与弦的交点就是弦中点！
        var diameter = HLine.FromPoints(center, polePt);
        return HLine.Intersect(diameter, line);
    }

    /// <summary>
    /// 【两圆/同形二次曲线的根轴（公共弦所在直线）】（零根号！）
    /// 【前提】两曲线的二次部分成比例——圆恒满足（M00 = M11 = 1, M01 = 0）；
    ///         一般二次曲线不满足，此时本公式只消去 x² 项、y² 与 xy 项残留，结果不是直线，不适用！
    /// 【结论】l = k₁·C₁ − k₂·C₂ 的线性部分（k₁ = C₂.M00, k₂ = C₁.M00）。
    /// 【推导】根轴是 |PX|² − r₁² = |PX|² − r₂² 的等值线。两圆方程相减时二次项
    ///         x² + y² 恰好抵消，剩下的线性部分即根轴：
    ///         2(c−a)x + 2(d−b)y + (a²+b²−r₁²) − (c²+d²−r₂²) = 0。
    ///         矩阵语言：C₁.M00 = C₂.M00 ⟹ k₁·C₁ − k₂·C₂ 的二次部分为零。
    ///         已符号验证（符号参数 a,b,r,c,d,s）：与标准根轴逐系数恒等。
    /// </summary>
    public static HLine RadicalAxis(Conic c1, Conic c2)
    {
        // 消去 x^2 项：k1 = c2.M00, k2 = c1.M00
        var m1 = c1.Matrix;
        var m2 = c2.Matrix;
        var k1 = m2.M00;
        var k2 = m1.M00;
        ExactScalar two = 2;

        return new HLine(
            A: two * (k1 * m1.M02 - k2 * m2.M02),
            B: two * (k1 * m1.M12 - k2 * m2.M12),
            C: k1 * m1.M22 - k2 * m2.M22
        );
    }

    /// <summary>
    /// 【通用直线与圆锥曲线求交】（带精确根号基因，沿直线方向有序返回 0/1/2 个交点）
    /// </summary>
    public HPoint[] Intersect(HLine line)
    {
        var n2 = line.A * line.A + line.B * line.B;
        if (n2.IsNumericallyZero()) return [];

        // p1 是直线上离原点最近的有限点，p2 是直线的方向（无穷远点）
        var p1 = new HPoint(-line.A * line.C, -line.B * line.C, n2);
        var p2 = new HPoint(-line.B, line.A, 0);

        var cp1 = PolarLine(p1);
        var cp2 = PolarLine(p2);

        var a = p2.X * cp2.A + p2.Y * cp2.B + p2.W * cp2.C;
        var b = p1.X * cp2.A + p1.Y * cp2.B + p1.W * cp2.C;
        var c = p1.X * cp1.A + p1.Y * cp1.B + p1.W * cp1.C;

        if (a.IsNumericallyZero())
        {
            if (b.IsNumericallyZero()) return [];
            var s = -c / (2 * b);
            return [new HPoint(p1.X + s * p2.X, p1.Y + s * p2.Y, p1.W + s * p2.W)];
        }

        var deltaHalf = b * b - a * c;
        var zeroCheck = deltaHalf.VerifyStrictZero();

        if (zeroCheck == StrictResult.StrictlyTrue)
        {
            var s = -b / a;
            return [new HPoint(p1.X + s * p2.X, p1.Y + s * p2.Y, p1.W + s * p2.W)];
        }

        if (deltaHalf.Approx < 0) return [];

        var root = ExactScalar.Sqrt(deltaHalf);
        var s1 = (-b - root) / a;
        var s2 = (-b + root) / a;
        if (s1.Approx > s2.Approx) (s1, s2) = (s2, s1); // 保证沿方向 (-B, A) 顺序稳定，不乱跳

        return
        [
            new HPoint(p1.X + s1 * p2.X, p1.Y + s1 * p2.Y, p1.W + s1 * p2.W),
            new HPoint(p1.X + s2 * p2.X, p1.Y + s2 * p2.Y, p1.W + s2 * p2.W)
        ];
    }
    
    /// <summary>返回直线与曲线的二次判别式 l^T * adj(C) * l（严格小于 0 代表有 2 个不同实交点）</summary>
    public ExactScalar LineRelationDiscriminant(HLine line)
    {
        var adjL = Matrix.Adjugate() * line;
        return line.A * adjL.A + line.B * adjL.B + line.C * adjL.C;
    }
}

/// <summary>
/// 齐次化双线组信息：直接读取两条未知连线 PA, PB 的斜率之和/之积
/// 【前提】Matrix 来自 <see cref="Conic.LinePairFrom"/>，即过 p 的线对（p 为奇点）。
/// 【推导】因 p 是奇点（D·p = 0），平移到 p 后二次型不变：设方向 (1, m, 0)，
///         线对方程 M₁₁m² + 2M₀₁m + M₀₀ = 0。韦达定理：
///         m₁ + m₂ = −2M₀₁/M₁₁，m₁·m₂ = M₀₀/M₁₁；垂直 ⇔ m₁m₂ = −1 ⇔ M₀₀ + M₁₁ = 0。
///         注意 M₁₁ = 0 时必有竖直方向，斜率和/积无定义（除法由 ExactScalar 守卫）。
/// </summary>
public readonly record struct LinePairInfo(Matrix3x3 Matrix)
{
    public ExactScalar SlopeSum => -(2 * Matrix.M01) / Matrix.M11;
    public ExactScalar SlopeProduct => Matrix.M00 / Matrix.M11;
    public StrictResult ArePerpendicular() => (Matrix.M00 + Matrix.M11).VerifyStrictZero();
}