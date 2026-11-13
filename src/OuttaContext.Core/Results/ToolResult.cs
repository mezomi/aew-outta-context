namespace OuttaContext.Core.Results;

/// <summary>
/// The outcome of asking an outside program to do something: either it worked and gave us a
/// <see cref="Value"/>, or it failed and we have an <see cref="Error"/> explaining why.
/// </summary>
/// <remarks>
/// <para>
/// Why not just throw? Outside programs fail for ordinary reasons (no internet, a video was
/// removed, a site blocked us). Returning a result makes those cases visible in the method
/// signature, so callers decide what to do: try another way, or give up with a clear message.
/// </para>
/// <para>Typical use:</para>
/// <code>
/// ToolResult&lt;ResolvedVideo&gt; result = await resolver.ResolveAsync(request, ct);
/// if (result.Failed)
/// {
///     reporter.Detail($"That didn't work: {result.Error.Message}");
///     return ...; // try something else
/// }
/// ResolvedVideo video = result.Value;
/// </code>
/// <para>Or, when there is nothing else to try: <c>var video = result.ValueOrThrow();</c></para>
/// </remarks>
public sealed class ToolResult<T>
{
    private readonly T? _value;
    private readonly ToolError? _error;

    private ToolResult(T? value, ToolError? error)
    {
        _value = value;
        _error = error;
    }

    public bool Succeeded => _error is null;

    public bool Failed => _error is not null;

    /// <summary>The value. Only read this after checking <see cref="Succeeded"/>.</summary>
    public T Value => Succeeded
        ? _value!
        : throw new InvalidOperationException($"No value: the tool failed ({_error!.Message}).");

    /// <summary>The error. Only read this after checking <see cref="Failed"/>.</summary>
    public ToolError Error => _error ?? throw new InvalidOperationException("No error: the tool succeeded.");

#pragma warning disable CA1000 // Static factories on a generic type read best here: ToolResult<T>.Success(x).
    public static ToolResult<T> Success(T value) => new(value, null);

    public static ToolResult<T> Failure(ToolError error) => new(default, error);
#pragma warning restore CA1000

    /// <summary>The value, or a <see cref="ToolFailedException"/> if the tool failed.</summary>
    public T ValueOrThrow() => Succeeded ? _value! : throw Error.ToException();

    /// <summary>Change the value if it worked; pass the error through if it didn't.</summary>
    public ToolResult<TOther> Map<TOther>(Func<T, TOther> map) =>
        Succeeded ? ToolResult<TOther>.Success(map(_value!)) : ToolResult<TOther>.Failure(Error);

    public static implicit operator ToolResult<T>(ToolError error) => Failure(error);
}

/// <summary>The value of a tool call that has nothing to return, only "it worked".</summary>
public readonly record struct Done
{
    public static readonly Done Value;
}

/// <summary>Shortcuts for results without a value.</summary>
public static class ToolResult
{
    public static ToolResult<Done> Success() => ToolResult<Done>.Success(Done.Value);

    public static ToolResult<Done> Failure(ToolError error) => ToolResult<Done>.Failure(error);
}
