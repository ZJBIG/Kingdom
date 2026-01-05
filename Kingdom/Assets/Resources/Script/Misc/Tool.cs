using System;
using UnityEngine;

[Serializable]
public struct Pair<T1, T2>
{
    public T1 first;
    public T2 second;
    public Pair(T1 first, T2 second)
    {
        this.first = first;
        this.second = second;
    }
}
[Serializable]
public struct Tuple<T1, T2, T3>
{
    public T1 first;
    public T2 second;
    public T3 third;
    public Tuple(T1 first, T2 second, T3 third)
    {
        this.first = first;
        this.second = second;
        this.third = third;
    }
}
public static class Tool
{
    public static string Colorize(this string s, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{s}</color>";
    public static bool NullOrEmpty(this string str) => string.IsNullOrEmpty(str);
}
