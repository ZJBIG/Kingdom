using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the rare-metal titanium/nickel chain from the supplied CSV pack.
/// New ScriptableObjects and their references are created through AssetDatabase.
/// </summary>
public static class ImportRareMetalIndustryCsv
{
    private const string CsvRoot = @"C:\Users\19603\Downloads\稀有金属_钛镍产业链_完整CSV";
    private const string ResourceRoot = "Assets/Resources/Datas/Resource";
    private const string BuildingRoot = "Assets/Resources/Datas/Building";
    private const string ResearchRoot = "Assets/Resources/Datas/Research";
    private const string WorkshopRoot = "Assets/Resources/Datas/Workshop";

    private sealed class Row : Dictionary<string, string>
    {
        public string V(string key) => TryGetValue(key, out string value) ? value.Trim() : string.Empty;
    }

    [MenuItem("Codex/Content/Import Rare Metal Titanium Nickel CSV")]
    public static void Import()
    {
        Dictionary<string, Resource> resources = Load<Resource>();
        Dictionary<string, Research> researches = Load<Research>();
        Dictionary<string, Building> buildings = Load<Building>();
        Dictionary<string, WorkshopUpgradeDefinition> workshops = Load<WorkshopUpgradeDefinition>();
        // The repository already contains this complete device as PoweredMining.
        // Preserve that stable ID and resolve the CSV's planning alias without creating a duplicate device.
        if (!workshops.ContainsKey("SteamRockDrills") && workshops.TryGetValue("PoweredMining", out WorkshopUpgradeDefinition steamRockDrills))
            workshops.Add("SteamRockDrills", steamRockDrills);

        List<Row> resourceRows = Read("01_新增资源完整表.csv");
        List<Row> buildingRows = Read("02_新增建筑完整总表.csv");
        List<Row> researchRows = Read("03_新增研究完整总表.csv");
        List<Row> workshopRows = Read("04_新增工坊器件完整总表.csv");

        foreach (Row row in resourceRows) EnsureResource(row, resources);
        foreach (Row row in researchRows) GetOrCreate(researches, row.V("ID"), ResearchRoot + "/Industrial");
        foreach (Row row in buildingRows) GetOrCreate(buildings, row.V("ID"), BuildingRoot + "/Industrial");
        foreach (Row row in workshopRows) GetOrCreate(workshops, row.V("ID"), WorkshopRoot);
        foreach (Row row in researchRows) EnsureResearch(row, researches, resources, buildings);
        foreach (Row row in buildingRows) EnsureBuilding(row, buildings, researches, resources, workshops);
        foreach (Row row in workshopRows) EnsureWorkshop(row, workshops, researches, resources, buildings);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log($"Rare-metal CSV imported: {resourceRows.Count} resources, {buildingRows.Count} buildings, {researchRows.Count} researches, {workshopRows.Count} workshops.");
    }

    private static void EnsureResource(Row row, Dictionary<string, Resource> all)
    {
        string id = row.V("ID");
        Resource asset = GetOrCreate(all, id, ResourceRoot + "/Industrial");
        asset.SetIdForEditor(id);
        asset.Label = row.V("Label");
        asset.Description = row.V("Description");
        asset.Color = new Color((float)Number(row.V("Color_r")), (float)Number(row.V("Color_g")), (float)Number(row.V("Color_b")), (float)Number(row.V("Color_a")));
        EditorUtility.SetDirty(asset);
    }

    private static void EnsureResearch(Row row, Dictionary<string, Research> all, Dictionary<string, Resource> resources, Dictionary<string, Building> buildings)
    {
        string id = row.V("ID");
        Research asset = GetOrCreate(all, id, ResearchRoot + "/Industrial");
        asset.SetIdForEditor(id);
        asset.Label = row.V("Label");
        asset.Description = row.V("Description");
        asset.BaseCost = row.V("BaseCost");
        asset.TechLevel = (TechLevel)Int(row.V("TechLevel"));
        asset.AdvancesTechLevel = Int(row.V("AdvancesTechLevel")) != 0;
        asset.SetPrerequisitesForEditor(ResolveResearches(Ids(row.V("前置研究汇总")), all));
        asset.SetResourceRequirementsForEditor(Pairs("03B_研究_资源需求.csv", "ResearchID", id, "ResourceID", "Amount", resources));
        List<ResearchEffectDefinition> effects = new List<ResearchEffectDefinition>();
        foreach (Row effect in RowsFor("03C_研究_效果.csv", "ResearchID", id))
        {
            ResearchEffectDefinition value = new ResearchEffectDefinition
            {
                Type = (ResearchEffectType)Int(effect.V("Type")),
                Value = new ExpantaNum(Number(effect.V("Value")))
            };
            if (buildings.TryGetValue(effect.V("TargetID"), out Building target)) value.Building = target;
            effects.Add(value);
        }
        asset.SetEffectsForEditor(effects);
        EditorUtility.SetDirty(asset);
    }

