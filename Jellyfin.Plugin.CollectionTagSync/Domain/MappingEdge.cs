namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Represents one active continuous source-to-target relationship.
/// </summary>
public sealed class MappingEdge
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MappingEdge"/> class.
    /// </summary>
    /// <param name="source">The edge source.</param>
    /// <param name="target">The edge target.</param>
    internal MappingEdge(Node source, Node target)
    {
        Source = source;
        Target = target;
    }

    /// <summary>
    /// Gets the edge source.
    /// </summary>
    public Node Source { get; }

    /// <summary>
    /// Gets the edge target.
    /// </summary>
    public Node Target { get; }
}
