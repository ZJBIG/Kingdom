using UnityEngine;
using System;

[CreateAssetMenu(fileName = "Create", menuName = "Resource", order = 0)]
public class Resource : ScriptableObject
{
    public string Label;
    public string Description;
    public Sprite Sprite;
    public BigNumber Amount;
}
