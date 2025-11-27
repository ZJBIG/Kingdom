using UnityEngine;

[CreateAssetMenu(fileName = "Create/Resource", menuName = "ResourceData", order = 0)]
public class Resource : ScriptableObject
{
    public string Label;
    public string Description;
    public Sprite Sprite;
}
