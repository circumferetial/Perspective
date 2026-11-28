using Perspective.Core.Math;
using Perspective.Experiment.Subspace;
using Perspective.Oracle.Z3;

namespace Perspective.Experiment.Z3;

/// <summary>
///     两个有理 conic 的 pencil：det(Q1 + λQ2)=0。
///     当前只做“求退化成员 λ”，不做直线分解/交点构造。
/// </summary>
public sealed class ConicPencil
{
    public BilinearForm3<BigRational> Q1 { get; }
    public BilinearForm3<BigRational> Q2 { get; }

    private ConicPencil(BilinearForm3<BigRational> q1, BilinearForm3<BigRational> q2)
    {
        Q1 = q1;
        Q2 = q2;
    }

    public static ConicPencil FromConics(
        BilinearForm3<BigRational> q1,
        BilinearForm3<BigRational> q2) => new(q1, q2);

    /// <summary>det(Q1 + λQ2) 的系数，升序：c0 + c1λ + c2λ² + c3λ³。</summary>
    public IReadOnlyList<BigRational> DeterminantCoefficients()
    {
        var d0 = DeterminantAt(BigRational.Zero);
        var d1 = DeterminantAt(BigRational.One);
        var dm1 = DeterminantAt(-BigRational.One);
        var d2 = DeterminantAt(BigRational.FromLong(2));

        var e1 = d1 - d0;
        var em1 = dm1 - d0;
        var e2 = d2 - d0;

        var c2 = (e1 + em1) / BigRational.FromLong(2);
        var c3 = (e2 - (e1 - em1) - BigRational.FromLong(4) * c2)
                 / BigRational.FromLong(6);
        var c1 = (e1 - em1) / BigRational.FromLong(2) - c3;

        return [d0, c1, c2, c3];
    }

    /// <summary>用 Z3 RCF 找 det(Q1+λQ2)=0 的全部实根。</summary>
    public IReadOnlyList<RcfNum> FindDegenerateLambdas(Z3RcfContext context)
    {
        var coefficients = DeterminantCoefficients();
        var rcfCoefficients = new List<RcfNum>(coefficients.Count);
        try
        {
            foreach (var coefficient in coefficients)
                rcfCoefficients.Add(context.Rational(coefficient.ToString()));

            return context.FindRealRoots(rcfCoefficients);
        }
        finally
        {
            foreach (var coefficient in rcfCoefficients)
                coefficient.Dispose();
        }
    }

    private BigRational DeterminantAt(BigRational lambda)
    {
        var a = Q1.Q00 + lambda * Q2.Q00;
        var b = Q1.Q01 + lambda * Q2.Q01;
        var c = Q1.Q02 + lambda * Q2.Q02;
        var d = Q1.Q11 + lambda * Q2.Q11;
        var e = Q1.Q12 + lambda * Q2.Q12;
        var f = Q1.Q22 + lambda * Q2.Q22;

        return a * (d * f - e * e)
               - b * (b * f - e * c)
               + c * (b * e - d * c);
    }
}
