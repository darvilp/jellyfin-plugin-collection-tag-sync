using System;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Defines the accepted destructive bulk-plan safety limits.
/// </summary>
public sealed class DestructiveCircuitBreakerOptions
{
    /// <summary>The default maximum unique affected-item count.</summary>
    public const int DefaultMaximumAffectedItems = 25;

    /// <summary>The default maximum percentage of current target assignments removed per group.</summary>
    public const int DefaultMaximumRemovalPercentage = 20;

    /// <summary>The default population floor for percentage evaluation.</summary>
    public const int DefaultMinimumAssignmentPopulation = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="DestructiveCircuitBreakerOptions"/> class.
    /// </summary>
    /// <param name="isEnabled">Whether excessive destructive plans pause.</param>
    /// <param name="maximumAffectedItems">The inclusive unique affected-item limit.</param>
    /// <param name="maximumRemovalPercentage">The inclusive per-group percentage limit.</param>
    /// <param name="minimumAssignmentPopulation">The population floor for percentage evaluation.</param>
    public DestructiveCircuitBreakerOptions(
        bool isEnabled = true,
        int maximumAffectedItems = DefaultMaximumAffectedItems,
        int maximumRemovalPercentage = DefaultMaximumRemovalPercentage,
        int minimumAssignmentPopulation = DefaultMinimumAssignmentPopulation)
    {
        if (!IsValidMaximumAffectedItems(maximumAffectedItems))
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAffectedItems));
        }

        if (!IsValidMaximumRemovalPercentage(maximumRemovalPercentage))
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRemovalPercentage));
        }

        if (!IsValidMinimumAssignmentPopulation(minimumAssignmentPopulation))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAssignmentPopulation));
        }

        IsEnabled = isEnabled;
        MaximumAffectedItems = maximumAffectedItems;
        MaximumRemovalPercentage = maximumRemovalPercentage;
        MinimumAssignmentPopulation = minimumAssignmentPopulation;
    }

    /// <summary>Gets a value indicating whether excessive destructive plans pause.</summary>
    public bool IsEnabled { get; }

    /// <summary>Gets the inclusive unique affected-item limit.</summary>
    public int MaximumAffectedItems { get; }

    /// <summary>Gets the inclusive per-group removal percentage limit.</summary>
    public int MaximumRemovalPercentage { get; }

    /// <summary>Gets the population floor for percentage evaluation.</summary>
    public int MinimumAssignmentPopulation { get; }

    /// <summary>Determines whether an absolute limit is valid.</summary>
    /// <param name="value">The candidate value.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidMaximumAffectedItems(int value) => value >= 0;

    /// <summary>Determines whether a percentage limit is valid.</summary>
    /// <param name="value">The candidate value.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidMaximumRemovalPercentage(int value) => value is >= 0 and <= 100;

    /// <summary>Determines whether a percentage population floor is valid.</summary>
    /// <param name="value">The candidate value.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidMinimumAssignmentPopulation(int value) =>
        value >= DefaultMinimumAssignmentPopulation;
}
