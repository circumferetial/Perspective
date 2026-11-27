namespace Perspective.Experiment.Subspace;

/// <summary>几何退化原因。Unknown = 0 专门留给 Result 的 default（“失败，但原因未知”）。</summary>
public enum GeometryError
{
    Unknown = 0,
    ZeroVector,
    CoincidentPoints,
    NotCollinear,
    AtInfinity,
    UndeterminedBasis,
    LineContainedInConic,
    DegenerateLine,
    UndefinedParameter,
    CollinearPoints
}

/// <summary>
///     非装箱的 Result：两个 payload 各占一个字段，E 保持泛型。
///     <para>
///         <c>default(Result&lt;T,E&gt;)</c> 恒为失败（<see cref="IsOk"/> = false），
///         错误原因取 <c>default(E)</c>——所以实例化时请让 E 的零值表示“未知/未指定”。
///         没有任何路径能让 default 伪装成成功。
///     </para>
/// </summary>
public readonly struct Result<T, E>
{
    private readonly bool _ok;
    private readonly T _value;
    private readonly E _error;

    private Result(bool ok, T value, E error)
    {
        _ok = ok;
        _value = value;
        _error = error;
    }

    public static Result<T, E> Ok(T value) => new(true, value, default!);
    public static Result<T, E> Fail(E error) => new(false, default!, error);

    public bool IsOk => _ok;
    public bool IsFailure => !_ok;

    /// <summary>成功时写出 value；失败时 value = default，返回值本身才是判据。</summary>
    public bool TryGet(out T value)
    {
        value = _value;
        return _ok;
    }

    /// <summary>成功值；失败时抛异常——只在“已知非退化”的调用点使用。</summary>
    public T Value => _ok
        ? _value
        : throw new InvalidOperationException($"失败的结果没有 Value。原因：{_error}");

    /// <summary>失败原因；成功时抛异常。</summary>
    public E Error => _ok
        ? throw new InvalidOperationException("成功的结果没有 Error。")
        : _error;
}
