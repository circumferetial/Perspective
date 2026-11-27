using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Jet = Perspective.Experiment.Subspace.DualNumber<Perspective.Core.Math.ExactScalar>;
using PointJ = Perspective.Experiment.Subspace.Primal3<
    Perspective.Experiment.Subspace.DualNumber<Perspective.Core.Math.ExactScalar>>;
using LineJ = Perspective.Experiment.Subspace.Dual3<
    Perspective.Experiment.Subspace.DualNumber<Perspective.Core.Math.ExactScalar>>;
using ConicJ = Perspective.Experiment.Subspace.BilinearForm3<
    Perspective.Experiment.Subspace.DualNumber<Perspective.Core.Math.ExactScalar>>;

namespace Perspective.Experiment.Subspace.Demos;

/// <summary>
///     抛物列演示：对偶数入环后，泛型骨架不需要任何改动就能表达“点 + 速度”，
///     于是“相切”变成约束，构造的导数自动传播。
/// </summary>
public static class ParabolicGeometryDemo
{
    private static Jet C(int v) => Jet.FromReal(ExactScalar.FromInt(v));
    private static Jet J(int real, int eps) => Jet.FromJet(ExactScalar.FromInt(real), ExactScalar.FromInt(eps));
    private static void Say(string message) => Console.WriteLine("  " + message);

    public static void Run()
    {
        Console.WriteLine("\n================ 抛物列：对偶数入环（ε² = 0） ================");

        // [1] 环公理：ε² = 0，乘法带乘积法则
        var a = J(1, 2);
        var b = J(3, 4);
        Say($"[1] (1+2ε)(3+4ε) = {a * b}（应为 3 + 10ε）");
        var epsSq = J(0, 1) * J(0, 1);
        Say($"[1] ε·ε = {epsSq}，严格为零？{epsSq.VerifyZero()}");

        // [2] 相切 = 约束：Q(p)=0 同时含“点在曲线上”和“速度沿切线”
        var circle = new ConicJ(C(1), C(0), C(0), C(1), C(0), C(-1)); // x² + y² − w²
        var pTangent = new PointJ(J(1, 0), J(0, 1), J(1, 0)); // P=(1,0,1)，速度 (0,1)
        var pNormal = new PointJ(J(1, 1), J(0, 0), J(1, 0)); // P=(1,0,1)，速度 (1,0)

        var evalTangent = circle.Eval(pTangent);
        var evalNormal = circle.Eval(pNormal);
        Say($"[2] 切向速度 (0,1)：Q(p) = {evalTangent}，为零？{evalTangent.VerifyZero()}");
        Say($"[2] 法向速度 (1,0)：Q(p) = {evalNormal}，为零？{evalNormal.VerifyZero()}");

        // [3] 构造的导数自动传播：Join(P+εv, P) 的 ε 部就是过 P 的方向直线
        var p0 = new PointJ(C(1), C(0), C(1));
        var join = SubspaceAlgebra.Join(pTangent, p0);
        var realIsZero = SubspaceAlgebra.AllZero(join.A.Real, join.B.Real, join.C.Real);
        var autoLine = new Dual3<ExactScalar>(join.A.Eps, join.B.Eps, join.C.Eps);
        var polar = new BilinearForm3<ExactScalar>(1, 0, 0, 1, 0, -1)
            .Polar(Primal3<ExactScalar>.Fixed(1, 0));
        Say($"[3] Join(P+εv, P) = {join}");
        Say($"[3]   实部为零？{realIsZero}；ε 部自动给出直线 {autoLine}");
        Say($"[3]   与 Polar 切线 {polar} 同一条？{SubspaceAlgebra.SameSubspace(autoLine, polar).Value}");

        // [4] 运动约束：速度沿直线/离开直线，同一条 Pair 约束给出不同答案
        var xLine = new LineJ(C(1), C(0), C(-1)); // x = w
        Say($"[4] 点在 x=w 上、速度沿直线？{SubspaceAlgebra.Pair(xLine, pTangent).VerifyZero()}");
        Say($"[4] 点在 x=w 上、速度离开直线？{SubspaceAlgebra.Pair(xLine, pNormal).VerifyZero()}");

        // [5] 同一套 GeometryLens<Jet> 直接工作：中点构造自动带上速度
        var q0 = new PointJ(C(0), C(1), C(1)); // (0,1,1)，也在圆上
        var mid = GeometryLens<Jet>.EuclideanXoy.Affine.Midpoint(pTangent, q0).Value;
        Say($"[5] GeometryLens<DualNumber<ExactScalar>>.Midpoint 自动带速度：{mid}");

        // [6] 抛物列算子：平移（对偶单位群 1+εt 的 2D 版本）
        var translation = new UnitTranslation<ExactScalar>(ExactScalar.FromInt(3), ExactScalar.FromInt(4));
        var moved = translation.Apply(Primal3<ExactScalar>.Fixed(1, 2));
        var back = translation.Compose(translation.Inverse()).Apply(Primal3<ExactScalar>.Fixed(1, 2));
        Say($"[6] 平移 (3,4) 作用于 (1,2)：{moved}");
        Say($"[6] 复合逆平移回到 (1,2)？{SubspaceAlgebra.SameSubspace(back, Primal3<ExactScalar>.Fixed(1, 2)).Value}");
    }
}
