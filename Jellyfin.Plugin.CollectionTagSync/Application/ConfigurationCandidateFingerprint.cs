using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.CollectionTagSync.Configuration;
using Jellyfin.Plugin.CollectionTagSync.Domain;

namespace Jellyfin.Plugin.CollectionTagSync.Application;

/// <summary>
/// Creates deterministic identities for complete validated candidate configurations.
/// </summary>
internal static class ConfigurationCandidateFingerprint
{
    /// <summary>Creates one deterministic candidate identity.</summary>
    /// <param name="candidate">The submitted serializer configuration.</param>
    /// <param name="configuration">The validated immutable mapping configuration.</param>
    /// <returns>A SHA-256 identity over canonical semantic candidate content.</returns>
    public static string Create(
        PluginConfiguration candidate,
        MappingConfiguration configuration)
    {
        var canonical = new StringBuilder();
        Append(canonical, candidate.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, candidate.StartupReconcileDelayMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, candidate.DestructiveCircuitBreakerEnabled ? "1" : "0");
        Append(canonical, candidate.DestructiveMaximumAffectedItems.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, candidate.DestructiveMaximumRemovalPercentage.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, candidate.DestructiveMinimumAssignmentPopulation.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, candidate.DestructiveCircuitBreakerDisableAcknowledged ? "1" : "0");

        foreach (var group in configuration.Groups
            .Select(CreateGroupKey)
            .OrderBy(value => value, StringComparer.Ordinal))
        {
            Append(canonical, group);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string CreateGroupKey(MappingGroup group)
    {
        var key = new StringBuilder();
        Append(key, CreateNodeKey(group.Target));
        Append(key, ((int)group.Policy).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(key, group.IsEnabled ? "1" : "0");
        foreach (var source in group.Sources
            .Select(CreateNodeKey)
            .OrderBy(value => value, StringComparer.Ordinal))
        {
            Append(key, source);
        }

        return key.ToString();
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
