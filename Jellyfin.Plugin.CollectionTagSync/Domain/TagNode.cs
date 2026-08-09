using System;

namespace Jellyfin.Plugin.CollectionTagSync.Domain;

/// <summary>
/// Represents a validated tag identity and configured spelling.
/// </summary>
public sealed class TagNode : Node
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TagNode"/> class.
    /// </summary>
    /// <param name="value">The normalized configured spelling.</param>
    internal TagNode(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the trimmed administrator-configured tag spelling.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string DisplayLabel => $"Tag \"{Value}\"";

    /// <inheritdoc />
    public override bool Equals(Node? other)
    {
        return other is TagNode tag
            && StringComparer.OrdinalIgnoreCase.Equals(Value, tag.Value);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
    }
}
