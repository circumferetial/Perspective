using Perspective.Core.Geometry;
using Perspective.Core.Math;

namespace Perspective.Experiment;

/// <summary>
/// 实验：把点/线统一成"三维向量空间中的子空间"，交/并由同一个楔积完成。
/// 这是射影对偶最朴素的实现：点 = 1 维子空间，线 = 对偶空间里的 1 维子空间。
/// </summary>
public readonly record struct Vec3(ExactScalar X, ExactScalar Y, ExactScalar Z)
{
    public static Vec3 Point(ExactScalar x, ExactScalar y) => new(x, y, 1);

    /// <summary>楔积：两个点 → 过它们的直线（对偶向量）；两条直线 → 交点。</summary>
    public static Vec3 Wedge(Vec3 a, Vec3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);

    /// <summary>关联关系：点 p 在直线 l 上 ⇔ l·p = 0（对偶空间配对）。</summary>
    public bool Incident(Vec3 other)
    {
        var dot = X * other.X + Y * other.Y + Z * other.Z;
        return dot.VerifyStrictZero() == StrictResult.StrictlyTrue;
    }
}

/// <summary>把"对偶侧"的对象标出来，避免点/线混用（类型安全靠包装而非运行时）。</summary>
public readonly record struct Dual(Vec3 V);

public static class Incidence
{
    /// <summary>两点连线（点 → 对偶对象）。</summary>
    public static Dual Join(Vec3 p, Vec3 q) => new(Vec3.Wedge(p, q));

    /// <summary>两线交点（对偶对象 → 点）。同一个楔积，方向相反。</summary>
    public static Vec3 Meet(Dual l1, Dual l2) => Vec3.Wedge(l1.V, l2.V);
}

public static class Program
{
    public static void Main()
    {
        // 单位圆上的两点
        var p = Vec3.Point(1, 0);
        var q = Vec3.Point(0, 1);

        // 连线：点 → 线（对偶）
        var chord = Incidence.Join(p, q);
        Console.WriteLine($"p、q 连线 = ({chord.V.X}, {chord.V.Y}, {chord.V.Z})");
        Console.WriteLine($"p 在连线上? {p.Incident(chord.V)}；q 在连线上? {q.Incident(chord.V)}");

        // 交点：线 → 点（同一个楔积）
        var another = new Dual(new Vec3(ExactScalar.FromInt(1), -ExactScalar.FromInt(1),
            ExactScalar.FromInt(0)));   // x − y = 0（与连线不同）
        var intersection = Incidence.Meet(chord, another);
        Console.WriteLine($"两线交点 = ({intersection.X}, {intersection.Y}, {intersection.Z})" +
                          "（平行时为无穷远点，W=0 自动出现）");

        // 圆锥曲线的极线：二次型对点的作用（仍是同一个线性代数）
        var conic = Conic.Circle(0, 0, ExactScalar.FromInt(1));
        var polar = conic.Matrix * new HPoint(p.X, p.Y, p.Z);
        Console.WriteLine($"圆上点 p 的极线 = ({polar.X}, {polar.Y}, {polar.W})（即切线，对偶映射）");

        // 直线系 / 曲线系：线性组合（约束的"线性系"）
        var l1 = Incidence.Join(Vec3.Point(1, 0), Vec3.Point(0, 1));
        var l2 = Incidence.Join(Vec3.Point(1, 0), Vec3.Point(0, 2));
        var pencil = new Vec3(l1.V.X + l2.V.X, l1.V.Y + l2.V.Y, l1.V.Z + l2.V.Z);
        Console.WriteLine($"线系 λ=μ=1 的元素 = ({pencil.X}, {pencil.Y}, {pencil.Z})，过公共点 (1,0)? " +
                          $"{Vec3.Point(1, 0).Incident(pencil)}");
    }
}
