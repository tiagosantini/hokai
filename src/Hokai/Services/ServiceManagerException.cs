namespace Hokai.Services;

/// <summary>Represents a failure during a service management operation.</summary>
public sealed class ServiceManagerException : Exception
{
    public ServiceManagerException(string message) : base(message) { }
    public ServiceManagerException(string message, Exception inner) : base(message, inner) { }
}