    private static void EnsureBuilding(Row row, Dictionary<string, Building> all, Dictionary<string, Research> researches, Dictionary<string, Resource> resources, Dictionary<string, WorkshopUpgradeDefinition> workshops)
    {
        string id = row.V("ID");
        Building asset = GetOrCreate(all, id, BuildingRoot + "/Industrial");
        asset.SetIdForEditor(id);
        asset.Label = row.V("Label");
        asset.Description = row.V("Description");
        asset.TechLevel = (TechLevel)Int(row.V("TechLevel"));
        asset.ConfigureEconomyForEditor(
            Number(row.V("costGrowth")), Number(row.V("spaceCost")), Number(row.V("productivityConsumption")), Number(row.V("productivityGranted")), Number(row.V("populationCapacityGranted")), Number(row.V("researchPowerGranted")),
            Number(row.V("foodProductionRate")), Number(row.V("foodConsumptionRate")), Number(row.V("foodCapacityGranted")), Number(row.V("powerProductionRate")), Number(row.V("powerConsumptionRate")), Number(row.V("logisticsProductionRate")), Number(row.V("logisticsConsumptionRate")),
            Number(row.V("attackPowerGranted")), Number(row.V("defensePowerGranted")), Number(row.V("militaryManpowerGranted")),
            Pairs("02A_建筑_建造资源需求.csv", "BuildingID", id, "ResourceID", "Amount", resources),
            Pairs("02B_建筑_每周期生成.csv", "BuildingID", id, "ResourceID", "AmountPerCycle", resources),
            Pairs("02C_建筑_每周期消耗.csv", "BuildingID", id, "ResourceID", "AmountPerCycle", resources));
        asset.SetRequiredResearchForEditor(ResolveResearches(Ids(row.V("前置研究汇总")), researches));
        asset.SetRequiredWorkshopUpgradesForEditor(ResolveWorkshops(Ids(row.V("前置工坊汇总")), workshops));
        EditorUtility.SetDirty(asset);
    }

    private static void EnsureWorkshop(Row row, Dictionary<string, WorkshopUpgradeDefinition> all, Dictionary<string, Research> researches, Dictionary<string, Resource> resources, Dictionary<string, Building> buildings)
    {
        string id = row.V("ID");
        WorkshopUpgradeDefinition asset = GetOrCreate(all, id, WorkshopRoot);
        asset.SetIdForEditor(id);
        asset.Label = row.V("Label");
        asset.Description = row.V("Description");
        asset.SortOrder = Int(row.V("SortOrder"));
        asset.TechLevel = (TechLevel)Int(row.V("TechLevel"));
        List<WorkshopEffectDefinition> effects = new List<WorkshopEffectDefinition>();
        foreach (Row effect in RowsFor("04D_工坊_效果.csv", "WorkshopID", id))
        {
            WorkshopEffectDefinition value = new WorkshopEffectDefinition { Type = (WorkshopEffectType)Int(effect.V("Type")), Value = new ExpantaNum(Number(effect.V("Value"))) };
            if (buildings.TryGetValue(effect.V("TargetID"), out Building target)) value.Building = target;
            effects.Add(value);
        }
        asset.ConfigureForEditor(ResolveResearches(Ids(row.V("前置研究汇总")), researches), ResolveWorkshops(Ids(row.V("前置工坊汇总")), all), Pairs("04C_工坊_制造资源需求.csv", "WorkshopID", id, "ResourceID", "Amount", resources), effects);
        EditorUtility.SetDirty(asset);
    }

