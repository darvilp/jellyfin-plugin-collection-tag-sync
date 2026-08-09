namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Describes one server-side configuration activation validation failure.
/// </summary>
public sealed class ConfigurationActivationError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationActivationError"/> class.
    /// </summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The administrator-facing message.</param>
    public ConfigurationActivationError(ConfigurationActivationErrorCode code, string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary>
    /// Gets the stable error code.
    /// </summary>
    public ConfigurationActivationErrorCode Code { get; }

    /// <summary>
    /// Gets the administrator-facing message.
    /// </summary>
    public string Message { get; }
}
