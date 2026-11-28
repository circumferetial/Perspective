using System.Runtime.InteropServices;

namespace Perspective.Oracle.Z3;

/// <summary>
///     Z3 实闭域（RCF）的最小托管包装。
///     只覆盖有限根式需要的操作：整数/有理数、+−×、sqrt、精确比较。
/// </summary>
public sealed class Z3RcfContext : IDisposable
{
    private IntPtr _context;

    public Z3RcfContext()
    {
        var config = Z3RcfNative.Z3_mk_config();
        _context = Z3RcfNative.Z3_mk_context(config);
        Z3RcfNative.Z3_del_config(config);
    }

    public IntPtr Handle => _context;

    public RcfNum FromInt(int value) =>
        new(this, Z3RcfNative.Z3_rcf_mk_small_int(_context, value));

    public RcfNum Rational(string value) =>
        new(this, Z3RcfNative.Z3_rcf_mk_rational(_context, value));

    public RcfNum Add(RcfNum a, RcfNum b) =>
        new(this, Z3RcfNative.Z3_rcf_add(_context, a.Handle, b.Handle));

    public RcfNum Sub(RcfNum a, RcfNum b) =>
        new(this, Z3RcfNative.Z3_rcf_sub(_context, a.Handle, b.Handle));

    public RcfNum Mul(RcfNum a, RcfNum b) =>
        new(this, Z3RcfNative.Z3_rcf_mul(_context, a.Handle, b.Handle));

    public RcfNum Neg(RcfNum a) =>
        new(this, Z3RcfNative.Z3_rcf_neg(_context, a.Handle));

    /// <summary>正平方根；负数返回失败。</summary>
    public bool TrySqrt(RcfNum value, out RcfNum root)
    {
        var zero = FromInt(0);
        if (Z3RcfNative.Z3_rcf_eq(_context, value.Handle, zero.Handle))
        {
            root = zero;
            return true;
        }

        var negative = Z3RcfNative.Z3_rcf_lt(_context, value.Handle, zero.Handle);
        if (negative)
        {
            zero.Dispose();
            root = null!;
            return false;
        }

        var negA = Neg(value);
        var one = FromInt(1);
        var coeffs = new[] { negA.Handle, zero.Handle, one.Handle };
        var roots = new IntPtr[3];

        uint count;
        unsafe
        {
            fixed (IntPtr* pCoeffs = coeffs)
            fixed (IntPtr* pRoots = roots)
            {
                count = Z3RcfNative.Z3_rcf_mk_roots(
                    _context, (uint)coeffs.Length, pCoeffs, pRoots);
            }
        }

        RcfNum? positive = null;
        for (var i = 0; i < count; i++)
        {
            var candidate = new RcfNum(this, roots[i]);
            if (positive is null
                && Z3RcfNative.Z3_rcf_gt(_context, roots[i], zero.Handle))
                positive = candidate;
            else
                candidate.Dispose();
        }

        zero.Dispose();
        negA.Dispose();
        one.Dispose();

        if (positive is null)
        {
            root = null!;
            return false;
        }

        root = positive;
        return true;
    }

    public int Sign(RcfNum value)
    {
        var zero = FromInt(0);
        var sign = Z3RcfNative.Z3_rcf_eq(_context, value.Handle, zero.Handle) ? 0
            : Z3RcfNative.Z3_rcf_gt(_context, value.Handle, zero.Handle) ? 1 : -1;
        zero.Dispose();
        return sign;
    }

    public string ToDecimal(RcfNum value, uint precision)
    {
        var ptr = Z3RcfNative.Z3_rcf_num_to_decimal_string(_context, value.Handle, precision);
        return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }

    public void Dispose()
    {
        if (_context == IntPtr.Zero) return;
        Z3RcfNative.Z3_del_context(_context);
        _context = IntPtr.Zero;
    }
}

public sealed class RcfNum : IDisposable
{
    private readonly Z3RcfContext _owner;

    internal RcfNum(Z3RcfContext owner, IntPtr handle)
    {
        _owner = owner;
        Handle = handle;
    }

    internal IntPtr Handle { get; private set; }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero) return;
        Z3RcfNative.Z3_rcf_del(_owner.Handle, Handle);
        Handle = IntPtr.Zero;
    }
}

internal static partial class Z3RcfNative
{
    private const string Library = "libz3";

    [LibraryImport(Library)]
    internal static partial IntPtr Z3_mk_config();

    [LibraryImport(Library)]
    internal static partial void Z3_del_config(IntPtr config);

    [LibraryImport(Library)]
    internal static partial IntPtr Z3_mk_context(IntPtr config);

    [LibraryImport(Library)]
    internal static partial void Z3_del_context(IntPtr context);

    [LibraryImport(Library)]
    internal static partial IntPtr Z3_rcf_mk_small_int(IntPtr context, int value);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr Z3_rcf_mk_rational(IntPtr context, string value);

    [LibraryImport(Library)]
    internal static partial IntPtr Z3_rcf_add(IntPtr context, IntPtr a, IntPtr b);

    [LibraryImport(Library)]
    internal static partial IntPtr Z3_rcf_sub(IntPtr context, IntPtr a, IntPtr b);

    [LibraryImport(Library)]
    internal static partial IntPtr Z3_rcf_mul(IntPtr context, IntPtr a, IntPtr b);

    [LibraryImport(Library)]
    internal static partial IntPtr Z3_rcf_neg(IntPtr context, IntPtr a);

    [LibraryImport(Library)]
    internal static unsafe partial uint Z3_rcf_mk_roots(
        IntPtr context,
        uint count,
        IntPtr* coefficients,
        IntPtr* roots);

    [LibraryImport(Library)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool Z3_rcf_eq(IntPtr context, IntPtr a, IntPtr b);

    [LibraryImport(Library)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool Z3_rcf_lt(IntPtr context, IntPtr a, IntPtr b);

    [LibraryImport(Library)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool Z3_rcf_gt(IntPtr context, IntPtr a, IntPtr b);

    [LibraryImport(Library)]
    internal static partial IntPtr Z3_rcf_num_to_decimal_string(
        IntPtr context,
        IntPtr value,
        uint precision);

    [LibraryImport(Library)]
    internal static partial void Z3_rcf_del(IntPtr context, IntPtr value);
}
