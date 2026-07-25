[Serializable]
public struct Pair<T1, T2> : IEquatable<Pair<T1, T2>>
{
    [SerializeField] private T1 first;
    [SerializeField] private T2 second;
    public readonly T1 First => first;
    public readonly T2 Second => second;

    public Pair(T1 first, T2 second)
    {
        this.first = first;
        this.second = second;
    }
    public readonly void Deconstruct(out T1 first, out T2 second)
    {
        first = this.first;
        second = this.second;
    }

    public readonly bool Equals(Pair<T1, T2> other) =>
        EqualityComparer<T1>.Default.Equals(first, other.first)
        && EqualityComparer<T2>.Default.Equals(second, other.second);

    public override readonly bool Equals(object obj) =>
        obj is Pair<T1, T2> other && Equals(other);

    public override readonly int GetHashCode()
    {
        unchecked
        {
            int firstHash = EqualityComparer<T1>.Default.GetHashCode(first);
            int secondHash = EqualityComparer<T2>.Default.GetHashCode(second);
            return (firstHash * 397) ^ secondHash;
        }
    }

    public static bool operator ==(Pair<T1, T2> left, Pair<T1, T2> right) => left.Equals(right);
    public static bool operator !=(Pair<T1, T2> left, Pair<T1, T2> right) => !left.Equals(right);
}
