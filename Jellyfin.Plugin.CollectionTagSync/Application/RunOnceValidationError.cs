namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Describes one server-side run-once validation failure.
/// </summary>
public sealed class RunOnceValidationError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunOnceValidationError"/> class.
    /// </summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The administrator-facing message.</param>
    public RunOnceValidationError(RunOnceValidationErrorCode code, string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary>Gets the stable error code.</summary>
    public RunOnceValidationErrorCode Code { get; }

    /// <summary>Gets the administrator-facing message.</summary>
    public string Message { get; }
}
