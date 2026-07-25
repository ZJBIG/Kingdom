using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

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
public static class Tool
{
    private static readonly Dictionary<Type, Dictionary<string, string>> DescriptionCache = new();

    public static string Colorize(this string s, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{s}</color>";
    public static bool NullOrEmpty(this string str) => string.IsNullOrEmpty(str);
    public static string GetDescription(this Enum value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        Type enumType = value.GetType();
        string name = value.ToString();
        if (!DescriptionCache.TryGetValue(enumType, out Dictionary<string, string> typeCache))
        {
            typeCache = new Dictionary<string, string>();
            DescriptionCache.Add(enumType, typeCache);
        }

        if (typeCache.TryGetValue(name, out string description))
            return description;

        var fieldInfo = enumType.GetField(name);
        if (fieldInfo == null)
            return name;

        var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
        description = attributes.Length > 0 ? attributes[0].Description : name;
        typeCache.Add(name, description);
        return description;
    }
}
