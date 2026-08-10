using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Creates deterministic identities for validated run-once operations and exclusions.
/// </summary>
internal static class RunOnceOperationFingerprint
{
    /// <summary>Creates one SHA-256 identity over canonical operation content.</summary>
    /// <param name="operation">The validated run-once operation.</param>
    /// <param name="excludedItemIds">The normalized ephemeral exclusion set.</param>
    /// <returns>The canonical operation identity.</returns>
    public static string Create(
        RunOnceOperation operation,
        IEnumerable<Guid> excludedItemIds)
    {
        return Create(Guid.Empty, operation, excludedItemIds);
    }

    /// <summary>Creates one SHA-256 identity over a saved group identity and canonical operation content.</summary>
    /// <param name="groupId">The selected persisted group identity.</param>
    /// <param name="operation">The validated run-once operation.</param>
    /// <param name="excludedItemIds">The normalized ephemeral exclusion set.</param>
    /// <returns>The canonical selected-group identity.</returns>
    public static string Create(
        Guid groupId,
        RunOnceOperation operation,
        IEnumerable<Guid> excludedItemIds)
    {
        var canonical = new StringBuilder();
        Append(canonical, groupId.ToString("N", CultureInfo.InvariantCulture));
        Append(canonical, CreateNodeKey(operation.Target));
        Append(canonical, ((int)operation.Policy).ToString(CultureInfo.InvariantCulture));
        foreach (var source in operation.Sources.Select(CreateNodeKey))
        {
            Append(canonical, source);
        }

        foreach (var itemId in excludedItemIds.Distinct().Order())
        {
            Append(canonical, itemId.ToString("N", CultureInfo.InvariantCulture));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string CreateNodeKey(Node node)
    {
        return node switch
        {
            TagNode tag => $"T:{tag.Value}",
            CollectionNode collection => $"C:{collection.Id:N}",
            _ => throw new InvalidOperationException("Unknown node type."),
        };
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value.Length).Append(':').Append(value).Append(';');
    }
}
