namespace Xping.Sdk.Core.Session.Comparison;

/// <summary>
/// Encapsulates the result of a TestSession comparison.
/// </summary>
public class DiffResult : IEquatable<DiffResult>
{
    private static readonly DiffResult empty = new();
    private readonly List<Difference> _differences = [];

    internal TestSession Session1 { get; private set; } = null!;
    internal TestSession Session2 { get; private set; } = null!;

    /// <summary>
    /// Gets a singleton instance of the <see cref="DiffResult"/> class that represents an empty diff result.
    /// </summary>
    public static DiffResult Empty => empty;

    /// <summary>
    /// Gets or sets the readon only collection of differences between two TestSession instances.
    /// </summary>
    public IReadOnlyCollection<Difference> Differences => _differences;

    /// <summary>
    /// Initializes a new instance of the DiffResult class.
    /// </summary>
    public DiffResult()
    { }

    /// <summary>
    /// Constructs a DiffResult object exclusively for use by <see cref="DiffEngine"/>. It accepts two TestSession 
    /// instances for comparison, which are subsequently utilized by the DiffPresenter class to format the DiffResult.
    /// </summary>
    /// <param name="session1">The first TestSession instance for comparison.</param>
    /// <param name="session2">The second TestSession instance for comparison.</param>
    internal DiffResult(TestSession session1, TestSession session2)
    {
        Session1 = session1;
        Session2 = session2;
    }

    /// <summary>
    /// Adds the differences from another DiffResult instance to this instance.
    /// </summary>
    /// <param name="result">The DiffResult instance containing the differences to add.</param>
    internal void Add(DiffResult result)
    {
        if (result != null)
        {
            if (!ReferenceEquals(this.Differences, result.Differences))
            {
                _differences.AddRange(result.Differences);
            }
        }
    }

    /// <summary>
    /// Adds the differences to this instance.
    /// </summary>
    /// <param name="difference">The difference instance to be added.</param>
    internal void AddDifference(Difference difference)
    {
        if (difference != null)
        {
            _differences.Add(difference);
        }
    }

    /// <summary>
    /// Determines whether the current DiffResult object is equal to another DiffResult object.
    /// </summary>
    /// <param name="other">The DiffResult object to compare with the current object.</param>
    /// <returns><c>true</c> if the current object and other have the same value; otherwise, <c>false</c>.</returns>
    public bool Equals(DiffResult? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null)
        {
            return false;
        }

        return _differences.SequenceEqual(other._differences);
    }

    /// <summary>
    /// Determines whether the current DiffResult object is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>
    /// <c>true</c> if the current object and obj are both DiffResult objects and have the same value; otherwise, 
    /// <c>false</c>.
    /// </returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as DiffResult);
    }

    /// <summary>
    /// Determines whether two DiffResult objects have the same value.
    /// </summary>
    /// <param name="lhs">The first DiffResult object to compare.</param>
    /// <param name="rhs">The second DiffResult object to compare.</param>
    /// <returns><c>true</c> if lhs and rhs have the same value; otherwise, <c>false</c>.</returns>
    public static bool operator ==(DiffResult? lhs, DiffResult? rhs)
    {
        if (lhs is null || rhs is null)
        {
            return Equals(lhs, rhs);
        }

        return lhs.Equals(rhs);
    }

    /// <summary>
    /// Determines whether two DiffResult objects have different values.
    /// </summary>
    /// <param name="lhs">The first DiffResult object to compare.</param>
    /// <param name="rhs">The second DiffResult object to compare.</param>
    /// <returns><c>true</c> if lhs and rhs have different values; otherwise, <c>false</c>.</returns>
    public static bool operator !=(DiffResult? lhs, DiffResult? rhs)
    {
        if (lhs is null || rhs is null)
        {
            return !Equals(lhs, rhs);
        }

        return !lhs.Equals(rhs);
    }

    /// <summary>
    /// Returns the hash code for the current DiffResult object.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode()
    {
        return _differences.GetHashCode();
    }
}
