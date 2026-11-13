namespace OuttaContext.Core.Common;

/// <summary>
/// Carries an <see cref="Error"/> through an exception, for the rare cases where returning a
/// result isn't possible (for example a corrupt settings file found while the app starts up).
/// The CLI prints it like any other error.
/// </summary>
public sealed class ErrorException : Exception
{
    public ErrorException(Error error)
        : base(error.Message)
    {
        Error = error;
    }

    public ErrorException()
        : this(new Error("unknown", "Something went wrong."))
    {
    }

    public ErrorException(string message)
        : this(new Error("unknown", message))
    {
    }

    public ErrorException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = new Error("unknown", message);
    }

    public Error Error { get; }
}
