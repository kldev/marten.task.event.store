namespace Halcyon.Domain;

/// <summary>
/// A rule refused the request. The message is written for the person who clicked (Polish) and is shown as it is.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message) { }

    public DomainException() { }

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