    private static List<Pair<Resource, ExpantaNum>> Pairs(string file, string groupKey, string id, string resourceKey, string amountKey, Dictionary<string, Resource> resources) =>
        RowsFor(file, groupKey, id).Select(row => new Pair<Resource, ExpantaNum>(Require(resources, row.V(resourceKey)), new ExpantaNum(Number(row.V(amountKey))))).ToList();

    private static Resource Require(Dictionary<string, Resource> all, string id) => all.TryGetValue(id, out Resource value) ? value : throw new InvalidDataException($"Missing resource '{id}'.");
    private static List<Research> ResolveResearches(IEnumerable<string> ids, Dictionary<string, Research> all) => ids.Select(id => all.TryGetValue(id, out Research value) ? value : throw new InvalidDataException($"Missing research '{id}'.")).ToList();
    private static List<WorkshopUpgradeDefinition> ResolveWorkshops(IEnumerable<string> ids, Dictionary<string, WorkshopUpgradeDefinition> all) => ids.Select(id => all.TryGetValue(id, out WorkshopUpgradeDefinition value) ? value : throw new InvalidDataException($"Missing workshop '{id}'.")).ToList();

    private static IEnumerable<string> Ids(string value) => string.IsNullOrWhiteSpace(value) ? Enumerable.Empty<string>() : value.Split(new[] { '；', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(StripId);
    private static string StripId(string value) { int index = value.IndexOf('（'); return (index < 0 ? value : value.Substring(0, index)).Trim(); }
    private static double Number(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? number : 0d;
    private static int Int(string value) => (int)Number(value);

    private static List<Row> RowsFor(string file, string key, string value) => Read(file).Where(row => row.V(key) == value).ToList();
    private static List<Row> Read(string file)
    {
        string path = Path.Combine(CsvRoot, file);
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        string[] lines = File.ReadAllLines(path, new UTF8Encoding(false));
        List<string> headers = Parse(lines[0]);
        List<Row> rows = new List<Row>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            List<string> values = Parse(lines[i]); Row row = new Row();
            for (int j = 0; j < headers.Count; j++) row[headers[j]] = j < values.Count ? values[j] : string.Empty;
            rows.Add(row);
        }
        return rows;
    }

    private static List<string> Parse(string line)
    {
        List<string> result = new List<string>(); StringBuilder current = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < line.Length; i++) { char c = line[i]; if (c == '"') quoted = !quoted; else if (c == ',' && !quoted) { result.Add(current.ToString()); current.Clear(); } else current.Append(c); }
        result.Add(current.ToString()); return result;
    }

    private static Dictionary<string, T> Load<T>() where T : GameDefinition => AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>).Where(value => value != null && !string.IsNullOrEmpty(value.Id)).ToDictionary(value => value.Id, StringComparer.OrdinalIgnoreCase);

    private static Resource GetOrCreate(Dictionary<string, Resource> all, string id, string folder) { if (all.TryGetValue(id, out Resource value)) return value; EnsureFolder(folder); value = ScriptableObject.CreateInstance<Resource>(); AssetDatabase.CreateAsset(value, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + id + ".asset")); all[id] = value; return value; }
    private static Building GetOrCreate(Dictionary<string, Building> all, string id, string folder) { if (all.TryGetValue(id, out Building value)) return value; EnsureFolder(folder); value = ScriptableObject.CreateInstance<Building>(); AssetDatabase.CreateAsset(value, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + id + ".asset")); all[id] = value; return value; }
    private static Research GetOrCreate(Dictionary<string, Research> all, string id, string folder) { if (all.TryGetValue(id, out Research value)) return value; EnsureFolder(folder); value = ScriptableObject.CreateInstance<Research>(); AssetDatabase.CreateAsset(value, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + id + ".asset")); all[id] = value; return value; }
    private static WorkshopUpgradeDefinition GetOrCreate(Dictionary<string, WorkshopUpgradeDefinition> all, string id, string folder) { if (all.TryGetValue(id, out WorkshopUpgradeDefinition value)) return value; EnsureFolder(folder); value = ScriptableObject.CreateInstance<WorkshopUpgradeDefinition>(); AssetDatabase.CreateAsset(value, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + id + ".asset")); all[id] = value; return value; }
    private static void EnsureFolder(string path) { string[] parts = path.Split('/'); string current = parts[0]; for (int i = 1; i < parts.Length; i++) { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; } }
}
