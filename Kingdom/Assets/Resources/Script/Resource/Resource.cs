using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Resource", order = 0)]
public class Resource : ScriptableObject
{
    public enum Set
    {
        WoodSet,
        OreSet,
        MineralSet,
        IngotSet,
        UltraTechSet
    }
    public string Label;
    public string Description;
    public Sprite Sprite;
    public Color Color = Color.white;
    public Set DisplayerSet;
}