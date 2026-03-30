using System;
using System.Reflection;
using UnityEngine;

public class Finder<T> : ScriptableObject where T : ScriptableObject
{
    public bool TryGetFromString(string name, out T item)
    {
        Type type = GetType();
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            item = (T)field.GetValue(this);
            return true;
        }
        item = default;
        return false;
    }
    public T GetFromString(string name)
    {
        Type type = GetType();
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
            return (T)field.GetValue(this);

        Debug.LogWarning($"{typeof(T).Name} not containes {name}");
        return default;
    }
}
public class ResourceFinder : Finder<Resource>
{
    [Header("木头")]
    public Resource WoodLog;
    public Resource WhiteBirchLog;
    public Resource RoseWoodLog;
    public Resource ScentedWoodLog;
    public Resource EbonyLog;
    public Resource ElvenWoodLog;
    [Header("矿石")]
    public Resource CopperOre;
    public Resource TinOre;
    public Resource IronOre;
    public Resource SilverOre;
    public Resource GoldOre;
    public Resource TitaniumOre;
    public Resource MithrilOre;
    [Header("矿质")]
    public Resource Coal;
    public Resource Citrine;
    public Resource Emerald;
    public Resource Jade;
    public Resource Ruby;
    public Resource Sapphire;
    public Resource Diamond;
    public Resource Uranium;
    [Header("石块")]
    public Resource StoneBrick_Marble;
    public Resource StoneChunk_Marble;
    [Header("金属锭")]
    public Resource Bronze;
    public Resource Iron;
    public Resource Silver;
    public Resource Gold;
    public Resource Steel;
    public Resource Titanium;
    public Resource Mithril;
}
public class BuildingFinder : Finder<Building>
{
    public Building ConstructionCenter;
    public Building WoodHouse;
    public Building Quarry;
    public Building Farm;
    public Building Lumberyard;
}
public class ResearchFinder : Finder<Research>
{
    public Research Quarry;
    public Research StoneCutting;
}
