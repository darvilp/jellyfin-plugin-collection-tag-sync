using System;
using System.Linq;
using System.Security.Claims;

namespace Jellyfin.Plugin.CollectionTagSync.Api;

/// <summary>
/// Reads the authenticated Jellyfin administrator identity from elevated API requests.
/// </summary>
internal static class AdministratorIdentity
{
    private const string JellyfinUserIdClaim = "Jellyfin-UserId";

    /// <summary>Gets the Jellyfin user identity, or an empty GUID when absent or malformed.</summary>
    /// <param name="principal">The authenticated request principal.</param>
    /// <returns>The Jellyfin administrator identity.</returns>
    public static Guid Get(ClaimsPrincipal principal)
    {
        var value = principal.Claims.FirstOrDefault(claim => string.Equals(
            claim.Type,
            JellyfinUserIdClaim,
            StringComparison.OrdinalIgnoreCase))?.Value;
        return Guid.TryParse(value, out var administratorId) ? administratorId : Guid.Empty;
    }
}
