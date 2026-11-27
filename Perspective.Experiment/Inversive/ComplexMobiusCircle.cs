using Perspective.Core.Math;
using Perspective.Experiment.Subspace;

namespace Perspective.Experiment.Inversive;

/// <summary>
///     复 Möbius 对广义圆 4 向量的作用。
///     用 Hermitian 矩阵 H=[[A,B],[C,D]]，H' = M^{-*} H M^{-1}。
///     结果允许整体复标量缩放。
/// </summary>
public static class ComplexMobiusCircle
{
    public static Result<GeneralizedCircle<ExactComplex>, GeometryError> Apply(
        this MobiusMap map,
        GeneralizedCircle<ExactComplex> circle)
    {
        if (!map.IsInvertible)
            return Result<GeneralizedCircle<ExactComplex>, GeometryError>.Fail(GeometryError.UndefinedParameter);

        var a = map.A;
        var b = map.B;
        var c = map.C;
        var d = map.D;

        // M^{-*} 与 M^{-1} 去掉公共行列式因子后的矩阵。
        var left = new[,]
        {
            { d.Conjugate(), -c.Conjugate() },
            { -b.Conjugate(), a.Conjugate() }
        };
        var right = new[,]
        {
            { d, -b },
            { -c, a }
        };
        var h = new[,]
        {
            { circle.A, circle.B },
            { circle.C, circle.D }
        };

        var transformed = Multiply(Multiply(left, h), right);
        return Result<GeneralizedCircle<ExactComplex>, GeometryError>.Ok(
            new GeneralizedCircle<ExactComplex>(
                transformed[0, 0], transformed[0, 1],
                transformed[1, 0], transformed[1, 1]));
    }

    /// <summary>复广义圆在 CP¹ 点上的取值：A|z|²+Bz+Cz̄+D。</summary>
    public static ExactComplex EvalAt(this GeneralizedCircle<ExactComplex> circle, HComplex p)
    {
        if (p.W.VerifyStrictZero() == StrictResult.StrictlyTrue)
            return circle.A;

        var z = p.Z / p.W;
        var zBar = z.Conjugate();
        return circle.A * z * zBar + circle.B * z + circle.C * zBar + circle.D;
    }

    private static ExactComplex[,] Multiply(ExactComplex[,] a, ExactComplex[,] b)
    {
        var result = new ExactComplex[2, 2];
        for (var i = 0; i < 2; i++)
        for (var j = 0; j < 2; j++)
            result[i, j] = a[i, 0] * b[0, j] + a[i, 1] * b[1, j];
        return result;
    }
}
