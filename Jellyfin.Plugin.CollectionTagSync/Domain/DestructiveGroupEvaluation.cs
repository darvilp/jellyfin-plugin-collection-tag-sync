namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Reports removal pressure for one Authoritative mapping group.
/// </summary>
public sealed class DestructiveGroupEvaluation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DestructiveGroupEvaluation"/> class.
    /// </summary>
    /// <param name="target">The normalized group target.</param>
    /// <param name="currentAssignmentCount">The current direct target assignment population.</param>
    /// <param name="removalCount">The planned removal count.</param>
    /// <param name="exceedsPercentageLimit">Whether the accepted percentage limit is exceeded.</param>
    internal DestructiveGroupEvaluation(
        Node target,
        int currentAssignmentCount,
        int removalCount,
        bool exceedsPercentageLimit)
    {
        Target = target;
        CurrentAssignmentCount = currentAssignmentCount;
        RemovalCount = removalCount;
        ExceedsPercentageLimit = exceedsPercentageLimit;
    }

    /// <summary>Gets the normalized group target.</summary>
    public Node Target { get; }

    /// <summary>Gets the current direct target assignment population.</summary>
    public int CurrentAssignmentCount { get; }

    /// <summary>Gets the planned removal count.</summary>
    public int RemovalCount { get; }

    /// <summary>Gets a value indicating whether the accepted percentage limit is exceeded.</summary>
    public bool ExceedsPercentageLimit { get; }
}
