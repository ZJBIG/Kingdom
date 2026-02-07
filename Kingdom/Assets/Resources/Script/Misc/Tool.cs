using System;
using System.ComponentModel;
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
    public readonly void Deconstruct(out T1 first, out T2 second)
    {
        first = this.first;
        second = this.second;
    }
}
public static class Tool
{
    public static string Colorize(this string s, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{s}</color>";
    public static bool NullOrEmpty(this string str) => string.IsNullOrEmpty(str);
    public static string GetDescription(this Enum value)
    {
        var fieldInfo = value.GetType().GetField(value.ToString());
        var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0 ? attributes[0].Description : value.ToString();
    }
}
