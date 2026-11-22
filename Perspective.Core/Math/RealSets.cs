using AngouriMath;
using AngouriMath.Core;

namespace Perspective.Core.Math;

/// <summary>
/// 一元实解集（区间并集）的集合代数工具。
/// <para>
/// 【为什么需要它】"像人一样理解范围"需要三件事：**表示**（区间/有限点/并集）、
/// **集合代数**（并/交/差）与**精确成员判定**。这三件事 AngouriMath 已经提供，
/// 本类只做薄封装，固定两个项目内约定（全集 = ℝ；非退化参数要抠掉），避免各处重复。
/// </para>
/// <para>
/// 【不自己写】区间合并、端点排序、开闭端点判断一律委托
/// <c>MathS.Union</c> / <c>MathS.Intersection</c> / <c>MathS.Sets.Complement</c>；
/// 成员判定委托 <c>MathS.Sets.ElementInSet</c> + <c>Simplify</c>。
/// 端点可以保留精确代数式（如 1±√2），是否可比较由符号引擎决定。
/// </para>
/// </summary>
public static class RealSets
{
    /// <summary>实数全集 ℝ。</summary>
    public static Entity.Set RealNumbers => Entity.Set.SpecialSet.Create(Domain.Real);

    /// <summary>空集。</summary>
    public static Entity.Set Empty => Entity.Set.Empty;

    /// <summary>解一个关于 <paramref name="variable"/> 的（不）等式，返回实解集。</summary>
    public static Entity.Set Solve(Entity statement, Entity.Variable variable) => statement.Solve(variable);

    /// <summary>并集（多个取值范围取并）。</summary>
    public static Entity.Set Union(Entity.Set a, Entity.Set b) => (Entity.Set)MathS.Union(a, b).Simplify();

    /// <summary>交集（多个约束同时成立）。</summary>
    public static Entity.Set Intersect(Entity.Set a, Entity.Set b) => (Entity.Set)MathS.Intersection(a, b).Simplify();

    /// <summary>差集 A \ B（在 ℝ 内取补）——NDG 就是"把若干参数值从解集中抠掉"。</summary>
    public static Entity.Set Subtract(Entity.Set a, Entity.Set b) =>
        (Entity.Set)MathS.Intersection(a, MathS.Sets.Complement(b, RealNumbers)).Simplify();

    /// <summary>从解集中抠掉若干精确点（如斜率无定义的参数值）。</summary>
    public static Entity.Set SubtractPoints(Entity.Set a, params ExactScalar[] points)
    {
        if (points.Length == 0) return a;
        var finite = MathS.Sets.Finite(points.Select(p => p.Exact!).ToArray());
        return Subtract(a, finite);
    }

    /// <summary>
    /// 成员判定的三态结果：严格在集合内 / 严格不在 / 无法化简（集合表达式没化成布尔）。
    /// </summary>
    public static StrictResult Contains(Entity.Set set, ExactScalar value)
    {
        if (value.Exact is null) return StrictResult.NoSymbolicData;

        var result = MathS.Sets.ElementInSet(value.Exact, set).Simplify();
        if (result.Equals(Entity.Boolean.True)) return StrictResult.StrictlyTrue;
        if (result.Equals(Entity.Boolean.False)) return StrictResult.StrictlyFalse;
        return StrictResult.NoSymbolicData;
    }

    /// <summary>人类可读的区间记法（含一次化简）。</summary>
    public static string Describe(Entity.Set set) => set.Simplify().ToString();
}
