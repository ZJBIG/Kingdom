using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Resource", order = 0)]
public class Resource : GameDefinition
{
    public enum Set
    {
        PrimitiveEraSet = 0,
        NeolithicEraSet = 1,
        MedievalEraSet = 2,
        IndustrialEraSet = 3,
        SpaceEraSet = 4,
        ExtremeEraSet = 5,
        AncientEraSet = 6,
        MineralSet = 7,
        IngotSet = 8
    }
    public string Label;
    public string Description;
    public Sprite Sprite;
    public Color Color = Color.white;
    public Set DisplayerSet;
}
