using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the 2026-07-27 first-three-eras design pack through Unity so new assets
/// receive editor-managed GUIDs and references. Research layout is generated at runtime from prerequisites.
/// </summary>
public static class GenerateFirstThreeErasContent
{
    private const string PackRoot = @"C:\Users\19603\Desktop\Kingdom_FirstThreeEras_Design_Pack_2026-07-27";
    private const string BuildingsJson = PackRoot + @"\NewBuildings_FirstThreeEras.json";
    private const string ResearchJson = PackRoot + @"\NewResearch_FirstThreeEras.json";
    private const string BuildingRoot = "Assets/Resources/Datas/Building";
    private const string ResearchRoot = "Assets/Resources/Datas/Research";

    private sealed class EffectSpec
    {
        public ResearchEffectType Type;
        public string BuildingId;
        public string ResourceId = null;
        public double Value;
    }

    [MenuItem("Codex/Content/Import First Three Eras Design Pack")]
    public static void Import()
    {
        if (!File.Exists(BuildingsJson) || !File.Exists(ResearchJson))
            throw new FileNotFoundException("First-three-eras design pack JSON was not found.");

        Dictionary<string, Building> buildings = LoadDefinitions<Building>("t:Building");
        Dictionary<string, Research> researches = LoadDefinitions<Research>("t:Research");
        Dictionary<string, Resource> resources = LoadDefinitions<Resource>("t:Resource");

        List<object> buildingRows = MiniJson.Deserialize(File.ReadAllText(BuildingsJson)) as List<object>;
        List<object> researchRows = MiniJson.Deserialize(File.ReadAllText(ResearchJson)) as List<object>;
        if (buildingRows == null || researchRows == null)
            throw new InvalidDataException("Design pack JSON root must be an array.");

        for (int i = 0; i < buildingRows.Count; i++)
            EnsureBuildingAsset((Dictionary<string, object>)buildingRows[i], buildings, researches, resources);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        buildings = LoadDefinitions<Building>("t:Building");
        researches = LoadDefinitions<Research>("t:Research");
        resources = LoadDefinitions<Resource>("t:Resource");

        for (int i = 0; i < researchRows.Count; i++)
            EnsureResearchAsset((Dictionary<string, object>)researchRows[i], buildings, researches, resources);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        buildings = LoadDefinitions<Building>("t:Building");
        researches = LoadDefinitions<Research>("t:Research");

        for (int i = 0; i < buildingRows.Count; i++)
            WireUpgradeReference((Dictionary<string, object>)buildingRows[i], buildings);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"已导入 {buildingRows.Count} 个前三时代建筑和 {researchRows.Count} 项研究，研究布局将在运行时根据前置关系生成。");
    }

    private static void EnsureBuildingAsset(
        Dictionary<string, object> row,
        Dictionary<string, Building> buildings,
        Dictionary<string, Research> researches,
        Dictionary<string, Resource> resources)
    {
        string id = StringValue(row, "id");
        Building building = GetOrCreate(buildings, id, BuildingRoot, EraFolder(StringValue(row, "era")), typeof(Building));
        building.SetIdForEditor(id);
        building.Label = StringValue(row, "name");
        building.Description = StringValue(row, "desc");
        building.TechLevel = ParseTechLevel(StringValue(row, "era"));

        List<Pair<Resource, ExpantaNum>> requirements = Pairs(DictionaryValue(row, "cost"), resources);
        List<Pair<Resource, ExpantaNum>> generation = Pairs(DictionaryValue(row, "gen"), resources);
        List<Pair<Resource, ExpantaNum>> consumption = Pairs(DictionaryValue(row, "con"), resources);
        building.ConfigureEconomyForEditor(
            NumberValue(row, "growth", 1.15),
            NumberValue(row, "space", 0),
            NumberValue(row, "productivity_cost", 0),
            NumberValue(row, "productivity", 0),
            NumberValue(row, "population", 0),
            NumberValue(row, "research", 0),
            NumberValue(row, "food_prod", 0),
            0,
            NumberValue(row, "food_cap", 0),
            NumberValue(row, "power_prod", 0),
            NumberValue(row, "power_cons", 0),
            NumberValue(row, "log_prod", 0),
            NumberValue(row, "log_cons", 0),
            NumberValue(row, "attack", 0),
            NumberValue(row, "defense", 0),
            NumberValue(row, "manpower", 0),
            requirements,
            generation,
            consumption);
        building.SetRequiredResearchForEditor(ResolveResearches(ArrayValue(row, "prereq"), researches));
        EditorUtility.SetDirty(building);
    }

    private static void EnsureResearchAsset(
        Dictionary<string, object> row,
        Dictionary<string, Building> buildings,
        Dictionary<string, Research> researches,
        Dictionary<string, Resource> resources)
    {
        string id = StringValue(row, "id");
        Research research = GetOrCreate(researches, id, ResearchRoot, EraFolder(StringValue(row, "era")), typeof(Research));
        research.SetIdForEditor(id);
        research.Label = StringValue(row, "name");
        research.Description = $"研究{research.Label}，{StringValue(row, "unlock")}";
        research.BaseCost = NumberValue(row, "cost", 0).ToString(CultureInfo.InvariantCulture);
        research.TechLevel = ParseTechLevel(StringValue(row, "era"));
        research.AdvancesTechLevel = false;
        research.SetPrerequisitesForEditor(ResolveResearches(ArrayValue(row, "prereq"), researches));
        research.SetResourceRequirementsForEditor(Pairs(DictionaryValue(row, "resources"), resources));
        research.SetEffectsForEditor(BuildEffects(id, buildings, resources));
        EditorUtility.SetDirty(research);
    }

    private static void WireUpgradeReference(Dictionary<string, object> row, Dictionary<string, Building> buildings)
    {
        string id = StringValue(row, "id");
        Building building;
        if (!buildings.TryGetValue(id, out building))
            return;

        string upgradeTo = null;
        string line = StringValue(row, "line");
        if (id.Equals("WoodHouse", StringComparison.OrdinalIgnoreCase)) upgradeTo = "StoneHouse";
        else if (id.Equals("StoneHouse", StringComparison.OrdinalIgnoreCase)) upgradeTo = "TownHouse";
        building.SetUpgradeToForEditor(upgradeTo != null && buildings.TryGetValue(upgradeTo, out Building next) ? next : null);
        EditorUtility.SetDirty(building);
    }

    private static List<ResearchEffectDefinition> BuildEffects(string id, Dictionary<string, Building> buildings, Dictionary<string, Resource> resources)
    {
        List<ResearchEffectDefinition> effects = new List<ResearchEffectDefinition>();
        EffectSpec[] specs;
        if (!EffectMap.TryGetValue(id, out specs))
            specs = new[] { new EffectSpec { Type = ResearchEffectType.GlobalBuildingProductionMultiplier, Value = 1.02 } };
        for (int i = 0; i < specs.Length; i++)
        {
            EffectSpec spec = specs[i];
            ResearchEffectDefinition effect = new ResearchEffectDefinition { Type = spec.Type, Value = new ExpantaNum(spec.Value) };
            if (spec.BuildingId != null && !buildings.TryGetValue(spec.BuildingId, out effect.Building))
                throw new InvalidDataException($"Missing effect building '{spec.BuildingId}' for research '{id}'.");
            if (spec.ResourceId != null && !resources.TryGetValue(spec.ResourceId, out effect.Resource))
                throw new InvalidDataException($"Missing effect resource '{spec.ResourceId}' for research '{id}'.");
            effects.Add(effect);
        }
        return effects;
    }

    private static readonly Dictionary<string, EffectSpec[]> EffectMap = new Dictionary<string, EffectSpec[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["FishingTechniques"] = new[] { BuildingFood("FishingCamp", 1.10) },
        ["Woodworking"] = new[] { BuildingProduction("Lumberyard", 1.15), Global(ResearchEffectType.GlobalConstructionMultiplier, 0.95) },
        ["HerbalKnowledge"] = new[] { Global(ResearchEffectType.GlobalBuildingProductionMultiplier, 1.03) },
        ["FoodPreservation"] = new[] { Global(ResearchEffectType.FoodCapacityMultiplier, 1.10) },
        ["VillageOrganization"] = new[] { Global(ResearchEffectType.ProductivityGranted, 10) },
        ["OrganizedDefense"] = new[] { Global(ResearchEffectType.GlobalBuildingProductionMultiplier, 1.03) },
        ["CharcoalMaking"] = new[] { BuildingProduction("CharcoalKiln", 1.20) },
        ["PermanentArchitecture"] = new[] { Global(ResearchEffectType.GlobalConstructionMultiplier, 1.05) },
        ["AnimalFodder"] = new[] { BuildingFood("PlantingField", 1.20) },
        ["KilnEfficiency"] = new[] { BuildingProduction("CeramicKiln", 1.25) },
        ["IrrigationEngineering"] = new[] { BuildingFood("Farm", 1.20) },
        ["VillageCrafts"] = new[] { Global(ResearchEffectType.GlobalBuildingProductionMultiplier, 1.05) },
        ["Smithing_Bronze"] = new[] { BuildingProduction("MetalSmelter", 1.15) },
        ["CropRotation"] = new[] { BuildingFood("Farm", 1.20), BuildingFood("PlantingField", 1.10), Global(ResearchEffectType.PopulationGrowthMultiplier, 1.15) },
        ["CouncilGovernance"] = new[] { Global(ResearchEffectType.GlobalResearchMultiplier, 1.05) },
        ["OrganizedWatch"] = new[] { Global(ResearchEffectType.GlobalBuildingProductionMultiplier, 1.04) },
        ["BronzeImplements"] = new[] { Global(ResearchEffectType.GlobalBuildingProductionMultiplier, 1.15), Global(ResearchEffectType.GlobalConstructionMultiplier, 1.05) },
        ["UrbanHousing"] = new[] { Global(ResearchEffectType.GlobalConstructionMultiplier, 1.05) },
        ["GuildSystem"] = new[] { Global(ResearchEffectType.GlobalBuildingProductionMultiplier, 1.05) },
        ["ScholasticInstitutions"] = new[] { Global(ResearchEffectType.GlobalResearchMultiplier, 1.08) },
        ["MerchantAccounting"] = new[] { Global(ResearchEffectType.GlobalLogisticsMultiplier, 1.05) },
        ["ImprovedMilling"] = new[] { BuildingFood("WaterMill", 1.50), BuildingFood("Farm", 1.10) },
        ["PublicHealth"] = new[] { Global(ResearchEffectType.ProductivityGranted, 20), Global(ResearchEffectType.PopulationGrowthMultiplier, 1.40) },
        ["Astronomy"] = new[] { BuildingResearch("Observatory", 1.25) },
        ["RoadEngineering"] = new[] { Global(ResearchEffectType.GlobalLogisticsMultiplier, 1.10) },
        ["MechanicalPrinting"] = new[] { BuildingResearch("PrintingHouse", 1.20) },
        ["MetallurgicalStandards"] = new[] { BuildingProduction("SteelForge", 1.25) },
        ["CastleArchitecture"] = new[] { Global(ResearchEffectType.GlobalBuildingProductionMultiplier, 1.05) },
        ["ArsenalOrganization"] = new[] { Global(ResearchEffectType.GlobalBuildingProductionMultiplier, 1.10) }
    };

    private static EffectSpec Global(ResearchEffectType type, double value) => new EffectSpec { Type = type, Value = value };
    private static EffectSpec BuildingFood(string id, double value) => new EffectSpec { Type = ResearchEffectType.BuildingFoodProductionMultiplier, BuildingId = id, Value = value };
    private static EffectSpec BuildingProduction(string id, double value) => new EffectSpec { Type = ResearchEffectType.BuildingProductionMultiplier, BuildingId = id, Value = value };
    private static EffectSpec BuildingResearch(string id, double value) => new EffectSpec { Type = ResearchEffectType.BuildingResearchPowerMultiplier, BuildingId = id, Value = value };

    private static Building GetOrCreate(Dictionary<string, Building> map, string id, string root, string eraFolder, Type type)
    {
        if (map.TryGetValue(id, out Building existing)) return existing;
        string folder = $"{root}/{eraFolder}";
        EnsureFolder(folder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{id}.asset");
        Building created = ScriptableObject.CreateInstance<Building>();
        AssetDatabase.CreateAsset(created, path);
        map[id] = created;
        return created;
    }

    private static Research GetOrCreate(Dictionary<string, Research> map, string id, string root, string eraFolder, Type type)
    {
        if (map.TryGetValue(id, out Research existing)) return existing;
        string folder = $"{root}/{eraFolder}";
        EnsureFolder(folder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{id}.asset");
        Research created = ScriptableObject.CreateInstance<Research>();
        AssetDatabase.CreateAsset(created, path);
        map[id] = created;
        return created;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static Dictionary<string, T> LoadDefinitions<T>(string filter) where T : UnityEngine.Object
    {
        Dictionary<string, T> result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets(filter);
        for (int i = 0; i < guids.Length; i++)
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            GameDefinition definition = asset as GameDefinition;
            if (definition != null && !string.IsNullOrEmpty(definition.Id)) result[definition.Id] = asset;
        }
        return result;
    }

    private static List<Pair<Resource, ExpantaNum>> Pairs(Dictionary<string, object> values, Dictionary<string, Resource> resources)
    {
        List<Pair<Resource, ExpantaNum>> result = new List<Pair<Resource, ExpantaNum>>();
        foreach (KeyValuePair<string, object> entry in values)
        {
            Resource resource = FindResource(entry.Key, resources);
            result.Add(new Pair<Resource, ExpantaNum>(resource, new ExpantaNum(Number(entry.Value))));
        }
        return result;
    }

    private static Resource FindResource(string label, Dictionary<string, Resource> resources)
    {
        foreach (Resource resource in resources.Values)
            if (string.Equals(resource.Label, label, StringComparison.OrdinalIgnoreCase) || string.Equals(resource.Id, label, StringComparison.OrdinalIgnoreCase)) return resource;
        throw new InvalidDataException($"No existing Resource matches design-pack label '{label}'.");
    }

    private static List<Research> ResolveResearches(List<object> labels, Dictionary<string, Research> researches)
    {
        List<Research> result = new List<Research>();
        for (int i = 0; i < labels.Count; i++)
        {
            string label = labels[i] as string;
            Research match = null;
            foreach (Research research in researches.Values)
                if (string.Equals(research.Label, label, StringComparison.OrdinalIgnoreCase) || string.Equals(research.Id, label, StringComparison.OrdinalIgnoreCase)) { match = research; break; }
            if (match == null) throw new InvalidDataException($"No Research matches design-pack prerequisite '{label}'.");
            result.Add(match);
        }
        return result;
    }

    private static string EraFolder(string era) => ParseTechLevel(era).ToString();
    private static TechLevel ParseTechLevel(string era)
    {
        if (era.Contains("新石器")) return TechLevel.Neolithic;
        if (era.Contains("中世纪")) return TechLevel.Medieval;
        return TechLevel.Animal;
    }

    private static string StringValue(Dictionary<string, object> row, string key) => row.TryGetValue(key, out object value) ? value as string ?? value.ToString() : string.Empty;
    private static double NumberValue(Dictionary<string, object> row, string key, double fallback) => row.TryGetValue(key, out object value) ? Number(value) : fallback;
    private static double Number(object value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);
    private static Dictionary<string, object> DictionaryValue(Dictionary<string, object> row, string key) => row.TryGetValue(key, out object value) && value is Dictionary<string, object> map ? map : new Dictionary<string, object>();
    private static List<object> ArrayValue(Dictionary<string, object> row, string key) => row.TryGetValue(key, out object value) && value is List<object> list ? list : new List<object>();

    private static class MiniJson
    {
        public static object Deserialize(string json) { int index = 0; return ParseValue(json, ref index); }
        private static object ParseValue(string s, ref int i)
        {
            Skip(s, ref i);
            if (s[i] == '{') return ParseObject(s, ref i);
            if (s[i] == '[') return ParseArray(s, ref i);
            if (s[i] == '"') return ParseString(s, ref i);
            int start = i; while (i < s.Length && ",]}\r\n \t".IndexOf(s[i]) < 0) i++;
            string token = s.Substring(start, i - start);
            if (token == "true") return true; if (token == "false") return false; if (token == "null") return null;
            return double.Parse(token, CultureInfo.InvariantCulture);
        }
        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            Dictionary<string, object> result = new Dictionary<string, object>(); i++;
            Skip(s, ref i); if (s[i] == '}') { i++; return result; }
            while (i < s.Length) { string key = ParseString(s, ref i); Skip(s, ref i); i++; result[key] = ParseValue(s, ref i); Skip(s, ref i); if (s[i] == '}') { i++; break; } i++; }
            return result;
        }
        private static List<object> ParseArray(string s, ref int i)
        {
            List<object> result = new List<object>(); i++; Skip(s, ref i); if (s[i] == ']') { i++; return result; }
            while (i < s.Length) { result.Add(ParseValue(s, ref i)); Skip(s, ref i); if (s[i] == ']') { i++; break; } i++; }
            return result;
        }
        private static string ParseString(string s, ref int i)
        {
            i++; System.Text.StringBuilder b = new System.Text.StringBuilder();
            while (i < s.Length && s[i] != '"') { if (s[i] == '\\') { i++; char c = s[i]; if (c == 'n') b.Append('\n'); else if (c == 'r') b.Append('\r'); else if (c == 't') b.Append('\t'); else b.Append(c); } else b.Append(s[i]); i++; }
            i++; return b.ToString();
        }
        private static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
    }
}
