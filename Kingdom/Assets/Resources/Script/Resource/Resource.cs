using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Resource", order = 0)]
public class Resource : ScriptableObject
{
    public string Label;
    public string Description;
    public Sprite Sprite;
    public Color Color = Color.white;
}